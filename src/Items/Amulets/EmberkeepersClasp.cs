using System;
using Godot;
using healerfantasy.SpellResources;
using healerfantasy.SpellSystem;

namespace healerfantasy.Items.Amulets;

/// <summary>
/// Emberkeeper's Clasp — Rare amulet that can drop from any boss.
/// Carved from living wood and threaded with wild-grown thorns, this clasp
/// channels the raw energy of the forest into the wearer's ember magic.
///
/// Stat bonus: +20% Ember school spell damage.
/// </summary>
public class EmberkeepersClasp : EquippableItem
{
	static readonly float Bonus = 0.20f;
	public override string ItemId => "naturalists_clasp";

	public EmberkeepersClasp()
	{
		Name = "Emberkeeper's Clasp";
		Description = $"{Math.Round(Bonus * 100)}% increased Ember spell damage & light restored.";
		Rarity = ItemRarity.Rare;
		Slot = EquipSlot.Amulet;
		School = SpellSchool.Ember;
		Icon = GD.Load<Texture2D>(AssetConstants.AmuletIconPath(1));
		CharacterModifiers.Add(new EmberModifier());
	}

	class EmberModifier : ICharacterModifier
	{
		public void Modify(CharacterStats stats)
		{
			stats.SpellSchoolIncreasedDamage[SpellSchool.Ember] += Bonus;
			stats.SpellSchoolIncreasedHealing[SpellSchool.Ember] += Bonus;
		}
	}
}