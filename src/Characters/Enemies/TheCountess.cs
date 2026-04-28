using System.Collections.Generic;
using Godot;
using healerfantasy;
using healerfantasy.SpellResources;
using healerfantasy.SpellSystem;

/// <summary>
/// The Countess — second boss of the Castle of Blood.
///
/// An ancient vampiric noblewoman of terrifying elegance, wielding blood
/// sorcery to weaken her enemies and punish the healer's spell choices.
///
/// Behaviour
/// ─────────
/// • Every <see cref="MeleeAttackInterval"/> seconds — Noble Strike:
///   a swift melee attack at the tank (Templar).
///
/// • Every <see cref="BloodBoltInterval"/> seconds — Blood Bolt:
///   a ranged crimson projectile hurled at a random party member.
///
/// • Every <see cref="CurseInterval"/> seconds — Crimson Curse:
///   applies a 10-second dispellable debuff to a random party member that
///   deals 15 damage/sec AND reduces all healing received by 50%.
///   Forces a healing triage decision: dispel (removing the healing penalty)
///   or tank the DoT and heal through it normally.
///
/// • Every <see cref="NovaInterval"/> seconds — Sanguine Nova:
///   a telegraphed AoE blood explosion targeting the whole party.
///   Opens a <see cref="NovaWindupDuration"/>-second parry window.
///   Deflect cancels it; otherwise all party members take 60 damage.
///
/// • Blood Shield (once, at 35% HP) — the Countess conjures a large shield
///   of coagulated blood, absorbing the next 450 damage dealt to her.
///   Only triggers once per fight.
///
/// Animations (individual PNGs in res://assets/enemies/the-countess/):
///   "idle"    — idle1–idle2     (looping)
///   "attack"  — attack1–attack4 (one-shot → idle)
///   "casting" — casting1–casting3 (one-shot → idle; used for spells and Nova wind-up)
/// </summary>
public partial class TheCountess : Character
{
	public TheCountess()
	{
		MaxHealth = GameConstants.BossHealthBaseValuesByDungeonTier[GameConstants.CastleOfBloodTier][1];
	}

	// ── signals ───────────────────────────────────────────────────────────────

	[Signal]
	public delegate void CastWindupStartedEventHandler(string spellName, Texture2D icon, float duration);

	[Signal]
	public delegate void CastWindupEndedEventHandler();

	// ── tuneable exports ──────────────────────────────────────────────────────

	[Export] public float MeleeAttackInterval = 2.5f;
	[Export] public float BloodBoltInterval = 4.0f;
	[Export] public float CurseInterval = 9.0f;
	[Export] public float NovaInterval = 14.0f;
	[Export] public float NovaWindupDuration = 3.5f;

	[Export] public float MeleeDamage = 38f;
	[Export] public float BloodBoltDamage = 42f;
	[Export] public float NovaDamage = 60f;

	/// <summary>HP fraction (0–1) at which Blood Shield activates. Default 35%.</summary>
	[Export] public float ShieldThreshold = 0.35f;

	// ── internal state ────────────────────────────────────────────────────────

	float _meleeTimer;
	float _bloodBoltTimer;
	float _curseTimer;
	float _novaTimer;
	float _novaWindupTimer;

	BossCountessStrikeSpell _meleeSpell;
	BossCountessBloodBoltSpell _bloodBoltSpell;
	BossCountessCrimsonCurseSpell _curseSpell;
	BossCountessSanguineNovaSpell _novaSpell;
	BossCountessBloodShieldSpell _shieldSpell;

	AnimatedSprite2D _sprite;
	AudioStreamPlayer _riserPlayer;

	bool _bloodShieldUsed;

	enum PendingAttack
	{
		None,
		Melee,
		BloodBolt,
		Curse
	}

	PendingAttack _pendingAttack;
	Character _pendingTarget;

	const string AssetBase = "res://assets/enemies/the-countess/";

	// ── lifecycle ─────────────────────────────────────────────────────────────

	public override void _Ready()
	{
		base._Ready();
		CharacterName = GameConstants.CastleBoss2Name;
		RemoveFromGroup("party");
		AddToGroup(GameConstants.BossGroupName);
		IsFriendly = false;

		// Stagger first attacks.
		_meleeTimer = MeleeAttackInterval;
		_bloodBoltTimer = BloodBoltInterval;
		_curseTimer = CurseInterval;
		_novaTimer = NovaInterval;

		_meleeSpell = new BossCountessStrikeSpell { DamageAmount = MeleeDamage };
		_bloodBoltSpell = new BossCountessBloodBoltSpell { DamageAmount = BloodBoltDamage };
		_curseSpell = new BossCountessCrimsonCurseSpell();
		_novaSpell = new BossCountessSanguineNovaSpell { DamageAmount = NovaDamage };
		_shieldSpell = new BossCountessBloodShieldSpell();

		GlobalAutoLoad.RegisterSignalEmitter(this, nameof(CastWindupStarted));
		GlobalAutoLoad.RegisterSignalEmitter(this, nameof(CastWindupEnded));

		_riserPlayer = new AudioStreamPlayer();
		_riserPlayer.Stream = GD.Load<AudioStream>(AssetConstants.DeflectRiserSoundPath);
		AddChild(_riserPlayer);

		_sprite = GetNode<AnimatedSprite2D>("AnimatedSprite2D");
		_sprite.Scale = new Vector2(0.8f, 0.8f);
		SetupAnimations();
		_sprite.AnimationFinished += OnAnimationFinished;
		_sprite.Play("idle");
	}

