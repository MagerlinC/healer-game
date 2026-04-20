using Godot;
using healerfantasy;
using healerfantasy.SpellSystem;

namespace healerfantasy.SpellResources;

[GlobalClass]
public partial class HealOverTimeSpellResource : SpellResource
{
	[Export] public float HealPerTick = 6f;
	[Export] public float EffectDuration = 10f;
	[Export] public float TickInterval = 1f;

	public HealOverTimeSpellResource()
	{
		Name = "Renewing Light";
		Description = $"Heals the target for {HealPerTick} every {TickInterval}s for {EffectDuration}s.";
		ManaCost = 6f;
		CastTime = 0.0f;
		// HealOverTime implies Healing; the per-tick value is what gets modified.
		Tags = SpellTags.Healing | SpellTags.HealOverTime;
		Icon = GD.Load<Texture2D>("res://assets/spell-icons/healer/healer3.png");
	}

	/// <summary>
	/// The base value is the per-tick heal; modifiers scale each tick.
	/// </summary>
	public override float GetBaseValue()
	{
		return HealPerTick;
	}

	public override void Apply(SpellContext ctx)
	{
		// ctx.FinalValue is the modifier-adjusted per-tick heal amount.
		ctx.Target?.ApplyEffect(new HealOverTimeEffect(ctx.FinalValue, EffectDuration, TickInterval)
		{
			Icon = Icon,
			SourceCharacterName = ctx.Caster.CharacterName,
			AbilityName = Name
		});
	}
}