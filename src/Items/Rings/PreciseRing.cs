using Godot;
using healerfantasy.SpellSystem;
using healerfantasy.Talents;

namespace healerfantasy.Items.Rings;

public class PreciseRing : EquippableItem
{
	readonly float _critChance = 0.1f;
	public override string ItemId => "precise_ring";

	public PreciseRing()
	{
		Name = "Precise Ring";
		Description = $"+{_critChance:F0}% to critical strike chance";
		Rarity = ItemRarity.Rare;
		Slot = EquipSlot.Ring1;
		Icon = GD.Load<Texture2D>(AssetConstants.RingIconPath(5));
		CharacterModifiers.Add(new CriticalStrikeModifier(_critChance));
	}

	class CriticalStrikeModifier : ICharacterModifier
	{
		readonly float _critChance;

		public CriticalStrikeModifier(float critChance)
		{
			_critChance = critChance;
		}

		public void Modify(CharacterStats stats)
		{
			stats.CritChance += _critChance;
		}
	}
}