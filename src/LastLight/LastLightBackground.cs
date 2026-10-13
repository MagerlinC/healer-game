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
		Sanctuary
	}

	public Preset BackgroundPreset = Preset.AncientKeep;

	float Horizon => BackgroundPreset == Preset.Sanctuary ? 0.42f : 0.2f;

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
		groundMat.SetShaderParameter("horizon", Horizon);
		_ground = FullRect(groundMat);

		_ruins = new Control { MouseFilter = MouseFilterEnum.Ignore };
		_ruins.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		_ruins.Draw += DrawRuins;
		_ruins.Resized += _ruins.QueueRedraw;
		AddChild(_ruins);

		_lightMat = new ShaderMaterial { Shader = GD.Load<Shader>("res://src/LastLight/background_light.gdshader") };
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
	}

	// ── ruins ────────────────────────────────────────────────────────────────

	void DrawRuins()
	{
		var s = _ruins.Size;
		var far = new Color(0.15f, 0.148f, 0.142f, 0.9f);
		var mid = new Color(0.085f, 0.08f, 0.076f, 1f);
		var near = new Color(0.025f, 0.022f, 0.02f, 1f);

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

		if (BackgroundPreset == Preset.Sanctuary)
		{
			DrawSanctuary(Shape, w, h, far, mid, near);
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
}
