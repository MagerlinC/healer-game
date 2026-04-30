using Godot;
using healerfantasy;
using healerfantasy.CombatLog;
using healerfantasy.SpellSystem;

namespace healerfantasy.SpellResources;

[GlobalClass]
public partial class RenewingBloomSpell : SpellResource
{
	[Export] public float HealPerTick = 6f;
	[Export] public float EffectDuration = 10f;
	[Export] public float TickInterval = 1f;

	public RenewingBloomSpell()
	{
		Name = "Renewing Bloom";
		Description = $"Heals the target for {HealPerTick} every {TickInterval}s for {EffectDuration}s.";
		ManaCost = 6f;
		CastTime = 0.0f;
		School = SpellSchool.Nature;
		// HealOverTime implies Healing; the per-tick value is what gets modified.
		Tags = SpellTags.Healing | SpellTags.Duration;
		Icon = GD.Load<Texture2D>(AssetConstants.SpellIconAssets + "healer/healer1.png");
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
		ctx.Target?.ApplyEffect(new Effects.HealOverTimeEffect(ctx.FinalValue, EffectDuration, TickInterval)
		{
			EffectId = Name,
			Icon = Icon,
			SourceCharacterName = ctx.Caster.CharacterName,
			AbilityName = Name,
			Description = Description,
			School = School,
			HasteMultiplier = 1f + ctx.CasterStats.IncreasedHaste
		});
	}
}