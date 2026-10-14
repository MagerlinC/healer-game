using Godot;

namespace healerfantasy.LastLight;

/// <summary>
/// What an Ember heal leaves behind: a small smouldering pocket of light on the
/// ground under the ally, with embers drifting up. Holds back the dark while it
/// burns (registered in <see cref="LastLightStyle.Pockets"/>), then fades out.
/// </summary>
public partial class EmberPocket : Node2D
{
	const float Lifetime = 6f;

	readonly LightPocket _pocket = new() { Radius = 0.09f };
	Sprite2D _glow = null!;
	CpuParticles2D _embers = null!;
	float _age;
	float _seed;

	/// <summary>Spawns a pocket at the feet of <paramref name="target"/>, in its parent's space.</summary>
	public static void SpawnUnder(Character target)
	{
		if (target.GetParent() is not Node2D parent) return;
		var pocket = new EmberPocket
		{
			// Character origins sit mid-body; the feet are a little below.
			Position = parent.ToLocal(target.GlobalPosition + new Vector2(0f, 20f)),
			ZIndex = -1
		};
		parent.AddChild(pocket);
	}

	public override void _Ready()
	{
		_seed = GD.Randf() * 10f;

		var g = new Gradient();
		g.SetColor(0, new Color(1f, 0.6f, 0.25f, 0.55f));
		g.SetColor(1, new Color(1f, 0.4f, 0.12f, 0f));
		_glow = new Sprite2D
		{
			Texture = new GradientTexture2D
			{
				Gradient = g, Width = 128, Height = 128,
				Fill = GradientTexture2D.FillEnum.Radial,
				FillFrom = new Vector2(0.5f, 0.5f), FillTo = new Vector2(0.5f, 0f)
			},
			Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add },
			Scale = new Vector2(0.45f, 0.18f)
		};
		AddChild(_glow);

		_embers = new CpuParticles2D
		{
			Amount = 10,
			Lifetime = 1.6f,
			EmissionShape = CpuParticles2D.EmissionShapeEnum.Rectangle,
			EmissionRectExtents = new Vector2(12f, 2f),
			Direction = new Vector2(0f, -1f),
			Spread = 25f,
			Gravity = new Vector2(0f, -8f),
			InitialVelocityMin = 6f,
			InitialVelocityMax = 14f,
			Color = new Color(1f, 0.55f, 0.2f),
			Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add }
		};
		var fade = new Gradient();
		fade.SetColor(0, new Color(1f, 0.75f, 0.35f, 1f));
		fade.SetColor(1, new Color(1f, 0.35f, 0.1f, 0f));
		_embers.ColorRamp = fade;
		AddChild(_embers);
	}

	public override void _EnterTree() => LastLightStyle.Pockets.Add(_pocket);
	public override void _ExitTree() => LastLightStyle.Pockets.Remove(_pocket);

	public override void _Process(double delta)
	{
		_age += (float)delta;
		var life = Mathf.Clamp(1f - _age / Lifetime, 0f, 1f);
		var flick = 0.85f + 0.15f * Mathf.Sin(_age * 9f + _seed);

		_glow.Modulate = new Color(1f, 1f, 1f, Mathf.Min(_age * 4f, 1f) * life * flick);
		_embers.Emitting = life > 0.15f;

		var size = GetViewport().GetVisibleRect().Size;
		_pocket.Uv = GetGlobalTransformWithCanvas().Origin / size;
		_pocket.Radius = 0.09f * life;

		if (_age >= Lifetime + 1.6f) QueueFree();
	}
}
