using System.Collections.Generic;
using Godot;
using healerfantasy.CombatLog;
using healerfantasy.SpellResources;

/// <summary>
/// A sweeping void-energy wave hazard spawned by <see cref="FlyingSkull"/> during
/// the Necrotic Waves ability.
///
/// The wave travels from one edge of the viewport to the opposite edge, covering
/// the full perpendicular span except for a small <see cref="GapSize"/>-pixel gap
/// centred at <see cref="GapCenter"/>. Any party member whose position falls inside
/// the wave band but outside the gap takes <see cref="DamageAmount"/> void damage
/// — once per wave passage.
///
/// Directions
/// ──────────
///   LeftToRight / RightToLeft — wave body spans the full screen height; the gap
///     is a Y-range. Player must be at the correct vertical position to avoid damage.
///   TopToBottom / BottomToTop — wave body spans the full screen width; the gap
///     is an X-range. Player must be at the correct horizontal position to avoid damage.
///
/// Visual style matches <see cref="NecroticPool"/> (deep purple fill, bright violet
/// border) so the two abilities feel thematically linked.
/// </summary>
public partial class NecroticWave : Node2D
{
	public enum WaveDirection
	{
		LeftToRight,
		RightToLeft,
		TopToBottom,
		BottomToTop
	}

	// ── tuneable constants ─────────────────────────────────────────────────────

	/// <summary>Thickness of the wave band in pixels.</summary>
	public const float WaveThickness = 32f;

	// ── instance data ──────────────────────────────────────────────────────────

	/// <summary>Direction this wave travels across the screen.</summary>
	public WaveDirection Direction { get; set; }

	/// <summary>
	/// Centre of the safe gap in screen coordinates.
	///   Horizontal wave (LTR/RTL): Y value — player must match this Y.
	///   Vertical wave   (TTB/BTT): X value — player must match this X.
	/// </summary>
	public float GapCenter { get; set; }

	/// <summary>Total pixel size of the safe gap. Set by the spawner.</summary>
	public float GapSize { get; set; } = 120f;

	/// <summary>Wave travel speed in pixels/second.</summary>
	public float Speed { get; set; } = 100f;

	/// <summary>Void damage dealt to each party member caught outside the gap.</summary>
	public float DamageAmount { get; set; } = 45f;

	// ── visuals ────────────────────────────────────────────────────────────────

	// Matches NecroticPool palette — deep purple fill, bright violet border.
	static readonly Color FillColour = new(0.28f, 0.00f, 0.52f, 0.50f);

	static readonly Color BorderColour = new(0.65f, 0.10f, 1.00f, 0.95f);

	// Gap is a faint, lighter purple so players can read it at a glance.
	static readonly Color GapFillColour = new(0.55f, 0.15f, 0.80f, 0.18f);
	const float BorderWidth = 2.5f;

	// ── internal state ─────────────────────────────────────────────────────────

	/// <summary>Leading-edge coordinate on the travel axis (X for LTR/RTL, Y for TTB/BTT).</summary>
	float _leadingEdge;

	/// <summary>Viewport bounds on the travel axis.</summary>
	float _screenTravelMin;

	float _screenTravelMax;

	/// <summary>Viewport bounds on the perpendicular axis (for drawing).</summary>
	float _screenPerpMin;

	float _screenPerpMax;

	bool _movingPositive; // true = LTR or TTB
	bool _isHorizontal; // true = LTR or RTL (wave body spans screen height)
	bool _done;

	/// <summary>Party members already damaged this wave (ensures one-hit-per-wave).</summary>
	readonly HashSet<Node> _alreadyHit = new();

	// ── lifecycle ──────────────────────────────────────────────────────────────

