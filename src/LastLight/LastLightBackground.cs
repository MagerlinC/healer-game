using Godot;

namespace healerfantasy.LastLight;

/// <summary>
/// Full-screen arena backdrop in the Last Light style, built from three layers:
///   1. ground  — fog sky + flagstone floor (shader)
///   2. ruins   — flat silhouettes drawn with polygons, in screen-relative units
///   3. light   — the healer's warm light and the surrounding dark (shader)
/// Lives on the same CanvasLayer the original background TextureRect used.
/// </summary>
public partial class LastLightBackground : Control
{
	public enum Preset
	{
		/// <summary>Arena: a ruined keep on the horizon, flagstones, framing pillars.</summary>
		AncientKeep,

		/// <summary>Menu / overworld: a ring of broken standing stones around the light.</summary>
		Sanctuary,

		/// <summary>Rusted fortress, smokestacks and a fallen gear under an amber haze; embers rise.</summary>
		ForsakenCitadel,

		/// <summary>A cathedral facade with dull red windows, spires and torn banners; red haze.</summary>
		CastleOfBlood,

		/// <summary>A starry sky, floating rock islands, a broken orrery ring and obelisks.</summary>
		SanctumOfStars,

		/// <summary>Jagged peaks, falling snow, a pale floor and the frost ring of the arena.</summary>
		FrozenPeak,

		/// <summary>Nothing on the horizon at all; a broken floor whose fragments drift up into the black.</summary>
		Abyss,

		/// <summary>Overworld / camp: a trampled clearing walled in by dark pines, under a canopy.</summary>
		Camp
	}

	public Preset BackgroundPreset = Preset.AncientKeep;

	/// <summary>
	/// The camp's clearing on the floor: centre (UV) and radii (screen heights).
	/// The camp's fire and props are laid out to sit inside it.
	/// </summary>
	public static readonly Vector4 CampClearing = new(0.5f, 0.69f, 0.66f, 0.25f);

	/// <summary>Everything that gives a backdrop its mood, besides its silhouettes.</summary>
	sealed record Palette(
		float Horizon,
		Color SkyTop, Color SkyLow, Color GroundFar, Color GroundNear, Color Fog,
		Color Far, Color Mid, Color Near,
		Color Speck, float SpeckSpeed, float SpeckDensity, float SpeckSize, float SpeckStrength,
		float Stars = 0f);

	static readonly Palette Neutral = new(0.2f,
		new(0.10f, 0.10f, 0.105f), new(0.28f, 0.275f, 0.265f), new(0.19f, 0.185f, 0.175f), new(0.065f, 0.06f, 0.056f),
		new(0.3f, 0.3f, 0.29f),
		new(0.15f, 0.148f, 0.142f, 0.9f), new(0.085f, 0.08f, 0.076f), new(0.025f, 0.022f, 0.02f),
		new(0.5f, 0.48f, 0.45f), 1.6f, 0.012f, 1f, 0.3f);

	Palette Mood => BackgroundPreset switch
	{
		Preset.Sanctuary => Neutral with { Horizon = 0.42f },
		Preset.ForsakenCitadel => new Palette(0.22f,
			new(0.09f, 0.07f, 0.05f), new(0.34f, 0.25f, 0.14f), new(0.2f, 0.16f, 0.11f), new(0.07f, 0.055f, 0.04f),
			new(0.36f, 0.27f, 0.15f),
			new(0.17f, 0.13f, 0.09f, 0.92f), new(0.09f, 0.07f, 0.05f), new(0.03f, 0.022f, 0.016f),
			new(1f, 0.5f, 0.2f), 2.2f, 0.01f, 1.1f, 0.45f),
		Preset.CastleOfBlood => new Palette(0.24f,
			new(0.07f, 0.02f, 0.03f), new(0.28f, 0.07f, 0.07f), new(0.17f, 0.08f, 0.08f), new(0.06f, 0.025f, 0.025f),
			new(0.35f, 0.09f, 0.08f),
			new(0.15f, 0.06f, 0.06f, 0.92f), new(0.08f, 0.03f, 0.03f), new(0.025f, 0.01f, 0.01f),
			new(0.6f, 0.14f, 0.1f), 0.9f, 0.012f, 1f, 0.35f),
		Preset.SanctumOfStars => new Palette(0.3f,
			new(0.015f, 0.015f, 0.03f), new(0.08f, 0.07f, 0.13f), new(0.1f, 0.09f, 0.15f), new(0.03f, 0.03f, 0.05f),
			new(0.14f, 0.12f, 0.24f),
			new(0.09f, 0.08f, 0.14f, 0.92f), new(0.05f, 0.045f, 0.08f), new(0.015f, 0.014f, 0.025f),
			new(0.6f, 0.66f, 1f), 0.7f, 0.01f, 0.8f, 0.4f, Stars: 0.006f),
		Preset.FrozenPeak => new Palette(0.2f,
			new(0.12f, 0.14f, 0.18f), new(0.42f, 0.46f, 0.52f), new(0.42f, 0.45f, 0.5f), new(0.2f, 0.22f, 0.26f),
			new(0.5f, 0.55f, 0.6f),
			new(0.26f, 0.3f, 0.36f, 0.92f), new(0.13f, 0.15f, 0.19f), new(0.04f, 0.045f, 0.06f),
			new(0.92f, 0.96f, 1f), -2.4f, 0.03f, 1.3f, 0.45f),
		Preset.Abyss => new Palette(0.26f,
			new(0.008f, 0.006f, 0.012f), new(0.05f, 0.035f, 0.07f), new(0.07f, 0.055f, 0.09f), new(0.025f, 0.02f, 0.035f),
			new(0.1f, 0.07f, 0.15f),
			new(0.05f, 0.04f, 0.07f, 0.9f), new(0.03f, 0.024f, 0.045f), new(0.01f, 0.008f, 0.015f),
			new(0.62f, 0.52f, 0.78f), 1.1f, 0.008f, 0.9f, 0.35f),
		Preset.Camp => new Palette(0.4f,
			new(0.02f, 0.02f, 0.024f), new(0.13f, 0.125f, 0.12f), new(0.15f, 0.13f, 0.105f), new(0.07f, 0.058f, 0.045f),
			new(0.2f, 0.19f, 0.18f),
			new(0.07f, 0.068f, 0.066f, 0.95f), new(0.035f, 0.032f, 0.03f), new(0.01f, 0.009f, 0.008f),
			new(0.5f, 0.48f, 0.45f), 1.2f, 0.006f, 0.9f, 0.22f),
		_ => Neutral
	};

