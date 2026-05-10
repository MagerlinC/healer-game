using System.Linq;
using Godot;
using healerfantasy.CombatLog;
using healerfantasy.SpellResources;
using healerfantasy.SpellSystem;
using healerfantasy.Talents;

namespace healerfantasy.Effects;

public partial class ArchangelOfLightEffect : CharacterEffect, ISpellModifier, ICharacterModifier
{

	float _healingIncrease;
	float _totalPartyHealthBeforeCast;
	public ArchangelOfLightEffect(float duration, float healingIncrease, float tickInterval = 0) : base(duration, tickInterval)
	{
		EffectId = "ArchangelOfLightEffect";
		_healingIncrease = healingIncrease;
	}

	public void Modify(CharacterStats stats)
	{
		stats.ManaCostMultiplier = 0f;
	}

	public ModifierPriority Priority { get; } = ModifierPriority.BASE;
	public void OnBeforeCast(SpellContext context)
	{
		if (!context.IsSpellOfSchool(SpellSchool.Holy) || !context.Spell.Tags.HasFlag(SpellTags.Healing)) return;

		var partyMembers = context.Caster.CollectAlivePartyMembers();
		_totalPartyHealthBeforeCast = partyMembers.Sum(pm => pm.CurrentHealth);
	}
	public void OnCalculate(SpellContext context)
	{
		if (context.Spell.School == SpellSchool.Holy && context.Spell.Tags.HasFlag(SpellTags.Healing))
		{
			context.FinalValue *= _healingIncrease;
		}
	}
	public void OnAfterCast(SpellContext context)
	{
		if (!context.IsSpellOfSchool(SpellSchool.Holy) || !context.Spell.Tags.HasFlag(SpellTags.Healing)) return;
		// Heal all allies by excess healing, and damage all enemies by the same amount
		var partyMembers = context.Caster.CollectAlivePartyMembers();
		var totalPartyHealthAfterCast = partyMembers.Sum(pm => pm.CurrentHealth);
		var excessHealing = totalPartyHealthAfterCast - _totalPartyHealthBeforeCast;

		if (!(excessHealing > 0)) return;

		foreach (var ally in partyMembers.Where(ally => ally != context.Target))
		{
			context.Caster.RaiseFloatingCombatText(excessHealing, true, (int)School, false);
			CombatLog.CombatLog.Record(new CombatEventRecord
			{
				Timestamp = Time.GetTicksMsec() / 1000.0,
				SourceName = SourceCharacterName,
				TargetName = ally.CharacterName,
				AbilityName = AbilityName ?? EffectId,
				Amount = excessHealing,
				Description = Description,
				Type = CombatEventType.Healing,
				IsCrit = false
			});
			ally.Heal(excessHealing);
		}

		foreach (var enemy in context.Caster.CollectAliveEnemies())
		{

			enemy.RaiseFloatingCombatText(excessHealing, false, (int)School, false);
			CombatLog.CombatLog.Record(new CombatEventRecord
			{
				Timestamp = Time.GetTicksMsec() / 1000.0,
				SourceName = SourceCharacterName,
				TargetName = enemy.CharacterName,
				AbilityName = AbilityName ?? EffectId,
				Amount = excessHealing,
				Description = Description,
				Type = CombatEventType.Damage,
				IsCrit = false
			});
			enemy.TakeDamage(excessHealing);
		}
	}
}