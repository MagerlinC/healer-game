using Godot;
using healerfantasy.SpellSystem;
using healerfantasy.Talents;

namespace healerfantasy.Items.Rings;

public class BandOfArcaneRecovery : EquippableItem
{
	static readonly float ManaRestorationRate = 0.1f;
	public BandOfArcaneRecovery()
	{
		Name = "Band of Arcane Recovery";
		Description = $"Healing a target refunds mana equal to {100 * ManaRestorationRate:F0}% of the amount healed.";
		Rarity = ItemRarity.Legendary;
		Slot = EquipSlot.Ring1;
		Icon = GD.Load<Texture2D>(AssetConstants.RingIconPath(6));
		SpellModifiers.Add(new ManaRecoupEffect(ManaRestorationRate));
	}

	public override string ItemId { get; } = "band_of_arcane_recovery";


	class ManaRecoupEffect : ISpellModifier
	{
		float _manaRestorationRate = 0f;
		public ManaRecoupEffect(float restorationRate)
		{
			_manaRestorationRate = restorationRate;

		}
		public ModifierPriority Priority { get; } = ModifierPriority.BASE;
		public void OnBeforeCast(SpellContext context)
		{
		}
		public void OnCalculate(SpellContext context)
		{
		}
		public void OnAfterCast(SpellContext context)
		{
			var manaRecovery = context.FinalValue * _manaRestorationRate;
			context.Caster.RestoreMana(manaRecovery);
		}
	}
}