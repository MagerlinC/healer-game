using healerfantasy.SpellSystem;

namespace healerfantasy.Effects;

/// <summary>
/// A stacking buff that increases the caster's cast speed by
/// <see cref="CastSpeedPerStack"/> per stack (default 10 %).
///
/// Stacks are added each time the buff is re-applied (up to
/// <see cref="MaxStacks"/>), and the duration is refreshed on every
/// application. Implements <see cref="ICharacterModifier"/> so that
/// <see cref="Character.GetCharacterStats"/> automatically picks up
/// the cast-speed contribution while the buff is active.
/// </summary>
public partial class AccelerationEffect : CharacterEffect, ICharacterModifier
{
	public const int MaxStacks = 5;

	/// <summary>Cast-speed bonus added to <see cref="CharacterStats.IncreasedCastSpeed"/> per stack.</summary>
	public float CastSpeedPerStack { get; } = 0.10f; // +10 % per stack

	public AccelerationEffect(float duration)
		: base(duration, 0f)
	{
		EffectId = "Acceleration";
		MaximumStacks = MaxStacks;
	}

	// ── stacking ─────────────────────────────────────────────────────────────

	/// <summary>
	/// Refreshes duration and adds a stack (up to <see cref="MaxStacks"/>).
	/// Called by <see cref="Character.ApplyEffect"/> when the buff is already
	/// active — the existing instance is kept; <paramref name="newEffect"/> is discarded.
	/// </summary>
	public override void OnReapplied(Character target, CharacterEffect newEffect)
	{
		Refresh();
		if (CurrentStacks < MaximumStacks)
			CurrentStacks++;
	}

	// ── ICharacterModifier ────────────────────────────────────────────────────

	/// <summary>
	/// Contributes cast-speed to the character's stat snapshot.
	/// Called automatically by <see cref="Character.GetCharacterStats"/> while
	/// this effect is active.
	/// </summary>
	public void Modify(CharacterStats stats)
	{
		stats.IncreasedCastSpeed += CastSpeedPerStack * CurrentStacks;
	}
}