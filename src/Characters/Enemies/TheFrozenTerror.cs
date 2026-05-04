using System.Collections.Generic;
using Godot;
using healerfantasy;
using healerfantasy.SpellResources;
using healerfantasy.SpellSystem;

/// <summary>
/// The Frozen Terror — a massive ice golem that crashes into the arena when the
/// Queen of the Frozen Wastes reaches 50% health.
///
/// Behaviour
/// ─────────
/// • Walks toward the Templar and auto-attacks every <see cref="MeleeAttackInterval"/>
///   seconds with a melee strike (attack frames).
///
/// • Every <see cref="ChargeInterval"/> seconds — Triple Charge:
///   picks a random direction from the boss's current position, draws a faint red
///   preview line toward the arena wall, then charges at high speed in that direction.
///   Damages the Healer (player) if caught in the path. Repeated three times before
///   the boss walks back into melee.
///
/// • Every <see cref="JumpSlamInterval"/> seconds — Glacial Slam:
///   the boss launches into the air and slams down on each living party member in
///   turn (jump1–3 take-off, jump4 hang-time while moving, land1–2 on impact).
///   Deals <see cref="JumpSlamDamage"/> on landing.
///
/// The fight ends only when BOTH this boss and the Queen are dead.
///
/// Animations (res://assets/enemies/the-frozen-terror/):
///   "idle"   — idle1–4     (looping)
///   "attack" — attack1–5   (one-shot → idle)
///   "charge" — charge1–3   (looping while charging)
///   "jump"   — jump1–4     (one-shot for take-off; jump4 held during hang time)
///   "land"   — land1–2     (one-shot → idle)
/// </summary>
public partial class TheFrozenTerror : Character
{
	public TheFrozenTerror()
	{
		MaxHealth = 3000f; // purposely lower than the Queen — a dangerous add, not a full mirror
	}

	// ── signals ───────────────────────────────────────────────────────────────

	[Signal]
	public delegate void CastWindupStartedEventHandler(string spellName, Texture2D icon, float duration);

	[Signal]
	public delegate void CastWindupEndedEventHandler();

	// ── tuneable exports ──────────────────────────────────────────────────────

	[Export] public float MoveSpeed = 70f;
	[Export] public float MeleeRange = 55f;
	[Export] public float MeleeAttackInterval = 2.8f;
	[Export] public float MeleeDamage = 35f;

	[Export] public float ChargeInterval = 14f;
	[Export] public float ChargeWindupDuration = 1.2f; // red line shown before each charge
	[Export] public float ChargeSpeed = 900f;
	[Export] public float ChargeDamage = 50f;
	[Export] public float ChargeHitWidth = 28f; // half-width of the danger strip

	[Export] public float JumpSlamInterval = 20f;
	[Export] public float JumpSlamDamage = 55f;
	[Export] public float JumpTakeoffDuration = 0.5f; // time playing jump1–3
	[Export] public float JumpHangDuration = 0.6f; // time on jump4 while flying to target
	[Export] public float JumpLandDuration = 0.4f; // land1–2 before returning to idle

	// ── internal state ────────────────────────────────────────────────────────

	/// <summary>
	/// Set to true by <see cref="FrozenTerrorJumpInPhase"/> while the entrance
	/// animation plays. All combat logic is suppressed until cleared.
	/// </summary>
	public bool SuppressCombat { get; set; } = true;

	float _meleeTimer;
	float _chargeTimer;
	float _jumpSlamTimer;

	AnimatedSprite2D _sprite;

	// ── ability state machines ────────────────────────────────────────────────

	enum BossState
	{
		Normal,
		Melee,
		Charging,
		JumpSlam
	}

	BossState _state = BossState.Normal;

	// Charge sequence
	int _chargesRemaining;
	Vector2 _chargeDirection;
	Vector2 _chargeTarget;
	FrozenTerrorChargeLine _chargeLine;

	// Jump slam sequence
	List<Character> _jumpTargets;
	int _jumpTargetIndex;

	// Jump SFX
	AudioStreamPlayer _sfxPlayer;

	const string AssetBase = "res://assets/enemies/the-frozen-terror/";

