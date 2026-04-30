using Godot;
using healerfantasy;

/// <summary>
/// A non-player party member (Templar, Assassin, Wizard).
///
/// Target tracking
/// ───────────────
/// When the player explicitly attacks a boss, <see cref="NotifyPlayerAttackedBoss"/>
/// is called with that boss. All party members then prefer to attack the same
/// target, creating the "switch targets" mechanic needed for the Astral Twins
/// fight and making the party feel responsive to player decisions.
/// </summary>
public partial class PartyMember : Character
{
	// ── Shared targeting state ────────────────────────────────────────────────

	// ── Knockback stun ────────────────────────────────────────────────────────

	/// <summary>
	/// Seconds of movement pause remaining after a knockback. While this is
	/// greater than zero <see cref="IsKnockedBack"/> returns true and the
	/// Templar/Assassin movement loops should skip their advance logic.
	/// Ticked down in <see cref="_Process"/>.
	/// </summary>
	float _knockbackStunTimer;

	/// <summary>True while the party member is stunned by a knockback and should not move.</summary>
	public bool IsKnockedBack => _knockbackStunTimer > 0f;

	/// <summary>
	/// Applies a positional knockback and begins a movement stun of
	/// <paramref name="stunDuration"/> seconds. Clamps the resulting position
	/// to the active arena boundary if one is set.
	/// </summary>
	public void ApplyKnockback(Vector2 direction, float distance, float stunDuration = 1.5f)
	{
		GlobalPosition += direction * distance;
		ClampToArenaBoundary();
		Velocity = Vector2.Zero;
		_knockbackStunTimer = stunDuration;
	}

	/// <summary>
	/// Teleports the party member directly to <paramref name="destination"/> and
	/// begins a knockback stun of <paramref name="stunDuration"/> seconds.
	/// Use this instead of <see cref="ApplyKnockback"/> when the target position
	/// has already been computed (e.g. the arena boundary edge).
	/// </summary>
	public void KnockbackTo(Vector2 destination, float stunDuration = 1.5f)
	{
		GlobalPosition         = destination;
		Velocity               = Vector2.Zero;
		_knockbackStunTimer    = stunDuration;
	}

	/// <summary>
	/// Stuns the party member in place for <paramref name="duration"/> seconds
	/// without changing their position. While stunned <see cref="IsKnockedBack"/>
	/// returns true and movement logic is suppressed, allowing an external tween
	/// (e.g. from <c>ConeOfColdPhase</c>) to move them smoothly.
	/// </summary>
	public void StunInPlace(float duration)
	{
		Velocity            = Vector2.Zero;
		_knockbackStunTimer = duration;
	}

	/// <summary>
	/// Immediately clears any active knockback / stun, allowing the party member
	/// to resume normal movement on the next frame.
	/// </summary>
	public void ClearKnockbackStun()
	{
		_knockbackStunTimer = 0f;
	}

	public override void _Process(double delta)
	{
		base._Process(delta);
		if (_knockbackStunTimer > 0f)
			_knockbackStunTimer = Mathf.Max(_knockbackStunTimer - (float)delta, 0f);
	}

	// ── Arena boundary ────────────────────────────────────────────────────────

	/// <summary>
	/// Optional elliptical arena boundary set by <c>World</c> at fight start.
	/// When non-null, <see cref="ClampToArenaBoundary"/> will push any party
	/// member that has strayed outside the ellipse back to its nearest edge.
	/// Set to null between fights to disable the constraint.
	/// </summary>
	public static (Vector2 Center, float RadiusX, float RadiusY)? ArenaBoundary { get; set; }

	/// <summary>
	/// Clamps <paramref name="node"/>'s <c>GlobalPosition</c> to the active
	/// <see cref="ArenaBoundary"/> ellipse.  Use this from outside
	/// <see cref="PartyMember"/> (e.g. from a spell class) when you need to
	/// constrain any <see cref="Node2D"/> — including the Player — after an
	/// externally applied position change such as a knockback.
	/// No-op when <see cref="ArenaBoundary"/> is null.
	/// </summary>
	public static void ClampNodeToArenaBoundary(Node2D node)
	{
		if (ArenaBoundary is not { } bounds) return;

		var delta = node.GlobalPosition - bounds.Center;
		var ex    = delta.X / bounds.RadiusX;
		var ey    = delta.Y / bounds.RadiusY;
		if (ex * ex + ey * ey <= 1f) return;

		var len = Mathf.Sqrt(ex * ex + ey * ey);
		// Pull 4 px inside the surface so floating-point error never places the
		// node on the wrong side of the one-sided SegmentShape2D physics walls.
		const float Inset = 4f;
		node.GlobalPosition = bounds.Center + new Vector2(
			(ex / len) * (bounds.RadiusX - Inset),
			(ey / len) * (bounds.RadiusY - Inset));
	}

