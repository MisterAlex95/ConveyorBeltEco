namespace Eco.Mods.TechTree
{
  using System;
  using System.Reflection;
  using System.Collections.Generic;
  using System.Linq;
  using System.ComponentModel;

  using Eco.Core.Utils;
  using Eco.Core.Utils.AtomicAction;

  using Eco.Gameplay.Systems.Chat;
  using Eco.Gameplay.Blocks;
  using Eco.Gameplay.Components;
  using Eco.Gameplay.Components.Auth;
  using Eco.Gameplay.DynamicValues;
  using Eco.Gameplay.Economy;
  using Eco.Gameplay.Housing;
  using Eco.Gameplay.Interactions;
  using Eco.Gameplay.Items;
  using Eco.Gameplay.Minimap;
  using Eco.Gameplay.Objects;
  using Eco.Gameplay.Players;
  using Eco.Gameplay.Property;
  using Eco.Gameplay.Skills;
  using Eco.Gameplay.Systems.TextLinks;
  using Eco.Gameplay.Pipes.LiquidComponents;
  using Eco.Gameplay.Pipes.Gases;
  using Eco.Gameplay.Systems.Tooltip;
  using Eco.Shared;
  using Eco.Shared.Math;
  using Eco.Shared.Localization;
  using Eco.Shared.Serialization;
  using Eco.Shared.Utils;
  using Eco.Shared.View;
  using Eco.Shared.Items;
  using Eco.Gameplay.Pipes;
  using Eco.World;
  using Eco.World.Blocks;
  using Eco.Simulation.WorldLayers;
  using Eco.Simulation.WorldLayers.Layers;
  using Eco.Simulation.WorldLayers.Pushers;

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
      // ChatManager.ServerMessageToAll(Localizer.Format("ROT x:{0} y:{1} z:{2}", this.Rotation.Right.x, this.Rotation.Right.y, this.Rotation.Right.z), true);
      // ChatManager.ServerMessageToAll(Localizer.Format("POS x:{0} y:{1} z:{2}", this.Position3i.x, this.Position3i.y, this.Position3i.z), true);
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
      ChatManager.ServerMessageToAll(Localizer.Format("myInventories {0}", myInventories), false);

      PublicStorageComponent storage = this.GetComponent<PublicStorageComponent>();
      storage.Initialize(DefaultDim.x * DefaultDim.z);
      storage.Storage.AddInvRestriction(new StockpileStackRestriction(DefaultDim.y)); // limit stack sizes to the y-height of the stockpile
      this.GetComponent<LinkComponent>().Initialize(1);
    }

    public override void Tick()
    {
      if (updateThrottle.DoUpdate)
      {
        if (this.isEmpty())
          PullFromBack();
        else
          PushFront();
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

    private void PushFront()
    {
      Vector3i newPosition = GetNextBlockPosition(false);

      var blockInFront = World.GetBlock(newPosition);

      PublicStorageComponent our = this.GetComponent<PublicStorageComponent>();

      if (!blockInFront.Is<Empty>() && blockInFront.GetType() == typeof(WorldObjectBlock))
      {
        // we have another conveyorbelt in front of us (or a not-filled stockpile)
        var obj = (WorldObjectBlock)(blockInFront);
        var o = obj.WorldObjectHandle.Object;
        PublicStorageComponent front = o.GetComponent<PublicStorageComponent>();
        MoveFromTo(our, front);
      }
      else
      {
        // if front is not a worldObjectBlock, try to move self into linked stockpiles
        var invToPushIn = myInventories != null ? myInventories.FirstOrDefault() : null;
        if (invToPushIn != null)
        {
          Inventory ourStorage = our.Storage;
          if (ourStorage == null) return;
          IEnumerable<ItemStack> stacks = ourStorage.Stacks;
          ItemStack stack = stacks.FirstOrDefault();
          if (stack != null)
          {
            Item itemToGive = stack.Item;
            if (itemToGive != null && invToPushIn != null)
            {
              int itemQuantity = DefaultDim.y; //stack.Quantity;
              ourStorage.TryMoveItems<Item>(itemToGive.Type, itemQuantity, invToPushIn);
            }
          }
        }
        else
        {
          // ChatManager.ServerMessageToAll(Localizer.Format("Object in front is not storage"), false);
        }

      }

    }

    private void PullFromBack()
    {
      Vector3i newPosition = GetNextBlockPosition(true);

      var blockInBack = World.GetBlock(newPosition);
      PublicStorageComponent our = this.GetComponent<PublicStorageComponent>();
      if (!blockInBack.Is<Empty>() && blockInBack.GetType() == typeof(WorldObjectBlock))
      {
        WorldObjectBlock worldObjectBlock = (WorldObjectBlock)(blockInBack);
        var worldObject = worldObjectBlock.WorldObjectHandle.Object;

        // Skip if it's a conveyor
        if (worldObject.DisplayName == DisplayName) return;

        PublicStorageComponent back = worldObject.GetComponent<PublicStorageComponent>();
        MoveFromTo(back, our);
      }
      else
      {
        var invToPullFrom = GetFirstNotEmptyInventory(myInventories);
        if (invToPullFrom != null && our != null)
        {
          MoveFromTo(invToPullFrom, our.Storage);
        }
      }
    }

    private Inventory GetFirstNotEmptyInventory(IEnumerable<Inventory> inventories)
    {
      if (myInventories == null) return null;
      foreach (Inventory inv in inventories)
      {
        if (!inv.IsEmpty) return inv;
      }
      return null;
    }
    private ItemStack GetFirstItemStackNotEmpty(IEnumerable<ItemStack> stacks)
    {
      foreach (ItemStack stack in stacks)
      {
        if (!stack.Empty) return stack;
      }
      return null;
    }

    private Vector3i GetNextBlockPosition(bool inverted)
    {
      if ((!inverted && _orientation == Orientation.NORTH) || (inverted && _orientation == Orientation.SOUTH))
        return new Vector3i(this.Position3i.x, this.Position3i.y, this.Position3i.z + 1);
      else if ((!inverted && _orientation == Orientation.EAST) || (inverted && _orientation == Orientation.WEST))
        return new Vector3i(this.Position3i.x + 1, this.Position3i.y, this.Position3i.z);
      else if ((!inverted && _orientation == Orientation.SOUTH) || (inverted && _orientation == Orientation.NORTH))
        return new Vector3i(this.Position3i.x, this.Position3i.y, this.Position3i.z - 1);
      else if ((!inverted && _orientation == Orientation.WEST) || (inverted && _orientation == Orientation.EAST))
        return new Vector3i(this.Position3i.x - 1, this.Position3i.y, this.Position3i.z);
      return new Vector3i(this.Position3i.x, this.Position3i.y, this.Position3i.z);
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

    private bool isEmpty()
    {
      Inventory our = this.GetComponent<PublicStorageComponent>().Storage;
      return our.IsEmpty;
    }
  }
}