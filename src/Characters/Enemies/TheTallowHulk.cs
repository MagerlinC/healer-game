using System.Collections.Generic;
using Godot;
using healerfantasy.LastLight;
using healerfantasy;
using healerfantasy.CombatLog;
using healerfantasy.SpellResources;
using healerfantasy.SpellSystem;

/// <summary>
/// The Tallow Hulk — a giant of melted-down candles that rises out of the darkness
/// beside the great candle when the Tallow Queen reaches 50% health. It stands on
/// the ground left of the platform and leans over it; it never moves, so it stays
/// clear of the fight in the middle of the arena.
///
/// Behaviour
/// ─────────
/// • Every <see cref="MeleeAttackInterval"/> seconds it reaches out and strikes
///   the Dawnwarden — its arms reach anywhere in the arena.
///
/// • Every <see cref="BeamInterval"/> seconds — Dark Wax Beam:
///   three beams in a row, each aimed where the Lightkeeper stands. A thin line
///   telegraphs each one for <see cref="BeamWindupDuration"/> seconds before it
///   fires across the arena; move off the line to dodge it.
///
/// • Every <see cref="SlamInterval"/> seconds — Tallow Slam:
///   its fists come down on each living party member in turn. A circle marks
///   where each fist will land; anyone inside when it lands is hit.
///
/// The fight ends only when BOTH this boss and the Queen are dead.
/// </summary>
public partial class TheTallowHulk : EnemyCharacter
{
	/// <summary>
	/// Animation clips. These are gameplay: an attack's damage lands when its clip
	/// finishes, so a clip's length is its wind-up.
	/// </summary>
	static readonly Dictionary<string, CharacterVisual.Clip> Clips = new()
	{
		["idle"] = new(4 / 5f, Loop: true),
		["attack"] = new(5 / 10f),
		["cast"] = new(1 / 1f, Loop: true),
		["land"] = new(2 / 6f)
	};

	public TheTallowHulk()
	{
		MaxHealth = 2500f; // purposely lower than the Queen — a dangerous add, not a full mirror
	}

	// ── signals ───────────────────────────────────────────────────────────────

	[Signal]
	public delegate void CastWindupStartedEventHandler(string spellName, Texture2D icon, float duration);

	[Signal]
	public delegate void CastWindupEndedEventHandler();

	// ── tuneable exports ──────────────────────────────────────────────────────

	[Export] public float MeleeAttackInterval = 2.8f;
	[Export] public float MeleeDamage = 30f;

	[Export] public float BeamInterval = 14f;
	[Export] public int BeamCount = 3;
	[Export] public float BeamWindupDuration = 1.2f; // telegraph line shown before each beam
	[Export] public float BeamPause = 0.5f; // between beams
	[Export] public float BeamDamage = 50f;
	[Export] public float BeamHitWidth = 30f; // half-width of the danger strip

	[Export] public float SlamInterval = 20f;
	[Export] public float SlamDamage = 45f;
	[Export] public float SlamWindupDuration = 1.1f; // circle shown before the fist lands
	[Export] public float SlamPause = 0.45f; // between fists
	[Export] public float SlamRadius = 42f; // horizontal radius of the impact circle

	/// <summary>
	/// Beams come from the Hulk's mouth, this far from its waist (its rig's origin,
	/// facing right toward the arena: design point (148, -325) × rig scale 0.5).
	/// </summary>
	static readonly Vector2 BeamOriginOffset = new(74f, -162f);

	// ── internal state ────────────────────────────────────────────────────────

	/// <summary>
	/// Set to true by <see cref="TallowHulkEmergePhase"/> while the entrance
	/// plays. All combat logic is suppressed until cleared.
	/// </summary>
	public bool SuppressCombat { get; set; } = true;

	/// <summary>
	/// Set by the Queen during her Molten Cone phase: the Hulk finishes what it is
	/// doing but starts nothing new, and its timers wait.
	/// </summary>
	public bool HoldAbilities { get; set; }

	float _meleeTimer;
	float _beamTimer;
	float _slamTimer;

	CharacterVisual _visual = null!;

	enum BossState
	{
		Normal,
		Melee,
		Beams,
		Slams
	}

	BossState _state = BossState.Normal;

	Character _meleeTarget;

	// Beam sequence
	int _beamsRemaining;
	TallowHulkBeam _beam;

	// Slam sequence
	List<Character> _slamTargets;
	int _slamTargetIndex;

	AudioStreamPlayer _sfxPlayer;
	TallowHulkShroud _shroud;