	float Horizon => Mood.Horizon;

	delegate void ShapeFn(Color c, params float[] xy);

	ColorRect _ground = null!;
	Control _ruins = null!;
	ColorRect _light = null!;
	ShaderMaterial _lightMat = null!;

	public override void _Ready()
	{
		SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		MouseFilter = MouseFilterEnum.Ignore;

		var groundMat = new ShaderMaterial { Shader = GD.Load<Shader>("res://src/LastLight/background_ground.gdshader") };
		var mood = Mood;
		groundMat.SetShaderParameter("horizon", mood.Horizon);
		groundMat.SetShaderParameter("sky_top", mood.SkyTop);
		groundMat.SetShaderParameter("sky_low", mood.SkyLow);
		groundMat.SetShaderParameter("ground_far", mood.GroundFar);
		groundMat.SetShaderParameter("ground_near", mood.GroundNear);
		groundMat.SetShaderParameter("fog_color", mood.Fog);
		groundMat.SetShaderParameter("stars", mood.Stars);
		if (BackgroundPreset == Preset.Camp)
		{
			groundMat.SetShaderParameter("floor_style", 1f);
			groundMat.SetShaderParameter("clearing", CampClearing);
		}
		_ground = FullRect(groundMat);

		_ruins = new Control { MouseFilter = MouseFilterEnum.Ignore };
		_ruins.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		_ruins.Draw += DrawRuins;
		_ruins.Resized += _ruins.QueueRedraw;
		AddChild(_ruins);

		_lightMat = new ShaderMaterial { Shader = GD.Load<Shader>("res://src/LastLight/background_light.gdshader") };
		_lightMat.SetShaderParameter("speck_color", mood.Speck);
		_lightMat.SetShaderParameter("speck_speed", mood.SpeckSpeed);
		_lightMat.SetShaderParameter("speck_density", mood.SpeckDensity);
		_lightMat.SetShaderParameter("speck_size", mood.SpeckSize);
		_lightMat.SetShaderParameter("speck_strength", mood.SpeckStrength);
		_light = FullRect(_lightMat);
	}

	ColorRect FullRect(ShaderMaterial mat)
	{
		var rect = new ColorRect { Material = mat, MouseFilter = MouseFilterEnum.Ignore };
		rect.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		AddChild(rect);
		return rect;
	}

	public override void _Process(double delta)
	{
		var size = Size;
		var viewport = GetViewportRect().Size;
		if (size != viewport)
		{
			// Anchors don't resolve against a CanvasLayer parent, so follow the viewport by hand.
			Position = Vector2.Zero;
			Size = viewport;
			size = viewport;
		}
		_lightMat.SetShaderParameter("light_uv", LastLightStyle.LightUv);
		_lightMat.SetShaderParameter("feet_uv", LastLightStyle.LightFeetUv);
		_lightMat.SetShaderParameter("strength", LastLightStyle.LightStrength);
		_lightMat.SetShaderParameter("aspect", size.Y > 0f ? size.X / size.Y : 1.7778f);
		var hasPocket = LastLightStyle.Pockets.Count > 0;
		_lightMat.SetShaderParameter("extra_strength", hasPocket ? 1f : 0f);
		if (hasPocket) _lightMat.SetShaderParameter("extra_uv", LastLightStyle.Pockets[0].Uv);
	}

