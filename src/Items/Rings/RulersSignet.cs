using System;
using Godot;
using healerfantasy.SpellSystem;

namespace healerfantasy.Items.Rings;

/// <summary>
/// Arcane Accelerator — Epic staff that can drop from any boss.
/// Imbued with time-warping enchantments that compress the gap between thought
/// and spell, while also amplifying offensive output.
///
/// Stat bonuses: +15% haste, +10% damage multiplier.
/// </summary>
public class RulersSignet : EquippableItem
{

	static readonly float _hasteAmount = 0.05f;
	public override string ItemId => "rulers_signet";

	public RulersSignet()
	{
		Name = "Ruler's Signet";
		Description = $"+{Math.Round(_hasteAmount * 100)}% haste";
		Rarity = ItemRarity.Rare;
		Slot = EquipSlot.Ring1;
		Icon = GD.Load<Texture2D>(AssetConstants.RingIconPath(1));
		CharacterModifiers.Add(new SpeedDamageModifier());
	}

	class SpeedDamageModifier : ICharacterModifier
	{
		public void Modify(CharacterStats stats)
		{
			stats.IncreasedHaste += 0.5f;
		}
	}
}