	// ── lifecycle ─────────────────────────────────────────────────────────────

	public override void _Ready()
	{
		base._Ready();
		CharacterName = GameConstants.TallowHulkName;

		_meleeTimer = MeleeAttackInterval;
		_beamTimer = 6f; // the first beams come soon after it rises, so the threat is learned early
		_slamTimer = SlamInterval;

		GlobalAutoLoad.RegisterSignalEmitter(this, nameof(CastWindupStarted));
		GlobalAutoLoad.RegisterSignalEmitter(this, nameof(CastWindupEnded));

		// This boss is spawned programmatically (not from a .tscn), so we create
		// the visual and collision shape ourselves rather than fetching them via GetNode.
		_visual = CharacterVisual.Attach(this, Clips);

		var col = new CollisionShape2D();
		col.Shape = new CapsuleShape2D { Radius = 14f, Height = 36f };
		AddChild(col);

		_visual.AnimationFinished += OnAnimationFinished;
		_visual.Play("idle");

		_sfxPlayer = new AudioStreamPlayer();
		_sfxPlayer.Stream = GD.Load<AudioStream>(AssetConstants.LandingImpactSoundPath);
		_sfxPlayer.VolumeDb = 0f;
		AddChild(_sfxPlayer);

		// It reaches out of the dark: a shroud of darkness over its body (and the floor
		// around it), with only its eyes glowing through. Both are children, so they
		// rise and fade in with it.
		_shroud = new TallowHulkShroud { ZIndex = -2 }; // above its rig (-3), below the party and the Queen
		AddChild(_shroud);
		AddChild(new TallowHulkEyes { ZIndex = -1 });
		Died += _ => _shroud.Lift();
	}

	public override void _Process(double delta)
	{
		base._Process(delta);
		if (!IsAlive || SuppressCombat) return;
		if (_state != BossState.Normal) return; // other states self-advance via timers
		if (HoldAbilities) return;

		// ── Ability priority: Slam > Beams > Melee ─────────────────────────────
		var dt = (float)delta;
		_slamTimer -= dt;
		_beamTimer -= dt;
		_meleeTimer -= dt;

		if (_slamTimer <= 0f)
		{
			_slamTimer = SlamInterval;
			BeginSlams();
		}
		else if (_beamTimer <= 0f)
		{
			_beamTimer = BeamInterval;
			BeginBeams();
		}
		else if (_meleeTimer <= 0f)
		{
			_meleeTimer = MeleeAttackInterval;
			PerformMeleeAttack();
		}
	}

	public override void _ExitTree()
	{
		if (IsInstanceValid(_beam)) _beam.QueueFree();
		base._ExitTree();
	}

	void ReturnToNormal()
	{
		_state = BossState.Normal;
		_meleeTimer = Mathf.Max(_meleeTimer, MeleeAttackInterval * 0.5f);
		if (IsAlive) _visual.Play("idle");
	}

	// ── Melee ─────────────────────────────────────────────────────────────────

	void PerformMeleeAttack()
	{
		var target = SelectCurrentMeleeTarget();
		if (target == null) return;
		_state = BossState.Melee;
		_meleeTarget = target;
		NoteAttackTarget(target);
		_visual.Play("attack"); // damage lands in OnAnimationFinished
	}

	// ── Dark Wax Beam ─────────────────────────────────────────────────────────

	void BeginBeams()
	{
		_state = BossState.Beams;
		_beamsRemaining = BeamCount;
		AimNextBeam();
	}

	void AimNextBeam()
	{
		if (!IsAlive || _state != BossState.Beams) return;
		if (_beamsRemaining <= 0)
		{
			ReturnToNormal();
			return;
		}

		var healer = FindHealer();
		if (healer == null)
		{
			ReturnToNormal();
			return;
		}

		_beamsRemaining--;
		NoteAttackTarget(healer);

		// Aim through where the Lightkeeper stands now, out to the far wall.
		var from = GlobalPosition + BeamOriginOffset;
		var dir = healer.GlobalPosition - from;
		dir = dir.LengthSquared() > 1f ? dir.Normalized() : Vector2.Down;
		var to = ArenaWallAlong(healer.GlobalPosition, dir);

		_beam = new TallowHulkBeam { From = from, To = to, HalfWidth = BeamHitWidth, ZIndex = 10 };
		GetParent().AddChild(_beam);
		_visual.Play("cast");

		GetTree().CreateTimer(BeamWindupDuration).Timeout += FireBeam;
	}

