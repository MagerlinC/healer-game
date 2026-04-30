using System;
using System.Collections.Generic;
using Godot;
using healerfantasy;
using healerfantasy.CombatLog;
using healerfantasy.SpellResources;

/// <summary>
/// Self-contained node that runs the Cone of Cold phase for the
/// <see cref="QueenOfTheFrozenWastes"/>. Add this node to the scene tree and
/// call <see cref="Start"/> — it frees itself when done.
///
/// Sequence
/// ────────
///   1. Boss tweens to a hover position above the arena centre.
///   2. All living non-healer party members are stunned and pulled together
///      into the safe-zone slot (left / middle / right, chosen at random).
///   3. An ice-block overlay appears on each encased member.
///   4. Boss channels Cone of Cold for <see cref="CastDuration"/> seconds
///      (cast bar shown via <see cref="ShowCastBar"/>).
///   5. Cone visual fires; the player takes <see cref="ConeDamage"/> if they
///      are NOT standing in the safe third of the arena.
///   6. Ice overlays are removed, party members are un-stunned, boss tweens
///      back to its saved position, and <see cref="OnPhaseComplete"/> fires.
/// </summary>
public partial class ConeOfColdPhase : Node2D
{
	// ── constants ─────────────────────────────────────────────────────────────
	const string AssetBase      = "res://assets/enemies/queen-of-the-frozen-wastes/";
	const float  FlyDuration    = 0.6f;
	const float  PullDuration   = 0.5f;
	const float  EncasePause    = 0.3f;  // brief pause after ice appears before cast starts
	const float  CastDuration   = 1.5f;
	const float  ConeLinger     = 0.5f;  // how long the cone visual stays visible
	const float  ReturnDuration = 0.6f;
	const float  ConeDamage     = 120f;
	const float  EncaseStunDur  = 12f;   // well beyond the total phase duration
	const float  NpcXSpacing    = 14f;   // horizontal gap between encased members
	const float  NpcYDepth      = 14f;   // how far the outer members step back behind the centre

	// ── safe zone enum ────────────────────────────────────────────────────────
	public enum SafeZone { Left, Middle, Right }

	// ── callbacks — set by the queen before calling Start ────────────────────
	/// <summary>Called when the Cone of Cold cast bar should appear.</summary>
	public Action<string, Texture2D, float> ShowCastBar    { get; set; }
	/// <summary>Called to hide the cast bar after the cone fires.</summary>
	public Action                           HideCastBar    { get; set; }
	/// <summary>Called when the entire phase has finished and this node is about to free itself.</summary>
	public Action                           OnPhaseComplete { get; set; }

	// ── runtime state ─────────────────────────────────────────────────────────
	Character              _boss;
	SafeZone               _safeZone;
	Vector2                _bossSavedPosition;
	readonly List<PartyMember> _encased  = new();
	readonly List<Node2D>      _overlays = new();

	// ── public API ────────────────────────────────────────────────────────────

	/// <summary>Begin the phase. The boss must already be set as invulnerable by the caller.</summary>
	public void Start(Character boss)
	{
		_boss              = boss;
		_bossSavedPosition = boss.GlobalPosition;
		_safeZone          = (SafeZone)(int)(GD.Randi() % 3u);

		GD.Print($"[ConeOfCold] Phase started — safe zone: {_safeZone}");
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
			    pm.CharacterName != GameConstants.HealerName)
				_encased.Add(pm);

		var anchor = SlotPosition();
		for (var i = 0; i < _encased.Count; i++)
		{
			var pm     = _encased[i];
			// Wedge / arrowhead formation: the member closest to centre (i == middle)
			// sits at the anchor (nearest the boss / cone tip); outer members step
			// back in Y and out in X, so the group tapers toward the narrow point.
			var centre = (_encased.Count - 1) / 2f;
			var xOff   = (i - centre) * NpcXSpacing;
			var yOff   = Mathf.Abs(i - centre) * NpcYDepth;
			var dest   = anchor + new Vector2(xOff, yOff);

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
		if (PartyMember.ArenaBoundary is { } b) { cx = b.Center.X; cy = b.Center.Y; rx = b.RadiusX; ry = b.RadiusY; }

		var xOffset = _safeZone switch
		{
			SafeZone.Left  => -rx * 0.5f,
			SafeZone.Right =>  rx * 0.5f,
			_              =>  0f
		};
		return new Vector2(cx + xOffset, cy - ry * 0.70f);
	}

