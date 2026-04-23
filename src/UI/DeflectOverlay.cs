using Godot;
using healerfantasy;

/// <summary>
/// Full-screen darkening overlay that activates during deflectable boss casts
/// (Structural Crush). Sits on CanvasLayer 1 — above the game world but below
/// the UI (layer 10) — so it darkens enemies and the arena without obscuring
/// party frames or the cast bar.
///
/// Effect behaviour
/// ────────────────
/// • When a parryable cast begins, a dark veil fades over the screen.
/// • A soft circular spotlight is kept clear around the boss so it stays in
///   focus; everything outside grows progressively darker as the cast
///   approaches completion.
/// • The darkness ramps from 0 % at cast start to ~85 % at cast end, using an
///   eased curve so the last second feels especially threatening.
/// • When the cast resolves (deflected or landed) the overlay disappears
///   immediately.
///
/// Implementation note
/// ───────────────────
/// Uses a pure inline <c>canvas_item</c> shader on a fullscreen
/// <see cref="ColorRect"/> — no external asset required. To replace the solid
/// black with a texture (e.g. a vignette sprite), set the ColorRect's texture (TextureRect)
/// and sample it in the fragment shader instead of outputting a flat colour.
/// </summary>
public partial class DeflectOverlay : Node
{
	// ── shader source ─────────────────────────────────────────────────────────
	const string ShaderSrc = """
	                         shader_type canvas_item;

	                         // 0.0 = invisible, 1.0 = fully applied
	                         uniform float intensity : hint_range(0.0, 1.0) = 0.0;

	                         // Boss position in normalised screen UV space (0-1).
	                         uniform vec2 boss_uv = vec2(0.5, 0.5);

	                         // Radius of the clear spotlight in UV units (aspect-corrected).
	                         uniform float clear_radius : hint_range(0.0, 0.5) = 0.18;

	                         // Width of the soft edge around the spotlight.
	                         uniform float edge_softness : hint_range(0.0, 0.3) = 0.10;

	                         // Viewport width / height — set from C# because VIEWPORT_SIZE
	                         // is not available in canvas_item shaders.
	                         uniform float aspect_ratio = 1.777;

	                         void fragment() {
	                             vec2 diff = SCREEN_UV - boss_uv;
	                             // Correct for aspect ratio so the spotlight looks circular.
	                             diff.x *= aspect_ratio;
	                             float dist = length(diff);

	                             // 0 inside the clear circle, 1 in the dark region.
	                             float mask = smoothstep(clear_radius, clear_radius + edge_softness, dist);

	                             float darkness = mask * intensity * 0.85;
	                             COLOR = vec4(0.0, 0.0, 0.0, darkness);
	                         }
	                         """;

	// ── shader parameter names ────────────────────────────────────────────────
	const string PIntensity    = "intensity";
	const string PBossUv       = "boss_uv";
	const string PClearRadius  = "clear_radius";
	const string PEdgeSoft     = "edge_softness";
	const string PAspectRatio  = "aspect_ratio";

	// ── scene refs ────────────────────────────────────────────────────────────
	ShaderMaterial _mat;

	// ── cast state ────────────────────────────────────────────────────────────
	bool _active;
	float _duration;
	float _elapsed;

	// ── lifecycle ─────────────────────────────────────────────────────────────
	public override void _Ready()
	{
		// Sit between game world (layer –10) and UI (layer 10).
		var layer = new CanvasLayer { Layer = 1 };
		AddChild(layer);

		var rect = new ColorRect();
		rect.Color = new Color(0, 0, 0, 0); // shader drives actual output
		rect.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		rect.MouseFilter = Control.MouseFilterEnum.Ignore;
		layer.AddChild(rect);

		var shader = new Shader { Code = ShaderSrc };
		_mat = new ShaderMaterial { Shader = shader };
		_mat.SetShaderParameter(PIntensity,   0f);
		_mat.SetShaderParameter(PBossUv,      new Vector2(0.5f, 0.5f));
		_mat.SetShaderParameter(PClearRadius, 0.18f);
		_mat.SetShaderParameter(PEdgeSoft,    0.10f);
		_mat.SetShaderParameter(PAspectRatio, 16f / 9f);
		rect.Material = _mat;

		GlobalAutoLoad.SubscribeToSignal(
			nameof(CrystalKnight.CastWindupStarted),
			Callable.From((string _, Texture2D __, float duration) => BeginOverlay(duration)));

		GlobalAutoLoad.SubscribeToSignal(
			nameof(CrystalKnight.CastWindupEnded),
			Callable.From(EndOverlay));
	}

	public override void _Process(double delta)
	{
		if (!_active) return;

		_elapsed += (float)delta;
		var progress = Mathf.Clamp(_elapsed / _duration, 0f, 1f);

		// Ease-in-quad: subtle at first, dramatic near the end.
		var intensity = progress * progress;
		_mat.SetShaderParameter(PIntensity, intensity);

		// Re-project boss world pos to screen UV each frame so the spotlight
		// stays accurate even if the viewport is resized or a camera is added.
		UpdateBossScreenUv();
	}

	// ── private ───────────────────────────────────────────────────────────────

	void BeginOverlay(float duration)
	{
		_duration = duration;
		_elapsed = 0f;
		_active = true;
		UpdateBossScreenUv();
	}

	void EndOverlay()
	{
		_active = false;
		_mat.SetShaderParameter(PIntensity, 0f);
	}

	/// <summary>
	/// Converts the boss node's GlobalPosition to a normalised screen UV and
	/// pushes it to the shader. Falls back to screen-centre if no boss is found.
	/// </summary>
	void UpdateBossScreenUv()
	{
		var viewport = GetViewport();
		if (viewport == null) return;

		var bossWorldPos = Vector2.Zero;
		var bossNodes = GetTree().GetNodesInGroup(GameConstants.BossGroupName);
		if (bossNodes.Count > 0 && bossNodes[0] is Node2D bossNode)
			bossWorldPos = bossNode.GlobalPosition;

		// CanvasTransform maps world coords → viewport pixels.
		var screenPt = viewport.GetCanvasTransform() * bossWorldPos;
		var vpSize = viewport.GetVisibleRect().Size;

		// Guard against a zero-size viewport on the first frame.
		if (vpSize.X < 1f || vpSize.Y < 1f) return;

		var uv = new Vector2(screenPt.X / vpSize.X, screenPt.Y / vpSize.Y);
		_mat.SetShaderParameter(PBossUv,      uv);
		_mat.SetShaderParameter(PAspectRatio, vpSize.X / vpSize.Y);
	}
}