	// ── ruins ────────────────────────────────────────────────────────────────

	void DrawRuins()
	{
		var s = _ruins.Size;
		var mood = Mood;
		var far = mood.Far;
		var mid = mood.Mid;
		var near = mood.Near;

		// Coordinates are in units of screen height (x from the left edge), so
		// shapes keep their proportions on any aspect ratio.
		void Shape(Color c, params float[] xy)
		{
			var p = new Vector2[xy.Length / 2];
			for (var i = 0; i < p.Length; i++) p[i] = new Vector2(xy[i * 2], xy[i * 2 + 1]) * s.Y;
			_ruins.DrawColoredPolygon(p, c);
		}

		var h = Horizon;
		var w = s.X / s.Y; // screen width in screen-height units

		void Line(Color c, float width, Vector2[] pts)
		{
			var scaled = new Vector2[pts.Length];
			for (var i = 0; i < pts.Length; i++) scaled[i] = pts[i] * s.Y;
			_ruins.DrawPolyline(scaled, c, width, true);
		}

		switch (BackgroundPreset)
		{
			case Preset.Sanctuary:
				DrawSanctuary(Shape, w, h, far, mid, near);
				return;
			case Preset.ForsakenCitadel:
				DrawCitadel(Shape, w, h, far, mid, near);
				return;
			case Preset.CastleOfBlood:
				DrawCastle(Shape, w, h, far, mid, near);
				return;
			case Preset.SanctumOfStars:
				DrawSanctum(Shape, Line, w, h, far, mid, near);
				return;
			case Preset.FrozenPeak:
				DrawFrozen(Shape, Line, w, h, far, mid, near);
				return;
			case Preset.Abyss:
				DrawAbyss(Shape, w, h, far, mid, near);
				return;
			case Preset.Camp:
				DrawCamp(Shape, w, h, far, mid, near);
				return;
		}

		// ── distant keep along the horizon ──
		Shape(far, 0, h, 0, h - 0.05f, 0.18f, h - 0.06f, 0.22f, h - 0.03f, 0.4f, h - 0.035f, 0.42f, h);
		Shape(far, 0.3f, h, 0.3f, h - 0.16f, 0.315f, h - 0.2f, 0.33f, h - 0.16f, 0.33f, h);
		Shape(far, 0.36f, h, 0.36f, h - 0.11f, 0.375f, h - 0.135f, 0.39f, h - 0.11f, 0.39f, h);
		Shape(far, 0.6f, h, 0.6f, h - 0.1f, 0.64f, h - 0.12f, 0.66f, h - 0.09f, 0.68f, h - 0.13f, 0.7f, h - 0.1f, 0.7f, h);
		Shape(far, w * 0.62f, h, w * 0.62f, h - 0.07f, w * 0.75f, h - 0.075f, w * 0.78f, h - 0.05f, w, h - 0.06f, w, h);
		Shape(far, w * 0.7f, h, w * 0.7f, h - 0.2f, w * 0.71f, h - 0.24f, w * 0.72f, h - 0.2f, w * 0.72f, h);

		// broken arch, mid-distance
		var ax = w * 0.46f;
		Shape(mid, ax, h + 0.03f, ax, h - 0.1f, ax + 0.02f, h - 0.15f, ax + 0.06f, h - 0.17f, ax + 0.08f, h - 0.165f,
			ax + 0.08f, h - 0.145f, ax + 0.055f, h - 0.14f, ax + 0.035f, h - 0.12f, ax + 0.03f, h - 0.08f, ax + 0.03f, h + 0.03f);
		Shape(mid, ax + 0.13f, h + 0.03f, ax + 0.13f, h - 0.09f, ax + 0.115f, h - 0.11f, ax + 0.1f, h - 0.075f, ax + 0.1f, h + 0.03f);

		// rubble and grave markers on the floor
		Shape(mid, w * 0.12f, h + 0.06f, w * 0.125f, h + 0.03f, w * 0.135f, h + 0.025f, w * 0.14f, h + 0.06f);
		Shape(mid, w * 0.85f, h + 0.07f, w * 0.852f, h + 0.035f, w * 0.86f, h + 0.03f, w * 0.868f, h + 0.036f, w * 0.87f, h + 0.07f);
		Shape(mid, w * 0.3f, h + 0.05f, w * 0.33f, h + 0.035f, w * 0.35f, h + 0.05f);

		// ── near pillars framing the arena ──
		Shape(near, 0, 1, 0, 0, 0.07f, 0, 0.08f, 0.05f, 0.075f, 0.4f, 0.085f, 0.42f, 0.09f, 0.62f, 0.07f, 0.66f, 0.09f, 1);
		Shape(near, w, 1, w, 0.08f, w - 0.05f, 0.1f, w - 0.065f, 0.3f, w - 0.055f, 0.33f, w - 0.07f, 0.7f, w - 0.05f, 0.75f, w - 0.06f, 1);
		Shape(near, 0, 1, 0.2f, 1, 0.15f, 0.95f, 0.08f, 0.93f, 0, 0.9f);
		Shape(near, w, 1, w - 0.25f, 1, w - 0.18f, 0.96f, w - 0.08f, 0.94f, w, 0.9f);
	}

