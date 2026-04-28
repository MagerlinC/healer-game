using Godot;
using healerfantasy.SpellResources;

namespace healerfantasy.Effects;

/// <summary>
/// A non-dispellable, ramping damage-over-time debuff applied to the whole party
/// during The Countess's Court of Reflections mechanic.
///
/// The debuff deals <see cref="BaseDamage"/> on its first tick, and increases by
/// <see cref="RampPerTick"/> every subsequent tick, punishing groups that take
/// too long to identify the real boss among her reflections.
///
/// Dispel interaction
/// ──────────────────
/// <see cref="IsDispellable"/> is false — the player cannot cleanse this debuff.
/// Only resolving the mechanic (finding the real Countess) removes it.
/// </summary>
public partial class CourtOfReflectionsEffect : CharacterEffect
{
	static readonly float EffectDuration = GameConstants.InfiniteDuration;

	const float TickInterval = 2f;

	public float BaseDamage { get; }
	public float RampPerTick { get; }

	/// <param name="baseDamage">Damage dealt on the first tick.</param>
	/// <param name="rampPerTick">Additional damage added each successive tick.</param>
	public CourtOfReflectionsEffect(float baseDamage = 8f, float rampPerTick = 5f)
		: base(EffectDuration, TickInterval)
	{
		BaseDamage = baseDamage;
		RampPerTick = rampPerTick;

		EffectId = "CourtOfReflections";
		AbilityName = "Court of Reflections";
		Description = "A shifting hex binds you in its web. The longer it lingers, the deeper it burns.";
		School = SpellSchool.Void;
		Icon = GD.Load<Texture2D>(AssetConstants.SpellIconAssets + "enemy/the-countess/court-of-reflections.png");
		// Not dispellable — only resolving the mechanic removes this.
		IsDispellable = false;
		IsHarmful = true;
	}

	protected override void OnTick(Character target)
	{
		// Tick number starts at 0 for the first tick.
		var elapsed = EffectDuration - Remaining;
		var tickNumber = Mathf.FloorToInt(elapsed / TickInterval);

		var damage = BaseDamage + RampPerTick * tickNumber;
		target.TakeDamage(damage);
		target.RaiseFloatingCombatText(damage, false, (int)School, false);
	}
}