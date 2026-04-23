using Godot;
using healerfantasy.CombatLog;
using healerfantasy.SpellResources;

/// <summary>
/// A world-space hazard node spawned by <see cref="healerfantasy.SpellResources.BossDetonationZoneSpell"/>.
///
/// The zone renders as a red rectangle centred on its
/// <see cref="Node2D.GlobalPosition"/> (snapped to the player's position at
/// cast time). After <see cref="FuseDuration"/> seconds it detonates:
/// every alive party member whose <see cref="Node2D.GlobalPosition"/> lies
/// within the zone bounds takes <see cref="DamageAmount"/> damage.
///
/// The node removes itself from the scene immediately after detonating.
/// </summary>
public partial class DetonationZone : Node2D
{
	// ── tuneable constants ─────────────────────────────────────────────────────

	/// <summary>Width and height of the danger square in world units.</summary>
	public static readonly Vector2 ZoneSize = new(80f, 80f);

	/// <summary>Seconds between zone placement and detonation.</summary>
	public const float FuseDuration = 3.0f;

	// ── instance data ──────────────────────────────────────────────────────────

	/// <summary>
	/// Damage dealt to each party member caught inside when the zone detonates.
	/// Set by <see cref="healerfantasy.SpellResources.BossDetonationZoneSpell.Apply"/>
	/// from the pipeline's <c>FinalValue</c> (already includes damage multipliers
	/// and any crit scaling).
	/// </summary>
	public float DamageAmount { get; set; }

	// ── visuals ────────────────────────────────────────────────────────────────

	// Semi-transparent red fill
	static readonly Color FillColour = new(1f, 0.10f, 0.05f, 0.28f);

	// Bright red-orange border
	static readonly Color BorderColour = new(1f, 0.30f, 0.10f, 0.90f);
	const float BorderWidth = 2.5f;

	// ── internal state ─────────────────────────────────────────────────────────

	float _fuse = FuseDuration;
	bool _detonated;

	// ── lifecycle ──────────────────────────────────────────────────────────────

	public override void _Ready()
	{
		// Trigger the initial draw pass.
		QueueRedraw();
	}

	public override void _Process(double delta)
	{
		_fuse -= (float)delta;
		if (_fuse <= 0f && !_detonated)
			Detonate();
	}

	// ── rendering ──────────────────────────────────────────────────────────────

	public override void _Draw()
	{
		// Draw centred on this node's local origin so GlobalPosition places
		// the centre of the zone over the player's feet.
		var rect = new Rect2(-ZoneSize / 2f, ZoneSize);
		DrawRect(rect, FillColour, true);
		DrawRect(rect, BorderColour, false, BorderWidth);
	}

	// ── detonation ─────────────────────────────────────────────────────────────

	void Detonate()
	{
		_detonated = true;

		// The bounding rectangle in world space.
		var bounds = new Rect2(GlobalPosition - ZoneSize / 2f, ZoneSize);

		foreach (var node in GetTree().GetNodesInGroup("party"))
		{
			if (node is not Character target || !target.IsAlive) continue;
			if (!bounds.HasPoint(target.GlobalPosition)) continue;

			target.TakeDamage(DamageAmount);

			// Emit floating combat text directly on the target (same pattern
			// used by DamageOverTimeEffect ticks).
			target.RaiseFloatingCombatText(
				DamageAmount,
				false,
				(int)SpellSchool.Generic,
				false);

			// Log the hit so it appears in the combat meter.
			CombatLog.Record(new CombatEventRecord
			{
				Timestamp = Time.GetTicksMsec() / 1000.0,
				SourceName = healerfantasy.GameConstants.Boss3Name,
				TargetName = target.CharacterName,
				AbilityName = "Detonation Zone",
				Amount = DamageAmount,
				Type = CombatEventType.Damage,
				IsCrit = false
			});
		}

		QueueFree();
	}
}