	void FireBeam()
	{
		if (!IsAlive || _state != BossState.Beams || !IsInstanceValid(_beam)) return;

		var (from, to) = (_beam.From, _beam.To);
		_beam.Fire();
		_beam = null;
		_visual.Play("attack");

		var healer = FindHealer();
		if (healer != null && IsOnBeam(healer.GlobalPosition, from, to))
			Hit(healer, BeamDamage, "Dark Wax Beam",
				"The Tallow Hulk spits a beam of dark wax across the arena.");

		GetTree().CreateTimer(BeamPause).Timeout += AimNextBeam;
	}

	bool IsOnBeam(Vector2 point, Vector2 from, Vector2 to)
	{
		var line = to - from;
		var len = line.Length();
		if (len < 0.01f) return false;
		var dir = line / len;
		var rel = point - from;
		var along = rel.Dot(dir);
		if (along < 0f || along > len) return false;
		return (rel - dir * along).Length() <= BeamHitWidth;
	}

	/// <summary>
	/// Where the ray from <paramref name="inside"/> (a point inside the arena) along
	/// <paramref name="dir"/> meets the arena's ellipse boundary.
	/// </summary>
	Vector2 ArenaWallAlong(Vector2 inside, Vector2 dir)
	{
		var result = inside + dir * 400f; // fallback with no arena boundary
		if (PartyMember.ArenaBoundary is { } b)
		{
			var o = inside - b.Center;
			var rx2 = b.RadiusX * b.RadiusX;
			var ry2 = b.RadiusY * b.RadiusY;
			var a = dir.X * dir.X / rx2 + dir.Y * dir.Y / ry2;
			var bb = 2f * (o.X * dir.X / rx2 + o.Y * dir.Y / ry2);
			var c = o.X * o.X / rx2 + o.Y * o.Y / ry2 - 1f;
			var disc = bb * bb - 4f * a * c;
			if (disc >= 0f)
			{
				var t = (-bb + Mathf.Sqrt(disc)) / (2f * a); // the forward root
				if (t > 0f) result = inside + dir * t;
			}
		}

		return result;
	}

	// ── Tallow Slam ───────────────────────────────────────────────────────────

	void BeginSlams()
	{
		_state = BossState.Slams;
		_slamTargets = CollectAlivePartyMembers();
		_slamTargetIndex = 0;
		SlamNextTarget();
	}

	void SlamNextTarget()
	{
		if (!IsAlive || _state != BossState.Slams) return;

		// Skip anyone who died during the sequence.
		while (_slamTargetIndex < _slamTargets.Count && !_slamTargets[_slamTargetIndex].IsAlive)
			_slamTargetIndex++;

		if (_slamTargetIndex >= _slamTargets.Count)
		{
			ReturnToNormal();
			return;
		}

		var target = _slamTargets[_slamTargetIndex++];
		NoteAttackTarget(target);

		// The circle is placed where the target stands now; it does not follow them.
		var marker = new TallowSlamMarker
		{
			RadiusX = SlamRadius,
			Duration = SlamWindupDuration,
			ZIndex = 1, // on the floor, under the characters (like the molten wax pools)
			GlobalPosition = target.GlobalPosition
		};
		GetParent().AddChild(marker);
		_visual.Play("cast");

		GetTree().CreateTimer(SlamWindupDuration).Timeout += () => LandSlam(marker);
	}

	void LandSlam(TallowSlamMarker marker)
	{
		if (!IsInstanceValid(marker)) return;
		var centre = marker.GlobalPosition;
		marker.Impact();
		if (!IsAlive || _state != BossState.Slams) return;

		_visual.Play("land");
		_sfxPlayer.Play();

		foreach (var node in GetTree().GetNodesInGroup(GameConstants.PartyGroupName))
			if (node is Character c && c.IsAlive && TallowSlamMarker.Contains(centre, SlamRadius, c.GlobalPosition))
				Hit(c, SlamDamage, "Tallow Slam",
					"The Tallow Hulk brings a fist of hardened wax down on the arena.");

		GetTree().CreateTimer(SlamPause).Timeout += SlamNextTarget;
	}

	// ── shared ────────────────────────────────────────────────────────────────

	void Hit(Character target, float damage, string ability, string description)
	{
		target.TakeDamage(damage);
		target.RaiseFloatingCombatText(damage, false, (int)SpellSchool.Generic, false);
		CombatLog.Record(new CombatEventRecord
		{
			Timestamp = Time.GetTicksMsec() / 1000.0,
			SourceName = CharacterName,
			TargetName = target.CharacterName,
			AbilityName = ability,
			Amount = damage,
			Description = description,
			Type = CombatEventType.Damage,
			IsCrit = false
		});
		GD.Print($"[TallowHulk] {ability} hit {target.CharacterName} for {damage}.");
	}

