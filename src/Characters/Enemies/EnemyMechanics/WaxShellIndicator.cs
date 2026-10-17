using System.Collections.Generic;
using Godot;
using healerfantasy.LastLight;

/// <summary>
/// Encases the Tallow Queen in a glowing shell of wax while her Wax Shell shield
/// holds, so the shield reads clearly on the model itself rather than only on the
/// boss health bar.
///
///   • Pops in around her body when attached.
///   • Shows its damage: molten cracks spread across it, chunks break off and fall
///     at set thresholds, it jolts when hit and trembles once it is close to breaking.
///   • Shatters — bursts into falling pieces — as soon as the shield is gone or she
///     dies, then frees itself. No explicit teardown call is needed.
/// </summary>
public partial class WaxShellIndicator : Node2D
{
	const string TexturePath = "res://assets/enemies/the-tallow-queen/wax-shell.png";
	const float PopInDuration = 0.25f;
	const float ShatterDuration = 0.35f;
	const float BodyPadding = 1.25f; // shell size relative to the body rect
	const float FullAlpha = 0.85f; // shell opacity at full shield
	const float DepletedAlpha = 0.6f; // …and as it is about to break (kept high so the cracks read)

	/// <summary>Damage fractions (0 = intact, 1 = broken) at which a chunk breaks off.</summary>
	static readonly float[] ChunkThresholds = { 0.3f, 0.55f, 0.8f };

	const int ShatterChunks = 7;
	const float HitJolt = 2.5f; // world px of shake when the shell takes a hit
	const float MaxTremble = 1.2f; // world px of constant tremble right before it breaks

	Character _owner;
	float _maxShield;
	Sprite2D _shell;
	Sprite2D _glow;
	WaxShellCracks _cracks;
	float _t;
	bool _shattering;
	float _lastShield;
	int _chunksBroken;
	float _jolt;

	/// <summary>Adds the indicator to <paramref name="owner"/>, which must already hold the shield.</summary>
	public static WaxShellIndicator Attach(Character owner)
	{
		var indicator = new WaxShellIndicator
		{
			_owner = owner,
			_maxShield = Mathf.Max(owner.CurrentShield, 1f),
			_lastShield = owner.CurrentShield,
			ZIndex = 2, // over her silhouette
			Visible = false // until it has been fitted to her body
		};
		owner.AddChild(indicator);
		return indicator;
	}

	public override void _Ready()
	{
		var glowGradient = new Gradient();
		glowGradient.SetColor(0, new Color(1f, 0.7f, 0.35f, 0.55f));
		glowGradient.SetColor(1, new Color(1f, 0.5f, 0.15f, 0f));
		_glow = new Sprite2D
		{
			Texture = new GradientTexture2D
			{
				Gradient = glowGradient, Width = 128, Height = 128,
				Fill = GradientTexture2D.FillEnum.Radial,
				FillFrom = new Vector2(0.5f, 0.5f), FillTo = new Vector2(0.5f, 0f)
			},
			Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add }
		};
		AddChild(_glow);

		_shell = new Sprite2D { Texture = GD.Load<Texture2D>(TexturePath) };
		AddChild(_shell);

		// Cracks live in the shell's texture space, so they scale and shake with it.
		if (_shell.Texture != null)
		{
			_cracks = new WaxShellCracks(_shell.Texture.GetSize());
			_shell.AddChild(_cracks);
		}

