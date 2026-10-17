using System;
using Godot;
using healerfantasy.SpellSystem;
using healerfantasy.Talents;

namespace healerfantasy.Items.Amulets;

public class BulwarkOfCinders : EquippableItem
{
	static readonly float ShieldConversion = 0.50f;
	public override string ItemId => "bulwark_of_cinders";

	public BulwarkOfCinders()
	{
		Name = "Bulwark of Cinders";
		Description = $"{100 * ShieldConversion:F0}% of light that overflows a full target is turned into shield.";
		Rarity = ItemRarity.Epic;
		Slot = EquipSlot.Amulet;
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