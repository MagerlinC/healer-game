using Godot;
using healerfantasy;
using healerfantasy.CombatLog;
using healerfantasy.SpellSystem;

namespace healerfantasy.SpellResources;

/// <summary>
/// Hurls a bolt of venom at an enemy, applying a nature-based poison that
/// deals damage over time. Nature's offensive counterpart to Renewing Bloom.
/// </summary>
[GlobalClass]
public partial class PoisonBoltSpell : SpellResource
{
	[Export] public float InstantDamage = 10f;
	[Export] public float DamagePerTick = 10f;
	[Export] public float EffectDuration = 8f;
	[Export] public float TickInterval = 1f;

	public PoisonBoltSpell()
	{
		Name = "Poison Bolt";
		Description =
			$"Blasts the target for {InstantDamage} nature damage, and infects the target with virulent poison, dealing {DamagePerTick} nature damage every {TickInterval}s for {EffectDuration}s.";
		ManaCost = 7f;
		CastTime = 0.0f;
		School = SpellSchool.Nature;
		Tags = SpellTags.Damage | SpellTags.Duration;
		EffectType = EffectType.Harmful;
		Icon = GD.Load<Texture2D>(AssetConstants.SpellIconAssets + "nature/poison-bolt.png");
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
		var scale = DamagePerTick > 0f ? ctx.FinalValue / DamagePerTick : 1f;
		var scaledInstantDamage = InstantDamage * scale;
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
			EffectId = Name, // "Poison Bolt" — unique per spell, not per class
			Icon = Icon,
			SourceCharacterName = ctx.Caster.CharacterName,
			AbilityName = Name,
			Description = Description,
			School = School,
			HasteMultiplier = 1f + ctx.CasterStats.IncreasedHaste
		});
	}
}