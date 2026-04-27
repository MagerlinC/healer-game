using Godot;
using healerfantasy;
using healerfantasy.SpellSystem;

namespace healerfantasy.SpellResources;

[GlobalClass]
public partial class DecaySpellResource : SpellResource
{
	[Export] public float DamagePerTick = 10f;
	[Export] public float EffectDuration = 10f;
	[Export] public float TickInterval = 1f;

	public DecaySpellResource()
	{
		Name = "Decay";
		Description =
			$"Atrophies the target, damaging it for {DamagePerTick} void damage every {TickInterval}s for {EffectDuration}s.";
		ManaCost = 6f;
		CastTime = 0.0f;
		// HealOverTime implies Healing; the per-tick value is what gets modified.
		Tags = SpellTags.Damage | SpellTags.Duration | SpellTags.Void;
		EffectType = EffectType.Harmful;
		School = SpellSchool.Void;
		RequiredSchoolPoints = 1;
		Icon = GD.Load<Texture2D>(AssetConstants.SpellIconAssets + "void/decay.png");
	}

	/// <summary>
	/// The base value is the per-tick DoT; modifiers scale each tick.
	/// </summary>
	public override float GetBaseValue()
	{
		return DamagePerTick;
	}

	public override void Apply(SpellContext ctx)
	{
		// ctx.FinalValue is the modifier-adjusted per-tick heal amount.
		ctx.Target?.ApplyEffect(new Effects.DamageOverTimeEffect(ctx.FinalValue, EffectDuration, TickInterval)
		{
			EffectId = Name, // "Decay" — unique per spell, not per class
			Icon = Icon,
			SourceCharacterName = ctx.Caster.CharacterName,
			AbilityName = Name,
			Description = Description,
			School = School,
			HasteMultiplier = 1f + ctx.CasterStats.IncreasedHaste
		});
	}
}