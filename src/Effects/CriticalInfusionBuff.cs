using healerfantasy.SpellSystem;
using healerfantasy.Talents;

namespace healerfantasy.Effects;

/// <summary>
/// A temporary buff granted by the <c>CriticalInfusion</c> talent when the player
/// scores a critical hit.
///
/// While active, the next damage spell cast by the owner is boosted by 30%.
/// The buff then removes itself so it can only ever fire once.
///
/// Implements both <see cref="CharacterEffect"/> (drives UI display and expiry)
/// and <see cref="ISpellModifier"/> (intercepts the pipeline to apply the bonus).
/// <see cref="Character.GetSpellModifiers"/> returns any active effect that
/// implements <see cref="ISpellModifier"/>, so this buff is automatically wired
/// into the cast pipeline while it is alive.
/// </summary>
public partial class CriticalInfusionBuff : CharacterEffect, ISpellModifier
{
	const float BonusScaling = 1.30f; // +30 %

	public ModifierPriority Priority => ModifierPriority.BASE;

	public CriticalInfusionBuff(float duration)
		: base(duration, 0f)
	{
		EffectId = "CriticalInfusion";
	}

	// ── ISpellModifier ───────────────────────────────────────────────────────
	public void OnBeforeCast(SpellContext ctx)
	{
	}

	public void OnCalculate(SpellContext ctx)
	{
		if (ctx.Tags.HasFlag(SpellTags.Damage) || ctx.Tags.HasFlag(SpellTags.Healing))
			ctx.FinalValue *= BonusScaling;
	}

	public void OnAfterCast(SpellContext ctx)
	{
		// Consume the buff after it has boosted a damage spell.
		if (ctx.Tags.HasFlag(SpellTags.Damage) || ctx.Tags.HasFlag(SpellTags.Healing))
			ctx.Caster.RemoveEffect(EffectId);
	}

	// ── CharacterEffect overrides ────────────────────────────────────────────
	public override void OnApplied(Character target)
	{
	}
	public override void OnExpired(Character target)
	{
	}
}