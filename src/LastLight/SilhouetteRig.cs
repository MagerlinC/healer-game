using System.Collections.Generic;
using Godot;

namespace healerfantasy.LastLight;

/// <summary>
/// Draws a <see cref="RigDefinition"/> for a character and animates it procedurally.
///
/// The character drives a <see cref="CharacterVisual"/> (its animation clock: Play("attack"),
/// AnimationFinished, facing, offset); each frame the rig reads that state and poses itself:
///   idle   → breathing and small sways
///   move   → bob + lean (detected from the character actually moving)
///   attack / cleave → wind-up, strike and lunge, timed to the clip
///   cast / spell    → arms raised and lifted for as long as the cast lasts
///   dead   → party topples and is fully consumed; darkness sinks and fades
/// </summary>
public partial class SilhouetteRig : CanvasGroup
{
	static Shader _shader;

	RigDefinition _def = null!;
	Node2D _owner = null!;
	Character _character; // null for puppets (menu, overworld)
	CharacterVisual _src;
	ShaderMaterial _mat = null!;

	sealed class JointState
	{
		public RigJoint Def = null!;
		public int Parent = -1;
		public Vector2 Rest;
		public float Rot, RotTarget;
		public Vector2 Scale = Vector2.One, ScaleTarget = Vector2.One;
		public Vector2 Offset, OffsetTarget;
		public Transform2D Xf = Transform2D.Identity;
	}

	readonly List<JointState> _joints = new();
	readonly Dictionary<string, int> _jointIndex = new();
	readonly List<(Polygon2D poly, int joint)> _polys = new();

	Polygon2D _pool;
	Sprite2D _glow;

	float _t, _seed, _facing = 1f, _move, _consumed, _deathT;
	Vector2 _srcBaseScale = Vector2.One;
	bool _isLightSource = true;
	Vector2 _lastPos;
	Vector2 _feetOffset;

	/// <summary>
	/// Adds a rig to <paramref name="owner"/>, driven by its <see cref="CharacterVisual"/>.
	/// A light rig that is not the light source (e.g. a Twin Flame) glows but does not
	/// move the scene's light.
	/// </summary>
	public static SilhouetteRig Attach(Node2D owner, RigDefinition def, CharacterVisual source = null,
		bool isLightSource = true)
	{
		var rig = new SilhouetteRig
		{
			_def = def, _owner = owner, _character = owner as Character, Name = "SilhouetteRig"
		};
		rig._src = source ?? owner.GetNodeOrNull<CharacterVisual>(CharacterVisual.NodeName);
		rig._isLightSource = isLightSource;
		owner.AddChild(rig);
		return rig;
	}

	bool IsAlive => _character?.IsAlive ?? true;

	public override void _Ready()
	{
		_shader ??= GD.Load<Shader>("res://src/LastLight/figure.gdshader");
		// Keep animating while the game is paused (death and pause screens), so the
		// figures settle into their final state instead of freezing mid-motion.
		ProcessMode = ProcessModeEnum.Always;
		_seed = GD.Randf() * 100f;
		_t = GD.Randf() * 10f;
		_lastPos = _owner.GlobalPosition;
		_feetOffset = new Vector2(0f, _def.Height * _def.Scale * 0.45f);
		// Some enemies pulse their sprite's scale; the rig follows relative to this.
		if (_src != null) _srcBaseScale = _src.Scale.Abs();

		FitMargin = 6f;
		ClearMargin = 6f;
		_mat = new ShaderMaterial { Shader = _shader };
		_mat.SetShaderParameter("mode", (int)_def.Mode);
		_mat.SetShaderParameter("seed", _seed);
		Material = _mat;

		BuildJoints();
		BuildPolygons();

		// Darkness is drawn behind the party so the people you are healing stay readable
		// (unless it is wrapped around them, like vines).
		if (_def.Mode == RigMode.Darkness)
			ZIndex = _def.InFront ? 1 : -1;

		if (_def.Pool != Vector2.Zero)
		{
			_pool = new Polygon2D
			{
				Color = _def.Hover ? new Color(_def.PoolColor, _def.PoolColor.A * 0.45f) : _def.PoolColor,
				ZIndex = -2,
				Antialiased = true
			};
			_owner.AddChild(_pool);
		}

		if (_def.Mode == RigMode.Light)
			BuildGlow();

		Scale = new Vector2(_def.Scale, _def.Scale);
		ApplyPose(1f);
	}

