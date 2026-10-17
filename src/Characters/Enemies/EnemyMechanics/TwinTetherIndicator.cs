using System.Collections.Generic;
using Godot;
using healerfantasy.LastLight;

/// <summary>
/// Shows an Astral Twin's phase shield on the models themselves, and what breaks it.
///
///   • An astral aura — glow plus a turning ring of starlight — wraps the shielded twin.
///   • A shimmering tether runs from it to its sibling, with motes flowing along it into a
///     pulsing latch point on the sibling's body: "hit that one".
///   • Both weaken as the sibling takes damage (the ring thins and dims, the tether
///     frays and flickers near breaking) and jolt on every hit.
///   • When the shield breaks — or either twin dies — the ring bursts outward, the
///     tether snaps, and the node frees itself. No explicit teardown call is needed.
/// </summary>
public partial class TwinTetherIndicator : Node2D
{
	const float PopInDuration = 0.25f;
	const float BreakDuration = 0.4f;
	const float RingPadding = 0.62f; // ring radii relative to the body size
	const float LatchDepth = 0.18f; // how far into the sibling's body the tether latches, relative to its size
	const int RingDashes = 12;
	const int TetherSegments = 28;
	const int TetherMotes = 6;
	const float MoteSpeed = 0.55f; // tether lengths per second
	const float HitJolt = 1f;

	static readonly Color AuraCore = new(0.78f, 0.88f, 1f);
	static readonly Color AuraEdge = new(0.6f, 0.5f, 1f);

	AstralTwin _owner;
	AstralTwin _sibling;
	Sprite2D _glow;
	float _t;
	float _appear; // 0 → 1 pop-in
	float _break = -1f; // < 0 while active, then 0 → 1 over the break animation
	float _lastStrength = 1f;
	float _jolt;

	// Last fitted geometry, kept so the break animation still has something to draw
	// after a twin has died or been freed.
	Vector2 _ringRadii = new(20f, 30f);
	Vector2 _targetCenter;
	Vector2 _latchRadii = new(5f, 10f);
	bool _fitted;

	/// <summary>Adds the indicator to the shielded <paramref name="owner"/>, tethered to <paramref name="sibling"/>.</summary>
	public static TwinTetherIndicator Attach(AstralTwin owner, AstralTwin sibling)
	{
		var indicator = new TwinTetherIndicator
		{
			_owner = owner,
			_sibling = sibling,
			ZIndex = 2, // over the silhouettes
			Visible = false // until it has been fitted to the bodies
		};
		owner.AddChild(indicator);
		return indicator;
	}

