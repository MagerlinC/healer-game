using Godot;
using healerfantasy.SpellResources;
using healerfantasy.SpellSystem;
using healerfantasy.Talents;

namespace healerfantasy.Items.Staves;

public class Shadeweaver : EquippableItem
{

	public override string ItemId => "shadeweaver";

	static readonly float DurationExtension = 2f;

	public Shadeweaver()
	{
		Name = "Shadeweaver";
		Description =
			$"20% increased Umbra damage. Casting an Umbra spell increases the remaining duration of damage effects on the target by {DurationExtension:F0}s";
		Rarity = ItemRarity.Legendary;
		Slot = EquipSlot.Staff;
		School = SpellSchool.Umbra;
		CharacterModifiers.Add(new UmbraModifier());
		SpellModifiers.Add(new RefreshDotsModifier());
	}

	class UmbraModifier : ICharacterModifier
	{
		public void Modify(CharacterStats stats)
		{
			stats.SpellSchoolIncreasedDamage[SpellSchool.Umbra] += 0.20f;
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
			if (context.IsSpellOfSchool(SpellSchool.Umbra))
				context.Target.ExtendAllPlayerEffects(DurationExtension, Character.EffectFilter.HarmfulOnly);
		}
	}
}