using Godot;

namespace healerfantasy.LastLight;

/// <summary>
/// A small steady light someone else is carrying (e.g. the camp merchant's lantern):
/// a flickering warm glow that holds back a little of the dark around it
/// (registered in <see cref="LastLightStyle.Pockets"/>).
/// </summary>
public partial class LanternLight : Node2D
{
	/// <summary>Pocket radius, as a fraction of the screen (the healer's light is ~0.3).</summary>
	public float Radius = 0.16f;

	readonly LightPocket _pocket = new();
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
			Scale = new Vector2(0.55f, 0.55f)
		};
		AddChild(_glow);
	}

	public override void _EnterTree() => LastLightStyle.Pockets.Add(_pocket);
	public override void _ExitTree() => LastLightStyle.Pockets.Remove(_pocket);

	public override void _Process(double delta)
	{
		_t += (float)delta;
		var flick = 0.88f + 0.07f * Mathf.Sin(_t * 7.3f) + 0.05f * Mathf.Sin(_t * 13.1f + 1f);

		_glow.Modulate = new Color(1f, 1f, 1f, flick);

		var size = GetViewport().GetVisibleRect().Size;
		_pocket.Uv = GetGlobalTransformWithCanvas().Origin / size;
		_pocket.Radius = Radius * flick;
	}
}