	static void DrawSanctuary(ShapeFn shape, float w, float h, Color far, Color mid, Color near)
	{
		// A standing stone: tapered, with a broken top. (x, base, width, height, lean)
		void Stone(Color c, float x, float b, float sw, float sh, float lean)
		{
			shape(c,
				x - sw * 0.5f, b,
				x - sw * 0.42f + lean, b - sh,
				x - sw * 0.05f + lean, b - sh - sh * 0.06f,
				x + sw * 0.12f + lean, b - sh * 0.92f,
				x + sw * 0.45f + lean, b - sh * 0.97f,
				x + sw * 0.5f, b);
		}

		// ── distant spires and ridges, barely there ──
		shape(far, 0, h, 0, h - 0.04f, w * 0.12f, h - 0.07f, w * 0.2f, h - 0.03f, w * 0.34f, h - 0.05f,
			w * 0.44f, h - 0.045f, w * 0.47f, h - 0.13f, w * 0.49f, h - 0.05f, w * 0.62f, h - 0.06f,
			w * 0.74f, h - 0.03f, w * 0.86f, h - 0.065f, w, h - 0.04f, w, h);
		shape(far, w * 0.56f, h - 0.05f, w * 0.565f, h - 0.17f, w * 0.57f, h - 0.19f, w * 0.575f, h - 0.17f, w * 0.58f, h - 0.05f);

		// ── the ring of standing stones (farther ones smaller and paler) ──
		Stone(far, w * 0.36f, h + 0.012f, 0.022f, 0.08f, 0.003f);
		Stone(far, w * 0.64f, h + 0.012f, 0.024f, 0.07f, -0.004f);
		Stone(mid, w * 0.27f, h + 0.05f, 0.036f, 0.15f, -0.01f);
		Stone(mid, w * 0.45f, h + 0.02f, 0.028f, 0.11f, 0.0f);
		Stone(mid, w * 0.55f, h + 0.02f, 0.03f, 0.075f, 0.004f); // snapped
		Stone(mid, w * 0.73f, h + 0.055f, 0.038f, 0.16f, 0.012f);

		// a dead tree, left
		var tx = w * 0.19f;
		var tb = h + 0.04f;
		shape(mid, tx - 0.008f, tb, tx - 0.004f, tb - 0.16f, tx + 0.004f, tb - 0.16f, tx + 0.009f, tb);
		shape(mid, tx - 0.002f, tb - 0.1f, tx - 0.05f, tb - 0.17f, tx - 0.055f, tb - 0.165f, tx - 0.004f, tb - 0.09f);
		shape(mid, tx + 0.001f, tb - 0.13f, tx + 0.04f, tb - 0.2f, tx + 0.045f, tb - 0.197f, tx + 0.004f, tb - 0.12f);
		shape(mid, tx - 0.002f, tb - 0.155f, tx - 0.012f, tb - 0.24f, tx - 0.008f, tb - 0.24f, tx + 0.002f, tb - 0.155f);

		// a fallen arch, right
		var ax = w * 0.82f;
		var ab = h + 0.05f;
		shape(mid, ax, ab, ax, ab - 0.1f, ax + 0.02f, ab - 0.14f, ax + 0.05f, ab - 0.15f, ax + 0.05f, ab - 0.13f,
			ax + 0.03f, ab - 0.115f, ax + 0.025f, ab - 0.08f, ax + 0.025f, ab);
		shape(mid, ax + 0.07f, ab + 0.01f, ax + 0.14f, ab - 0.01f, ax + 0.145f, ab + 0.015f, ax + 0.075f, ab + 0.03f);

		// ── near stones framing the scene ──
		Stone(near, w * 0.06f, 1.02f, 0.11f, 0.5f, 0.02f);
		Stone(near, w * 0.95f, 1.02f, 0.12f, 0.42f, -0.03f);
		shape(near, 0, 1, w * 0.2f, 1, w * 0.14f, 0.96f, w * 0.05f, 0.94f, 0, 0.93f);
		shape(near, w, 1, w * 0.78f, 1, w * 0.86f, 0.965f, w * 0.95f, 0.95f, w, 0.92f);
	}

