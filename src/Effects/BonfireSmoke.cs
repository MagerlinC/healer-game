using Godot;

namespace healerfantasy;

/// <summary>
/// Rising smoke column effect — designed to sit above a bonfire sprite in the
/// Overworld / Camp background.
///
/// Three layered <see cref="Sprite2D"/> quads share a single smoke shader but
/// scroll at different speeds and phase offsets, producing an organic, non-
/// repeating pillar that narrows near the bonfire and disperses toward the top.
///
/// Coordinates are in the shared 1920 × 1080 reference-canvas space used by
/// <see cref="LoadoutController"/> so they scale correctly with the viewport.
///
/// Place this node in the Godot editor (it is a <c>[GlobalClass]</c>) or let
/// <see cref="LoadoutController.SetupBonfireSmoke"/> spawn it automatically.
/// Export properties can be tweaked in the Inspector to match the background art.
/// </summary>
[GlobalClass]
public partial class BonfireSmoke : Node2D
{
	/// <summary>Horizontal centre of the smoke column (reference-canvas X).</summary>
	[Export] public float ColumnCenterX { get; set; } = 960f;

	/// <summary>Bottom of the smoke column — set this to the bonfire's Y position
	/// in reference-canvas space.</summary>
	[Export] public float BonfireY { get; set; } = 620f;

	/// <summary>How tall the smoke column reaches above the bonfire (reference pixels).</summary>
	[Export] public float ColumnHeight { get; set; } = 460f;

	/// <summary>Width of the quad hosting each shader layer.  The shader masks the
	/// visible smoke to a narrower band within this space, so err on the wider side.</summary>
	[Export] public float QuadWidth { get; set; } = 220f;

	public override void _Ready()
	{
		var tex    = GD.Load<Texture2D>(AssetConstants.MistTexturePath);
		var shader = BuildSmokeShader();

		// Three layers: slow/dense base, medium, fast/wispy top.
		// scrollY is positive = upward (positive UV offset samples higher in the
		// texture over time, which moves visible features toward the top of the screen).
		AddSmokeLayer(tex, shader, scrollY: 0.038f, scrollX:  0.005f, alpha: 0.42f, timeOffset:  0f);
		AddSmokeLayer(tex, shader, scrollY: 0.024f, scrollX: -0.004f, alpha: 0.32f, timeOffset: 13f);
		AddSmokeLayer(tex, shader, scrollY: 0.050f, scrollX:  0.002f, alpha: 0.22f, timeOffset: 27f);
	}

	void AddSmokeLayer(
		Texture2D tex, Shader shader,
		float scrollY, float scrollX, float alpha, float timeOffset)
	{
		var mat = new ShaderMaterial();
		mat.Shader = shader;
		mat.SetShaderParameter("scroll_x",    scrollX);
		mat.SetShaderParameter("scroll_y",    scrollY);
		mat.SetShaderParameter("alpha_scale", alpha);
		mat.SetShaderParameter("time_offset", timeOffset);

		var sprite = new Sprite2D();
		sprite.Texture  = tex;
		sprite.Centered = true;
		// Sprite2D is centred on its Position, so shift up by half the column height
		// so the bottom of the quad sits exactly at BonfireY.
		sprite.Position = new Vector2(ColumnCenterX, BonfireY - ColumnHeight * 0.5f);
		sprite.Scale    = new Vector2(
			QuadWidth    / tex.GetWidth(),
			ColumnHeight / tex.GetHeight());
		sprite.Material = mat;
		AddChild(sprite);
	}

	// ── Smoke shader ──────────────────────────────────────────────────────────