	void OnAnimationFinished()
	{
		if (!IsAlive) return;

		switch (_visual.Animation)
		{
			case "attack" when _state == BossState.Melee:
				if (_meleeTarget != null && _meleeTarget.IsAlive)
					Hit(_meleeTarget, MeleeDamage, "Melee Attack", "The Tallow Hulk reaches out and strikes.");
				_meleeTarget = null;
				ReturnToNormal();
				break;

			case "attack":
			case "land":
				// Beam / slam follow-through: hold the casting pose until the next one.
				_visual.Play(_state == BossState.Normal ? "idle" : "cast");
				break;
		}
	}

	// ── public animation API (called by TallowHulkEmergePhase) ─────────────

	/// <summary>Holds the raised-arms pose while the Hulk rises out of the wax.</summary>
	public void PlayRiseAnim() => _visual?.Play("cast");

	/// <summary>Brings its fists down onto the rim once it has risen.</summary>
	public void PlayArrivalSlam()
	{
		_visual?.Play("land");
		_sfxPlayer.Play();
	}
}

/// <summary>
/// The Dark Wax Beam: a thin pulsing line while it is being aimed, then a thick
/// beam of dark wax that fades out once it fires.
/// </summary>
public partial class TallowHulkBeam : Node2D
{
	public Vector2 From { get; set; }
	public Vector2 To { get; set; }
	public float HalfWidth { get; set; } = 30f;

	static readonly Color TelegraphColor = new(1f, 0.62f, 0.25f, 0.22f);
	static readonly Color TelegraphEdge = new(1f, 0.78f, 0.42f, 0.8f);
	static readonly Color BeamGlow = new(1f, 0.6f, 0.2f, 0.55f);
	static readonly Color BeamCore = new(0.1f, 0.06f, 0.03f, 0.95f);

	const float FadeDuration = 0.4f;

	bool _fired;
	float _t;

	/// <summary>Switches from the telegraph to the beam itself, which then fades and frees itself.</summary>
	public void Fire()
	{
		_fired = true;
		Modulate = Colors.White;
		var tween = CreateTween();
		tween.TweenProperty(this, "modulate:a", 0f, FadeDuration).SetEase(Tween.EaseType.In);
		tween.TweenCallback(Callable.From(QueueFree));
		QueueRedraw();
	}

	public override void _Process(double delta)
	{
		if (_fired) return;
		_t += (float)delta;
		Modulate = new Color(1f, 1f, 1f, 0.6f + 0.4f * Mathf.Sin(_t * 9f));
		QueueRedraw();
	}

	public override void _Draw()
	{
		// Drawn in world space: the node sits at the origin.
		var from = From - GlobalPosition;
		var to = To - GlobalPosition;
		if (!_fired)
		{
			DrawLine(from, to, TelegraphColor, HalfWidth * 2f);
			DrawLine(from, to, TelegraphEdge, 2f);
			return;
		}

		DrawLine(from, to, BeamGlow, HalfWidth * 2f);
		DrawLine(from, to, BeamCore, HalfWidth * 1.1f);
		DrawCircle(to, HalfWidth * 0.9f, BeamGlow);
	}
}

/// <summary>
/// Marks where a Tallow Slam fist will land: a flattened ring on the floor that
/// fills in as the fist comes down, then flashes and fades on impact.
/// </summary>
public partial class TallowSlamMarker : Node2D
{
	/// <summary>Floor circles are drawn squashed to match the arena's perspective.</summary>
	public const float Squash = 0.55f;

	public float RadiusX { get; set; } = 42f;
	public float Duration { get; set; } = 1.1f;

	static readonly Color Ring = new(1f, 0.72f, 0.32f, 0.85f);
	static readonly Color Fill = new(1f, 0.62f, 0.22f, 0.3f);
	static readonly Color ImpactColor = new(0.14f, 0.08f, 0.04f, 0.85f);

	float _t;
	bool _impacted;

	/// <summary>True if <paramref name="point"/> is inside a slam circle at <paramref name="centre"/>.</summary>
	public static bool Contains(Vector2 centre, float radiusX, Vector2 point)
	{
		var d = point - centre;
		var ry = radiusX * Squash;
		return d.X * d.X / (radiusX * radiusX) + d.Y * d.Y / (ry * ry) <= 1f;
	}