	/// <summary>Deterministic 0..1 noise for scattering shapes.</summary>
	static float Hash(int i) => Mathf.PosMod(Mathf.Sin(i * 12.9898f + 78.233f) * 43758.5453f, 1f);

	/// <summary>A tiered pine silhouette with its trunk base at (x, b).</summary>
	static void Pine(ShapeFn shape, Color c, float x, float b, float sw, float sh, int tiers = 4)
	{
		var left = new System.Collections.Generic.List<Vector2>();
		var trunk = sh * 0.07f;
		left.Add(new Vector2(x - sw * 0.06f, b));
		left.Add(new Vector2(x - sw * 0.06f, b - trunk));
		for (var k = 0; k < tiers; k++)
		{
			var y0 = b - trunk - (sh - trunk) * k / tiers * 0.95f;
			var y1 = b - trunk - (sh - trunk) * (k + 1) / tiers * 0.95f;
			var hw = sw * 0.5f * (1f - 0.75f * k / tiers);
			left.Add(new Vector2(x - hw, y0));
			left.Add(new Vector2(x - hw * 0.4f, y1 + (y0 - y1) * 0.3f));
		}
		var xy = new System.Collections.Generic.List<float>();
		foreach (var p in left) { xy.Add(p.X); xy.Add(p.Y); }
		xy.Add(x); xy.Add(b - sh);
		for (var i = left.Count - 1; i >= 0; i--) { xy.Add(2f * x - left[i].X); xy.Add(left[i].Y); }
		shape(c, xy.ToArray());
	}

	/// <summary>A tall trunk rising out of the frame, with a root flare at its base.</summary>
	static void Trunk(ShapeFn shape, Color c, float x, float b, float tw, float bend)
	{
		shape(c,
			x - tw * 0.5f - tw * 0.6f, b,
			x - tw * 0.5f, b - 0.06f,
			x - tw * 0.42f + bend * 0.5f, b - 0.4f,
			x - tw * 0.36f + bend, -0.02f,
			x + tw * 0.36f + bend, -0.02f,
			x + tw * 0.42f + bend * 0.5f, b - 0.4f,
			x + tw * 0.5f, b - 0.06f,
			x + tw * 0.5f + tw * 0.7f, b);
	}

	static void DrawCamp(ShapeFn shape, float w, float h, Color far, Color mid, Color near)
	{
		var cx = w * 0.5f;

		// ── a dense wall of pines along the horizon, fading into the fog ──
		var i = 0;
		for (var x = -0.02f; x < w + 0.04f; x += 0.032f, i++)
			Pine(shape, far, x + (Hash(i) - 0.5f) * 0.02f, h + 0.012f, 0.06f + Hash(i + 50) * 0.03f,
				0.13f + Hash(i + 100) * 0.12f, 4);

		// ── nearer, taller pines closing round the clearing; lower behind the fire ──
		i = 200;
		for (var x = 0.02f; x < w; x += 0.075f, i++)
		{
			var fromCentre = Mathf.Abs(x - cx) / cx;
			var height = 0.2f + fromCentre * 0.28f + Hash(i) * 0.08f;
			Pine(shape, mid, x + (Hash(i + 7) - 0.5f) * 0.04f, h + 0.035f + fromCentre * 0.06f,
				0.09f + fromCentre * 0.05f, height, 5);
		}

		// ── great trunks framing the camp on both sides ──
		Trunk(shape, near, 0.05f, 0.98f, 0.075f, 0.01f);
		Trunk(shape, near, 0.2f, h + 0.2f, 0.04f, -0.012f);
		Trunk(shape, near, w - 0.06f, 0.96f, 0.085f, -0.012f);
		Trunk(shape, near, w - 0.23f, h + 0.18f, 0.035f, 0.01f);

		// ── the canopy hanging overhead, deepest at the sides ──
		var canopy = new System.Collections.Generic.List<float> { 0, -0.01f, w, -0.01f };
		i = 300;
		for (var x = w; x >= -0.001f; x -= 0.04f, i++)
		{
			var edge = Mathf.Abs(x - cx) / cx;
			canopy.Add(x);
			canopy.Add(0.03f + edge * edge * 0.3f + Hash(i) * 0.05f);
		}
		shape(near, canopy.ToArray());

		// ── undergrowth across the foreground, thickest in the corners ──
		var brush = new System.Collections.Generic.List<float> { w, 1.01f, 0, 1.01f };
		i = 400;
		for (var x = 0f; x <= w + 0.001f; x += 0.03f, i++)
		{
			var edge = Mathf.Abs(x - cx) / cx;
			brush.Add(x);
			brush.Add(0.985f - edge * edge * edge * 0.2f - Hash(i) * 0.025f);
		}
		shape(near, brush.ToArray());
	}

	// ── per-dungeon skylines ────────────────────────────────────────────────

