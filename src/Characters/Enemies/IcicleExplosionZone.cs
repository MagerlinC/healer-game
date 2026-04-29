using Godot;
using healerfantasy;
using healerfantasy.CombatLog;
using healerfantasy.SpellResources;

/// <summary>
/// A permanent ground hazard spawned when a <see cref="VolatileIcicleProjectile"/>
/// explodes. Renders as a blue semi-transparent circle and deals damage per
/// second to any living party member standing within <see cref="Radius"/> pixels.
///
/// The zone persists until the scene is freed (end of the encounter). Multiple
/// zones accumulate over the fight — the intended pressure is that the arena
/// gradually fills with frost, punishing the party for failing to kite
/// icicles to the edges.
/// </summary>
public partial class IcicleExplosionZone : Node2D
{
	// ── tunables ──────────────────────────────────────────────────────────────
	/// <summary>Visual and collision radius in world pixels.</summary>
	public const float Radius = 60f;

	// ── colours ───────────────────────────────────────────────────────────────
	static readonly Color FillColour = new(0.25f, 0.55f, 1.0f, 0.30f); // icy blue fill
	static readonly Color BorderColour = new(0.50f, 0.80f, 1.0f, 0.85f); // bright ice border
	const float BorderWidth = 2.0f;

	// ── config ────────────────────────────────────────────────────────────────
	readonly float _damagePerTick;

	// ── runtime ───────────────────────────────────────────────────────────────
	float _tickTimer = 1f;

	// ── ctor ──────────────────────────────────────────────────────────────────
	public IcicleExplosionZone(float damagePerTick)
	{
		_damagePerTick = damagePerTick;
	}

	// ── lifecycle ─────────────────────────────────────────────────────────────
	public override void _Ready()
	{
		ZIndex = 1; // render below characters but above the background
		QueueRedraw();
	}

	public override void _Process(double delta)
	{
		_tickTimer -= (float)delta;
		if (_tickTimer <= 0f)
		{
			_tickTimer = 1f;
			DamageOccupants();
		}
	}

	public override void _Draw()
	{
		// Draw centred on this node's local origin.
		DrawCircle(Vector2.Zero, Radius, FillColour);

		// Draw the border as a series of points approximating a circle.
		// Godot 4's DrawArc handles this cleanly.
		DrawArc(Vector2.Zero, Radius, 0f, Mathf.Tau, 48, BorderColour, BorderWidth, true);
	}

	// ── private ───────────────────────────────────────────────────────────────

	void DamageOccupants()
	{
		foreach (var node in GetTree().GetNodesInGroup("party"))
		{
			if (node is not Character target || !target.IsAlive) continue;
			if (GlobalPosition.DistanceTo(target.GlobalPosition) > Radius) continue;

			target.TakeDamage(_damagePerTick);
			target.RaiseFloatingCombatText(_damagePerTick, false, (int)SpellSchool.Generic, false);

			CombatLog.Record(new CombatEventRecord
			{
				Timestamp = Time.GetTicksMsec() / 1000.0,
				SourceName = GameConstants.FrozenPeakBossName,
				TargetName = target.CharacterName,
				AbilityName = "Volatile Icicle",
				Amount = _damagePerTick,
				Type = CombatEventType.Damage,
				IsCrit = false,
				Description = "Standing in frozen ground left by the Queen's icicle."
			});
		}
	}
}