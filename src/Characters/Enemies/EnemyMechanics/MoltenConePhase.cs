using System;
using System.Collections.Generic;
using Godot;
using healerfantasy;
using healerfantasy.CombatLog;
using healerfantasy.SpellResources;

/// <summary>
/// Self-contained node that runs the Molten Cone phase for the
/// <see cref="TheTallowQueen"/>. Add this node to the scene tree and
/// call <see cref="Start"/> — it frees itself when done.
///
/// Sequence
/// ────────
///   1. Boss tweens to a hover position above the arena centre.
///   2. All living non-healer party members are stunned and pulled together
///      into the safe-zone slot (left / middle / right, chosen at random).
///   3. A wax-shell overlay appears on each entombed member.
///   4. Boss channels Molten Cone for <see cref="CastDuration"/> seconds
///      (cast bar shown via <see cref="ShowCastBar"/>), while a faint outline of
///      the cone telegraphs where it will land.
///   5. Cone visual fires; the player takes <see cref="ConeDamage"/> if they
///      are standing inside the drawn cone.
///   6. Wax overlays are removed, party members are un-stunned, boss tweens
///      back to its saved position, and <see cref="OnPhaseComplete"/> fires.
/// </summary>
public partial class MoltenConePhase : Node2D
{
	// ── constants ─────────────────────────────────────────────────────────────
	const string AssetBase = "res://assets/enemies/the-tallow-queen/";
	const float FlyDuration = 0.6f;
	const float PullDuration = 0.5f;
	const float EncasePause = 0.3f; // brief pause after the wax sets before cast starts
	const float CastDuration = 1.5f;
	const float ConeLinger = 0.5f; // how long the cone visual stays visible
	const float ReturnDuration = 0.6f;
	const float ConeDamage = 120f;
	const float EncaseStunDur = 12f; // well beyond the total phase duration
	const float NpcXSpacing = 14f; // horizontal gap between encased members
	const float NpcYDepth = 14f; // how far the outer members step back behind the centre

	// ── safe zone enum ────────────────────────────────────────────────────────
	public enum SafeZone
	{
		Left,
		Middle,
		Right
	}

	// ── callbacks — set by the queen before calling Start ────────────────────
	/// <summary>Called when the Molten Cone cast bar should appear.</summary>
	public Action<string, Texture2D, float> ShowCastBar { get; set; }

	/// <summary>Called to hide the cast bar after the cone fires.</summary>
	public Action HideCastBar { get; set; }

	/// <summary>Called when the entire phase has finished and this node is about to free itself.</summary>
	public Action OnPhaseComplete { get; set; }

	// ── runtime state ─────────────────────────────────────────────────────────
	Character _boss;
	SafeZone _safeZone;
	Vector2 _bossSavedPosition;
	Vector2 _coneTip;
	readonly List<PartyMember> _encased = new();
	readonly List<Node2D> _overlays = new();
	ConeDrawNode _telegraph;

	// ── public API ────────────────────────────────────────────────────────────

	/// <summary>Begin the phase. The boss must already be set as invulnerable by the caller.</summary>
	public void Start(Character boss)
	{
		_boss = boss;
		_bossSavedPosition = boss.GlobalPosition;
		_safeZone = (SafeZone)(int)(GD.Randi() % 3u);

		GD.Print($"[MoltenCone] Phase started — safe zone: {_safeZone}");
		StepFlyUp();
	}

	// ─────────────────────────────────────────────────────────────────────────
	// Step 1 — boss flies to hover position
	// ─────────────────────────────────────────────────────────────────────────