	/// <summary>A closed ellipse as points (screen-height units).</summary>
	static Vector2[] Ellipse(float cx, float cy, float rx, float ry, int n = 64)
	{
		var pts = new Vector2[n + 1];
		for (var i = 0; i <= n; i++)
		{
			var a = Mathf.Tau * i / n;
			pts[i] = new Vector2(cx + Mathf.Cos(a) * rx, cy + Mathf.Sin(a) * ry);
		}
		return pts;
	}

	/// <summary>Points → flat xy array for filled shapes; drops a closing duplicate of the first point.</summary>
	static float[] Flatten(Vector2[] pts)
	{
		var n = pts.Length > 2 && pts[^1].IsEqualApprox(pts[0]) ? pts.Length - 1 : pts.Length;
		var xy = new float[n * 2];
		for (var i = 0; i < n; i++) { xy[i * 2] = pts[i].X; xy[i * 2 + 1] = pts[i].Y; }
		return xy;
	}

	static void DrawCitadel(ShapeFn shape, float w, float h, Color far, Color mid, Color near)
	{
		// crenellated fortress wall along the horizon, with towers
		var wall = new System.Collections.Generic.List<float> { 0, h };
		for (var x = 0f; x < w; x += 0.06f)
			wall.AddRange(new[] { x, h - 0.07f, x + 0.03f, h - 0.07f, x + 0.03f, h - 0.085f, x + 0.06f, h - 0.085f });
		wall.AddRange(new[] { w, h - 0.07f, w, h });
		shape(far, wall.ToArray());
		shape(far, w * 0.18f, h, w * 0.18f, h - 0.17f, w * 0.2f, h - 0.19f, w * 0.22f, h - 0.17f, w * 0.22f, h);
		shape(far, w * 0.66f, h, w * 0.66f, h - 0.2f, w * 0.69f, h - 0.2f, w * 0.69f, h);
		// smokestacks
		shape(mid, w * 0.36f, h + 0.01f, w * 0.365f, h - 0.2f, w * 0.385f, h - 0.2f, w * 0.39f, h + 0.01f);
		shape(mid, w * 0.43f, h + 0.01f, w * 0.433f, h - 0.15f, w * 0.448f, h - 0.15f, w * 0.452f, h + 0.01f);
		// a giant broken gear, half sunk into the floor
		var gx = w * 0.78f;
		var gy = h + 0.06f;
		var gear = new System.Collections.Generic.List<float>();
		for (var i = 0; i <= 20; i++)
		{
			var a = Mathf.Pi + Mathf.Pi * i / 20f; // upper half only
			var r = i % 2 == 0 ? 0.16f : 0.135f;
			gear.Add(gx + Mathf.Cos(a) * r);
			gear.Add(gy + Mathf.Sin(a) * r);
		}
		shape(mid, gear.ToArray());
		shape(near, Flatten(Ellipse(gx, gy - 0.02f, 0.045f, 0.045f, 12)));
		// iron girders framing the arena
		shape(near, 0, 1, 0, 0.1f, 0.05f, 0.1f, 0.12f, 1);
		shape(near, 0, 0.3f, 0.2f, 0.08f, 0.21f, 0.11f, 0, 0.34f);
		shape(near, w, 1, w, 0.15f, w - 0.05f, 0.15f, w - 0.13f, 1);
		shape(near, 0, 1, 0.22f, 1, 0.16f, 0.95f, 0, 0.92f);
		shape(near, w, 1, w - 0.24f, 1, w - 0.17f, 0.955f, w, 0.93f);
	}