	public override void _Ready()
	{
		// GetViewportRect() returns screen-pixel coordinates (always 0,0 → w,h).
		// Multiplying by the inverse canvas transform converts those corners into
		// actual world-space coordinates, correctly accounting for camera position
		// and zoom so the wave sweeps the full visible arena regardless of where
		// the world origin sits relative to the screen.
		var screenRect = GetViewportRect();
		var toWorld = GetCanvasTransform().AffineInverse();
		var wTL = toWorld * screenRect.Position;
		var wBR = toWorld * screenRect.End;

		var worldLeft = Mathf.Min(wTL.X, wBR.X);
		var worldRight = Mathf.Max(wTL.X, wBR.X);
		var worldTop = Mathf.Min(wTL.Y, wBR.Y);
		var worldBottom = Mathf.Max(wTL.Y, wBR.Y);

		_isHorizontal = Direction == WaveDirection.LeftToRight || Direction == WaveDirection.RightToLeft;
		_movingPositive = Direction == WaveDirection.LeftToRight || Direction == WaveDirection.TopToBottom;

		if (_isHorizontal)
		{
			_screenTravelMin = worldLeft;
			_screenTravelMax = worldRight;
			_screenPerpMin = worldTop;
			_screenPerpMax = worldBottom;
		}
		else
		{
			_screenTravelMin = worldTop;
			_screenTravelMax = worldBottom;
			_screenPerpMin = worldLeft;
			_screenPerpMax = worldRight;
		}

		// Start just off-screen so the player sees the gap as soon as the wave enters.
		_leadingEdge = _movingPositive
			? _screenTravelMin // leading edge starts at the screen edge
			: _screenTravelMax; // (trailing end is still off-screen)

		QueueRedraw();
	}

	public override void _Process(double delta)
	{
		if (_done) return;

		var movement = Speed * (float)delta;
		_leadingEdge += _movingPositive ? movement : -movement;

		CheckHits();

		// Fully exited the opposite side?
		var finished = _movingPositive
			? _leadingEdge - WaveThickness > _screenTravelMax
			: _leadingEdge + WaveThickness < _screenTravelMin;

		if (finished)
		{
			_done = true;
			QueueFree();
			return;
		}

		QueueRedraw();
	}

	// ── rendering ──────────────────────────────────────────────────────────────

	public override void _Draw()
	{
		// Compute the wave band's start/end on the travel axis.
		var trailEdge = _movingPositive ? _leadingEdge - WaveThickness : _leadingEdge + WaveThickness;
		var waveMin = Mathf.Min(_leadingEdge, trailEdge);
		var waveMax = Mathf.Max(_leadingEdge, trailEdge);

		// Clamp to screen so we only draw visible portions.
		waveMin = Mathf.Max(waveMin, _screenTravelMin);
		waveMax = Mathf.Min(waveMax, _screenTravelMax);
		if (waveMax <= waveMin) return;

		var gapMin = GapCenter - GapSize * 0.5f;
		var gapMax = GapCenter + GapSize * 0.5f;

		if (_isHorizontal)
		{
			// Wave spans full screen height; gap is a Y-range.
			// Top section: inner edge faces downward toward the gap — omit bottom border.
			DrawHSection(waveMin, waveMax, _screenPerpMin, gapMin, omitBottom: true);
			// Bottom section: inner edge faces upward toward the gap — omit top border.
			DrawHSection(waveMin, waveMax, gapMax, _screenPerpMax, true);
			// Gap: faint fill only, no border, so there is truly nothing drawn at the gap edges.
			DrawRect(new Rect2(waveMin, gapMin, waveMax - waveMin, gapMax - gapMin), GapFillColour);
		}
		else
		{
			// Wave spans full screen width; gap is an X-range.
			// Left section: inner edge faces rightward toward the gap — omit right border.
			DrawVSection(waveMin, waveMax, _screenPerpMin, gapMin, omitRight: true);
			// Right section: inner edge faces leftward toward the gap — omit left border.
			DrawVSection(waveMin, waveMax, gapMax, _screenPerpMax, true);
			DrawRect(new Rect2(gapMin, waveMin, gapMax - gapMin, waveMax - waveMin), GapFillColour);
		}
	}