	// ─────────────────────────────────────────────────────────────────────────
	// Step 3 — ice-block overlays appear, then cast begins
	// ─────────────────────────────────────────────────────────────────────────

	void StepEncase()
	{
		var iceTex = GD.Load<Texture2D>(AssetBase + "ice-block.png");
		foreach (var pm in _encased)
		{
			var overlay = new Sprite2D { ZIndex = 8 };
			if (iceTex != null)
			{
				overlay.Texture = iceTex;
				overlay.Scale   = new Vector2(0.25f, 0.25f);
			}
			overlay.GlobalPosition = pm.GlobalPosition;
			GetParent().AddChild(overlay);
			_overlays.Add(overlay);
		}

		GetTree().CreateTimer(EncasePause).Timeout += StepCast;
	}

	// ─────────────────────────────────────────────────────────────────────────
	// Step 4 — 1.5 s Cone of Cold cast
	// ─────────────────────────────────────────────────────────────────────────

	void StepCast()
	{
		ShowCastBar?.Invoke("Cone of Cold", null, CastDuration);
		GetTree().CreateTimer(CastDuration).Timeout += StepFire;
	}

	// ─────────────────────────────────────────────────────────────────────────
	// Step 5 — cone fires, damage applied
	// ─────────────────────────────────────────────────────────────────────────

	void StepFire()
	{
		HideCastBar?.Invoke();
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
			SafeZone       = _safeZone,
			BossTip        = _boss.GlobalPosition,
			ZIndex         = 15,
			GlobalPosition = Vector2.Zero,
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
			    c.CharacterName == GameConstants.HealerName)
				{ healer = c; break; }

		if (healer == null) return;
		if (IsInSafeZone(healer.GlobalPosition))
		{
			GD.Print("[ConeOfCold] Player is in the safe zone — no damage.");
			return;
		}

		healer.TakeDamage(ConeDamage);
		healer.RaiseFloatingCombatText(ConeDamage, false, (int)SpellSchool.Generic, false);

		CombatLog.Record(new CombatEventRecord
		{
			Timestamp   = Time.GetTicksMsec() / 1000.0,
			SourceName  = GameConstants.FrozenPeakBossName,
			TargetName  = healer.CharacterName,
			AbilityName = "Cone of Cold",
			Amount      = ConeDamage,
			Type        = CombatEventType.Damage,
			IsCrit      = false,
			Description = "Caught in the Queen's Cone of Cold."
		});
	}

	/// <summary>
	/// Returns true when <paramref name="worldPos"/> falls within the safe third
	/// of the arena. The arena is divided into three equal vertical strips along
	/// the X axis using the active <see cref="PartyMember.ArenaBoundary"/>.
	/// </summary>
	bool IsInSafeZone(Vector2 worldPos)
	{
		var cx   = PartyMember.ArenaBoundary?.Center.X ?? 0f;
		var rx   = PartyMember.ArenaBoundary is { } b ? b.RadiusX : 300f;
		// The arena spans [-rx, rx] around cx; each third is rx*2/3 wide.
		// The boundary between left and middle is at cx - rx/3,
		// and between middle and right is at cx + rx/3.
		var edge = rx / 3f;
		var relX = worldPos.X - cx;

		return _safeZone switch
		{
			SafeZone.Left   => relX < -edge,
			SafeZone.Middle => relX >= -edge && relX <= edge,
			SafeZone.Right  => relX > edge,
			_               => false
		};
	}

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
		GD.Print("[ConeOfCold] Phase complete.");
		OnPhaseComplete?.Invoke();
		QueueFree();
	}
}

/// <summary>
/// Draws the Cone of Cold danger sectors as textured pizza-slice triangles
/// whose tip is the boss position. Positioned at world origin so local and
/// world coordinates are identical, avoiding any transform confusion.
/// </summary>
public partial class ConeDrawNode : Node2D
{
	public ConeOfColdPhase.SafeZone SafeZone { get; set; }

	/// <summary>World position of the boss — used as the tip of every slice.</summary>
	public Vector2 BossTip { get; set; }

	// ── visual tuning ───────────────────────────────────────────────────────
	/// <summary>
	/// How far beyond the arena's horizontal edge (world pixels) the outer
	/// corners of the left and right slices extend. Increase to push the outer
	/// edges further from centre and widen the apparent angle of each slice.
	/// </summary>
	const float OuterOvershoot = 120f;