	/// <summary>
	/// Builds the shared canvas_item shader for all smoke layers.
	///
	/// Technique (adapted from the main-menu mist shader):
	///   Detail samples A + B  — staggered tiles eliminate hard tiling seams.
	///   Shape mask            — slow large-scale sample gives organic cloud edges.
	///   Column mask           — horizontal smoothstep narrows toward the bonfire
	///                           and widens as the smoke rises; a slight time-driven
	///                           wobble makes the column sway naturally.
	///   Vertical fades        — fade-in just above the bonfire; fade-out near the
	///                           top so smoke appears to disperse into the air.
	///   Colour gradient       — warm amber near the fire, dark charcoal at the top.
	/// </summary>
	static Shader BuildSmokeShader()
	{
		var s = new Shader();
		s.Code = @"
shader_type canvas_item;
uniform float scroll_x    =  0.005;
uniform float scroll_y    =  0.038;   // positive = upward (UV shifts up → features rise)
uniform float alpha_scale =  0.42;
uniform float time_offset =  0.0;

void fragment() {
    float t  = TIME + time_offset;

    // ── Tiled detail UVs (scroll upward) ─────────────────────────────────
    vec2 uv = vec2(UV.x * 2.8 + scroll_x * t,
                   UV.y * 2.2 + scroll_y * t);

    // ── Detail sample A (main tile) ──────────────────────────────────────
    vec2  fA   = fract(uv);
    float lumA = dot(texture(TEXTURE, fA).rgb, vec3(0.299, 0.587, 0.114));
    float eA   = smoothstep(0.0, 0.28, fA.x) * (1.0 - smoothstep(0.72, 1.0, fA.x))
               * smoothstep(0.0, 0.28, fA.y) * (1.0 - smoothstep(0.72, 1.0, fA.y));

    // ── Detail sample B (half-tile offset — seams never align with A) ────
    vec2  fB   = fract(uv + vec2(0.5, 0.5));
    float lumB = dot(texture(TEXTURE, fB).rgb, vec3(0.299, 0.587, 0.114));
    float eB   = smoothstep(0.0, 0.28, fB.x) * (1.0 - smoothstep(0.72, 1.0, fB.x))
               * smoothstep(0.0, 0.28, fB.y) * (1.0 - smoothstep(0.72, 1.0, fB.y));

    // ── Shape mask (slow, large-scale — organic cloud outlines) ──────────
    vec2  fS    = fract(vec2(UV.x * 0.70 + scroll_x * 0.15 * t,
                             UV.y * 0.60 + scroll_y * 0.15 * t));
    float shape = smoothstep(0.15, 0.55,
                             dot(texture(TEXTURE, fS).rgb, vec3(0.299, 0.587, 0.114)));

    float density = max(lumA * eA, lumB * eB) * shape;

    // ── Column mask ───────────────────────────────────────────────────────
    // UV.y = 1 is the bottom (bonfire); UV.y = 0 is the top (open air).
    // The column narrows near the bonfire and spreads as smoke rises.
    // A slow sine wobble makes the column sway like real smoke.
    float wobble = sin(t * 0.75 + UV.y * 3.14159) * 0.035;
    float cx     = 0.5 + wobble;
    float half_w = mix(0.12, 0.40, 1.0 - UV.y);   // wide at top, narrow at base
    float col    = smoothstep(0.0, 0.10, half_w - abs(UV.x - cx));

    // ── Vertical fades ────────────────────────────────────────────────────
    // Fade in a short distance above the bonfire (avoid a hard bottom edge).
    float v_bottom = smoothstep(0.0, 0.07, 1.0 - UV.y);
    // Fade out as smoke disperses toward the top.
    float v_top    = smoothstep(0.0, 0.50, UV.y);

    float smoke = density * col * v_bottom * v_top;

    // ── Colour gradient ───────────────────────────────────────────────────
    // Warm amber near the fire → dark charcoal as smoke rises.
    // mix(a, b, UV.y): UV.y=0 → col_top (charcoal), UV.y=1 → col_base (warm).
    vec3 col_top  = vec3(0.30, 0.28, 0.27);   // dark charcoal
    vec3 col_base = vec3(0.58, 0.44, 0.28);   // warm amber near fire
    vec3 col_out  = mix(col_top, col_base, UV.y);

    COLOR = vec4(col_out, smoke * alpha_scale);
}
";
		return s;
	}
}
