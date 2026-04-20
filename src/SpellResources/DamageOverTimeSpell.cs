using Godot;
using healerfantasy;
using healerfantasy.SpellSystem;

namespace healerfantasy.SpellResources;

[GlobalClass]
public partial class DamageOverTimeSpellResource : SpellResource
{
	[Export] public float DamagePerTick = 10f;
	[Export] public float EffectDuration = 10f;
	[Export] public float TickInterval = 1f;

	public DamageOverTimeSpellResource()
	{
		Name = "Renewing Light";
		Description = $"Damages the target for {DamagePerTick} every {TickInterval}s for {EffectDuration}s.";
		ManaCost = 6f;
		CastTime = 0.0f;
		// HealOverTime implies Healing; the per-tick value is what gets modified.
		Tags = SpellTags.Damage | SpellTags.Duration;
		TargetType = TargetType.Enemy;
		Icon = GD.Load<Texture2D>("res://assets/spell-icons/healer/healer5.png");
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
		ctx.Target?.ApplyEffect(new DamageOverTimeEffect(ctx.FinalValue, EffectDuration, TickInterval)
		{
			Icon = Icon,
			SourceCharacterName = ctx.Caster.CharacterName,
			AbilityName = Name
		});
	}
}