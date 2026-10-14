using System.Collections.Generic;
using Godot;
using healerfantasy.LastLight;
using healerfantasy;
using healerfantasy.SpellResources;
using healerfantasy.SpellSystem;

/// <summary>
/// The Flying Demon — first boss of The Forsaken Citadel.
///
/// Behaviour
/// ─────────
/// • Stands still (no movement).
/// • Every <see cref="MeleeInterval"/> seconds — Fel Strike: a claw swipe
///   against the tank; uses the "attack" animation.
/// • Every <see cref="BoltInterval"/> seconds — Hellfire Bolt: a fireball
///   hurled at a random party member; uses the "cast" animation.
/// • Every <see cref="BurnInterval"/> seconds — Fel Burn: coats a random
///   party member in demonic fire (15 dmg/s DoT, 8 s, dispellable);
///   uses the "cast" animation.
/// • Every <see cref="EruptionInterval"/> seconds — Infernal Eruption: a
///   3-second telegraphed AoE that scorches all party members for 60 damage
///   unless the player casts Deflect in time; uses the "cast" animation.
/// </summary>
public partial class FlyingDemon : EnemyCharacter
{
	/// <summary>
	/// Animation clips (frames / fps from the original sprites). These are gameplay:
	/// an attack's spell is cast when its clip finishes, so a clip's length is its wind-up.
	/// </summary>
	static readonly Dictionary<string, CharacterVisual.Clip> Clips = new()
	{
		["idle"] = new(4 / 8f, Loop: true),
		["attack"] = new(8 / 12f),
		["cast"] = new(4 / 8f),
		["hurt"] = new(4 / 10f),
		["death"] = new(6 / 8f)
	};

	public FlyingDemon()
	{
		MaxHealth = GameConstants.BossHealthBaseValuesByDungeonTier[GameConstants.ForsakenCitadelTier][0];
	}

	// ── signals ───────────────────────────────────────────────────────────────

	[Signal]
	public delegate void CastWindupStartedEventHandler(string spellName, Texture2D icon, float duration);

	[Signal]
	public delegate void CastWindupEndedEventHandler();

	// ── tuneable exports ──────────────────────────────────────────────────────

	[Export] public float MeleeInterval = 2.5f;
	[Export] public float BoltInterval = 5.0f;
	[Export] public float BurnInterval = 9.0f;
	[Export] public float EruptionInterval = 14.0f;
	[Export] public float EruptionWindup = 3.0f;

	[Export] public float MeleeDamage = 45f;
	[Export] public float BoltDamage = 35f;
	[Export] public float EruptionDamage = 60f;

	// ── internal state ────────────────────────────────────────────────────────

	float _meleeTimer;
	float _boltTimer;
	float _burnTimer;
	float _eruptionTimer;
	float _eruptionWindupTimer;

	BossFelStrikeSpell _felStrikeSpell;
	BossHellfireBoltSpell _hellfireBoltSpell;
	BossFelBurnSpell _felBurnSpell;
	BossInfernalEruptionSpell _eruptionSpell;

	CharacterVisual _visual = null!;
	AudioStreamPlayer _riserPlayer;

	enum PendingAttack
	{
		None,
		Melee,
		HellfireBolt,
		FelBurn
	}

	PendingAttack _pendingAttack;
	Character _pendingTarget;


	// ── lifecycle ─────────────────────────────────────────────────────────────

	public override void _Ready()
	{
		base._Ready();
		CharacterName = GameConstants.ForsakenBoss1Name;

		_meleeTimer = MeleeInterval;
		_boltTimer = BoltInterval;
		_burnTimer = BurnInterval;
		_eruptionTimer = EruptionInterval;

		_felStrikeSpell = new BossFelStrikeSpell { DamageAmount = MeleeDamage };
		_hellfireBoltSpell = new BossHellfireBoltSpell { DamageAmount = BoltDamage };
		_felBurnSpell = new BossFelBurnSpell();
		_eruptionSpell = new BossInfernalEruptionSpell { DamageAmount = EruptionDamage };

		GlobalAutoLoad.RegisterSignalEmitter(this, nameof(CastWindupStarted));
		GlobalAutoLoad.RegisterSignalEmitter(this, nameof(CastWindupEnded));

		_riserPlayer = new AudioStreamPlayer();
		_riserPlayer.Stream = GD.Load<AudioStream>(AssetConstants.DeflectRiserSoundPath);
		AddChild(_riserPlayer);

		_visual = CharacterVisual.Attach(this, Clips);
		_visual.AnimationFinished += OnAnimationFinished;
		_visual.Play("idle");
		ApplyRuneModifiers();
	}