	// ── lifecycle ─────────────────────────────────────────────────────────────

	public override void _Ready()
	{
		base._Ready();
		CharacterName = GameConstants.FrozenTerrorName;
		RemoveFromGroup("party");
		AddToGroup(GameConstants.BossGroupName);
		IsFriendly = false;

		_meleeTimer = MeleeAttackInterval;
		_chargeTimer = ChargeInterval;
		_jumpSlamTimer = JumpSlamInterval;

		GlobalAutoLoad.RegisterSignalEmitter(this, nameof(CastWindupStarted));
		GlobalAutoLoad.RegisterSignalEmitter(this, nameof(CastWindupEnded));

		// This boss is spawned programmatically (not from a .tscn), so we create
		// the sprite and collision shape ourselves rather than fetching them via GetNode.
		_sprite = new AnimatedSprite2D();
		_sprite.Scale = new Vector2(0.30f, 0.30f);
		AddChild(_sprite);

		var col = new CollisionShape2D();
		col.Shape = new CapsuleShape2D { Radius = 14f, Height = 36f };
		AddChild(col);

		SetupAnimations();
		_sprite.AnimationFinished += OnAnimationFinished;
		_sprite.Play("idle");

		_sfxPlayer = new AudioStreamPlayer();
		var sfx = GD.Load<AudioStream>(AssetConstants.LandingImpactSoundPath);
		_sfxPlayer.Stream = sfx;
		_sfxPlayer.VolumeDb = 0f;
		AddChild(_sfxPlayer);
	}

	public override void _Process(double delta)
	{
		base._Process(delta);
		if (!IsAlive || SuppressCombat) return;

		// State-machine routing
		switch (_state)
		{
			case BossState.Normal:
				ProcessNormal((float)delta);
				break;
			// Other states self-advance via timers / callbacks — nothing to tick here.
		}
	}

	// ── Normal state: move + attack ────────────────────────────────────────────

	void ProcessNormal(float delta)
	{
		// ── Ability priority: JumpSlam > Charge > Melee ─────────────────────
		_jumpSlamTimer -= delta;
		_chargeTimer -= delta;
		_meleeTimer -= delta;

		if (_jumpSlamTimer <= 0f)
		{
			_jumpSlamTimer = JumpSlamInterval;
			BeginJumpSlam();
			return;
		}

		if (_chargeTimer <= 0f)
		{
			_chargeTimer = ChargeInterval;
			BeginChargeSequence();
			return;
		}

		// ── Movement: walk toward Templar ──────────────────────────────────
		var target = FindTank() ?? PickRandomPartyMember();
		if (target != null)
		{
			var dist = GlobalPosition.DistanceTo(target.GlobalPosition);
			if (dist > MeleeRange)
			{
				var dir = (target.GlobalPosition - GlobalPosition).Normalized();
				Velocity = dir * MoveSpeed;
				MoveAndSlide();
				return; // not in range yet — skip attack
			}
		}

		Velocity = Vector2.Zero;
		MoveAndSlide();

		// ── Melee attack ───────────────────────────────────────────────────
		if (_meleeTimer <= 0f)
		{
			_meleeTimer = MeleeAttackInterval;
			PerformMeleeAttack();
		}
	}

	// ── Melee ─────────────────────────────────────────────────────────────────

	void PerformMeleeAttack()
	{
		var target = FindTank() ?? PickRandomPartyMember();
		if (target == null) return;
		_state = BossState.Melee;
		_sprite.Play("attack");
		// Damage is applied in OnAnimationFinished when the attack animation completes.
		// Store target in a field:
		_meleeTarget = target;
	}

	Character _meleeTarget;

	// ── Charge sequence ────────────────────────────────────────────────────────

	void BeginChargeSequence()
	{
		_chargesRemaining = 3;
		_state = BossState.Charging;
		StartNextCharge();
	}

