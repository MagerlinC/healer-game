using System;
using Godot;
using healerfantasy.SpellSystem;

namespace healerfantasy.Items.Rings;

/// <summary>
/// Seer's Band — Epic ring that can drop from any boss.
/// Worn by oracles who read the tides of fate, this ring sharpens the
/// wearer's instincts — making their critical strikes strike deeper
/// and hit more often.
///
/// Stat bonuses: +8% crit chance, crit multiplier increased to 1.8×.
/// </summary>
public class SeersBand : EquippableItem
{
	static readonly float _critChanceBonus = 0.08f;
	static readonly float _critMultiplierBonus = 0.15f; // default is 1.5×; this brings it to 1.8×

	public override string ItemId => "seers_band";

	public SeersBand()
	{
		Name = "Seer's Band";
		Description =
			$"+{Math.Round(_critChanceBonus * 100)}% crit chance. +{Math.Round(_critMultiplierBonus * 100)}% Critical strike multiplier.";
		Rarity = ItemRarity.Epic;
		Slot = EquipSlot.Ring1;
		Icon = GD.Load<Texture2D>(AssetConstants.RingIconPath(3));
		CharacterModifiers.Add(new CritModifier());
	}

	class CritModifier : ICharacterModifier
	{
		public void Modify(CharacterStats stats)
		{
			stats.CritChance += _critChanceBonus;
			stats.CritMultiplier += _critMultiplierBonus;
		}
	}
}