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
public class RingOfMending : EquippableItem
{
	static readonly float _healingBonus = 0.10f;
	public override string ItemId => "ring_of_mending";

	public RingOfMending()
	{
		Name = "Ring of Mending";
		Description = $"+{100 * _healingBonus:F0}% healing";
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