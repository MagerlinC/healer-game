using Godot;
using healerfantasy;
using healerfantasy.CombatLog;
using healerfantasy.SpellSystem;

namespace healerfantasy.SpellResources;

/// <summary>
/// Hurls a bolt of venom at an enemy, applying a ember-based poison that
/// deals damage over time. Ember's offensive counterpart to Smoulder.
/// </summary>
[GlobalClass]
public partial class CinderSpell : SpellResource
{
	[Export] public float InstantDamage = 10f;
	[Export] public float DamagePerTick = 10f;
	[Export] public float EffectDuration = 8f;
	[Export] public float TickInterval = 1f;

	public CinderSpell()
	{
		Name = "Cinder";
		Description =
			$"Blasts the target for {InstantDamage} ember damage, and sears the target with smouldering cinders, dealing {DamagePerTick} ember damage every {TickInterval}s for {EffectDuration}s.";
		ManaCost = 9f;
		CastTime = 0.0f;
		School = SpellSchool.Ember;
		Tags = SpellTags.Damage | SpellTags.Duration;
		TargetingType = TargetingType.Enemy;
	}

	public override float GetBaseValue()
	{
		return DamagePerTick;
	}

	public override void Apply(SpellContext ctx)
	{
		// Scale the instant hit by the same modifier ratio the pipeline applied to
		// the DoT tick (ctx.FinalValue / DamagePerTick), so both parts benefit from
		// IncreasedDamage, school bonuses, and crit equally.
		// The Duration tag suppresses central logging and FCT for this spell, so
		// we handle them here for the instant portion only.
		var scaledInstantDamage = ctx.ScaleWithModifiers(InstantDamage);
		var isCrit = ctx.Tags.HasFlag(SpellTags.Critical);

		ctx.Target?.TakeDamage(scaledInstantDamage);

		if (ctx.Target != null)
		{
			ctx.Target.RaiseFloatingCombatText(scaledInstantDamage, false, (int)School, isCrit);

			CombatLog.CombatLog.Record(new CombatEventRecord
			{
				Timestamp = ctx.Timestamp,
				SourceName = ctx.Caster.CharacterName,
				TargetName = ctx.Target.CharacterName,
				AbilityName = Name,
				Amount = scaledInstantDamage,
				Type = CombatEventType.Damage,
				IsCrit = isCrit,
				Description = Description
			});
		}

		ctx.Target?.ApplyEffect(new Effects.DamageOverTimeEffect(ctx.FinalValue, EffectDuration, TickInterval)
		{
			EffectId = Name, // "Cinder" — unique per spell, not per class
			Icon = Icon,
			SourceCharacterName = ctx.Caster.CharacterName,
			AbilityName = Name,
			Description = Description,
			School = School,
			IsDispellable = false,
			HasteMultiplier = 1f + ctx.CasterStats.IncreasedHaste
		});
	}
}