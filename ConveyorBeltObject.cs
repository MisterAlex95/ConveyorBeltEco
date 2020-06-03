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
  public partial class ConveyorBeltObject : ConveyorBelt
  {

  }
}