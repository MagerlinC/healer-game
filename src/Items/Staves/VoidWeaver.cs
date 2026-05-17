using Godot;
using healerfantasy.SpellResources;
using healerfantasy.SpellSystem;
using healerfantasy.Talents;

namespace healerfantasy.Items.Staves;

public class VoidWeaver : EquippableItem
{

	public override string ItemId => "void_weaver";

	static readonly float DurationExtension = 2f;

	public VoidWeaver()
	{
		Name = "Void Weaver";
		Description =
			$"20% increased void damage. Casting a void spell increases the remaining duration of damage effects on the target by {DurationExtension:F0}s";
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
			if (context.IsSpellOfSchool(SpellSchool.Void))
				context.Target.ExtendAllPlayerEffects(DurationExtension, Character.EffectFilter.HarmfulOnly);
		}
	}
}