		Scale = Vector2.One * 0.6f;
		CreateTween().TweenProperty(this, "scale", Vector2.One, PopInDuration)
			.SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
	}

	public override void _Process(double delta)
	{
		if (_shattering) return;
		var dt = (float)delta;
		_t += dt;

		if (!IsInstanceValid(_owner) || !_owner.IsAlive || _owner.CurrentShield <= 0f)
		{
			Shatter();
			return;
		}

		// Refit every frame: the rig may attach after the Queen's _Ready, and it bobs.
		FitToBody();

		var remaining = Mathf.Clamp(_owner.CurrentShield / _maxShield, 0f, 1f);
		var damage = 1f - remaining;

		// ── hit reaction ─────────────────────────────────────────────────────
		if (_owner.CurrentShield < _lastShield) _jolt = HitJolt;
		_lastShield = _owner.CurrentShield;
		_jolt = Mathf.MoveToward(_jolt, 0f, dt * HitJolt * 6f);

		// Trembles harder the closer it is to breaking; still until half gone.
		var tremble = MaxTremble * Mathf.Clamp((damage - 0.5f) / 0.5f, 0f, 1f);
		var shake = _jolt + tremble;
		_shell.Position = shake > 0f
			? new Vector2((float)GD.RandRange(-shake, shake), (float)GD.RandRange(-shake, shake))
			: Vector2.Zero;

		// ── damage stages ─────────────────────────────────────────────────────
		if (_cracks != null) _cracks.Damage = damage;
		while (_chunksBroken < ChunkThresholds.Length && damage >= ChunkThresholds[_chunksBroken])
		{
			_chunksBroken++;
			BreakOffChunk(burst: false);
		}

		var pulse = 0.5f + 0.5f * Mathf.Sin(_t * 3f);
		_shell.Modulate = new Color(1f, 1f, 1f, Mathf.Lerp(DepletedAlpha, FullAlpha, remaining));
		_glow.Modulate = new Color(1f, 1f, 1f, (0.6f + 0.4f * pulse) * Mathf.Lerp(0.5f, 1f, remaining));
	}

	/// <summary>Centres and sizes the shell and glow on the Queen's body.</summary>
	void FitToBody()
	{
		if (_shell.Texture == null) return;
		var rig = _owner.GetNodeOrNull<SilhouetteRig>("SilhouetteRig");
		if (rig == null) return;
		Visible = true;

		var body = rig.GetBodyRectLocal();
		var size = body.Size * BodyPadding;
		var tex = _shell.Texture.GetSize();
		Position = body.GetCenter();
		_shell.Scale = new Vector2(size.X / tex.X, size.Y / tex.Y);
		_glow.Scale = size * 1.5f / 128f;
	}

	/// <summary>
	/// Cuts a random piece of the shell texture from near its edge and drops it into
	/// the world. A <paramref name="burst"/> piece flies outward (the final shatter);
	/// otherwise it just loosens and falls.
	/// </summary>
	void BreakOffChunk(bool burst)
	{
		if (_shell.Texture == null || !Visible || _owner.GetParent() is not { } world) return;

		var tex = _shell.Texture.GetSize();
		var piece = tex * (float)GD.RandRange(0.18f, 0.28f);
		var angle = (float)GD.RandRange(0f, Mathf.Tau);
		// Region centre on an ellipse ~70% of the way to the edge, in texture px from the centre.
		var local = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * (tex * 0.5f - piece * 0.5f) * 0.85f;
		var region = new Rect2(tex * 0.5f + local - piece * 0.5f, piece);

		var outward = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
		var velocity = burst
			? outward * (float)GD.RandRange(60f, 110f) + new Vector2(0f, -40f)
			: outward * 15f + new Vector2(0f, -10f);

		var chunk = new FallingWaxChunk(velocity, (float)GD.RandRange(-4f, 4f))
		{
			Texture = _shell.Texture,
			RegionEnabled = true,
			RegionRect = region,
			ZIndex = ZIndex,
			Modulate = _shell.Modulate
		};
		world.AddChild(chunk);
		chunk.GlobalPosition = _shell.ToGlobal(local);
		chunk.GlobalScale = _shell.GlobalScale;
	}

	/// <summary>Bursts the shell into falling pieces and fades the rest, then frees the node.</summary>
	void Shatter()
	{
		_shattering = true;
		if (IsInstanceValid(_owner))
			for (var i = 0; i < ShatterChunks; i++)
				BreakOffChunk(burst: true);

		var tween = CreateTween().SetParallel();
		tween.TweenProperty(this, "scale", Vector2.One * 1.35f, ShatterDuration)
			.SetEase(Tween.EaseType.Out);
		tween.TweenProperty(this, "modulate:a", 0f, ShatterDuration);
		tween.Chain().TweenCallback(Callable.From(QueueFree));
	}
}