	static void DrawCastle(ShapeFn shape, float w, float h, Color far, Color mid, Color near)
	{
		var glow = new Color(0.4f, 0.06f, 0.05f);
		// distant spires
		for (var i = 0; i < 7; i++)
		{
			var x = w * (0.06f + i * 0.15f);
			var top = h - 0.12f - (i % 3) * 0.05f;
			shape(far, x - 0.015f, h, x - 0.015f, top + 0.05f, x, top, x + 0.015f, top + 0.05f, x + 0.015f, h);
		}
		// the cathedral facade, centred
		var cx = w * 0.5f;
		shape(mid, cx - 0.2f, h + 0.02f, cx - 0.2f, h - 0.12f, cx - 0.12f, h - 0.16f, cx - 0.08f, h - 0.2f,
			cx, h - 0.27f, cx + 0.08f, h - 0.2f, cx + 0.12f, h - 0.16f, cx + 0.2f, h - 0.12f, cx + 0.2f, h + 0.02f);
		shape(mid, cx - 0.2f, h - 0.1f, cx - 0.19f, h - 0.3f, cx - 0.17f, h - 0.36f, cx - 0.15f, h - 0.3f, cx - 0.14f, h - 0.1f);
		shape(mid, cx + 0.14f, h - 0.1f, cx + 0.15f, h - 0.3f, cx + 0.17f, h - 0.36f, cx + 0.19f, h - 0.3f, cx + 0.2f, h - 0.1f);
		// the rose window and lancet windows, glowing dull red
		shape(glow, Flatten(Ellipse(cx, h - 0.15f, 0.03f, 0.03f, 16)));
		shape(glow, cx - 0.1f, h, cx - 0.1f, h - 0.07f, cx - 0.09f, h - 0.09f, cx - 0.08f, h - 0.07f, cx - 0.08f, h);
		shape(glow, cx + 0.08f, h, cx + 0.08f, h - 0.07f, cx + 0.09f, h - 0.09f, cx + 0.1f, h - 0.07f, cx + 0.1f, h);
		shape(near, cx - 0.025f, h + 0.02f, cx - 0.025f, h - 0.05f, cx, h - 0.08f, cx + 0.025f, h - 0.05f, cx + 0.025f, h + 0.02f);
		// torn banners hanging at the sides
		shape(near, 0.06f, 0, 0.16f, 0, 0.16f, 0.55f, 0.13f, 0.5f, 0.11f, 0.58f, 0.09f, 0.5f, 0.06f, 0.55f);
		shape(new Color(0.18f, 0.02f, 0.03f), 0.08f, 0.02f, 0.14f, 0.02f, 0.14f, 0.45f, 0.11f, 0.48f, 0.08f, 0.45f);
		shape(near, w - 0.16f, 0, w - 0.06f, 0, w - 0.06f, 0.5f, w - 0.09f, 0.46f, w - 0.11f, 0.54f, w - 0.13f, 0.46f, w - 0.16f, 0.5f);
		shape(new Color(0.18f, 0.02f, 0.03f), w - 0.14f, 0.02f, w - 0.08f, 0.02f, w - 0.08f, 0.41f, w - 0.11f, 0.44f, w - 0.14f, 0.41f);
		// gothic pillars framing the arena
		shape(near, 0, 1, 0, 0, 0.05f, 0, 0.05f, 1);
		shape(near, w, 1, w, 0, w - 0.05f, 0, w - 0.05f, 1);
		shape(near, 0, 1, 0.2f, 1, 0.14f, 0.95f, 0, 0.93f);
		shape(near, w, 1, w - 0.22f, 1, w - 0.15f, 0.955f, w, 0.93f);
	}

	delegate void LineFn(Color c, float width, Vector2[] pts);

	static void DrawSanctum(ShapeFn shape, LineFn line, float w, float h, Color far, Color mid, Color near)
	{
		// a vast broken orrery ring across the sky
		var arc = new Vector2[40];
		for (var i = 0; i < arc.Length; i++)
		{
			var a = Mathf.Pi * (1.08f + 0.7f * i / (arc.Length - 1));
			arc[i] = new Vector2(w * 0.5f + Mathf.Cos(a) * w * 0.42f, h + 0.2f + Mathf.Sin(a) * 0.42f);
		}
		line(new Color(0.42f, 0.44f, 0.62f, 0.35f), 3f, arc);
		// floating islands: flat tops, jagged undersides
		void Island(float x, float y, float r)
		{
			shape(far, x - r, y, x - r * 0.6f, y - r * 0.12f, x + r * 0.5f, y - r * 0.1f, x + r, y,
				x + r * 0.6f, y + r * 0.35f, x + r * 0.2f, y + r * 0.9f, x - r * 0.2f, y + r * 0.5f, x - r * 0.6f, y + r * 0.3f);
		}
		Island(w * 0.18f, h - 0.16f, 0.08f);
		Island(w * 0.62f, h - 0.22f, 0.06f);
		Island(w * 0.84f, h - 0.12f, 0.1f);
		// a ruined arch on the largest island
		shape(far, w * 0.84f - 0.03f, h - 0.13f, w * 0.84f - 0.03f, h - 0.19f, w * 0.84f, h - 0.21f, w * 0.84f + 0.03f, h - 0.19f,
			w * 0.84f + 0.03f, h - 0.13f, w * 0.84f + 0.015f, h - 0.13f, w * 0.84f + 0.015f, h - 0.175f, w * 0.84f - 0.015f, h - 0.175f,
			w * 0.84f - 0.015f, h - 0.13f);
		// leaning obelisks
		shape(mid, w * 0.3f, h + 0.04f, w * 0.31f, h - 0.14f, w * 0.318f, h - 0.16f, w * 0.326f, h - 0.14f, w * 0.334f, h + 0.04f);
		shape(mid, w * 0.7f, h + 0.05f, w * 0.716f, h - 0.1f, w * 0.722f, h - 0.11f, w * 0.73f, h - 0.1f, w * 0.732f, h + 0.05f);
		// monolith fragments framing the arena
		shape(near, 0, 1, 0, 0.2f, 0.08f, 0.24f, 0.1f, 0.7f, 0.06f, 1);
		shape(near, w, 1, w, 0.3f, w - 0.07f, 0.28f, w - 0.1f, 0.75f, w - 0.05f, 1);
	}

