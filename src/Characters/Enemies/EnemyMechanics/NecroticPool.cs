using Godot;
using healerfantasy.CombatLog;
using healerfantasy.SpellResources;

/// <summary>
/// A world-space hazard node spawned by <see cref="healerfantasy.SpellResources.BossNecroticPoolSpell"/>.
///
/// Unlike <see cref="DetonationZone"/> (which detonates once), the Necrotic Pool
/// is a lingering patch of void energy that pulses damage every second for its
/// full <see cref="LifetimeDuration"/>. Any alive party member whose position
/// lies within the pool radius takes <see cref="DamagePerPulse"/> each tick.
///
/// Counterplay: move out of the pool before the next pulse.
///
/// The pool fades and removes itself after all pulses have fired.
/// </summary>
public partial class NecroticPool : Node2D
{
	// ── tuneable constants ─────────────────────────────────────────────────────

	/// <summary>Radius of the circular danger zone in world units.</summary>
	public const float PoolRadius = 55f;

	/// <summary>Total time the pool lingers (seconds).</summary>
	public const float LifetimeDuration = 4f;

	/// <summary>Seconds between damage pulses.</summary>
	public const float PulseInterval = 1f;

	// ── instance data ──────────────────────────────────────────────────────────

	/// <summary>
	/// Damage dealt per pulse to each party member caught inside the pool.
	/// Set by <see cref="healerfantasy.SpellResources.BossNecroticPoolSpell.Apply"/>.
	/// </summary>
	public float DamagePerPulse { get; set; }

	// ── visuals ────────────────────────────────────────────────────────────────

	// Deep purple fill — distinct from the red DetonationZone
	static readonly Color FillColour = new(0.30f, 0.00f, 0.55f, 0.30f);
	static readonly Color BorderColour = new(0.65f, 0.10f, 1.00f, 0.90f);
	const float BorderWidth = 2.5f;

	// ── internal state ─────────────────────────────────────────────────────────

	float _lifetime = LifetimeDuration;
	float _pulseTimer = PulseInterval; // fires first pulse after 1 s
	bool _expired;

	// ── lifecycle ──────────────────────────────────────────────────────────────

	public override void _Ready()
	{
		QueueRedraw();
	}

	public override void _Process(double delta)
	{
		if (_expired) return;

		_lifetime -= (float)delta;
		_pulseTimer -= (float)delta;

		if (_pulseTimer <= 0f)
		{
			Pulse();
			_pulseTimer = PulseInterval;
		}

		if (_lifetime <= 0f)
		{
			_expired = true;
			QueueFree();
		}

		// Fade the fill as the pool ages so players can anticipate it ending.
		QueueRedraw();
	}

	// ── rendering ──────────────────────────────────────────────────────────────

	public override void _Draw()
	{
		// Lerp alpha so the pool visually fades out over its lifetime.
		var alpha = Mathf.Clamp(_lifetime / LifetimeDuration, 0f, 1f);

		var fill = new Color(FillColour.R, FillColour.G, FillColour.B, FillColour.A * alpha);
		var border = new Color(BorderColour.R, BorderColour.G, BorderColour.B, BorderColour.A * alpha);

		DrawCircle(Vector2.Zero, PoolRadius, fill);
		// Draw border as a ring of arc segments (Godot 4 has no stroke-circle primitive).
		DrawArc(Vector2.Zero, PoolRadius, 0f, Mathf.Tau, 64, border, BorderWidth);
	}

	// ── pulse ──────────────────────────────────────────────────────────────────

	void Pulse()
	{
		foreach (var node in GetTree().GetNodesInGroup("party"))
		{
			if (node is not Character target || !target.IsAlive) continue;

			// Distance check — pool is circular.
			if (GlobalPosition.DistanceTo(target.GlobalPosition) > PoolRadius) continue;

			target.TakeDamage(DamagePerPulse);

			target.RaiseFloatingCombatText(
				DamagePerPulse,
				false,
				(int)SpellSchool.Void,
				false);

			CombatLog.Record(new CombatEventRecord
			{
				Timestamp = Time.GetTicksMsec() / 1000.0,
				SourceName = healerfantasy.GameConstants.ForsakenBoss3Name,
				TargetName = target.CharacterName,
				AbilityName = "Necrotic Pool",
				Amount = DamagePerPulse,
				Type = CombatEventType.Damage,
				IsCrit = false,
				Description = "A swirling pool of void energy left by the Flying Skull that pulses damage to anyone standing inside."
			});
		}
	}
}