	/// <summary>
	/// How far past the arena bottom the base of every slice extends, expressed
	/// as a fraction of RadiusY. Decreasing this shortens the cone and increases
	/// the apparent angle; increasing it lengthens the cone and narrows the angle.
	/// </summary>
	const float BaseExtentFraction = 1.6f;

	/// <summary>
	/// Half-width of the safe zone as a fraction of RadiusX (0 = no safe zone,
	/// 0.5 = safe zone covers half the arena either side of centre).
	/// Applies to all three variants — widening the centre gap for Middle-safe
	/// simultaneously narrows the dangerous wedges for Left-safe and Right-safe.
	/// Default 0.33 = equal thirds.
	/// </summary>
	const float SafeZoneHalfWidth = 0.33f;

	Texture2D _tex;

	public override void _Ready()
	{
		_tex = GD.Load<Texture2D>(
			"res://assets/enemies/queen-of-the-frozen-wastes/cone.png");
		if (_tex == null)
			GD.PrintErr("[ConeDrawNode] Could not load cone.png — slices will be flat-coloured.");
	}

	public override void _Draw()
	{
		// Collect arena dimensions (fallback values keep editor previews sane).
		float cx = 0f, cy = 0f, rx = 200f, ry = 150f;
		if (PartyMember.ArenaBoundary is { } b)
			{ cx = b.Center.X; cy = b.Center.Y; rx = b.RadiusX; ry = b.RadiusY; }

		var tip = BossTip;

		// Horizontal boundaries that divide the arena into equal thirds.
		float xLeft = cx - rx - OuterOvershoot;   // outer corners spread past arena edge
		float xRight = cx + rx + OuterOvershoot;
		float xLB   = cx - rx * SafeZoneHalfWidth;   // left-to-middle boundary
		float xRB   = cx + rx * SafeZoneHalfWidth;   // middle-to-right boundary
		// Base of every pizza slice — past the arena bottom so the fill is solid.
		float yBase  = cy + ry * BaseExtentFraction;

		// White with slight transparency lets the texture show its natural colours.
		// The outline is kept as a bright ice-blue edge.
		var tint    = new Color(1f, 1f, 1f, 0.85f);
		var outline = new Color(0.55f, 0.85f, 1.00f, 0.90f);

		switch (SafeZone)
		{
			case ConeOfColdPhase.SafeZone.Left:
				// Danger covers middle + right third.
				DrawSlice(tip, xLB,    yBase, xRight, yBase, tint, outline);
				break;

			case ConeOfColdPhase.SafeZone.Middle:
				// Danger covers left third and right third (two separate slices).
				DrawSlice(tip, xLeft,  yBase, xLB,   yBase, tint, outline);
				DrawSlice(tip, xRB,    yBase, xRight, yBase, tint, outline);
				break;

			case ConeOfColdPhase.SafeZone.Right:
				// Danger covers left + middle third.
				DrawSlice(tip, xLeft,  yBase, xRB,   yBase, tint, outline);
				break;
		}
	}

	/// <summary>
	/// Draws a single textured triangle from <paramref name="tip"/> down to the
	/// horizontal span [x0, x1] at y. UVs map the texture so its narrow end sits
	/// at the tip and its wide end covers the base — matching a cone asset that
	/// is narrow at the top and wide at the bottom.
	/// </summary>
	void DrawSlice(Vector2 tip,
	              float x0, float y0,
	              float x1, float y1,
	              Color tint, Color outline)
	{
		var pts = new Vector2[] { tip, new Vector2(x0, y0), new Vector2(x1, y1) };

		// UV layout:
		//   tip          → (0.5, 0)  — top-centre of the cone texture
		//   bottom-left  → (0,   1)  — bottom-left corner of the texture
		//   bottom-right → (1,   1)  — bottom-right corner of the texture
		var uvs = new Vector2[]
		{
			new Vector2(0.5f, 0f),
			new Vector2(0f,   1f),
			new Vector2(1f,   1f),
		};

		DrawPolygon(pts, new Color[] { tint }, uvs, _tex);
		DrawPolyline(new Vector2[] { tip, new Vector2(x0, y0),
		                             new Vector2(x1, y1), tip }, outline, 2.5f);
	}
}
