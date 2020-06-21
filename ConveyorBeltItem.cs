namespace Eco.Mods.TechTree
{
  // [DoNotLocalize]
  using System;
  using System.Collections.Generic;
  using System.ComponentModel;
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
  using Eco.World.Blocks;

  [Serialized]
  public partial class ConveyorBeltItem :
      WorldObjectItem<ConveyorBeltObject>
  {
    public override LocString DisplayName { get { return Localizer.DoStr("Conveyor Belt"); } }
    public override LocString DisplayDescription { get { return Localizer.DoStr("Convey things with belts"); } }

    static ConveyorBeltItem() { }
  }

  [RequiresSkill(typeof(IndustrySkill), 1)]
  public partial class ConveyorBeltRecipe : Recipe
  {
    public ConveyorBeltRecipe()
    {
      this.Products = new CraftingElement[]
      {
                new CraftingElement<ConveyorBeltItem>(),
      };

      this.Ingredients = new CraftingElement[]
      {
                new CraftingElement<GearItem>(typeof(IndustrySkill), 1, IndustrySkill.MultiplicativeStrategy, typeof(IndustryLavishResourcesTalent)),
                new CraftingElement<SteelItem>(typeof(IndustrySkill), 1, IndustrySkill.MultiplicativeStrategy, typeof(IndustryLavishResourcesTalent)),
      };

      this.ExperienceOnCraft = 5;
      this.CraftMinutes = CreateCraftTimeValue(typeof(ConveyorBeltRecipe), Item.Get<ConveyorBeltItem>().UILink(), 1, typeof(IndustrySkill), typeof(IndustryFocusedSpeedTalent), typeof(IndustryParallelSpeedTalent));
      this.Initialize(Localizer.DoStr("Conveyor Belt"), typeof(ConveyorBeltRecipe));
      CraftingComponent.AddRecipe(typeof(AssemblyLineObject), this);
    }
  }
}