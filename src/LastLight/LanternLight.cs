using Godot;

namespace healerfantasy.LastLight;

/// <summary>
/// A small steady light that isn't the Lightkeeper's: the camp merchant's lantern, or
/// the glow that marks something you can interact with. A flickering warm glow that
/// holds back a little of the dark around it (registered in <see cref="LastLightStyle.Pockets"/>).
/// </summary>
public partial class LanternLight : Node2D
{
	/// <summary>Pocket radius, as a fraction of the screen (the healer's light is ~0.3).</summary>
	public float Radius = 0.16f;

	/// <summary>Opacity and size of the visible glow sprite.</summary>
	public float GlowAlpha = 1f, GlowScale = 0.55f;

	/// <summary>How much (0..1) and how fast (1 = a lively flame) the light flickers.</summary>
	public float FlickerAmount = 1f, FlickerSpeed = 1f;

	/// <summary>Multiplier on the pocket and glow, eased towards (e.g. brighter while hovered).</summary>
	public float Boost = 1f;

	float _boost = 1f;

	// Carried lights glow softly; only the Lightkeeper and fires get ragged, smouldering edges.
	readonly LightPocket _pocket = new() { Soft = 1f };
	Sprite2D _glow = null!;
	float _t;

	public override void _Ready()
	{
		_t = GD.Randf() * 10f;

		var g = new Gradient();
		g.SetColor(0, new Color(1f, 0.78f, 0.42f, 0.6f));
		g.SetColor(1, new Color(1f, 0.55f, 0.2f, 0f));
		g.AddPoint(0.15f, new Color(1f, 0.85f, 0.55f, 0.45f));
		_glow = new Sprite2D
		{
			Texture = new GradientTexture2D
			{
				Gradient = g, Width = 128, Height = 128,
				Fill = GradientTexture2D.FillEnum.Radial,
				FillFrom = new Vector2(0.5f, 0.5f), FillTo = new Vector2(0.5f, 0f)
			},
			Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add },
			Scale = new Vector2(GlowScale, GlowScale)
		};
		AddChild(_glow);
	}

	public override void _EnterTree() => LastLightStyle.Pockets.Add(_pocket);
	public override void _ExitTree() => LastLightStyle.Pockets.Remove(_pocket);

	public override void _Process(double delta)
	{
		_t += (float)delta * FlickerSpeed;
		_boost = Mathf.Lerp(_boost, Boost, 1f - Mathf.Exp(-10f * (float)delta));
		var wobble = 0.07f * Mathf.Sin(_t * 7.3f) + 0.05f * Mathf.Sin(_t * 13.1f + 1f);
		var flick = (1f - FlickerAmount * (0.12f - wobble)) * _boost;

		_glow.Modulate = new Color(1f, 1f, 1f, Mathf.Min(GlowAlpha * flick, 1f));
		_glow.Scale = Vector2.One * GlowScale * Mathf.Sqrt(_boost);

		var size = GetViewport().GetVisibleRect().Size;
		_pocket.Uv = GetGlobalTransformWithCanvas().Origin / size;
		_pocket.Radius = Radius * flick;
	}
}