	public override void _Process(double delta)
	{
		base._Process(delta);
		if (!IsAlive) return;

		// ── Infernal Eruption wind-up countdown ───────────────────────────────
		if (_eruptionWindupTimer > 0f)
		{
			_eruptionWindupTimer -= (float)delta;
			if (_eruptionWindupTimer <= 0f)
				ExecuteEruption();
			return;
		}

		_meleeTimer -= (float)delta;
		_boltTimer -= (float)delta;
		_burnTimer -= (float)delta;
		_eruptionTimer -= (float)delta;

		if (_pendingAttack != PendingAttack.None) return;

		if (_meleeTimer <= 0f)
		{
			_meleeTimer = MeleeInterval;
			PerformFelStrike();
		}
		else if (_boltTimer <= 0f)
		{
			_boltTimer = BoltInterval;
			CastHellfireBolt();
		}
		else if (_burnTimer <= 0f)
		{
			_burnTimer = BurnInterval;
			CastFelBurn();
		}
		else if (_eruptionTimer <= 0f)
		{
			_eruptionTimer = EruptionInterval;
			BeginEruption();
		}
	}

	// ── combat actions ────────────────────────────────────────────────────────

	void PerformFelStrike()
	{
		var target = SelectCurrentMeleeTarget();
		if (target == null) return;
		_pendingTarget = target;
		_pendingAttack = PendingAttack.Melee;
		_visual.Play("attack");
	}

	void CastHellfireBolt()
	{
		var target = PickRandomPartyMember();
		if (target == null) return;
		_pendingTarget = target;
		_pendingAttack = PendingAttack.HellfireBolt;
		_visual.Play("cast");
	}

	void CastFelBurn()
	{
		var target = PickRandomPartyMember();
		if (target == null) return;
		_pendingTarget = target;
		_pendingAttack = PendingAttack.FelBurn;
		_visual.Play("cast");
	}

	void BeginEruption()
	{
		_eruptionWindupTimer = EruptionWindup;
		_riserPlayer.Play();
		ParryWindowManager.OpenWindow(_eruptionSpell.Name, _eruptionSpell.Icon, EruptionWindup);
		EmitSignalCastWindupStarted(_eruptionSpell.Name, _eruptionSpell.Icon, EruptionWindup);
		_pendingAttack = PendingAttack.None;
		_visual.Play("cast");
	}

	void ExecuteEruption()
	{
		EmitSignalCastWindupEnded();

		if (ParryWindowManager.ConsumeResult())
		{
			GD.Print("[FlyingDemon] Infernal Eruption was deflected!");
			return;
		}

		var anyTarget = PickRandomPartyMember();
		if (anyTarget != null)
			SpellPipeline.Cast(_eruptionSpell, this, anyTarget);
	}

	void OnAnimationFinished()
	{
		if (_pendingTarget != null && _pendingTarget.IsAlive)
		{
			SpellResource spell = _pendingAttack switch
			{
				PendingAttack.Melee => _felStrikeSpell,
				PendingAttack.HellfireBolt => _hellfireBoltSpell,
				PendingAttack.FelBurn => _felBurnSpell,
				_ => null
			};

			if (spell != null)
				SpellPipeline.Cast(spell, this, _pendingTarget);
		}

		_pendingTarget = null;
		_pendingAttack = PendingAttack.None;
		_visual.Play("idle");
	}

	// ── targeting helpers ─────────────────────────────────────────────────────

	/// <summary>Rune of Time: scale all ability intervals by the haste multiplier.</summary>
	protected override void OnApplyHasteRune()
	{
		MeleeInterval /= GameConstants.RuneTimeHasteMultiplier;
		BoltInterval /= GameConstants.RuneTimeHasteMultiplier;
		BurnInterval /= GameConstants.RuneTimeHasteMultiplier;
		EruptionInterval /= GameConstants.RuneTimeHasteMultiplier;
	}

}