	// ── construction ─────────────────────────────────────────────────────────

	void BuildJoints()
	{
		var root = new JointState { Def = new RigJoint("", "", Vector2.Zero) };
		_joints.Add(root);
		_jointIndex[""] = 0;

		foreach (var j in _def.Joints)
		{
			var parent = _jointIndex[j.Parent];
			var state = new JointState
			{
				Def = j,
				Parent = parent,
				Rest = j.Pivot - _joints[parent].Def.Pivot
			};
			_jointIndex[j.Name] = _joints.Count;
			_joints.Add(state);
		}
	}

	void BuildPolygons()
	{
		// Draw order: by Z, then declaration order. Polygons are kept flat (not
		// nested under joints) so Z works across joints inside the CanvasGroup.
		var order = new List<int>();
		for (var i = 0; i < _def.Parts.Count; i++) order.Add(i);
		order.Sort((a, b) =>
		{
			var z = _def.Parts[a].Z.CompareTo(_def.Parts[b].Z);
			return z != 0 ? z : a.CompareTo(b);
		});

		foreach (var i in order)
		{
			var part = _def.Parts[i];
			var poly = new Polygon2D { Polygon = part.Points, Color = part.Color, Antialiased = true };
			AddChild(poly);
			_polys.Add((poly, _jointIndex[part.Joint]));
		}
	}