	public void Impact()
	{
		_impacted = true;
		QueueRedraw();
		var tween = CreateTween();
		tween.TweenProperty(this, "modulate:a", 0f, 0.6f).SetEase(Tween.EaseType.In);
		tween.TweenCallback(Callable.From(QueueFree));
	}

	public override void _Process(double delta)
	{
		if (_impacted) return;
		_t += (float)delta;
		QueueRedraw();
	}

	public override void _Draw()
	{
		DrawSetTransform(Vector2.Zero, 0f, new Vector2(1f, Squash));
		if (_impacted)
		{
			// A splash of hardened wax where the fist hit.
			DrawCircle(Vector2.Zero, RadiusX, ImpactColor);
			DrawArc(Vector2.Zero, RadiusX, 0f, Mathf.Tau, 48, Ring, 3f);
			return;
		}

		var fill = Mathf.Clamp(_t / Duration, 0f, 1f);
		DrawCircle(Vector2.Zero, RadiusX * fill, Fill);
		DrawArc(Vector2.Zero, RadiusX, 0f, Mathf.Tau, 48, Ring, 2.5f);
	}
}

/// <summary>
/// The darkness the Tallow Hulk stands in: a soft pool of black centred on its body,
/// thick enough to swallow its torso and fading out before its hands, so it looks
/// like it reaches out of the dark. A wider, fainter falloff dims the scene around it.
/// Lifts when the Hulk dies.
/// </summary>
public partial class TallowHulkShroud : Node2D
{
	/// <summary>Centre of the darkness, from the Hulk's waist: behind its chest.</summary>
	static readonly Vector2 Centre = new(-20f, -140f);

	const float SolidRadius = 110f;  // fully dark inside this
	const float FadeRadius = 280f;   // fades to the faint falloff by here
	const float FalloffRadius = 620f; // and to nothing by here
	const float SolidAlpha = 0.9f;
	const float FalloffAlpha = 0.35f;
	const float Squash = 0.8f; // a little wider than tall, like the arena

	static readonly Color Dark = new(0.012f, 0.009f, 0.006f);

	public void Lift()
	{
		var tween = CreateTween();
		tween.TweenProperty(this, "modulate:a", 0f, 2.5f).SetEase(Tween.EaseType.InOut);
	}

	public override void _Draw()
	{
		const int n = 64;
		var solid = new Color(Dark, SolidAlpha);
		DrawSetTransform(Centre, 0f, new Vector2(1f, Squash));
		DrawCircle(Vector2.Zero, SolidRadius, solid);
		Ring(SolidRadius, FadeRadius, solid, new Color(Dark, FalloffAlpha), n);
		Ring(FadeRadius, FalloffRadius, new Color(Dark, FalloffAlpha), new Color(Dark, 0f), n);
	}

	/// <summary>A ring whose colour blends from <paramref name="inner"/> to <paramref name="outer"/>.</summary>
	void Ring(float r0, float r1, Color inner, Color outer, int n)
	{
		for (var i = 0; i < n; i++)
		{
			var a0 = Mathf.Tau * i / n;
			var a1 = Mathf.Tau * (i + 1) / n;
			var d0 = new Vector2(Mathf.Cos(a0), Mathf.Sin(a0));
			var d1 = new Vector2(Mathf.Cos(a1), Mathf.Sin(a1));
			DrawPolygon(
				new[] { d0 * r0, d0 * r1, d1 * r1, d1 * r0 },
				new[] { inner, outer, outer, inner });
		}
	}
}

/// <summary>
/// The Tallow Hulk's eyes, drawn above its shroud so they glow out of the darkness:
/// dim, drooping slits matching its rig (design points × rig scale 0.5).
/// </summary>
public partial class TallowHulkEyes : Node2D
{
	static readonly Color Eye = new(1f, 0.66f, 0.3f, 0.85f);
	static readonly Color Glow = new(1f, 0.55f, 0.2f, 0.035f);

	static readonly Vector2[] Left = { new(55f, -180f), new(68f, -176f), new(67f, -171f), new(56f, -174f) };
	static readonly Vector2[] Right = { new(79f, -176f), new(93f, -181f), new(92f, -175f), new(80f, -171f) };

	public override void _Draw()
	{
		// a faint, soft glow: a few stacked translucent discs, densest in the middle
		foreach (var centre in new[] { new Vector2(61f, -175f), new Vector2(86f, -176f) })
			for (var r = 3f; r <= 9f; r += 2f)
				DrawCircle(centre, r, Glow);
		DrawColoredPolygon(Left, Eye);
		DrawColoredPolygon(Right, Eye);
	}
}
