using System.Collections.Generic;
using Godot;
using healerfantasy;
using healerfantasy.SpellResources;
using healerfantasy.SpellSystem;

/// <summary>
/// The Blood Knight — first boss of the Castle of Blood.
///
/// A hulking warrior who has given himself wholly to blood magic, augmenting
/// his already fearsome strength with vampiric power.
///
/// Behaviour
/// ─────────
/// • Every <see cref="MeleeAttackInterval"/> seconds — Bloodthirst:
///   a savage melee strike at the tank (Templar). Falls back to a random
///   party member if the Templar is dead.
///
/// • Every <see cref="CleaveInterval"/> seconds — Crimson Strike:
///   a wide cleave that simultaneously hits the Templar and the Assassin
///   (both frontline melee characters).
///
/// • Every <see cref="DrainInterval"/> seconds — Blood Drain:
///   a telegraphed channel that siphons the life of a random party member,
///   dealing 65 damage and healing the Blood Knight for the same amount.
///   Opens a parry window for <see cref="DrainWindupDuration"/> seconds.
///   Deflecting it cancels both the damage and the heal.
///
/// • Enrage at 30% HP — once the Blood Knight falls below 30% health he
///   enters a bloodlust frenzy, permanently increasing melee and cleave
///   damage by 40%.
///
/// Animations (individual PNGs in res://assets/enemies/blood-knight/):
///   "idle"   — idle1–idle3   (looping)
///   "attack" — attack1–attack3 (one-shot → idle)
///   "cast"   — cast1–cast3   (one-shot → idle; also used during Blood Drain wind-up)
/// </summary>
public partial class BloodKnight : EnemyCharacter
{
	public BloodKnight()
	{
		MaxHealth = GameConstants.BossHealthBaseValuesByDungeonTier[GameConstants.CastleOfBloodTier][0];
	}

	// ── signals ───────────────────────────────────────────────────────────────

	[Signal]
	public delegate void CastWindupStartedEventHandler(string spellName, Texture2D icon, float duration);

	[Signal]
	public delegate void CastWindupEndedEventHandler();

	// ── tuneable exports ──────────────────────────────────────────────────────

	[Export] public float MeleeAttackInterval = 2.2f;
	[Export] public float CleaveInterval = 5.5f;
	[Export] public float DrainInterval = 12.0f;
	[Export] public float DrainWindupDuration = 3.0f;

	[Export] public float MeleeDamage = 45f;
	[Export] public float CleaveDamage = 28f;
	[Export] public float DrainDamage = 65f;

	/// <summary>HP fraction (0–1) below which Enrage triggers. Default 30%.</summary>
	[Export] public float EnrageThreshold = 0.30f;

	/// <summary>Damage multiplier applied to melee and cleave when Enraged.</summary>
	[Export] public float EnrageDamageMultiplier = 1.4f;

	// ── internal state ────────────────────────────────────────────────────────

	float _meleeTimer;
	float _cleaveTimer;
	float _drainTimer;
	float _drainWindupTimer;

	BossBloodKnightMeleeSpell _meleeSpell;
	BossBloodKnightCleaveSpell _cleaveSpell;
	BossBloodDrainSpell _drainSpell;

	AnimatedSprite2D _sprite;
	AudioStreamPlayer _riserPlayer;

	bool _enraged;

	enum PendingAttack
	{
		None,
		Melee,
		Cleave
	}

	PendingAttack _pendingAttack;
	Character _pendingTarget;

	const string AssetBase = "res://assets/enemies/blood-knight/";

	// ── lifecycle ─────────────────────────────────────────────────────────────

	public override void _Ready()
	{
		base._Ready();
		CharacterName = GameConstants.CastleBoss1Name;
		RemoveFromGroup("party");
		AddToGroup(GameConstants.BossGroupName);
		IsFriendly = false;

		// Stagger first attacks so the player has a moment to react.
		_meleeTimer = MeleeAttackInterval;
		_cleaveTimer = CleaveInterval;
		_drainTimer = DrainInterval;

		_meleeSpell = new BossBloodKnightMeleeSpell { DamageAmount = MeleeDamage };
		_cleaveSpell = new BossBloodKnightCleaveSpell { DamageAmount = CleaveDamage };
		_drainSpell = new BossBloodDrainSpell { DamageAmount = DrainDamage, Boss = this };

		GlobalAutoLoad.RegisterSignalEmitter(this, nameof(CastWindupStarted));
		GlobalAutoLoad.RegisterSignalEmitter(this, nameof(CastWindupEnded));

		_riserPlayer = new AudioStreamPlayer();
		_riserPlayer.Stream = GD.Load<AudioStream>(AssetConstants.DeflectRiserSoundPath);
		AddChild(_riserPlayer);

		_sprite = GetNode<AnimatedSprite2D>("AnimatedSprite2D");
		_sprite.Scale = new Vector2(0.4f, 0.4f);
		SetupAnimations();
		_sprite.AnimationFinished += OnAnimationFinished;
		_sprite.Play("idle");
		ApplyRuneModifiers();
	}

