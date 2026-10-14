using System;
using Godot;
using healerfantasy.SpellResources;
using healerfantasy.SpellSystem;

namespace healerfantasy.Items.Amulets;

/// <summary>
/// Shadowbound Chain — Epic amulet that can drop from any boss.
/// Forged from shadow-iron links that hum with barely-contained dark energy,
/// this chain accelerates the wearer's casting rhythm while sharpening the
/// bite of their umbral spells.
///
/// Stat bonuses: +15% Umbra spell damage, +10% haste.
/// </summary>
public class ShadowboundChain : EquippableItem
{
	static readonly float _umbraDamageBonus = 0.15f;
	static readonly float _hasteBonus = 0.10f;

	public override string ItemId => "shadowbound_chain";

	public ShadowboundChain()
	{
		Name = "Shadowbound Chain";
		Description = $"+{Math.Round(_umbraDamageBonus * 100)}% Umbra damage. +{Math.Round(_hasteBonus * 100)}% haste.";
		Rarity = ItemRarity.Epic;
		Slot = EquipSlot.Amulet;
		Icon = GD.Load<Texture2D>(AssetConstants.AmuletIconPath(3));
		CharacterModifiers.Add(new ShadowboundModifier());
	}

	class ShadowboundModifier : ICharacterModifier
	{
		public void Modify(CharacterStats stats)
		{
			stats.SpellSchoolIncreasedDamage[SpellSchool.Umbra] += 0.15f;
			stats.IncreasedHaste += 0.10f;
		}
	}
}