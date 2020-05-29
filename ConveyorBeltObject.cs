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
    enum Orientation
    {
      NORTH = 0,
      EAST,
      SOUTH,
      WEST
    };
    public static readonly Vector3i DefaultDim = new Vector3i(1, 1, 1);

    PeriodicUpdateRealTime updateThrottle = new PeriodicUpdateRealTime(1);
    public override LocString DisplayName { get { return Localizer.DoStr("Conveyor Belt"); } }
    public virtual Type RepresentedItemType { get { return typeof(ConveyorBeltItem); } }
    private Orientation _orientation = Orientation.NORTH;

    public List<Inventory> myInventories = new List<Inventory>();

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

    private void PushFront()
    {
      Vector3i newPosition = new Vector3i(0, 0, 0);
      if (_orientation == Orientation.NORTH)
        newPosition = new Vector3i(this.Position3i.x, this.Position3i.y, this.Position3i.z + 1);
      else if (_orientation == Orientation.EAST)
        newPosition = new Vector3i(this.Position3i.x + 1, this.Position3i.y, this.Position3i.z);
      else if (_orientation == Orientation.SOUTH)
        newPosition = new Vector3i(this.Position3i.x, this.Position3i.y, this.Position3i.z - 1);
      else if (_orientation == Orientation.WEST)
        newPosition = new Vector3i(this.Position3i.x - 1, this.Position3i.y, this.Position3i.z);

      // LinkComponent linkC = this.GetComponent<LinkComponent>();
      // if (linkC != null)
      // {

      var invToPushIn = myInventories != null ? myInventories.FirstOrDefault() : null;

      if (invToPushIn != null)
      {
        Inventory our = this.GetComponent<PublicStorageComponent>().Storage;
        IEnumerable<ItemStack> stacks = our.Stacks;
        ItemStack stack = stacks.FirstOrDefault();
        if (stack != null)
        {
          Item itemToGive = stack.Item;
          if (itemToGive != null && invToPushIn != null)
          {
            int itemQuantity = stack.Quantity;
            our.TryMoveItems<Item>(itemToGive.Type, itemQuantity, invToPushIn);
          }
        }

        foreach (Inventory linkedInv in myInventories)
        {
          ChatManager.ServerMessageToAll(Localizer.Format("ZOBIBI {0}", linkedInv), false);
          ChatManager.ServerMessageToAll(Localizer.Format("=)==================="), false);

        }
      }
      else
      {
        var block = World.GetBlock(newPosition);
        if (!block.Is<Empty>())
        {
          if (block.GetType() != typeof(WorldObjectBlock))
          {
            ChatManager.ServerMessageToAll(Localizer.Format("Object in front is not storage"), false);
            return;
          }
          var obj = (WorldObjectBlock)(block);
          var o = obj.WorldObjectHandle.Object;
          PublicStorageComponent front = o.GetComponent<PublicStorageComponent>();
          if (front != null)
          {
            Inventory frontStorage = front.Storage;
            Inventory our = this.GetComponent<PublicStorageComponent>().Storage;
            IEnumerable<ItemStack> stacks = our.Stacks;
            ItemStack stack = stacks.FirstOrDefault();
            if (stack != null)
            {
              Item itemToGive = stack.Item;
              if (itemToGive != null && frontStorage != null)
              {
                int itemQuantity = stack.Quantity;
                our.TryMoveItems<Item>(itemToGive.Type, itemQuantity, frontStorage);
              }
            }
          }
        }
      }

    }


    public override InteractResult OnActInteract(InteractionContext context)
    {
      LinkComponent linkC = this.GetComponent<LinkComponent>();
      ChatManager.ServerMessageToAll(Localizer.Format("OY {0}", context.Player), false);
      InventoryCollection invCol = linkC.GetSortedLinkedInventories(context.Player.User);
      if (invCol != null)
      {
        IEnumerable<Inventory> inventories = invCol.AllInventories;
        ChatManager.ServerMessageToAll(Localizer.Format("inventories {0}", inventories), false);
        foreach (var inv in inventories)
        {
          if (inv.GetType() == typeof(AuthorizationInventory))
          {
            ChatManager.ServerMessageToAll(Localizer.Format("Nbr of inv {0}", inv), false);
            if (!myInventories.Contains((Inventory)inv))
            {
              myInventories.Add((Inventory)inv);
            }
          }

        }
      }

      return base.OnActInteract(context);
    }

    private void PullFromBack()
    {
      Vector3i newPosition = new Vector3i(0, 0, 0);
      // Invert Orientation
      if (_orientation == Orientation.NORTH)
        newPosition = new Vector3i(this.Position3i.x, this.Position3i.y, this.Position3i.z - 1);
      else if (_orientation == Orientation.EAST)
        newPosition = new Vector3i(this.Position3i.x - 1, this.Position3i.y, this.Position3i.z);
      else if (_orientation == Orientation.SOUTH)
        newPosition = new Vector3i(this.Position3i.x, this.Position3i.y, this.Position3i.z + 1);
      else if (_orientation == Orientation.WEST)
        newPosition = new Vector3i(this.Position3i.x + 1, this.Position3i.y, this.Position3i.z);

      var block = World.GetBlock(newPosition);
      if (!block.Is<Empty>())
      {
        if (block.GetType() != typeof(WorldObjectBlock))
        {
          ChatManager.ServerMessageToAll(Localizer.Format("Object in back is not storage it's a {0}", block.GetType()), false);
          return;
        }
        var obj = (WorldObjectBlock)(block);
        var o = obj.WorldObjectHandle.Object;

        // If it's a conveyor skip
        if (o.DisplayName == DisplayName)
          return;

        PublicStorageComponent back = o.GetComponent<PublicStorageComponent>();
        if (back != null)
        {
          Inventory backStorage = back.Storage;
          Inventory our = this.GetComponent<PublicStorageComponent>().Storage;
          IEnumerable<ItemStack> stacks = backStorage.Stacks;
          ItemStack stack = stacks.FirstOrDefault();
          if (stack != null)
          {
            Item itemToGive = stack.Item;
            if (itemToGive != null && our != null)
            {
              int itemQuantity = stack.Quantity;
              backStorage.TryMoveItems<Item>(itemToGive.Type, itemQuantity, our);
            }
          }
        }
      }
    }

    private bool isEmpty()
    {
      Inventory our = this.GetComponent<PublicStorageComponent>().Storage;
      IEnumerable<ItemStack> stacks = our.Stacks;
      ItemStack stack = stacks.FirstOrDefault();
      return (stack == null || stack.Empty);
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
  }
}