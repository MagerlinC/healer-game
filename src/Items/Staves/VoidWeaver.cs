using Godot;
using healerfantasy.SpellResources;
using healerfantasy.SpellSystem;
using healerfantasy.Talents;

namespace healerfantasy.Items.Staves;

public class VoidWeaver : EquippableItem
{

	public override string ItemId => "void_weaver";

	public VoidWeaver()
	{
		Name = "Void Weaver";
		Description =
			"20% increased void damage. Dealing void damage has a chance to refresh all damage over time effects on the target";
		Rarity = ItemRarity.Legendary;
		Slot = EquipSlot.Staff;
		Icon = GD.Load<Texture2D>(AssetConstants.StaveIconPath(6));
		CharacterModifiers.Add(new VoidModifier());
		SpellModifiers.Add(new RefreshDotsModifier());
	}

	class VoidModifier : ICharacterModifier
	{
		public void Modify(CharacterStats stats)
		{
			stats.SpellSchoolIncreasedDamage[SpellSchool.Void] += 0.20f;
		}
	}

	class RefreshDotsModifier : ISpellModifier
	{
		public ModifierPriority Priority => ModifierPriority.BASE;

		public void OnBeforeCast(SpellContext context)
		{
		}
		public void OnCalculate(SpellContext context)
		{
		}

		public void OnAfterCast(SpellContext context)
		{
			context.Target.RefreshAllPlayerEffects(Character.EffectFilter.HarmfulOnly);
			var isCritHeal = context.Tags.HasFlag(SpellTags.Critical)
			                 && context.Tags.HasFlag(SpellTags.Healing);
			if (isCritHeal)
				context.Caster.RestoreMana(5f);
		}
	}
}