using healerfantasy.SpellResources;
using healerfantasy.SpellSystem;

namespace healerfantasy.Talents.Chronomancy;

/// <summary>
/// Chronomancy spells bend the flow of time in their wake, briefly making the
/// next non-Chronomancy spell cheaper to cast.
///
/// Weaving Chronomancy spells between other schools costs less — the temporal
/// momentum from a Rewind or Time Loop carries forward into your next action.
/// Encourages mixing Chronomancy into otherwise school-focused builds.
/// </summary>
public class TemporalMomentumTalent : ISpellModifier
{
	const float ManaSaving = 5f;

	bool _momentumReady;

	public ModifierPriority Priority => ModifierPriority.BASE;

	public void OnBeforeCast(SpellContext ctx)
	{
		// If momentum is primed and this is NOT a Chronomancy spell,
		// refund the mana saving. The mana has already been spent by the
		// time the pipeline calls OnBeforeCast, so we restore it here.
		if (!_momentumReady) return;
		if (ctx.Spell.School == SpellSchool.Chronomancy) return;

		_momentumReady = false;
		ctx.Caster.RestoreMana(ManaSaving);
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