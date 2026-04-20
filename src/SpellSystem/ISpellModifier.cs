using healerfantasy.Talents;

namespace healerfantasy.SpellSystem;

/// <summary>
/// Hooks into the three phases of the spell-cast pipeline.
/// Implement this on a <see cref="Talent"/> (or a buff <see cref="CharacterEffect"/>)
/// to intercept and alter spell behaviour.
///
/// Modifiers are sorted ascending by <see cref="Priority"/> before each cast.
/// Lower numbers run first. Suggested ranges:
///   0  – base/unconditional multipliers
///  10  – elemental / school bonuses
///  20  – conditional bonuses (e.g. tag-gated)
///  50  – consumable buffs
///  90  – "last word" modifiers (e.g. ArcaneMastery)
/// </summary>
public interface ISpellModifier
{
	ModifierPriority Priority { get; }

	/// <summary>
	/// Phase 1 — runs before any values are computed.
	/// Use for setup, gating, or context enrichment (e.g. adding extra tags).
	/// </summary>
	void OnBeforeCast(SpellContext context);

	/// <summary>
	/// Phase 2 — mutate <see cref="SpellContext.FinalValue"/>.
	/// The pipeline has already applied stat multipliers (DamageMultiplier,
	/// HealingMultiplier) before this phase runs.
	/// </summary>
	void OnCalculate(SpellContext context);

	/// <summary>
	/// Phase 3 — runs after the crit roll and after <see cref="OnCalculate"/>.
	/// <see cref="SpellContext.Tags"/> will contain <see cref="SpellTags.Critical"/>
	/// if the cast critted. Use this phase for reactions (apply buffs, record state, etc.)
	/// The spell itself has NOT yet been applied when this runs.
	/// </summary>
	void OnAfterCast(SpellContext context);
}