	/// <summary>
	/// Draws one filled section of a horizontally-travelling wave (travel axis = X,
	/// perp axis = Y). Uses explicit <see cref="DrawLine"/> calls for the border so
	/// the edge that faces the gap can be omitted, leaving a clean open gap.
	/// </summary>
	void DrawHSection(float xMin, float xMax, float yMin, float yMax,
		bool omitTop = false, bool omitBottom = false)
	{
		if (yMax <= yMin || xMax <= xMin) return;
		DrawRect(new Rect2(xMin, yMin, xMax - xMin, yMax - yMin), FillColour);
		// Left and right edges are always drawn (they run parallel to the gap).
		DrawLine(new Vector2(xMin, yMin), new Vector2(xMin, yMax), BorderColour, BorderWidth);
		DrawLine(new Vector2(xMax, yMin), new Vector2(xMax, yMax), BorderColour, BorderWidth);
		// Top and bottom edges are only drawn on the outer sides, never toward the gap.
		if (!omitTop)
			DrawLine(new Vector2(xMin, yMin), new Vector2(xMax, yMin), BorderColour, BorderWidth);
		if (!omitBottom)
			DrawLine(new Vector2(xMin, yMax), new Vector2(xMax, yMax), BorderColour, BorderWidth);
	}

	/// <summary>
	/// Draws one filled section of a vertically-travelling wave (travel axis = Y,
	/// perp axis = X). Omits the border edge that faces the gap.
	/// </summary>
	void DrawVSection(float yMin, float yMax, float xMin, float xMax,
		bool omitLeft = false, bool omitRight = false)
	{
		if (xMax <= xMin || yMax <= yMin) return;
		DrawRect(new Rect2(xMin, yMin, xMax - xMin, yMax - yMin), FillColour);
		// Top and bottom edges always drawn.
		DrawLine(new Vector2(xMin, yMin), new Vector2(xMax, yMin), BorderColour, BorderWidth);
		DrawLine(new Vector2(xMin, yMax), new Vector2(xMax, yMax), BorderColour, BorderWidth);
		// Left and right edges only drawn on the outer sides.
		if (!omitLeft)
			DrawLine(new Vector2(xMin, yMin), new Vector2(xMin, yMax), BorderColour, BorderWidth);
		if (!omitRight)
			DrawLine(new Vector2(xMax, yMin), new Vector2(xMax, yMax), BorderColour, BorderWidth);
	}

	// ── damage ─────────────────────────────────────────────────────────────────

	void CheckHits()
	{
		var trailEdge = _movingPositive ? _leadingEdge - WaveThickness : _leadingEdge + WaveThickness;
		var waveMin = Mathf.Min(_leadingEdge, trailEdge);
		var waveMax = Mathf.Max(_leadingEdge, trailEdge);

		var gapMin = GapCenter - GapSize * 0.5f;
		var gapMax = GapCenter + GapSize * 0.5f;

		foreach (var node in GetTree().GetNodesInGroup("party"))
		{
			if (node is not Character target || !target.IsAlive) continue;
			if (_alreadyHit.Contains(target)) continue;

			// NPC party members (Templar, Assassin, Wizard) cannot dodge, so
			// they are immune to Necrotic Waves — only the player takes damage.
			if (target.CharacterName != healerfantasy.GameConstants.HealerName) continue;

			// Coordinate of this character on the travel axis and the perp axis.
			var travelCoord = _isHorizontal ? target.GlobalPosition.X : target.GlobalPosition.Y;
			var perpCoord = _isHorizontal ? target.GlobalPosition.Y : target.GlobalPosition.X;

			// Is the character inside the wave band?
			if (travelCoord < waveMin || travelCoord > waveMax) continue;

			// Mark as processed regardless of outcome (safe or hit).
			_alreadyHit.Add(target);

			// Is the character in the safe gap?
			if (perpCoord >= gapMin && perpCoord <= gapMax) continue;

			// ── apply damage ───────────────────────────────────────────────────
			target.TakeDamage(DamageAmount);

			target.RaiseFloatingCombatText(
				DamageAmount,
				false,
				(int)SpellSchool.Void,
				false);

			CombatLog.Record(new CombatEventRecord
			{
				Timestamp = Time.GetTicksMsec() / 1000.0,
				SourceName = healerfantasy.GameConstants.ForsakenBoss3Name,
				TargetName = target.CharacterName,
				AbilityName = "Necrotic Waves",
				Amount = DamageAmount,
				Type = CombatEventType.Damage,
				IsCrit = false,
				Description = "A sweeping wall of necrotic void energy. Stand in the gap to avoid damage."
			});
		}
	}
}