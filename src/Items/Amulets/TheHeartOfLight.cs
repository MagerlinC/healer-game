using Godot;
using healerfantasy.SpellSystem;

namespace healerfantasy.Items.Amulets;

/// <summary>
/// Arcane Accelerator — Epic staff that can drop from any boss.
/// Imbued with time-warping enchantments that compress the gap between thought
/// and spell, while also amplifying offensive output.
///
/// Stat bonuses: +15% haste, +10% damage multiplier.
/// </summary>
public class TheHeartOfLight : EquippableItem
{
	static readonly float DamageReductionAmount = 0.3f;
	public override string ItemId => "the_heart_of_light";

	public TheHeartOfLight()
	{
		Name = "The Heart of Light";
		Description = $"While any holy effect is active on the target, they take {DamageReductionAmount}% less damage.";
		Rarity = ItemRarity.Legendary;
		Slot = EquipSlot.Amulet;
		Icon = GD.Load<Texture2D>(AssetConstants.RingIconPath(5));
		CharacterModifiers.Add(new SpeedDamageModifier());
	}

	class SpeedDamageModifier : ICharacterModifier
	{
		public void Modify(CharacterStats stats)
		{
			// TODO: implement damage reduction logic. This will likely require some refactoring of the CharacterStats and damage calculation pipeline to allow for conditional damage reduction based on active effects on the target.
		}
	}
}