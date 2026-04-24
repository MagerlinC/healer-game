using healerfantasy.SpellResources;
using healerfantasy.SpellSystem;

namespace healerfantasy.Talents.Chronomancy;

/// <summary>
/// Chronomancy spells bend the flow of time in their wake, making the
/// next non-Chronomancy spell instant to cast.
///
/// Weaving Chronomancy spells between other schools removes the cast time
/// entirely — the temporal momentum from a Rewind or Time Loop carries
/// forward into your next action.
/// Encourages mixing Chronomancy into otherwise school-focused builds.
/// </summary>
public class TemporalMomentumTalent : ISpellModifier, ICharacterModifier
{
	bool _momentumReady;

	public ModifierPriority Priority => ModifierPriority.BASE;

	// ── ICharacterModifier ────────────────────────────────────────────────────

	/// <summary>
	/// Flags the next cast as instant while momentum is primed.
	/// Called by <see cref="Character.GetCharacterStats"/> every time stats are
	/// computed, so Player can read it before deciding whether to start a timer.
	/// </summary>
	public void Modify(CharacterStats stats)
	{
		if (_momentumReady)
			stats.NextCastIsInstant = true;
	}

	// ── ISpellModifier ────────────────────────────────────────────────────────

	public void OnBeforeCast(SpellContext ctx)
	{
		// Consume momentum when a non-Chronomancy spell fires.
		// (Player.cs already skipped the cast timer; we just clear the flag here
		// so the next Chronomancy cast can re-prime it.)
		if (!_momentumReady) return;
		if (ctx.Spell.School == SpellSchool.Chronomancy) return;

		_momentumReady = false;
	}

	public void OnCalculate(SpellContext ctx)
	{
	}

	public void OnAfterCast(SpellContext ctx)
	{
		// Prime momentum after any Chronomancy cast.
		if (ctx.Spell.School == SpellSchool.Chronomancy)
			_momentumReady = true;
	}
}
