using System;
using Godot;
using healerfantasy.SpellResources;
using healerfantasy.SpellSystem;

namespace healerfantasy.Items.Amulets;

/// <summary>
/// Naturalist's Clasp — Rare amulet that can drop from any boss.
/// Carved from living wood and threaded with wild-grown vines, this clasp
/// channels the raw energy of the forest into the wearer's nature magic.
///
/// Stat bonus: +20% Nature school spell damage.
/// </summary>
public class NaturalistsClasp : EquippableItem
{
	static readonly float _natureDamageBonus = 0.20f;
	public override string ItemId => "naturalists_clasp";

	public NaturalistsClasp()
	{
		Name = "Naturalist's Clasp";
		Description = $"+{Math.Round(_natureDamageBonus * 100)}% Nature spell damage.";
		Rarity = ItemRarity.Rare;
		Slot = EquipSlot.Amulet;
		Icon = GD.Load<Texture2D>(AssetConstants.AmuletIconPath(1));
		CharacterModifiers.Add(new NatureModifier());
	}

	class NatureModifier : ICharacterModifier
	{
		public void Modify(CharacterStats stats)
		{
			stats.SpellSchoolIncreasedDamage[SpellSchool.Nature] += 0.20f;
		}
	}
}