	void BuildGlow()
	{
		var gradient = new Gradient();
		gradient.SetColor(0, new Color(1f, 0.9f, 0.66f, 0.55f));
		gradient.SetColor(1, new Color(1f, 0.7f, 0.3f, 0f));
		gradient.AddPoint(0.25f, new Color(1f, 0.82f, 0.5f, 0.22f));
		var tex = new GradientTexture2D
		{
			Gradient = gradient, Width = 256, Height = 256,
			Fill = GradientTexture2D.FillEnum.Radial,
			FillFrom = new Vector2(0.5f, 0.5f), FillTo = new Vector2(0.5f, 0f)
		};
		_glow = new Sprite2D
		{
			Texture = tex,
			Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add },
			ZIndex = -1
		};
		_owner.AddChild(_glow);
	}

	// ── per frame ────────────────────────────────────────────────────────────

	public override void _Process(double delta)
	{
		var dt = (float)delta;
		_t += dt;

		if (_src != null)
		{
			if (_deathT <= 0f) Modulate = _src.Modulate;

			// Follow the sprite's visibility (e.g. the Countess vanishing among her clones).
			Visible = _src.Visible;
			if (_pool != null) _pool.Visible = _src.Visible;
			if (_glow != null) _glow.Visible = _src.Visible;
		}

		Position = _feetOffset + (_src != null ? _src.Position + _src.Offset : Vector2.Zero);

		UpdateFacing(dt);
		UpdateMovement(dt);
		UpdateConsumption(dt);
		ApplyPose(1f - Mathf.Exp(-18f * dt));
		UpdateEffects();
		UpdateShader();
	}

	void UpdateFacing(float dt)
	{
		var target = _facing;
		if (_def.Mode == RigMode.Light)
		{
			// The healer's facing flag: true = facing right.
			if (_src != null) target = _src.FlipH ? 1f : -1f;
		}
		else if (IsAlive)
		{
			var look = _def.Mode == RigMode.Party ? FindNearestBoss() : FindPartyCentre();
			if (look is { } p && Mathf.Abs(p.X - _owner.GlobalPosition.X) > 4f)
				target = Mathf.Sign(p.X - _owner.GlobalPosition.X);
		}

		// Snap to the new direction: easing the flip squashes the flat figure through
		// zero width, which reads as spinning a 3D object rather than turning around.
		_facing = target;
		var pulse = _src != null && _srcBaseScale.X > 0f ? Mathf.Abs(_src.Scale.X) / _srcBaseScale.X : 1f;
		Scale = new Vector2(_def.Scale * _facing, _def.Scale) * pulse;
	}

	void UpdateMovement(float dt)
	{
		var speed = (_owner.GlobalPosition - _lastPos).Length() / Mathf.Max(dt, 0.0001f);
		_lastPos = _owner.GlobalPosition;
		var target = speed > 6f || PoseFor(_src?.Animation ?? "") == "move" ? 1f : 0f;
		_move = Mathf.MoveToward(_move, target, dt * 5f);
	}

	void UpdateConsumption(float dt)
	{
		if (_def.Mode != RigMode.Party) return;
		var target = _character == null ? 0f
			: IsAlive ? 1f - Mathf.Clamp(_character.CurrentHealth / Mathf.Max(_character.MaxHealth, 1f), 0f, 1f)
			: 1f;
		_consumed = Mathf.MoveToward(_consumed, target, dt * 0.9f);
	}

	/// <summary>Sets joint targets from the current state, then eases toward them.</summary>
	void ApplyPose(float ease)
	{
		foreach (var j in _joints)
		{
			j.RotTarget = 0f;
			j.ScaleTarget = Vector2.One;
			j.OffsetTarget = Vector2.Zero;
		}

		var root = _joints[0];
		var body = Get("body");
		var head = Get("head");
		var arm = Get("arm");
		var off = Get("off");

		// ── idle ──
		var breathe = Mathf.Sin(_t * 1.7f + _seed);
		if (body != null)
		{
			body.ScaleTarget = new Vector2(1f - 0.01f * breathe * _def.Breath, 1f + 0.022f * breathe * _def.Breath);
			body.RotTarget = 0.02f * Mathf.Sin(_t * 1.1f + _seed);
		}
		if (head != null) head.RotTarget = 0.04f * Mathf.Sin(_t * 1.3f + 1f);
		if (arm != null) arm.RotTarget = 0.05f * Mathf.Sin(_t * 1.5f + 2f);
		if (off != null) off.RotTarget = 0.06f * Mathf.Sin(_t * 1.4f + 3f);
		// any extra joint (scarf, hat tip, shards, burden, lantern) sways on its own;
		// wings beat
		for (var i = 1; i < _joints.Count; i++)
		{
			var n = _joints[i].Def.Name;
			if (n is "body" or "head" or "arm" or "off") continue;
			_joints[i].RotTarget = n.StartsWith("wing")
				? 0.32f * Mathf.Sin(_t * 6.5f + i * 0.6f)
				: 0.07f * Mathf.Sin(_t * (1.6f + i * 0.37f) + i * 1.3f + _seed);
		}

		// ── hover ──
		if (_def.Hover && IsAlive)
			root.OffsetTarget.Y += -_def.HoverHeight + Mathf.Sin(_t * 2.2f + _seed) * 6f;

		// ── move ──
		root.OffsetTarget.Y = -Mathf.Abs(Mathf.Sin(_t * 9f)) * 5f * _move;
		root.RotTarget = 0.08f * _move;

		// ── attack / cast, timed by the hidden sprite ──
		var anim = _src?.Animation ?? "idle";
		var pose = PoseFor(anim);
		var p = _src?.Progress ?? 0f;

		if (pose == "attack")
		{
			var wind = Smooth(0f, 0.35f, p);
			var strike = Smooth(0.35f, 0.55f, p);
			var recover = Smooth(0.6f, 1f, p);
			var swing = -0.7f * wind * (1f - strike) + 1f * strike * (1f - recover);
			if (arm != null) arm.RotTarget += swing * _def.SwingAngle;
			if (body != null) body.RotTarget += -0.1f * wind * (1f - strike) + 0.16f * strike * (1f - recover);
			root.OffsetTarget.X += _def.Lunge * strike * (1f - recover);
		}
		else if (pose == "cast")
		{
			var raise = Smooth(0f, 0.3f, p);
			if (arm != null) arm.RotTarget += -_def.CastRaise * raise;
			if (off != null) off.RotTarget += -0.45f * raise;
			if (head != null) head.RotTarget += -0.12f * raise;
			root.OffsetTarget.Y += -7f * raise;
			if (body != null) body.ScaleTarget *= new Vector2(1f, 1f + 0.05f * raise);
		}

		// ── the healer's flame flickers, harder while casting ──
		if (_def.Mode == RigMode.Light && Get(_def.LightJoint) is { } flame)
		{
			var casting = anim == "cast" ? 1f : 0f;
			var flick = Mathf.Sin(_t * 23f) * 0.5f + Mathf.Sin(_t * 37f + 1f) * 0.5f;
			flame.ScaleTarget = new Vector2(1f + 0.05f * flick, 1f + 0.1f * flick + 0.35f * casting);
			flame.RotTarget = 0.06f * Mathf.Sin(_t * 7f);
		}

		// ── dead ──
		if (!IsAlive)
		{
			_deathT += (float)GetProcessDeltaTime();
			if (_def.Mode == RigMode.Darkness)
			{
				root.OffsetTarget = new Vector2(0f, Mathf.Min(_deathT * 30f, 120f));
				Modulate = new Color(Modulate, Mathf.Clamp(1f - _deathT / 2.5f, 0f, 1f));
			}
			else
			{
				root.RotTarget = -1.45f;
			}
		}
		else
		{
			_deathT = 0f;
		}

		// ease + compose transforms (root → children; parents always come first)
		for (var i = 0; i < _joints.Count; i++)
		{
			var j = _joints[i];
			j.Rot = Mathf.Lerp(j.Rot, j.RotTarget, ease);
			j.Scale = j.Scale.Lerp(j.ScaleTarget, ease);
			j.Offset = j.Offset.Lerp(j.OffsetTarget, ease);
			var local = new Transform2D(j.Rot, j.Scale, 0f, j.Rest + j.Offset);
			j.Xf = j.Parent >= 0 ? _joints[j.Parent].Xf * local : local;
		}

		foreach (var (poly, ji) in _polys)
		{
			var j = _joints[ji];
			poly.Transform = j.Xf * new Transform2D(0f, -j.Def.Pivot);
		}
	}

	void UpdateEffects()
	{
		if (_pool != null)
		{
			const int n = 40;
			var pts = new Vector2[n];
			for (var i = 0; i < n; i++)
			{
				var a = Mathf.Tau * i / n;
				var r = 1f
				        + 0.12f * Mathf.Sin(3f * a + _t * 0.6f + _seed)
				        + 0.07f * Mathf.Sin(7f * a - _t * 1.1f);
				if (i % 3 == 0) r += 0.22f * (0.5f + 0.5f * Mathf.Sin(_t * 0.8f + i));
				pts[i] = new Vector2(Mathf.Cos(a) * _def.Pool.X * r, Mathf.Sin(a) * _def.Pool.Y * r) * _def.Scale;
			}
			_pool.Polygon = pts;
			_pool.Position = _feetOffset;
			_pool.Modulate = new Color(1, 1, 1, IsAlive ? 1f : Mathf.Clamp(1f - _deathT / 2.5f, 0f, 1f));
		}

		if (_def.Mode == RigMode.Light)
		{
			var light = LightPointLocal();
			var flick = 1f + 0.05f * Mathf.Sin(_t * 19f) + 0.04f * Mathf.Sin(_t * 31f);
			var casting = _src?.Animation == "cast" ? 1.25f : 1f;
			if (_glow != null)
			{
				_glow.Position = light;
				_glow.Scale = Vector2.One * 0.42f * flick * casting * (IsAlive ? 1f : 0.25f)
				              * Mathf.Sqrt(LightMechanics.LightMultiplier(_character));
			}

			if (!_isLightSource) return;
			var canvas = _owner.GetGlobalTransformWithCanvas();
			var size = GetViewport().GetVisibleRect().Size;
			LastLightStyle.LightUv = canvas * light / size;
			LastLightStyle.LightFeetUv = canvas * (_feetOffset + new Vector2(0f, 2f)) / size;
			// Flame brightens, Wick gutters, Gloom dims (see LightMechanics).
			LastLightStyle.LightStrength = (IsAlive ? flick * casting : 0.25f) * LightMechanics.LightMultiplier(_character);
		}
	}

	void UpdateShader()
	{
		var size = GetViewport().GetVisibleRect().Size;
		var xf = GetGlobalTransformWithCanvas();
		var feet = xf * Vector2.Zero;
		var top = xf * new Vector2(0f, -_def.Height);
		var width = xf.BasisXform(new Vector2(_def.Width, 0f)).Length();

		_mat.SetShaderParameter("body_feet", feet / size);
		_mat.SetShaderParameter("body_size", new Vector2(width / size.X, Mathf.Max(feet.Y - top.Y, 1f) / size.Y));
		_mat.SetShaderParameter("consumed", _consumed);
		_mat.SetShaderParameter("light_uv", LastLightStyle.LightUv);
		_mat.SetShaderParameter("light_radius", 0.85f * Mathf.Clamp(LastLightStyle.LightStrength, 0.2f, 1.3f));

		// Party: always outlined. Darkness: outlined in gold only while highlighted
		// (code that highlights a sprite brightens its modulate above 1).
		var highlighted = _src != null && (_src.Modulate.R > 1.05f || _src.Modulate.G > 1.05f);
		var outline = _def.Mode == RigMode.Party ? 2f : highlighted ? 2.5f : 0f;
		_mat.SetShaderParameter("outline_px", outline);
		_mat.SetShaderParameter("outline_color", _def.Mode == RigMode.Party
			? new Color(1f, 0.84f, 0.58f, 0.8f)
			: new Color(1f, 0.8f, 0.35f, 0.95f));
	}

	// ── helpers ──────────────────────────────────────────────────────────────

	/// <summary>The healer's light point, in the owner's local coordinates.</summary>
	Vector2 LightPointLocal()
	{
		var j = Get(_def.LightJoint) ?? _joints[0];
		return Transform * (j.Xf * _def.LightOffset);
	}

	/// <summary>Maps a sprite animation name to a pose: "attack", "cast", "move" or "idle".</summary>
	string PoseFor(string anim)
	{
		if (_def.AnimAliases.TryGetValue(anim, out var alias)) return alias;
		return anim switch
		{
			"attack" or "cleave" or "land" => "attack",
			"cast" or "spell" or "casting" or "beam" or "shield" or "shielded" or "ice_block"
				or "jump" or "jump_hang" => "cast",
			"walk" or "run" or "charge" => "move",
			_ => "idle"
		};
	}

	JointState Get(string name) =>
		_jointIndex.TryGetValue(name, out var i) && name != "" ? _joints[i] : null;

	static float Smooth(float a, float b, float x)
	{
		var t = Mathf.Clamp((x - a) / (b - a), 0f, 1f);
		return t * t * (3f - 2f * t);
	}

	Vector2? FindNearestBoss()
	{
		Vector2? best = null;
		var bestDist = float.MaxValue;
		foreach (var node in GetTree().GetNodesInGroup(GameConstants.BossGroupName))
		{
			if (node is not Character c || !c.IsAlive) continue;
			var d = c.GlobalPosition.DistanceSquaredTo(_owner.GlobalPosition);
			if (d < bestDist)
			{
				bestDist = d;
				best = c.GlobalPosition;
			}
		}
		return best;
	}

	Vector2? FindPartyCentre()
	{
		var sum = Vector2.Zero;
		var count = 0;
		foreach (var node in GetTree().GetNodesInGroup(GameConstants.PartyGroupName))
		{
			if (node is not Character c || !c.IsAlive) continue;
			sum += c.GlobalPosition;
			count++;
		}
		return count > 0 ? sum / count : null;
	}
}
