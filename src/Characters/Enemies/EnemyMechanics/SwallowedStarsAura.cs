using Godot;
using healerfantasy.LastLight;

/// <summary>
/// That Which Swallowed the Stars' phase-two aura: a breathing void haze with a faint
/// cold rim around the boss, dark star-dust swirling about it, and stolen starlight
/// spiralling inward until it is swallowed.
///
/// Fades in over <see cref="FadeInDuration"/> (the phase-two reveal) and away again
/// once the boss dies. Keeps animating while the tree is paused, like the rig, so it
/// blooms during the reveal.
/// </summary>
public partial class SwallowedStarsAura : Node2D
{
	static readonly Color Star = new(0.86f, 0.9f, 1f);
	static readonly Color StarDim = new(0.5f, 0.55f, 0.8f);
	static readonly Color VoidDust = new(0.025f, 0.018f, 0.05f);

	public float FadeInDuration = 4f;

	Character _owner = null!;
	Sprite2D _haze = null!;
	Sprite2D _rim = null!;
	CpuParticles2D _dust = null!;
	CpuParticles2D _stars = null!;
	Vector2 _hazeScale, _rimScale;
	float _t, _strength;
	bool _risen; // the boss sits at 0 health through the reveal; only a death after it ends the aura

	/// <summary>Attaches the aura to <paramref name="boss"/>, centred on its drawn body.</summary>
	public static SwallowedStarsAura Attach(Character boss)
	{
		var aura = new SwallowedStarsAura { _owner = boss, Name = "SwallowedStarsAura" };
		boss.AddChild(aura);
		return aura;
	}

	public override void _Ready()
	{
		ProcessMode = ProcessModeEnum.Always;
		Modulate = new Color(1f, 1f, 1f, 0f);

		// Fit to the rig's body; fall back to its design size if the rig isn't there.
		var body = _owner.GetNodeOrNull<SilhouetteRig>("SilhouetteRig")?.GetBodyRectLocal()
		           ?? new Rect2(-68f, -196f, 136f, 196f);
		Position = body.GetCenter();
		var radius = Mathf.Max(body.Size.X, body.Size.Y) * 0.5f;

		// The void: a black haze the silhouette bleeds into, drawn behind it.
		_haze = new Sprite2D { Texture = RadialTexture(new Color(VoidDust, 0.9f), new Color(VoidDust, 0f), 0.35f), ZIndex = -2 };
		_hazeScale = new Vector2(body.Size.X * 2.2f, body.Size.Y * 1.45f) / 256f;
		AddChild(_haze);

		// An event horizon: a faint ring of cold light where the void begins.
		var ring = new Gradient();
		ring.SetColor(0, new Color(StarDim, 0f));
		ring.SetColor(1, new Color(StarDim, 0f));
		ring.AddPoint(0.62f, new Color(StarDim, 0f));
		ring.AddPoint(0.76f, new Color(StarDim, 0.22f));
		ring.AddPoint(0.84f, new Color(StarDim, 0f));
		_rim = new Sprite2D
		{
			Texture = RadialTexture(ring),
			Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add },
			ZIndex = -2
		};
		_rimScale = _hazeScale;
		AddChild(_rim);

		// Dark star-dust drifting around the body, behind it so its face of stars stays clear.
		var dustFade = new Gradient();
		dustFade.SetColor(0, new Color(VoidDust, 0f));
		dustFade.SetColor(1, new Color(VoidDust, 0f));
		dustFade.AddPoint(0.25f, new Color(VoidDust, 0.85f));
		dustFade.AddPoint(0.7f, new Color(VoidDust, 0.6f));
		_dust = new CpuParticles2D
		{
			Amount = 90,
			Lifetime = 3.6f,
			Preprocess = 3.6f,
			LocalCoords = true,
			EmissionShape = CpuParticles2D.EmissionShapeEnum.Sphere,
			EmissionSphereRadius = radius * 1.3f,
			Gravity = new Vector2(0f, -6f),
			OrbitVelocityMin = 0.03f,
			OrbitVelocityMax = 0.07f,
			RadialAccelMin = -4f,
			RadialAccelMax = 6f,
			ScaleAmountMin = 0.4f,
			ScaleAmountMax = 1.1f,
			Texture = RadialTexture(Colors.White, new Color(1f, 1f, 1f, 0f), 0.2f, 64),
			ColorRamp = dustFade,
			ZIndex = -2
		};
		AddChild(_dust);

		// Stolen starlight, pulled in on a spiral and snuffed out as it reaches the body.
		var starFade = new Gradient();
		starFade.SetColor(0, new Color(StarDim, 0f));
		starFade.SetColor(1, new Color(StarDim, 0f));
		starFade.AddPoint(0.2f, new Color(Star, 1f));
		starFade.AddPoint(0.65f, new Color(StarDim, 0.8f));
		_stars = new CpuParticles2D
		{
			Amount = 46,
			Lifetime = 2.4f,
			Preprocess = 2.4f,
			LocalCoords = true,
			EmissionShape = CpuParticles2D.EmissionShapeEnum.SphereSurface,
			EmissionSphereRadius = radius * 1.45f,
			Gravity = Vector2.Zero,
			OrbitVelocityMin = 0.12f,
			OrbitVelocityMax = 0.22f,
			RadialAccelMin = -48f,
			RadialAccelMax = -30f,
			ScaleAmountMin = 0.08f,
			ScaleAmountMax = 0.16f,
			Texture = RadialTexture(Colors.White, new Color(1f, 1f, 1f, 0f), 0.15f, 32),
			ColorRamp = starFade,
			Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add },
			ZIndex = -1
		};
		AddChild(_stars);

		Update(0f);
	}

	public override void _Process(double delta)
	{
		var dt = (float)delta;
		_t += dt;

		_risen |= _owner.IsAlive;
		if (_owner.IsAlive || !_risen)
			_strength = Mathf.MoveToward(_strength, 1f, dt / Mathf.Max(FadeInDuration, 0.01f));
		else
		{
			_dust.Emitting = false;
			_stars.Emitting = false;
			_strength = Mathf.MoveToward(_strength, 0f, dt / 2f);
			if (_strength <= 0f)
			{
				QueueFree();
				return;
			}
		}

		Update(dt);
	}

	void Update(float dt)
	{
		// The void heaves slowly; the rim swells and contracts against it.
		var breathe = 1f + 0.06f * Mathf.Sin(_t * 1.1f) + 0.025f * Mathf.Sin(_t * 2.9f);
		_haze.Scale = _hazeScale * breathe;
		_rim.Scale = _rimScale * (2f - breathe);
		_rim.Modulate = new Color(1f, 1f, 1f, 0.7f + 0.3f * Mathf.Sin(_t * 0.8f));
		Modulate = new Color(1f, 1f, 1f, Mathf.SmoothStep(0f, 1f, _strength));
	}

	static GradientTexture2D RadialTexture(Color inner, Color outer, float solid, int size = 256)
	{
		var g = new Gradient();
		g.SetColor(0, inner);
		g.SetColor(1, outer);
		g.AddPoint(solid, inner);
		return RadialTexture(g, size);
	}

	static GradientTexture2D RadialTexture(Gradient g, int size = 256) => new()
	{
		Gradient = g, Width = size, Height = size,
		Fill = GradientTexture2D.FillEnum.Radial,
		FillFrom = new Vector2(0.5f, 0.5f), FillTo = new Vector2(0.5f, 0f)
	};
}