	public override void _Process(double delta)
	{
		base._Process(delta);

		if (!IsAlive) return;

		// ── Enrage check ──────────────────────────────────────────────────────
		if (!_enraged && CurrentHealth / MaxHealth <= EnrageThreshold)
			TriggerEnrage();

		// ── Blood Drain wind-up countdown ─────────────────────────────────────
		if (_drainWindupTimer > 0f)
		{
			_drainWindupTimer -= (float)delta;
			if (_drainWindupTimer <= 0f)
				ExecuteBloodDrain();
			return;
		}

		// ── Regular attack timers ─────────────────────────────────────────────
		_meleeTimer -= (float)delta;
		_cleaveTimer -= (float)delta;
		_drainTimer -= (float)delta;

		// Wait for the current animation to complete before starting the next.
		if (_pendingAttack != PendingAttack.None) return;

		if (_drainTimer <= 0f)
		{
			_drainTimer = DrainInterval;
			BeginBloodDrain();
		}
		else if (_meleeTimer <= 0f)
		{
			_meleeTimer = MeleeAttackInterval;
			PerformMeleeAttack();
		}
		else if (_cleaveTimer <= 0f)
		{
			_cleaveTimer = CleaveInterval;
			PerformCleave();
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

	void PerformCleave()
	{
		// Cleave's ResolveTargets handles finding the frontline — pass any target.
		var target = FindTank() ?? PickRandomPartyMember();
		if (target == null) return;
		_pendingTarget = target;
		_pendingAttack = PendingAttack.Cleave;
		_sprite.Play("attack");
	}

	/// <summary>
	/// Begins the Blood Drain wind-up: plays the riser, opens the parry window,
	/// and starts the countdown. The drain resolves in <see cref="ExecuteBloodDrain"/>
	/// when the timer expires.
	/// </summary>
	void BeginBloodDrain()
	{
		_drainWindupTimer = DrainWindupDuration;
		_riserPlayer.Play();
		ParryWindowManager.OpenWindow(_drainSpell.Name, _drainSpell.Icon, DrainWindupDuration);
		EmitSignalCastWindupStarted(_drainSpell.Name, _drainSpell.Icon, DrainWindupDuration);
		_pendingAttack = PendingAttack.None; // animation finish won't fire a spell
		_sprite.Play("cast");
	}

	/// <summary>
	/// Resolves Blood Drain at the end of the wind-up.
	/// Deflection cancels both the damage and the self-heal entirely.
	/// </summary>
	void ExecuteBloodDrain()
	{
		EmitSignalCastWindupEnded();

		var wasDeflected = ParryWindowManager.ConsumeResult();
		if (wasDeflected)
		{
			GD.Print("[BloodKnight] Blood Drain was deflected!");
			return;
		}

		var target = PickRandomPartyMember();
		if (target != null)
			SpellPipeline.Cast(_drainSpell, this, target);
	}

	void TriggerEnrage()
	{
		_enraged = true;
		_meleeSpell.DamageAmount = MeleeDamage * EnrageDamageMultiplier;
		_cleaveSpell.DamageAmount = CleaveDamage * EnrageDamageMultiplier;
		GD.Print("[BloodKnight] ENRAGED — damage increased by 40%!");
	}

	void OnAnimationFinished()
	{
		if (_pendingTarget != null && _pendingTarget.IsAlive)
		{
			SpellResource spell = _pendingAttack switch
			{
				PendingAttack.Melee => _meleeSpell,
				PendingAttack.Cleave => _cleaveSpell,
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



	// ── animation setup ───────────────────────────────────────────────────────

	/// <summary>
	/// Loads individual PNG frames from res://assets/enemies/blood-knight/.
	///
	/// idle1–3  (looping, 4 fps)
	/// attack1–3 (one-shot, 10 fps)
	/// cast1–3   (one-shot, 6 fps; used for Blood Drain wind-up)
	/// </summary>
	/// <summary>Rune of Time: scale all ability intervals by the haste multiplier.</summary>
	protected override void OnApplyHasteRune()
	{
		MeleeAttackInterval /= GameConstants.RuneTimeHasteMultiplier;
		CleaveInterval /= GameConstants.RuneTimeHasteMultiplier;
		DrainInterval /= GameConstants.RuneTimeHasteMultiplier;
	}

		void SetupAnimations()
	{
		var frames = new SpriteFrames();
		frames.RemoveAnimation("default");

		AddAnimFromFiles(frames, "idle", AssetBase + "idle", 3, 4f, true);
		AddAnimFromFiles(frames, "attack", AssetBase + "attack", 3, 10f, false);
		AddAnimFromFiles(frames, "cast", AssetBase + "cast", 3, 6f, false);

		_sprite.SpriteFrames = frames;
	}

}