	void StepFlyUp()
	{
		var tween = CreateTween();
		tween.TweenProperty(_boss, "global_position", HoverPosition(), FlyDuration)
			.SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Sine);
		tween.TweenCallback(Callable.From(StepPullParty));
	}

	Vector2 HoverPosition()
	{
		if (PartyMember.ArenaBoundary is { } b)
			return new Vector2(b.Center.X, b.Center.Y - b.RadiusY * 1.15f);
		return new Vector2(0f, -180f);
	}

	// ─────────────────────────────────────────────────────────────────────────
	// Step 2 — stun and pull non-healer party members to the safe-zone slot
	// ─────────────────────────────────────────────────────────────────────────

	void StepPullParty()
	{
		foreach (var node in _boss.GetTree().GetNodesInGroup("party"))
			if (node is PartyMember pm && pm.IsAlive &&
			    pm.CharacterName != GameConstants.LightkeeperName)
				_encased.Add(pm);

		var anchor = SlotPosition();
		for (var i = 0; i < _encased.Count; i++)
		{
			var pm = _encased[i];
			// Wedge / arrowhead formation: the member closest to centre (i == middle)
			// sits at the anchor (nearest the boss / cone tip); outer members step
			// back in Y and out in X, so the group tapers toward the narrow point.
			var centre = (_encased.Count - 1) / 2f;
			var xOff = (i - centre) * NpcXSpacing;
			var yOff = Mathf.Abs(i - centre) * NpcYDepth;
			var dest = anchor + new Vector2(xOff, yOff);

			pm.StunInPlace(EncaseStunDur);

			var tween = CreateTween();
			tween.TweenProperty(pm, "global_position", dest, PullDuration)
				.SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Sine);
		}

		GetTree().CreateTimer(PullDuration).Timeout += StepEncase;
	}

	/// <summary>World position of the chosen safe-zone slot, in the lower half of the arena.</summary>
	Vector2 SlotPosition()
	{
		float cx = 0f, cy = 0f, rx = 200f, ry = 150f;
		if (PartyMember.ArenaBoundary is { } b)
		{
			cx = b.Center.X;
			cy = b.Center.Y;
			rx = b.RadiusX;
			ry = b.RadiusY;
		}

		var xOffset = _safeZone switch
		{
			SafeZone.Left => -rx * 0.5f,
			SafeZone.Right => rx * 0.5f,
			_ => 0f
		};
		return new Vector2(cx + xOffset, cy - ry * 0.70f);
	}

	// ─────────────────────────────────────────────────────────────────────────
	// Step 3 — wax-shell overlays appear, then cast begins
	// ─────────────────────────────────────────────────────────────────────────

	void StepEncase()
	{
		var waxTex = GD.Load<Texture2D>(AssetBase + "wax-shell.png");
		foreach (var pm in _encased)
		{
			var overlay = new Sprite2D { ZIndex = 8 };
			if (waxTex != null)
			{
				overlay.Texture = waxTex;
				overlay.Scale = new Vector2(0.25f, 0.25f);
			}

			overlay.GlobalPosition = pm.GlobalPosition;
			GetParent().AddChild(overlay);
			_overlays.Add(overlay);
		}

		GetTree().CreateTimer(EncasePause).Timeout += StepCast;
	}

	// ─────────────────────────────────────────────────────────────────────────
	// Step 4 — 1.5 s Molten Cone cast
	// ─────────────────────────────────────────────────────────────────────────

	void StepCast()
	{
		ShowCastBar?.Invoke("Molten Cone",
			GD.Load<Texture2D>(AssetConstants.SpellIconAssets + "enemy/the-tallow-queen/molten-cone.png"), CastDuration);

		// Lock the cone's tip now so the telegraph, the fired cone and the hit
		// test all share exactly the same geometry.
		_coneTip = _boss.GlobalPosition;
		_telegraph = new ConeDrawNode
		{
			SafeZone = _safeZone,
			BossTip = _coneTip,
			Telegraph = true,
			ZIndex = 15,
			GlobalPosition = Vector2.Zero
		};
		GetParent().AddChild(_telegraph);

		GetTree().CreateTimer(CastDuration).Timeout += StepFire;
	}

	// ─────────────────────────────────────────────────────────────────────────
	// Step 5 — cone fires, damage applied
	// ─────────────────────────────────────────────────────────────────────────

	void StepFire()
	{
		HideCastBar?.Invoke();
		if (IsInstanceValid(_telegraph)) _telegraph.QueueFree();
		_telegraph = null;
		SpawnConeVisual();
		ApplyConeDamage();
		GetTree().CreateTimer(ConeLinger).Timeout += StepReturn;
	}

	void SpawnConeVisual()
	{
		// Draw the danger sectors as pizza-slice polygons whose tip is the boss
		// position and whose base sweeps across the arena floor. Using a Node2D
		// at world origin means DrawPolygon coordinates are world-space.
		var draw = new ConeDrawNode
		{
			SafeZone = _safeZone,
			BossTip = _coneTip,
			ZIndex = 15,
			GlobalPosition = Vector2.Zero
		};
		GetParent().AddChild(draw);

		// Hold visible briefly, then fade to zero over ConeLinger seconds.
		var tween = draw.CreateTween();
		tween.TweenProperty(draw, "modulate", new Color(1f, 1f, 1f, 0f), ConeLinger)
			.SetEase(Tween.EaseType.In);
		draw.GetTree().CreateTimer(ConeLinger).Timeout += draw.QueueFree;
	}

	void ApplyConeDamage()
	{
		// Find the healer (player).
		Character healer = null;
		foreach (var node in _boss.GetTree().GetNodesInGroup("party"))
			if (node is Character c && c.IsAlive &&
			    c.CharacterName == GameConstants.LightkeeperName)
			{
				healer = c;
				break;
			}

		if (healer == null) return;
		if (IsInSafeZone(healer.GlobalPosition))
		{
			GD.Print("[MoltenCone] Player is in the safe zone — no damage.");
			return;
		}

		healer.TakeDamage(ConeDamage);
		healer.RaiseFloatingCombatText(ConeDamage, false, (int)SpellSchool.Generic, false);

		CombatLog.Record(new CombatEventRecord
		{
			Timestamp = Time.GetTicksMsec() / 1000.0,
			SourceName = GameConstants.GutteringSpireBossName,
			TargetName = healer.CharacterName,
			AbilityName = "Molten Cone",
			Amount = ConeDamage,
			Type = CombatEventType.Damage,
			IsCrit = false,
			Description = "Caught in the Queen's Molten Cone of pouring wax."
		});
	}

	/// <summary>
	/// Returns true when <paramref name="worldPos"/> lies outside the drawn cone.
	/// Hit-tests the exact polygons <see cref="ConeDrawNode"/> draws, so the hit
	/// test always matches what the telegraph shows.
	/// </summary>
	bool IsInSafeZone(Vector2 worldPos) => ConeDrawNode.IsSafe(_safeZone, _coneTip, worldPos);

	// ─────────────────────────────────────────────────────────────────────────
	// Step 6 — cleanup, boss returns, phase complete
	// ─────────────────────────────────────────────────────────────────────────

	void StepReturn()
	{
		foreach (var o in _overlays) o.QueueFree();
		_overlays.Clear();

		foreach (var pm in _encased) pm.ClearKnockbackStun();
		_encased.Clear();

		var tween = CreateTween();
		tween.TweenProperty(_boss, "global_position", _bossSavedPosition, ReturnDuration)
			.SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Sine);
		tween.TweenCallback(Callable.From(StepComplete));
	}

	void StepComplete()
	{
		GD.Print("[MoltenCone] Phase complete.");
		OnPhaseComplete?.Invoke();
		QueueFree();
	}
}

