using Godot;
using healerfantasy.SpellSystem;
using healerfantasy.Talents;

namespace healerfantasy.Items.Rings;

public class BandOfTheVoid : EquippableItem
{
	readonly float _durationExtension = 0.5f;
	public override string ItemId => "band_of_the_void";

	public BandOfTheVoid()
	{
		Name = "Band of the Void";
		Description = $"Damage over time effects last {_durationExtension}s longer";
		Rarity = ItemRarity.Legendary;
		Slot = EquipSlot.Ring;
		Icon = GD.Load<Texture2D>(AssetConstants.RingIconPath(5));
		SpellModifiers.Add(new DurationExtenderModifier());
	}

	class DurationExtenderModifier : ISpellModifier
	{
		public ModifierPriority Priority { get; } = ModifierPriority.BASE;

		public void OnBeforeCast(SpellContext context)
		{
		}
		public void OnCalculate(SpellContext context)
		{
			// TODO: implement DoT duration extension logic. This might require some refactoring of the Spell system to allow for modifying DoT durations here.
		}
		public void OnAfterCast(SpellContext context)
		{
		}
	}
}