/// <summary>
/// Molten cracks across the wax shell, drawn in the shell texture's (centred) pixel
/// space. Cracks are generated once; each opens at its own damage threshold and
/// grows to full length over a short damage window, so the shell visibly splits
/// further with every hit.
/// </summary>
public partial class WaxShellCracks : Node2D
{
	const int CrackCount = 10;
	const float GrowWindow = 0.12f; // damage fraction over which a crack grows to full length
	static readonly Color CrackDark = new(0.32f, 0.16f, 0.06f, 0.9f);
	static readonly Color CrackMolten = new(1f, 0.78f, 0.42f, 0.95f);

	readonly List<(Vector2[] Points, float Threshold)> _cracks = new();
	float _damage;

	/// <summary>0 = intact, 1 = about to break. Redraws when it changes.</summary>
	public float Damage
	{
		get => _damage;
		set
		{
			if (Mathf.IsEqualApprox(value, _damage)) return;
			_damage = value;
			QueueRedraw();
		}
	}

	public WaxShellCracks(Vector2 textureSize)
	{
		var rng = new RandomNumberGenerator();
		rng.Randomize();
		var half = textureSize * 0.5f;
		var step = textureSize.Length() * 0.05f;

		for (var i = 0; i < CrackCount; i++)
		{
			// Start somewhere inside the shell, heading roughly outward.
			var a = rng.RandfRange(0f, Mathf.Tau);
			var p = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * half * rng.RandfRange(0.1f, 0.55f);
			var dir = a + rng.RandfRange(-0.5f, 0.5f);

			var pts = new List<Vector2> { p };
			var segments = rng.RandiRange(4, 6);
			for (var s = 0; s < segments; s++)
			{
				dir += rng.RandfRange(-0.6f, 0.6f);
				p += new Vector2(Mathf.Cos(dir), Mathf.Sin(dir)) * step * rng.RandfRange(0.6f, 1.2f);
				// keep inside the shell's ellipse
				var n = new Vector2(p.X / half.X, p.Y / half.Y);
				if (n.Length() > 0.92f) break;
				pts.Add(p);
			}

			if (pts.Count < 2) continue;
			// Spread opening thresholds from first hit to nearly broken.
			_cracks.Add((pts.ToArray(), 0.04f + 0.82f * i / CrackCount));
		}
	}

	public override void _Draw()
	{
		foreach (var (points, threshold) in _cracks)
		{
			var grow = Mathf.Clamp((_damage - threshold) / GrowWindow, 0f, 1f);
			if (grow <= 0f) continue;

			var visible = PartialPolyline(points, grow);
			DrawPolyline(visible, CrackDark, 9f, true);
			DrawPolyline(visible, CrackMolten, 3f, true);
		}
	}

	/// <summary>The first <paramref name="fraction"/> of the polyline's length.</summary>
	static Vector2[] PartialPolyline(Vector2[] points, float fraction)
	{
		if (fraction >= 1f) return points;

		var total = 0f;
		for (var i = 1; i < points.Length; i++) total += points[i].DistanceTo(points[i - 1]);
		var target = total * fraction;

		var result = new List<Vector2> { points[0] };
		for (var i = 1; i < points.Length; i++)
		{
			var seg = points[i].DistanceTo(points[i - 1]);
			if (target <= seg)
			{
				result.Add(points[i - 1].Lerp(points[i], target / Mathf.Max(seg, 0.0001f)));
				break;
			}

			target -= seg;
			result.Add(points[i]);
		}

		return result.ToArray();
	}
}

/// <summary>A piece of broken-off wax: falls under gravity, spins and fades, then frees itself.</summary>
public partial class FallingWaxChunk : Sprite2D
{
	const float Gravity = 260f;
	const float Lifetime = 0.8f;

	Vector2 _velocity;
	readonly float _spin;
	float _age;
	float _startAlpha;

	public FallingWaxChunk(Vector2 velocity, float spin)
	{
		_velocity = velocity;
		_spin = spin;
	}

	public override void _Ready() => _startAlpha = Modulate.A;

	public override void _Process(double delta)
	{
		var dt = (float)delta;
		_age += dt;
		_velocity.Y += Gravity * dt;
		GlobalPosition += _velocity * dt;
		Rotation += _spin * dt;
		Modulate = new Color(Modulate, _startAlpha * Mathf.Clamp(1f - _age / Lifetime, 0f, 1f));
		if (_age >= Lifetime) QueueFree();
	}
}