	void StartNextCharge()
	{
		if (_chargesRemaining <= 0)
		{
			// Done with all charges — return to normal
			_state = BossState.Normal;
			_meleeTimer = Mathf.Max(_meleeTimer, MeleeAttackInterval * 0.5f);
			_sprite.Play("idle");
			return;
		}

		_chargesRemaining--;

		// Pick a random direction and find arena-wall intersection
		var angle = (float)GD.RandRange(0.0, Mathf.Tau);
		_chargeDirection = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
		_chargeTarget = ComputeWallTarget(GlobalPosition, _chargeDirection);

		// Show the red preview line
		_chargeLine?.QueueFree();
		_chargeLine = new FrozenTerrorChargeLine
		{
			From = GlobalPosition,
			To = _chargeTarget,
			ZIndex = 10
		};
		GetParent().AddChild(_chargeLine);

		_sprite.Play("charge");

		// After the wind-up delay, execute the charge
		GetTree().CreateTimer(ChargeWindupDuration).Timeout += ExecuteCharge;
	}

	void ExecuteCharge()
	{
		_chargeLine?.QueueFree();
		_chargeLine = null;

		if (!IsAlive || _state != BossState.Charging) return;

		// Check if the healer is caught in the charge path before we move
		var startPos = GlobalPosition;
		CheckChargeDamage(startPos, _chargeTarget);

		// Tween to wall position at high speed
		var dist = GlobalPosition.DistanceTo(_chargeTarget);
		var duration = Mathf.Max(dist / ChargeSpeed, 0.05f);

		var tween = CreateTween();
		tween.TweenProperty(this, "global_position", _chargeTarget, duration)
			.SetEase(Tween.EaseType.In).SetTrans(Tween.TransitionType.Quad);
		tween.TweenCallback(Callable.From(OnChargeArrived));
	}

	void CheckChargeDamage(Vector2 from, Vector2 to)
	{
		var healer = FindHealer();
		if (healer == null || !healer.IsAlive) return;

		// Is the healer within ChargeHitWidth pixels of the charge line segment?
		var line = to - from;
		var lineLen = line.Length();
		if (lineLen < 0.01f) return;

		var lineDir = line / lineLen;
		var toHealer = healer.GlobalPosition - from;
		var proj = toHealer.Dot(lineDir);
		if (proj < 0f || proj > lineLen) return; // healer is outside segment extent

		var perp = toHealer - lineDir * proj;
		if (perp.Length() > ChargeHitWidth) return; // healer is outside the danger strip

		// Hit!
		healer.TakeDamage(ChargeDamage);
		healer.RaiseFloatingCombatText(ChargeDamage, false, (int)SpellSchool.Generic, false);
		GD.Print($"[FrozenTerror] Charge hit healer for {ChargeDamage} damage.");
	}

	void OnChargeArrived()
	{
		if (!IsAlive || _state != BossState.Charging) return;

		// Brief pause between charges, then start the next one
		_sprite.Play("idle");
		GetTree().CreateTimer(0.4f).Timeout += StartNextCharge;
	}

	/// <summary>
	/// Finds where the ray from <paramref name="origin"/> in <paramref name="dir"/>
	/// first hits the arena ellipse boundary (or falls back to a generous flat box).
	/// </summary>
	Vector2 ComputeWallTarget(Vector2 origin, Vector2 dir)
	{
		if (PartyMember.ArenaBoundary is { } b)
		{
			// Parametric ray–ellipse intersection: solve for t in
			// ((ox + dx*t - cx) / rx)^2 + ((oy + dy*t - cy) / ry)^2 = 1
			var rx = b.RadiusX;
			var ry = b.RadiusY;
			var cx = b.Center.X;
			var cy = b.Center.Y;
			var ex = origin.X - cx;
			var ey = origin.Y - cy;
			var dx = dir.X;
			var dy = dir.Y;

			var a = dx * dx / (rx * rx) + dy * dy / (ry * ry);
			var bCoef = 2f * (ex * dx / (rx * rx) + ey * dy / (ry * ry));
			var c = ex * ex / (rx * rx) + ey * ey / (ry * ry) - 1f;

			var discriminant = bCoef * bCoef - 4f * a * c;
			if (discriminant >= 0f)
			{
				var sqrtDisc = Mathf.Sqrt(discriminant);
				var t1 = (-bCoef + sqrtDisc) / (2f * a);
				var t2 = (-bCoef - sqrtDisc) / (2f * a);
				// Pick the positive root (forward direction)
				var t = t1 > 0f ? t1 : t2;
				if (t > 0f)
					return origin + dir * t;
			}
		}

		// Fallback: just go 400 pixels in the direction
		return origin + dir * 400f;
	}

