using Godot;
using healerfantasy.SpellSystem;

namespace healerfantasy.Items.Rings;

public partial class SignetOfTheOak : EquippableItem
{

	static readonly float HealthBonus = 10f;
	public override string ItemId => "signet_of_the_oak";

	public SignetOfTheOak()
	{
		Name = "Signet of the Oak";
		Description = $"+{HealthBonus:F0} Maximum Health";
		Rarity = ItemRarity.Rare;
		Slot = EquipSlot.Ring1;
		Icon = GD.Load<Texture2D>(AssetConstants.RingIconPath(1));
		CharacterModifiers.Add(new HealthModifier());
	}

	class HealthModifier : ICharacterModifier
	{
		public void Modify(CharacterStats stats)
		{
			stats.MaxHealth += HealthBonus;
		}
	}
}