/// <summary>
/// Draws the Molten Cone danger sectors as textured pizza-slice triangles
/// whose tip is the boss position. Positioned at world origin so local and
/// world coordinates are identical, avoiding any transform confusion.
/// </summary>
public partial class ConeDrawNode : Node2D
{
	public MoltenConePhase.SafeZone SafeZone { get; set; }

	/// <summary>
	/// Draw only a faint, pulsing outline (and a whisper of fill) of the area the cone
	/// will cover, shown while it is being cast.
	/// </summary>
	public bool Telegraph { get; set; }

	float _t;

	/// <summary>World position of the boss — used as the tip of every slice.</summary>
	public Vector2 BossTip { get; set; }

	// ── visual tuning ───────────────────────────────────────────────────────
	/// <summary>
	/// How far beyond the arena's horizontal edge (world pixels) the outer
	/// corners of the left and right slices extend. Increase to push the outer
	/// edges further from centre and widen the apparent angle of each slice.
	/// </summary>
	const float OuterOvershoot = 140f;

	/// <summary>
	/// Extra spread applied past the arena tangent so the outer edge never grazes
	/// the boundary, and a hard cap so a near-horizontal tangent can't explode.
	/// </summary>
	const float OuterTangentMargin = 1.1f;
	const float MaxOuterReach = 4000f;

	/// <summary>
	/// The cone is clipped to the arena ellipse scaled by this factor (and drawn as
	/// a polygon with this many segments), so it reads as molten floor rather than
	/// slices sprawling across the whole screen.
	/// </summary>
	const float ArenaClipMargin = 1.12f;
	const int ArenaClipSegments = 64;

	/// <summary>
	/// How far past the arena bottom the base of every slice extends, expressed
	/// as a fraction of RadiusY. Decreasing this shortens the cone and increases
	/// the apparent angle; increasing it lengthens the cone and narrows the angle.
	/// </summary>
	const float BaseExtentFraction = 1.8f;

	/// <summary>
	/// Half-width of the safe zone as a fraction of RadiusX (0 = no safe zone,
	/// 0.5 = safe zone covers half the arena either side of centre).
	/// Applies to all three variants — widening the centre gap for Middle-safe
	/// simultaneously narrows the dangerous wedges for Left-safe and Right-safe.
	/// Default 0.33 = equal thirds.
	/// </summary>
	const float SafeZoneHalfWidth = 0.35f;

	Texture2D _tex;