	// ── Jump Slam sequence ─────────────────────────────────────────────────────

	void BeginJumpSlam()
	{
		_state = BossState.JumpSlam;
		_jumpTargets = CollectAlivePartyMembers();
		_jumpTargetIndex = 0;
		JumpToNextTarget();
	}

	void JumpToNextTarget()
	{
		if (_jumpTargetIndex >= _jumpTargets.Count || !IsAlive)
		{
			// All targets visited — return to normal
			_state = BossState.Normal;
			_sprite.Play("idle");
			_meleeTimer = Mathf.Max(_meleeTimer, MeleeAttackInterval * 0.5f);
			return;
		}

		var target = _jumpTargets[_jumpTargetIndex];
		_jumpTargetIndex++;

		// Skip dead targets (can die during the slam sequence)
		if (!target.IsAlive)
		{
			JumpToNextTarget();
			return;
		}

		// Step 1: Play take-off frames (jump1–3)
		_sprite.Play("jump");
		GetTree().CreateTimer(JumpTakeoffDuration).Timeout += () => BeginHangTime(target);
	}

	void BeginHangTime(Character target)
	{
		if (!IsAlive) return;

		// Switch to the single-frame looping animation so the boss holds the
		// "jump4" hang-time pose while tweening toward the target.
		_sprite.Play("jump_hang");

		// Tween to a position just above the target
		var landPos = target.GlobalPosition + new Vector2(0f, -10f);
		var tween = CreateTween();
		tween.TweenProperty(this, "global_position", landPos, JumpHangDuration)
			.SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Sine);
		tween.TweenCallback(Callable.From(() => BeginLanding(target)));
	}

	void BeginLanding(Character target)
	{
		if (!IsAlive) return;

		_sprite.Play("land");
		// Damage fires halfway through the land animation (after land1)
		GetTree().CreateTimer(JumpLandDuration * 0.5f).Timeout += () => ApplyJumpSlamDamage(target);
		GetTree().CreateTimer(JumpLandDuration).Timeout += OnLandingComplete;
	}

	void ApplyJumpSlamDamage(Character target)
	{
		if (!target.IsAlive) return;
		target.TakeDamage(JumpSlamDamage);
		target.RaiseFloatingCombatText(JumpSlamDamage, false, (int)SpellSchool.Generic, false);
		GD.Print($"[FrozenTerror] Glacial Slam hit {target.CharacterName} for {JumpSlamDamage} damage.");
	}

	void OnLandingComplete()
	{
		if (!IsAlive) return;
		// Brief pause between slams before jumping to next target
		_sprite.Play("idle");
		_sfxPlayer.Play();
		GetTree().CreateTimer(0.5f).Timeout += JumpToNextTarget;
	}

	// ── Animation callbacks ────────────────────────────────────────────────────

	void OnAnimationFinished()
	{
		if (!IsAlive) return;

		switch (_sprite.Animation)
		{
			case "attack":
				// Melee attack animation done — deal damage then return to normal
				if (_meleeTarget != null && _meleeTarget.IsAlive)
				{
					_meleeTarget.TakeDamage(MeleeDamage);
					_meleeTarget.RaiseFloatingCombatText(MeleeDamage, false, (int)SpellSchool.Generic, false);
					GD.Print($"[FrozenTerror] Melee hit {_meleeTarget.CharacterName} for {MeleeDamage}.");
				}

				_meleeTarget = null;
				_state = BossState.Normal;
				_sprite.Play("idle");
				break;

			case "land":
				// Landing handled via timers above; guard return to idle
				if (_state == BossState.Normal)
					_sprite.Play("idle");
				break;
		}
	}

	// ── targeting helpers ──────────────────────────────────────────────────────

	Character FindTank()
	{
		foreach (var node in GetTree().GetNodesInGroup("party"))
			if (node is Character c && c.CharacterName == GameConstants.TemplarName && c.IsAlive)
				return c;
		return null;
	}

	Character FindHealer()
	{
		foreach (var node in GetTree().GetNodesInGroup("party"))
			if (node is Character c && c.CharacterName == GameConstants.HealerName && c.IsAlive)
				return c;
		return null;
	}

	Character PickRandomPartyMember()
	{
		var alive = new List<Character>();
		foreach (var node in GetTree().GetNodesInGroup("party"))
			if (node is Character c && c.IsAlive)
				alive.Add(c);
		if (alive.Count == 0) return null;
		return alive[(int)(GD.Randi() % (uint)alive.Count)];
	}

	List<Character> CollectAlivePartyMembers()
	{
		var members = new List<Character>();
		foreach (var node in GetTree().GetNodesInGroup("party"))
			if (node is Character c && c.IsAlive)
				members.Add(c);
		// Shuffle order for variety
		for (var i = members.Count - 1; i > 0; i--)
		{
			var j = (int)(GD.Randi() % (uint)(i + 1));
			(members[i], members[j]) = (members[j], members[i]);
		}

		return members;
	}

	// ── public animation API (called by FrozenTerrorJumpInPhase) ─────────────

	/// <summary>Plays the jump take-off animation from frame 1. Used during the entrance sequence.</summary>
	public void PlayJumpAnim()
	{
		_sprite?.Play("jump");
	}

	/// <summary>Plays the landing animation. Used during the entrance sequence.</summary>
	public void PlayLandAnim()
	{
		_sprite?.Play("land");
		_sfxPlayer.Play();
	}

	// ── animation setup ───────────────────────────────────────────────────────

	void SetupAnimations()
	{
		var frames = new SpriteFrames();
		frames.RemoveAnimation("default");

		AddAnimFromFiles(frames, "idle", "idle", 4, 5f, true);
		AddAnimFromFiles(frames, "attack", "attack", 5, 10f, false);
		AddAnimFromFiles(frames, "charge", "charge", 3, 8f, true);

		// Jump: frames 1–4 as a one-shot. We stop on frame 4 ("jump4") for hang time.
		AddAnimFromFiles(frames, "jump", "jump", 4, 8f, false);

		// jump_hang: single frame (jump4), looping — used while airborne over target.
		frames.AddAnimation("jump_hang");
		frames.SetAnimationLoop("jump_hang", true);
		frames.SetAnimationSpeed("jump_hang", 1f);
		var hangTex = GD.Load<Texture2D>(AssetBase + "jump4.png");
		if (hangTex != null) frames.AddFrame("jump_hang", hangTex);

		// land: frames 1–2, one-shot.
		AddAnimFromFiles(frames, "land", "land", 2, 6f, false);

		_sprite.SpriteFrames = frames;
	}

	static void AddAnimFromFiles(SpriteFrames frames, string animName,
		string filePrefix, int count, float fps, bool looping)
	{
		frames.AddAnimation(animName);
		frames.SetAnimationLoop(animName, looping);
		frames.SetAnimationSpeed(animName, fps);
		for (var i = 1; i <= count; i++)
		{
			var tex = GD.Load<Texture2D>(AssetBase + $"{filePrefix}{i}.png");
			frames.AddFrame(animName, tex);
		}
	}
}

/// <summary>
/// Draws a faint red line from the Frozen Terror's current position to the
/// arena wall, telegraphing the charge direction to the player.
/// </summary>
public partial class FrozenTerrorChargeLine : Node2D
{
	public Vector2 From { get; set; }
	public Vector2 To { get; set; }

	static readonly Color DangerColor = new(1f, 0.15f, 0.15f, 0.5f);
	static readonly Color DangerEdge = new(1f, 0.4f, 0.4f, 0.75f);
	const float LineWidth = 18f;

	public override void _Draw()
	{
		// Solid danger strip
		DrawLine(From, To, DangerColor, LineWidth);
		// Brighter centre seam for legibility
		DrawLine(From, To, DangerEdge, 2f);
	}

	public override void _Process(double delta)
	{
		// Redraw every frame so the line stays correct if the boss moves slightly
		QueueRedraw();
	}
}