	static void DrawAbyss(ShapeFn shape, float w, float h, Color far, Color mid, Color near)
	{
		// No horizon at all: the floor simply ends, and its broken pieces rise into the black.
		void Shard(Color c, float x, float y, float r, float tilt)
		{
			shape(c, x - r, y + tilt * r, x - r * 0.3f, y - r * 0.5f, x + r * 0.6f, y - r * 0.35f - tilt * r,
				x + r, y + r * 0.1f, x + r * 0.2f, y + r * 0.7f);
		}
		Shard(far, w * 0.12f, h - 0.18f, 0.025f, 0.3f);
		Shard(far, w * 0.3f, h - 0.32f, 0.015f, -0.2f);
		Shard(far, w * 0.74f, h - 0.26f, 0.02f, 0.4f);
		Shard(far, w * 0.9f, h - 0.12f, 0.03f, -0.3f);
		Shard(mid, w * 0.2f, h - 0.06f, 0.035f, -0.25f);
		Shard(mid, w * 0.82f, h - 0.05f, 0.04f, 0.2f);
		// the floor's broken far edge
		shape(mid, 0, h + 0.02f, w * 0.1f, h - 0.005f, w * 0.18f, h + 0.012f, w * 0.3f, h - 0.004f, w * 0.42f, h + 0.015f,
			w * 0.55f, h - 0.006f, w * 0.68f, h + 0.012f, w * 0.8f, h - 0.004f, w * 0.92f, h + 0.016f, w, h, w, h + 0.06f, 0, h + 0.06f);
		// jagged black fragments framing the arena
		shape(near, 0, 1, 0, 0.28f, 0.05f, 0.34f, 0.09f, 0.62f, 0.06f, 0.8f, 0.11f, 1);
		shape(near, w, 1, w, 0.32f, w - 0.06f, 0.4f, w - 0.1f, 0.7f, w - 0.07f, 1);
	}

	static void DrawFrozen(ShapeFn shape, LineFn line, float w, float h, Color far, Color mid, Color near)
	{
		// jagged peaks along the horizon, one great peak behind the arena
		shape(far, 0, h, 0, h - 0.08f, w * 0.08f, h - 0.15f, w * 0.14f, h - 0.07f, w * 0.22f, h - 0.13f, w * 0.3f, h - 0.05f,
			w * 0.42f, h - 0.16f, w * 0.5f, h - 0.3f, w * 0.58f, h - 0.17f, w * 0.68f, h - 0.1f, w * 0.76f, h - 0.17f,
			w * 0.86f, h - 0.06f, w * 0.94f, h - 0.12f, w, h - 0.07f, w, h);
		shape(new Color(0.75f, 0.82f, 0.9f, 0.55f), w * 0.475f, h - 0.25f, w * 0.5f, h - 0.3f, w * 0.525f, h - 0.25f, w * 0.51f, h - 0.26f, w * 0.495f, h - 0.24f);

		// the arena: a frozen lake inside a frost ring (matches the physics boundary)
		var cx = w * 0.5f;
		var cy = 0.64f;
		var rx = w * 0.36f;
		const float ry = 0.3f;
		shape(new Color(0.55f, 0.66f, 0.78f, 0.12f), Flatten(Ellipse(cx, cy, rx, ry)));
		line(new Color(0.85f, 0.93f, 1f, 0.45f), 4f, Ellipse(cx, cy, rx, ry));
		line(new Color(0.85f, 0.93f, 1f, 0.15f), 10f, Ellipse(cx, cy, rx + 0.005f, ry + 0.004f));
		// ice shards jutting outward from the ring
		for (var i = 0; i < 18; i++)
		{
			var a = Mathf.Tau * i / 18f + 0.12f;
			var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
			var basePt = new Vector2(cx + dir.X * rx, cy + dir.Y * ry);
			var len = 0.035f + (i % 3) * 0.015f;
			var tip = basePt + dir * len;
			var side = new Vector2(-dir.Y, dir.X) * 0.008f;
			shape(new Color(0.7f, 0.8f, 0.9f, 0.5f), (basePt - side).X, (basePt - side).Y, tip.X, tip.Y, (basePt + side).X, (basePt + side).Y);
		}
		// snow-heavy rocks framing the arena
		shape(near, 0, 1, 0, 0.35f, 0.06f, 0.3f, 0.12f, 0.55f, 0.1f, 0.8f, 0.16f, 1);
		shape(near, w, 1, w, 0.4f, w - 0.07f, 0.36f, w - 0.11f, 0.6f, w - 0.14f, 1);
	}
}
