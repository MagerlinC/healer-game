using System;
using Godot;
using healerfantasy.SpellResources;
using healerfantasy.SpellSystem;
using healerfantasy.Talents;

namespace healerfantasy.Items.Rings;

/// <summary>
/// Ring of Triage — Legendary ring that can drop from any boss.
/// A healer's instinct crystallised into gemstone: this ring pulses warmest
/// for those who need it most. Single-target healing spells on targets
/// below 30% health are amplified by 50%.
///
/// Implemented by checking the primary target's health fraction during the
/// calculate phase. Only triggers on single-target heals (no GroupSpell tag)
/// to preserve the intended triage fantasy.
/// </summary>
public class RingOfTriage : EquippableItem
{
	static readonly float _hpThreshold = 0.30f; // below 30% HP
	static readonly float _bonusMultiplier = 0.50f; // +50% healing

	public override string ItemId => "ring_of_triage";

	public RingOfTriage()
	{
		Name = "Ring of Triage";
		Description =
			$"Single-target heals on targets below {Math.Round(_hpThreshold * 100)}% health deal {Math.Round(_bonusMultiplier * 100)}% more.";
		Rarity = ItemRarity.Legendary;
		Slot = EquipSlot.Ring1;
		Icon = GD.Load<Texture2D>(AssetConstants.RingIconPath(4));
		SpellModifiers.Add(new TriageModifier(_hpThreshold, _bonusMultiplier));
	}

	class TriageModifier : ISpellModifier
	{
		readonly float _threshold;
		readonly float _bonus;

		public TriageModifier(float threshold, float bonus)
		{
			_threshold = threshold;
			_bonus = bonus;
		}

		public ModifierPriority Priority => ModifierPriority.BASE;

		public void OnBeforeCast(SpellContext context)
		{
		}

		public void OnCalculate(SpellContext context)
		{
			// Only single-target healing spells trigger the triage bonus.
			if (!context.Tags.HasFlag(SpellTags.Healing)) return;
			if (context.Tags.HasFlag(SpellTags.GroupSpell)) return;

			var target = context.Target;
			if (target == null || !target.IsFriendly) return;

			// Guard against divide-by-zero for characters with 0 MaxHealth.
			if (target.MaxHealth <= 0f) return;

			var hpFraction = target.CurrentHealth / target.MaxHealth;
			if (hpFraction < _threshold)
				context.FinalValue *= 1f + _bonus;
		}

		public void OnAfterCast(SpellContext context)
		{
		}
	}
}