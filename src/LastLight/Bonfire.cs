using Godot;

namespace healerfantasy.LastLight;

/// <summary>
/// A bonfire: a coiled sword in a mound of ash, burning. A second light that
/// holds the darkness back while it is in the scene (registered in
/// <see cref="LastLightStyle.Pockets"/>). Placed with its base at the node origin.
/// </summary>
public partial class Bonfire : Node2D
{
	/// <summary>Radius of the pocket it holds open, in screen heights.</summary>
	public float LightRadius = 0.27f;

	readonly LightPocket _pocket = new();
	readonly Polygon2D[] _flames = new Polygon2D[3];
	Sprite2D _glow = null!;
	float _t;

	static readonly Color Ash = new(0.06f, 0.05f, 0.045f);
	static readonly Color Bone = new(0.2f, 0.18f, 0.16f);
	static readonly Color Iron = new(0.13f, 0.12f, 0.11f);

	public override void _Ready()
	{
		_glow = new Sprite2D
		{
			Texture = RadialTexture(new Color(1f, 0.72f, 0.38f, 0.5f), new Color(1f, 0.5f, 0.2f, 0f)),
			Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add },
			Position = new Vector2(0f, -14f),
			Scale = new Vector2(0.7f, 0.55f)
		};
		AddChild(_glow);

		// ash mound, bones, coiled sword
		AddChild(new Polygon2D { Color = Ash, Antialiased = true, Polygon = RigDefinition.P(
			"-22,0 -18,-5 -10,-8 0,-9 10,-8 18,-5 22,0 10,2 -12,2") });
		AddChild(new Polygon2D { Color = Bone, Antialiased = true, Polygon = RigDefinition.Thick(1.6f, "-16,-3 -6,-6") });
		AddChild(new Polygon2D { Color = Bone, Antialiased = true, Polygon = RigDefinition.Thick(1.6f, "8,-6 17,-2") });
		AddChild(new Polygon2D { Color = Bone, Antialiased = true, Polygon = RigDefinition.Circle(-12f, -6f, 2.4f, 8) });

		// flames: three layers, hottest innermost
		var colors = new[] { new Color(0.85f, 0.32f, 0.1f), new Color(1f, 0.62f, 0.25f), new Color(1f, 0.93f, 0.75f) };
		var shapes = new[]
		{
			"-11,-6 -9,-16 -4,-24 -2,-34 2,-26 6,-32 8,-20 11,-12 9,-6",
			"-7,-7 -5,-15 -2,-22 1,-28 4,-20 7,-13 6,-7",
			"-3,-7 -2,-12 0,-18 2,-12 3,-7"
		};
		for (var i = 0; i < 3; i++)
		{
			_flames[i] = new Polygon2D { Color = colors[i], Antialiased = true, Polygon = RigDefinition.P(shapes[i]) };
			AddChild(_flames[i]);
		}

		// the sword, thrust through the fire
		AddChild(new Polygon2D { Color = Iron, Antialiased = true, Polygon = RigDefinition.P("-1.2,-4 1.2,-4 1.6,-30 0,-33 -1.6,-30") });
		AddChild(new Polygon2D { Color = Iron, Antialiased = true, Polygon = RigDefinition.P("-6,-30 6,-30 6,-28 -6,-28") });
		AddChild(new Polygon2D { Color = Iron, Antialiased = true, Polygon = RigDefinition.P("-1,-30 1,-30 1,-38 -1,-38") });

		// embers drifting up
		var embers = new CpuParticles2D
		{
			Amount = 14,
			Lifetime = 2.2f,
			Position = new Vector2(0f, -14f),
			EmissionShape = CpuParticles2D.EmissionShapeEnum.Rectangle,
			EmissionRectExtents = new Vector2(8f, 3f),
			Direction = new Vector2(0f, -1f),
			Spread = 20f,
			Gravity = new Vector2(0f, -12f),
			InitialVelocityMin = 8f,
			InitialVelocityMax = 20f,
			ScaleAmountMin = 0.8f,
			ScaleAmountMax = 1.6f,
			Color = new Color(1f, 0.6f, 0.25f),
			Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add }
		};
		var fade = new Gradient();
		fade.SetColor(0, new Color(1f, 0.8f, 0.4f, 1f));
		fade.SetColor(1, new Color(1f, 0.4f, 0.1f, 0f));
		embers.ColorRamp = fade;
		AddChild(embers);
	}

	public override void _EnterTree() => LastLightStyle.Pockets.Add(_pocket);
	public override void _ExitTree() => LastLightStyle.Pockets.Remove(_pocket);

	public override void _Process(double delta)
	{
		_t += (float)delta;
		for (var i = 0; i < 3; i++)
		{
			var f = Mathf.Sin(_t * (11f + i * 4f) + i) * 0.5f + Mathf.Sin(_t * (17f + i * 3f)) * 0.5f;
			_flames[i].Scale = new Vector2(1f + 0.06f * f, 1f + 0.12f * f);
			_flames[i].Skew = 0.08f * Mathf.Sin(_t * (3f + i));
		}
		var flick = 1f + 0.05f * Mathf.Sin(_t * 13f) + 0.04f * Mathf.Sin(_t * 29f);
		_glow.Scale = new Vector2(0.7f, 0.55f) * flick;

		var size = GetViewport().GetVisibleRect().Size;
		_pocket.Uv = GetGlobalTransformWithCanvas() * new Vector2(0f, -8f) / size;
		_pocket.Radius = LightRadius * Mathf.Lerp(0.96f, 1f, flick - 0.95f);
	}

	static GradientTexture2D RadialTexture(Color inner, Color outer)
	{
		var g = new Gradient();
		g.SetColor(0, inner);
		g.SetColor(1, outer);
		return new GradientTexture2D
		{
			Gradient = g, Width = 256, Height = 256,
			Fill = GradientTexture2D.FillEnum.Radial,
			FillFrom = new Vector2(0.5f, 0.5f), FillTo = new Vector2(0.5f, 0f)
		};
	}
}
