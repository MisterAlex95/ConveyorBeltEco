namespace Eco.Mods.TechTree
{
  using System;
  using System.Collections.Generic;
  using System.Linq;

  using Eco.Gameplay.Systems.Chat;
  using Eco.Gameplay.Components;
  using Eco.Gameplay.Components.Auth;
  using Eco.Gameplay.Interactions;
  using Eco.Gameplay.Items;
  using Eco.Gameplay.Objects;
  using Eco.Gameplay.Players;
  using Eco.Shared.Math;
  using Eco.Shared.Localization;
  using Eco.Shared.Serialization;
  using Eco.Shared.Utils;
  using Eco.World;
  using Eco.World.Blocks;

  [Serialized]
  [RequireComponent(typeof(PropertyAuthComponent))]
  [RequireComponent(typeof(PublicStorageComponent))]
  [RequireComponent(typeof(StockpileComponent))]
  [RequireComponent(typeof(WorldStockpileComponent))]
  [RequireComponent(typeof(LinkComponent))]
  [RequireComponent(typeof(StorageComponent))]
  public partial class ConveyorBeltObject : WorldObject, IChatCommandHandler
  {
    public static readonly Vector3i DefaultDim = new Vector3i(1, 1, 1);
    public override LocString DisplayName { get { return Localizer.DoStr("Conveyor Belt"); } }
    public virtual Type RepresentedItemType { get { return typeof(ConveyorBeltItem); } }

    private PeriodicUpdateRealTime updateThrottle = new PeriodicUpdateRealTime(1);
    private Orientation _orientation = Orientation.NORTH;
    private List<Inventory> myInventories = new List<Inventory>();

    private PublicStorageComponent front = null;
    private PublicStorageComponent back = null;

    private bool done = false;

    private enum Orientation
    {
      NORTH = 0,
      EAST,
      SOUTH,
      WEST
    };

    protected override void OnCreate(User creator)
    {
      base.OnCreate(creator);
      StockpileComponent.ClearPlacementArea(creator, this.Position3i, DefaultDim, this.Rotation);
    }

    protected override void Initialize()
    {
      base.Initialize();
      Vector3 rot = this.Rotation.Right;

      if (rot.x == 1)
        _orientation = Orientation.NORTH;
      else if (rot.z < -0.9 && rot.z > -1.1)
        _orientation = Orientation.EAST;
      else if (rot.x == -1)
        _orientation = Orientation.SOUTH;
      else if (rot.z > 0.9 && rot.z < 1.1)
        _orientation = Orientation.WEST;


      this.GetComponent<StockpileComponent>().Initialize(DefaultDim);

      PublicStorageComponent storage = this.GetComponent<PublicStorageComponent>();
      storage.Initialize(DefaultDim.x * DefaultDim.z);
      storage.Storage.AddInvRestriction(new StockpileStackRestriction(DefaultDim.y)); // limit stack sizes to the y-height of the stockpile
      this.GetComponent<LinkComponent>().Initialize(1);
    }

    public override void Tick()
    {
      if (updateThrottle.DoUpdate)
      {
        if (this.isConveyorEmpty())
        {
          if (back == null) this.initialize(true);
          PullFromBack();
        }
        else
        {
          if (front == null) this.initialize(false);
          PushFront();
        }
      }
    }

    public override InteractResult OnActInteract(InteractionContext context)
    {
      LinkComponent linkC = this.GetComponent<LinkComponent>();
      InventoryCollection invCol = linkC.GetSortedLinkedInventories(context.Player.User);
      if (invCol != null)
      {
        IEnumerable<Inventory> inventories = invCol.AllInventories;
        foreach (var inv in inventories)
        {
          if (inv.GetType() == typeof(AuthorizationInventory) || inv.GetType() == typeof(SelectionInventory))
          {
            if (!myInventories.Contains((Inventory)inv))
            {
              myInventories.Add((Inventory)inv);
            }
          }
        }
      }

      return base.OnActInteract(context);
    }

    private void initialize(bool inverted)
    {
      Vector3i wantedPosition;
      if ((!inverted && _orientation == Orientation.NORTH) || (inverted && _orientation == Orientation.SOUTH))
        wantedPosition = new Vector3i(this.Position3i.x, this.Position3i.y, this.Position3i.z + 1);
      else if ((!inverted && _orientation == Orientation.EAST) || (inverted && _orientation == Orientation.WEST))
        wantedPosition = new Vector3i(this.Position3i.x + 1, this.Position3i.y, this.Position3i.z);
      else if ((!inverted && _orientation == Orientation.SOUTH) || (inverted && _orientation == Orientation.NORTH))
        wantedPosition = new Vector3i(this.Position3i.x, this.Position3i.y, this.Position3i.z - 1);
      else if ((!inverted && _orientation == Orientation.WEST) || (inverted && _orientation == Orientation.EAST))
        wantedPosition = new Vector3i(this.Position3i.x - 1, this.Position3i.y, this.Position3i.z);
      else
        wantedPosition = new Vector3i(this.Position3i.x, this.Position3i.y, this.Position3i.z);

      // check front then front-top then front-bottom
      Vector3i blockTop = wantedPosition + new Vector3i(0, 1, 0);
      Vector3i blockBottom = wantedPosition + new Vector3i(0, -1, 0);

      IEnumerable<WorldObject> list = WorldObjectManager.GetObjectsWithin(this.Position3i, 5f);
      foreach (WorldObject item in list)
      {
        List<Vector3i> occupancy = item.WorldOccupancy;
        foreach (Vector3i position in occupancy)
        {
          if (inverted == false)
          {
            if (front == null && (position == wantedPosition || position == blockTop || position == blockBottom))
              front = item.GetComponent<PublicStorageComponent>();
          }
          else
          {
            if (back == null && (position == wantedPosition || position == blockTop || position == blockBottom))
              back = item.GetComponent<PublicStorageComponent>();
          }
        }
      }
    }

    private void PushFront()
    {
      PublicStorageComponent our = this.GetComponent<PublicStorageComponent>();
      if (front != null) MoveFromTo(our, front);
    }
    private void PullFromBack()
    {
      PublicStorageComponent our = this.GetComponent<PublicStorageComponent>();
      if (back != null) MoveFromTo(back, our);
    }

    private ItemStack GetFirstItemStackNotEmpty(IEnumerable<ItemStack> stacks)
    {
      foreach (ItemStack stack in stacks)
      {
        if (!stack.Empty) return stack;
      }
      return null;
    }

    private void MoveFromTo(PublicStorageComponent from, PublicStorageComponent to)
    {
      if (from == null || to == null) return;

      Inventory fromStorage = from.Storage;
      if (fromStorage == null) return;

      Inventory toStorage = to.Storage;
      MoveFromTo(fromStorage, toStorage);
    }
    private void MoveFromTo(Inventory from, Inventory to)
    {
      if (to == null) return;

      IEnumerable<ItemStack> stacks = from.Stacks;
      ItemStack stack = GetFirstItemStackNotEmpty(stacks);
      if (stack == null) return;

      Item itemToGive = stack.Item;
      if (itemToGive == null) return;

      int itemQuantity = DefaultDim.y;
      from.TryMoveItems<Item>(itemToGive.Type, itemQuantity, to);
    }

    private bool isConveyorEmpty()
    {
      Inventory our = this.GetComponent<PublicStorageComponent>().Storage;
      return our.IsEmpty;
    }
  }
}