	/// <summary>
	/// Returns the world-space point on the arena ellipse boundary that lies in
	/// <paramref name="direction"/> from the ellipse centre, inset by
	/// <paramref name="inset"/> pixels so the result is safely inside the wall.
	/// Returns null when no boundary is active.
	/// </summary>
	public static Vector2? GetArenaBoundaryPoint(Vector2 direction, float inset = 8f)
	{
		if (ArenaBoundary is not { } bounds) return null;

		// Parametric intersection of the ray (bounds.Center + t*dir) with the ellipse:
		// (dir.X*t / rx)² + (dir.Y*t / ry)² = 1  →  t = 1 / sqrt((dir.X/rx)² + (dir.Y/ry)²)
		var ex = direction.X / bounds.RadiusX;
		var ey = direction.Y / bounds.RadiusY;
		var t  = 1f / Mathf.Sqrt(ex * ex + ey * ey);

		return bounds.Center + direction * (t - inset);
	}

	/// <summary>
	/// If <see cref="ArenaBoundary"/> is active, clamps <see cref="Node2D.GlobalPosition"/>
	/// to lie within the ellipse. Call this after every <c>MoveAndSlide()</c>.
	/// Uses the same ellipse-containment formula as <c>IcicleExplosionZone</c>:
	/// (dx/rx)² + (dy/ry)² ≤ 1.
	/// </summary>
	protected void ClampToArenaBoundary()
	{
		if (ArenaBoundary is not { } bounds) return;

		var delta = GlobalPosition - bounds.Center;
		var ex    = delta.X / bounds.RadiusX;
		var ey    = delta.Y / bounds.RadiusY;

		// Already inside — nothing to do.
		if (ex * ex + ey * ey <= 1f) return;

		// Project back onto the ellipse surface along the same normalised direction.
		// Dividing (ex, ey) by their length gives a unit vector in ellipse space;
		// multiplying by the radii converts it back to world space.
		var len = Mathf.Sqrt(ex * ex + ey * ey);
		GlobalPosition = bounds.Center + new Vector2(
			(ex / len) * bounds.RadiusX,
			(ey / len) * bounds.RadiusY);
	}

	// ── Shared targeting state ────────────────────────────────────────────────

	/// <summary>
	/// The boss the player most recently attacked directly.
	/// Party members prefer this target via <see cref="FindPreferredBoss"/>.
	/// </summary>
	public static Character LastKnownBossTarget { get; private set; }

	/// <summary>
	/// Called by <see cref="Player"/> whenever it fires a harmful spell at a boss.
	/// Updates the shared target so all party members follow the player's focus.
	/// </summary>
	public static void NotifyPlayerAttackedBoss(Character boss)
	{
		LastKnownBossTarget = boss;
	}

	/// <summary>
	/// Clears the shared boss target. Call alongside <see cref="GlobalAutoLoad.Reset"/>
	/// on any scene transition so party members don't hold a stale reference to a
	/// freed boss node from the previous fight.
	/// </summary>
	public static void ResetTarget()
	{
		LastKnownBossTarget = null;
	}

	/// <summary>
	/// Returns the boss the party should focus — the player's last-attacked target
	/// if that target is still alive, otherwise the first alive boss found.
	/// </summary>
	protected Character FindPreferredBoss()
	{
		if (LastKnownBossTarget != null && LastKnownBossTarget.IsAlive)
			return LastKnownBossTarget;

		// Fallback: first alive boss in the group.
		foreach (var node in GetTree().GetNodesInGroup(GameConstants.BossGroupName))
			if (node is Character c && c.IsAlive)
				return c;

		return null;
	}
}
