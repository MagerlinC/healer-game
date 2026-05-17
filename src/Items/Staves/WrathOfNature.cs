using Godot;
using healerfantasy.SpellSystem;
using healerfantasy.Talents;

namespace healerfantasy.Items.Staves;

public class WrathOfNature : EquippableItem
{

	public override string ItemId => "wrath_of_nature";
	public WrathOfNature()
	{
		Name = "WrathOfNature";
		Description =
			"Whenever a healing-over-time-effect expires, it converts itself into a damage-over-time-effect on the boss, dealing the same base damage as that healing-over-time effect.";
		Rarity = ItemRarity.Legendary;
		Slot = EquipSlot.Staff;
		Icon = GD.Load<Texture2D>(AssetConstants.StaveIconPath(6));
		SpellModifiers.Add(new ConvertToDoTModifier());
	}

	class ConvertToDoTModifier : ISpellModifier
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
		}

	}

}