	public override void _Ready()
	{
		_tex = GD.Load<Texture2D>(
			"res://assets/enemies/the-tallow-queen/molten-cone.png");
		if (_tex == null)
			GD.PrintErr("[ConeDrawNode] Could not load molten-cone.png — slices will be flat-coloured.");
	}

	public override void _Process(double delta)
	{
		if (!Telegraph) return;
		// A slow, gentle pulse: noticeable, but quieter than the real cone.
		_t += (float)delta;
		Modulate = new Color(1f, 1f, 1f, 0.55f + 0.3f * Mathf.Sin(_t * 5f));
	}

	// ── shared geometry (drawing + hit test) ────────────────────────────────

	/// <summary>Arena dimensions (fallback values keep editor previews sane).</summary>
	static (float Cx, float Cy, float Rx, float Ry) Arena()
	{
		if (PartyMember.ArenaBoundary is { } b)
			return (b.Center.X, b.Center.Y, b.RadiusX, b.RadiusY);
		return (0f, 0f, 200f, 150f);
	}

	/// <summary>Y of the base of every pizza slice — past the arena bottom so the fill is solid.</summary>
	static float BaseY()
	{
		var (_, cy, _, ry) = Arena();
		return cy + ry * BaseExtentFraction;
	}

	/// <summary>Base X of the inner slice edge on <paramref name="side"/> (-1 left, +1 right).</summary>
	static float InnerBaseX(int side)
	{
		var (cx, _, rx, _) = Arena();
		return cx + side * rx * SafeZoneHalfWidth;
	}

	/// <summary>
	/// Base X of the outer slice corner on <paramref name="side"/> (-1 left, +1 right).
	/// The boss hovers just above the arena, so a fixed overshoot leaves the arena's
	/// flanks outside the drawn slice even though they are dangerous. Push the corner
	/// out until the outer edge clears the arena ellipse (tangent from the tip).
	/// </summary>
	static float OuterBaseX(Vector2 tip, int side)
	{
		var (cx, cy, rx, ry) = Arena();
		var yBase = BaseY();
		var x = cx + side * (rx + OuterOvershoot);

		// Tangent from the tip to the ellipse, solved in the ellipse's unit-circle space.
		var p = new Vector2((tip.X - cx) / rx, (tip.Y - cy) / ry);
		var d = p.Length();
		if (d <= 1f) return x; // tip inside the arena — no tangent exists

		var phi = p.Angle();
		var alpha = Mathf.Acos(1f / d);
		foreach (var a in new[] { phi + alpha, phi - alpha })
		{
			var q = new Vector2(cx + rx * Mathf.Cos(a), cy + ry * Mathf.Sin(a));
			var dy = q.Y - tip.Y;
			if (dy <= 0.01f || Mathf.Sign(q.X - tip.X) != side) continue;
			var reach = tip.X + (q.X - tip.X) / dy * (yBase - tip.Y) * OuterTangentMargin;
			x = side > 0 ? Mathf.Max(x, reach) : Mathf.Min(x, reach);
		}

		return Mathf.Clamp(x, cx - MaxOuterReach, cx + MaxOuterReach);
	}

	/// <summary>
	/// One danger slice. <see cref="Triangle"/> is the full pizza slice
	/// (tip, base-left, base-right), used only to map the texture; <see cref="Shape"/>
	/// is that slice clipped to the arena floor — the area that is drawn and hit-tested.
	/// </summary>
	public readonly record struct Slice(Vector2[] Triangle, Vector2[] Shape);

	/// <summary>
	/// The danger slices for <paramref name="zone"/>, clipped to the arena. This is
	/// the single source of truth for the cone: <see cref="_Draw"/> paints exactly
	/// these shapes and <see cref="IsSafe"/> hit-tests against them, so the two
	/// cannot drift apart.
	///
	/// Clipping matters for readability: the unclipped slices reach far off-screen,
	/// and their outlines would carve the off-arena corners into extra wedges that
	/// look like additional cones.
	/// </summary>
	public static List<Slice> DangerSlices(MoltenConePhase.SafeZone zone, Vector2 tip)
	{
		var xLeft = OuterBaseX(tip, -1); // outer corners spread past the arena
		var xRight = OuterBaseX(tip, 1);
		var xLB = InnerBaseX(-1); // left-to-middle boundary
		var xRB = InnerBaseX(1); // middle-to-right boundary
		var yBase = BaseY();

		var triangles = zone switch
		{
			// Danger covers middle + right third.
			MoltenConePhase.SafeZone.Left => new[] { (xLB, xRight) },
			// Danger covers left third and right third (two separate slices).
			MoltenConePhase.SafeZone.Middle => new[] { (xLeft, xLB), (xRB, xRight) },
			// Danger covers left + middle third.
			MoltenConePhase.SafeZone.Right => new[] { (xLeft, xRB) },
			_ => System.Array.Empty<(float, float)>()
		};

		var floor = ArenaClipPolygon();
		var slices = new List<Slice>();
		foreach (var (x0, x1) in triangles)
		{
			var tri = new[] { tip, new Vector2(x0, yBase), new Vector2(x1, yBase) };
			// Triangle ∩ convex ellipse is convex, so this yields at most one shape.
			foreach (var shape in Geometry2D.IntersectPolygons(tri, floor))
				slices.Add(new Slice(tri, shape));
		}

		return slices;
	}

