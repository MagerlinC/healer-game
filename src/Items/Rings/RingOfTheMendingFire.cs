using Godot;
using healerfantasy.SpellSystem;

namespace healerfantasy.Items.Rings;

/// <summary>
/// Ring of the Mending Fire — Rare ring that can drop from any boss.
/// Forged from ember-crystal and infused with a healing warmth that
/// amplifies the wearer's restorative magic.
///
/// Stat bonus: +10% healing output.
/// </summary>
public class RingOfTheMendingFire : EquippableItem
{
	static readonly float _healingBonus = 0.10f;
	public override string ItemId => "ring_of_the_mending_fire";

	public RingOfTheMendingFire()
	{
		Name = "Ring of the Mending Fire";
		Description = $"+{_healingBonus * 100}% healing";
		Rarity = ItemRarity.Rare;
		Slot = EquipSlot.Ring1;
		Icon = GD.Load<Texture2D>(AssetConstants.RingIconPath(2));
		CharacterModifiers.Add(new HealingModifier());
	}

	class HealingModifier : ICharacterModifier
	{
		public void Modify(CharacterStats stats)
		{
			stats.IncreasedHealing += 0.10f;
		}
	}
}
