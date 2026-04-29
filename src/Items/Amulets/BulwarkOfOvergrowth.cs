using System;
using Godot;
using healerfantasy.SpellSystem;
using healerfantasy.Talents;

namespace healerfantasy.Items.Amulets;

public class BulwarkOfOvergrowth : EquippableItem
{
	static readonly float ShieldConversion = 0.50f;
	public override string ItemId => "naturalists_clasp";

	public BulwarkOfOvergrowth()
	{
		Name = "Bulwark of Overgrowth";
		Description = $"{ShieldConversion:F0}% of overhealing is turned into shield.";
		Rarity = ItemRarity.Rare;
		Slot = EquipSlot.Amulet;
		Icon = GD.Load<Texture2D>(AssetConstants.AmuletIconPath(2));
		SpellModifiers.Add(new OverhealShieldingModifier());
	}

	class OverhealShieldingModifier : ISpellModifier
	{
		float _startHealth = 0f;
		public ModifierPriority Priority { get; }
		public void OnBeforeCast(SpellContext context)
		{
			_startHealth = context.Target.CurrentHealth;
		}
		public void OnCalculate(SpellContext context)
		{
		}
		public void OnAfterCast(SpellContext context)
		{
			if (Math.Abs(context.Target.CurrentHealth - context.Target.MaxHealth) < 0.005f)
			{
				var successfulHealAmount = context.Target.CurrentHealth - _startHealth;
				var overhealAmount = context.FinalValue - successfulHealAmount;
				context.Target.AddShield(overhealAmount * ShieldConversion);
			}
		}
	}
}