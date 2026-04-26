using Godot;
using healerfantasy.SpellSystem;

namespace healerfantasy.Items.Staves;

/// <summary>
/// Arcane Accelerator — Epic staff that can drop from any boss.
/// Imbued with time-warping enchantments that compress the gap between thought
/// and spell, while also amplifying offensive output.
///
/// Stat bonuses: +15% cast speed, +10% damage multiplier.
/// </summary>
public class ArcaneAccelerator : EquippableItem
{
	public override string ItemId => "arcane_accelerator";

	public ArcaneAccelerator()
	{
		Name = "Arcane Accelerator";
		Description = "+15% cast speed.\n+10% damage multiplier.";
		Rarity = ItemRarity.Epic;
		Slot = EquipSlot.Staff;
		Icon = GD.Load<Texture2D>(AssetConstants.StaveIconPath(4));
		CharacterModifiers.Add(new SpeedDamageModifier());
	}

	class SpeedDamageModifier : ICharacterModifier
	{
		public void Modify(CharacterStats stats)
		{
			stats.IncreasedCastSpeed *= 1.15f;
			stats.IncreasedDamage *= 1.10f;
		}
	}
}