	public override void _Ready()
	{
		var gradient = new Gradient();
		gradient.SetColor(0, new Color(AuraCore, 0.45f));
		gradient.SetColor(1, new Color(AuraEdge, 0f));
		_glow = new Sprite2D
		{
			Texture = new GradientTexture2D
			{
				Gradient = gradient, Width = 128, Height = 128,
				Fill = GradientTexture2D.FillEnum.Radial,
				FillFrom = new Vector2(0.5f, 0.5f), FillTo = new Vector2(0.5f, 0f)
			},
			Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add },
			ShowBehindParent = true
		};
		AddChild(_glow);
	}

	public override void _Process(double delta)
	{
		var dt = (float)delta;
		_t += dt;

		if (_break >= 0f)
		{
			_break += dt / BreakDuration;
			if (_break >= 1f) QueueFree();
			QueueRedraw();
			return;
		}

		if (!IsInstanceValid(_owner) || !_owner.IsAlive || !_owner.IsPhaseShielded
		    || !IsInstanceValid(_sibling) || !_sibling.IsAlive)
		{
			_break = 0f;
			return;
		}

		_appear = Mathf.Min(1f, _appear + dt / PopInDuration);
		Fit();

		var strength = _owner.ShieldStrength;
		if (strength < _lastStrength) _jolt = HitJolt;
		_lastStrength = strength;
		_jolt = Mathf.MoveToward(_jolt, 0f, dt * 5f);

		var pulse = 0.5f + 0.5f * Mathf.Sin(_t * 3f);
		_glow.Modulate = new Color(1f, 1f, 1f,
			_appear * (0.55f + 0.25f * pulse + 0.4f * _jolt) * Mathf.Lerp(0.45f, 1f, strength));
		QueueRedraw();
	}

	/// <summary>Centres the aura on the shielded twin and finds the sibling's body in local space.</summary>
	void Fit()
	{
		var rig = _owner.GetNodeOrNull<SilhouetteRig>("SilhouetteRig");
		var siblingRig = _sibling.GetNodeOrNull<SilhouetteRig>("SilhouetteRig");
		if (rig == null || siblingRig == null) return;

		var body = rig.GetBodyRectLocal();
		Position = body.GetCenter();
		_ringRadii = body.Size * RingPadding;
		_glow.Scale = body.Size * 1.6f / 128f;

		// Sibling geometry in this node's space (both twins are unscaled siblings in the scene).
		var siblingBody = siblingRig.GetBodyRectLocal();
		_targetCenter = ToLocal(_sibling.ToGlobal(siblingBody.GetCenter()));
		_latchRadii = siblingBody.Size * LatchDepth;

		_fitted = true;
		Visible = true;
	}

	public override void _Draw()
	{
		if (!_fitted) return;

		var strength = _break >= 0f ? _lastStrength : _owner.ShieldStrength;
		var fade = _break >= 0f ? 1f - _break : _appear;
		var weak = Mathf.Clamp((0.35f - strength) / 0.35f, 0f, 1f); // 0 healthy → 1 about to break
		var flicker = weak > 0f && GD.Randf() < weak * 0.25f ? 0.45f : 1f;
		var alpha = fade * flicker * Mathf.Lerp(0.55f, 1f, strength) * (1f + 0.3f * _jolt);

		DrawAura(strength, alpha);
		if (_break < 0.35f) // the tether snaps early in the break
			DrawTether(strength, alpha * (_break >= 0f ? 1f - _break / 0.35f : 1f), weak);
	}

	void DrawAura(float strength, float alpha)
	{
		// The ring bursts outward when the shield breaks; pops in from smaller.
		var grow = _break >= 0f ? 1f + 0.6f * _break : Mathf.Lerp(0.6f, 1f, Mathf.Ease(_appear, 0.4f));
		var radii = _ringRadii * grow;
		var width = Mathf.Lerp(1.5f, 3.5f, strength) + 1.5f * _jolt;

		// Faint full ring…
		DrawPolyline(Ellipse(Vector2.Zero, radii, 0f, Mathf.Tau, 48), new Color(AuraEdge, 0.35f * alpha), width + 3f, true);
		// …overlaid with turning bright dashes.
		var spin = _t * 0.6f;
		var dashLength = Mathf.Tau / RingDashes * 0.55f;
		for (var i = 0; i < RingDashes; i++)
		{
			var a = spin + i * Mathf.Tau / RingDashes;
			DrawPolyline(Ellipse(Vector2.Zero, radii, a, a + dashLength, 6), new Color(AuraCore, 0.9f * alpha), width, true);
		}

		// A few stars orbiting the other way.
		for (var i = 0; i < 4; i++)
		{
			var a = -_t * 1.1f + i * Mathf.Tau / 4f;
			var p = new Vector2(Mathf.Cos(a) * radii.X, Mathf.Sin(a) * radii.Y) * 0.92f;
			DrawStar(p, 2.5f + 1f * Mathf.Sin(_t * 5f + i), new Color(AuraCore, alpha));
		}
	}

	void DrawTether(float strength, float alpha, float weak)
	{
		if (alpha <= 0f) return;

		var dir = _targetCenter.Normalized();
		if (dir == Vector2.Zero) return;
		var normal = new Vector2(-dir.Y, dir.X);
		var start = EllipseEdge(Vector2.Zero, _ringRadii, dir);
		var end = EllipseEdge(_targetCenter, _latchRadii, -dir);
		var length = start.DistanceTo(end);

		// A gently waving beam that frays (wavier, jittery) as the shield weakens.
		var amplitude = 3f + 5f * weak + 3f * _jolt;
		var points = new Vector2[TetherSegments + 1];
		for (var i = 0; i <= TetherSegments; i++)
		{
			var f = (float)i / TetherSegments;
			var envelope = Mathf.Sin(f * Mathf.Pi); // pinned at both ends
			var wave = Mathf.Sin(f * 9f - _t * 5f) * amplitude * envelope;
			if (weak > 0f) wave += (GD.Randf() - 0.5f) * 3f * weak * envelope;
			points[i] = start.Lerp(end, f) + normal * wave;
		}

		var width = Mathf.Lerp(1f, 2.5f, strength);
		DrawPolyline(points, new Color(AuraEdge, 0.3f * alpha), width + 4f, true);
		DrawPolyline(points, new Color(AuraCore, 0.85f * alpha), width, true);

		// Motes flowing toward the sibling — the direction to look.
		for (var i = 0; i < TetherMotes; i++)
		{
			var f = Mathf.PosMod(_t * MoteSpeed * 120f / Mathf.Max(length, 1f) + (float)i / TetherMotes, 1f);
			var idx = Mathf.Clamp((int)(f * TetherSegments), 0, TetherSegments - 1);
			var p = points[idx].Lerp(points[idx + 1], f * TetherSegments - idx);
			DrawCircle(p, 1.8f, new Color(AuraCore, alpha * Mathf.Sin(f * Mathf.Pi)));
		}

		// Pulsing latch where the tether hooks into the sibling — a point, not a ring,
		// so the sibling doesn't read as shielded too.
		var pulse = 0.5f + 0.5f * Mathf.Sin(_t * 4f);
		DrawCircle(end, 4f + 2f * pulse, new Color(AuraEdge, 0.35f * alpha));
		DrawStar(end, 4f + 2.5f * pulse, new Color(AuraCore, alpha));
	}

	void DrawStar(Vector2 p, float size, Color color)
	{
		DrawLine(p - new Vector2(size, 0f), p + new Vector2(size, 0f), color, 1.2f, true);
		DrawLine(p - new Vector2(0f, size), p + new Vector2(0f, size), color, 1.2f, true);
		DrawCircle(p, size * 0.35f, color);
	}

	static Vector2[] Ellipse(Vector2 center, Vector2 radii, float from, float to, int segments)
	{
		var pts = new List<Vector2>(segments + 1);
		for (var i = 0; i <= segments; i++)
		{
			var a = Mathf.Lerp(from, to, (float)i / segments);
			pts.Add(center + new Vector2(Mathf.Cos(a) * radii.X, Mathf.Sin(a) * radii.Y));
		}

		return pts.ToArray();
	}

	/// <summary>Where a ray from the ellipse's centre in <paramref name="dir"/> meets its edge.</summary>
	static Vector2 EllipseEdge(Vector2 center, Vector2 radii, Vector2 dir)
	{
		var k = 1f / Mathf.Sqrt(dir.X * dir.X / (radii.X * radii.X) + dir.Y * dir.Y / (radii.Y * radii.Y));
		return center + dir * k;
	}
}