	/// <summary>
	/// The arena ellipse as a polygon, inflated by <see cref="ArenaClipMargin"/> so a
	/// player hugging the boundary wall is still inside the drawn (and hit-tested) area.
	/// </summary>
	static Vector2[] ArenaClipPolygon()
	{
		var (cx, cy, rx, ry) = Arena();
		var pts = new Vector2[ArenaClipSegments];
		for (var i = 0; i < ArenaClipSegments; i++)
		{
			var a = Mathf.Tau * i / ArenaClipSegments;
			pts[i] = new Vector2(cx + rx * ArenaClipMargin * Mathf.Cos(a), cy + ry * ArenaClipMargin * Mathf.Sin(a));
		}

		return pts;
	}

	/// <summary>True when <paramref name="pos"/> lies outside every slice from <see cref="DangerSlices"/>.</summary>
	public static bool IsSafe(MoltenConePhase.SafeZone zone, Vector2 tip, Vector2 pos)
	{
		foreach (var slice in DangerSlices(zone, tip))
			if (Geometry2D.IsPointInPolygon(pos, slice.Shape))
				return false;
		return true;
	}

	public override void _Draw()
	{
		// White with slight transparency lets the texture show its natural colours.
		// The outline is kept as a bright molten-amber edge.
		var tint = new Color(1f, 1f, 1f, 0.85f);
		var outline = new Color(1.00f, 0.72f, 0.32f, 0.90f);
		if (Telegraph)
		{
			tint = new Color(1.00f, 0.72f, 0.32f, 0.32f);
			outline = new Color(1.00f, 0.85f, 0.55f, 0.8f);
		}

		foreach (var slice in DangerSlices(SafeZone, BossTip))
			DrawSlice(slice, tint, outline);
	}

	/// <summary>
	/// Draws one clipped danger slice. UVs are mapped from the full slice triangle
	/// so the texture's narrow end sits at the tip and its wide end covers the
	/// base — matching a cone asset that is narrow at the top and wide at the bottom.
	/// </summary>
	void DrawSlice(Slice slice, Color tint, Color outline)
	{
		var shape = slice.Shape;
		var uvs = new Vector2[shape.Length];
		for (var i = 0; i < shape.Length; i++)
			uvs[i] = TriangleUv(shape[i], slice.Triangle);

		// The telegraph is untextured: just a tinted wash under its outline.
		DrawPolygon(shape, new Color[] { tint }, uvs, Telegraph ? null : _tex);

		var closed = new Vector2[shape.Length + 1];
		shape.CopyTo(closed, 0);
		closed[^1] = shape[0];
		DrawPolyline(closed, outline, Telegraph ? 3f : 2.5f);
	}

	/// <summary>
	/// Texture coordinate of <paramref name="p"/> via barycentric interpolation over
	/// the slice triangle, whose corners map to:
	///   tip          → (0.5, 0)  — top-centre of the cone texture
	///   bottom-left  → (0,   1)  — bottom-left corner of the texture
	///   bottom-right → (1,   1)  — bottom-right corner of the texture
	/// </summary>
	static Vector2 TriangleUv(Vector2 p, Vector2[] tri)
	{
		var (a, b, c) = (tri[0], tri[1], tri[2]);
		var den = (b.Y - c.Y) * (a.X - c.X) + (c.X - b.X) * (a.Y - c.Y);
		if (Mathf.IsZeroApprox(den)) return new Vector2(0.5f, 0.5f);
		var wa = ((b.Y - c.Y) * (p.X - c.X) + (c.X - b.X) * (p.Y - c.Y)) / den;
		var wb = ((c.Y - a.Y) * (p.X - c.X) + (a.X - c.X) * (p.Y - c.Y)) / den;
		var wc = 1f - wa - wb;
		return wa * new Vector2(0.5f, 0f) + wb * new Vector2(0f, 1f) + wc * new Vector2(1f, 1f);
	}
}