	public override void _Process(double delta)
	{
		base._Process(delta);

		if (!IsAlive) return;

		// ── Blood Shield — once at 35% HP ─────────────────────────────────────
		if (!_bloodShieldUsed && CurrentHealth / MaxHealth <= ShieldThreshold)
		{
			_bloodShieldUsed = true;
			SpellPipeline.Cast(_shieldSpell, this, this);
			GD.Print("[TheCountess] Blood Shield activated!");
		}

		// ── Sanguine Nova wind-up countdown ──────────────────────────────────
		if (_novaWindupTimer > 0f)
		{
			_novaWindupTimer -= (float)delta;
			if (_novaWindupTimer <= 0f)
				ExecuteSanguineNova();
			return;
		}

		// ── Regular attack timers ─────────────────────────────────────────────
		_meleeTimer -= (float)delta;
		_bloodBoltTimer -= (float)delta;
		_curseTimer -= (float)delta;
		_novaTimer -= (float)delta;

		if (_pendingAttack != PendingAttack.None) return;

		if (_novaTimer <= 0f)
		{
			_novaTimer = NovaInterval;
			BeginSanguineNova();
		}
		else if (_curseTimer <= 0f)
		{
			_curseTimer = CurseInterval;
			CastCrimsonCurse();
		}
		else if (_bloodBoltTimer <= 0f)
		{
			_bloodBoltTimer = BloodBoltInterval;
			CastBloodBolt();
		}
		else if (_meleeTimer <= 0f)
		{
			_meleeTimer = MeleeAttackInterval;
			PerformMeleeAttack();
		}
	}

	// ── combat actions ────────────────────────────────────────────────────────

	void PerformMeleeAttack()
	{
		var target = FindTank() ?? PickRandomPartyMember();
		if (target == null) return;
		_pendingTarget = target;
		_pendingAttack = PendingAttack.Melee;
		_sprite.Play("attack");
	}

	void CastBloodBolt()
	{
		var target = PickRandomPartyMember();
		if (target == null) return;
		_pendingTarget = target;
		_pendingAttack = PendingAttack.BloodBolt;
		_sprite.Play("casting");
	}

	void CastCrimsonCurse()
	{
		var target = PickRandomPartyMember();
		if (target == null) return;
		_pendingTarget = target;
		_pendingAttack = PendingAttack.Curse;
		_sprite.Play("casting");
	}

	/// <summary>
	/// Begins the Sanguine Nova wind-up: plays the riser, opens the parry
	/// window, and starts the countdown.
	/// </summary>
	void BeginSanguineNova()
	{
		_novaWindupTimer = NovaWindupDuration;
		_riserPlayer.Play();
		ParryWindowManager.OpenWindow();
		EmitSignalCastWindupStarted(_novaSpell.Name, _novaSpell.Icon, NovaWindupDuration);
		_pendingAttack = PendingAttack.None;
		_sprite.Play("casting");
	}

	/// <summary>
	/// Resolves Sanguine Nova at the end of the wind-up.
	/// Deflection cancels the blast; otherwise all party members take damage.
	/// </summary>
	void ExecuteSanguineNova()
	{
		EmitSignalCastWindupEnded();

		var wasDeflected = ParryWindowManager.ConsumeResult();
		if (wasDeflected)
		{
			GD.Print("[TheCountess] Sanguine Nova was deflected!");
			return;
		}

		var anyTarget = PickRandomPartyMember();
		if (anyTarget != null)
			SpellPipeline.Cast(_novaSpell, this, anyTarget);
	}

	void OnAnimationFinished()
	{
		if (_pendingTarget != null && _pendingTarget.IsAlive)
		{
			SpellResource spell = _pendingAttack switch
			{
				PendingAttack.Melee => _meleeSpell,
				PendingAttack.BloodBolt => _bloodBoltSpell,
				PendingAttack.Curse => _curseSpell,
				_ => null
			};

			if (spell != null)
				SpellPipeline.Cast(spell, this, _pendingTarget);
		}

		_pendingTarget = null;
		_pendingAttack = PendingAttack.None;

		if (IsAlive)
			_sprite.Play("idle");
	}

	// ── targeting helpers ─────────────────────────────────────────────────────

	Character FindTank()
	{
		foreach (var node in GetTree().GetNodesInGroup("party"))
			if (node is Character c && c.CharacterName == GameConstants.TemplarName && c.IsAlive)
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

	// ── animation setup ───────────────────────────────────────────────────────

	/// <summary>
	/// Loads individual PNG frames from res://assets/enemies/the-countess/.
	///
	/// idle1–2     (looping, 3 fps)
	/// attack1–4   (one-shot, 10 fps)
	/// casting1–3  (one-shot, 6 fps; used for spells and Nova wind-up)
	/// </summary>
	void SetupAnimations()
	{
		var frames = new SpriteFrames();
		frames.RemoveAnimation("default");

		AddAnimFromFiles(frames, "idle", "idle", 2, 3f, true);
		AddAnimFromFiles(frames, "attack", "attack", 4, 10f, false);
		AddAnimFromFiles(frames, "casting", "casting", 3, 6f, false);

		_sprite.SpriteFrames = frames;
	}

	static void AddAnimFromFiles(SpriteFrames frames, string animName,
		string filePrefix, int count, float fps, bool loop)
	{
		frames.AddAnimation(animName);
		frames.SetAnimationLoop(animName, loop);
		frames.SetAnimationSpeed(animName, fps);
		for (var i = 1; i <= count; i++)
		{
			var texture = GD.Load<Texture2D>(AssetBase + $"{filePrefix}{i}.png");
			frames.AddFrame(animName, texture);
		}
	}
}