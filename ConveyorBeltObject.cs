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

        public override LocString DisplayName { get { return Localizer.DoStr("Conveyor Belt"); } }
        public virtual Type RepresentedItemType { get { return typeof(ConveyorBeltItem); } }
        private Orientation _orientation = Orientation.NORTH;

        protected override void OnCreate(User creator)
        {
            base.OnCreate(creator);
            StockpileComponent.ClearPlacementArea(creator, this.Position3i, DefaultDim, this.Rotation);
        }

        protected override void Initialize()
        {
            base.Initialize();
            if (this.Rotation.W >= 1)
                _orientation = Orientation.NORTH;
            else if (this.Rotation.W >= 0)
                _orientation = Orientation.EAST;
            else if (this.Rotation.Y >= 1)
                _orientation = Orientation.SOUTH;
            else
                _orientation = Orientation.WEST;

            this.GetComponent<StockpileComponent>().Initialize(DefaultDim);

            PublicStorageComponent storage = this.GetComponent<PublicStorageComponent>();
            storage.Initialize(DefaultDim.x * DefaultDim.z);
            storage.Storage.AddInvRestriction(new StockpileStackRestriction(DefaultDim.y)); // limit stack sizes to the y-height of the stockpile
            this.GetComponent<LinkComponent>().Initialize(1);
        }

        public override void Tick()
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
                            ChatManager.ServerMessageToAll(Localizer.Format("Give {0}", itemToGive.Type), false);
                        }
                    }
                }

            }
        }
    }
}