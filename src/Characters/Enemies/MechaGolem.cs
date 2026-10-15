using System.Collections.Generic;
using Godot;
using healerfantasy.LastLight;
using healerfantasy;
using healerfantasy.SpellResources;
using healerfantasy.SpellSystem;

/// <summary>
/// The Mecha Golem — second boss of The Forsaken Citadel.
///
/// Behaviour
/// ─────────
/// • Stands still (no movement).
/// • Every <see cref="MeleeInterval"/> seconds — Iron Fist: a thunderous
///   mechanical punch against the tank; uses the "attack" animation.
/// • Every <see cref="BarrageInterval"/> seconds — Rocket Barrage: a spread
///   of rockets that hits ALL party members for moderate damage;
///   uses the "cast" animation.
/// • Every <see cref="PulseInterval"/> seconds — Magnetic Pulse: disrupts a
///   random party member's armour, applying Vulnerable (+20% damage taken)
///   for 10 seconds; uses the "cast" animation.
/// • Every <see cref="OverloadInterval"/> seconds — System Overload: a 3.5-second
///   telegraphed AoE during which the Golem raises its shield and builds a
///   catastrophic charge. If not deflected, all party members take 70 damage;
///   uses the "shield" animation as the telegraph.
/// </summary>
public partial class MechaGolem : EnemyCharacter
{
	/// <summary>
	/// Animation clips (frames / fps from the original sprites). These are gameplay:
	/// an attack's spell is cast when its clip finishes, so a clip's length is its wind-up.
	/// </summary>
	static readonly Dictionary<string, CharacterVisual.Clip> Clips = new()
	{
		["idle"] = new(4 / 8f, Loop: true),
		["attack"] = new(7 / 12f),
		["cast"] = new(7 / 10f),
		["shield"] = new(8 / 8f),
		["hurt"] = new(8 / 12f),
		["death"] = new(23 / 8f)
	};

	public MechaGolem()
	{
		MaxHealth = GameConstants.BossHealthBaseValuesByDungeonTier[GameConstants.ForsakenCitadelTier][1];
	}

	// ── signals ───────────────────────────────────────────────────────────────

	[Signal]
	public delegate void CastWindupStartedEventHandler(string spellName, Texture2D icon, float duration);

	[Signal]
	public delegate void CastWindupEndedEventHandler();

	// ── tuneable exports ──────────────────────────────────────────────────────

	[Export] public float MeleeInterval = 2.5f;
	[Export] public float BarrageInterval = 5.0f;
	[Export] public float PulseInterval = 10.0f;
	[Export] public float OverloadInterval = 16.0f;
	[Export] public float OverloadWindup = 3.5f;

	[Export] public float MeleeDamage = 50f;
	[Export] public float BarrageDamage = 30f;
	[Export] public float OverloadDamage = 70f;

	// ── internal state ────────────────────────────────────────────────────────

	float _meleeTimer;
	float _barrageTimer;
	float _pulseTimer;
	float _overloadTimer;
	float _overloadWindupTimer;

	BossIronFistSpell _ironFistSpell;
	BossRocketBarrageSpell _rocketBarrageSpell;
	BossCrushedSpell _crushedSpell;
	BossSystemOverloadSpell _systemOverloadSpell;

	CharacterVisual _visual = null!;
	AudioStreamPlayer _riserPlayer;

	enum PendingAttack
	{
		None,
		Melee,
		RocketBarrage,
		MagneticPulse
	}

	PendingAttack _pendingAttack;
	Character _pendingTarget;


	// ── lifecycle ─────────────────────────────────────────────────────────────

	public override void _Ready()
	{
		base._Ready();
		CharacterName = GameConstants.ForsakenBoss2Name;
		IsFriendly = false;

		_meleeTimer = MeleeInterval;
		_barrageTimer = BarrageInterval;
		_pulseTimer = PulseInterval;
		_overloadTimer = OverloadInterval;

		_ironFistSpell = new BossIronFistSpell { DamageAmount = MeleeDamage };
		_rocketBarrageSpell = new BossRocketBarrageSpell { DamageAmount = BarrageDamage };
		_crushedSpell = new BossCrushedSpell();
		_systemOverloadSpell = new BossSystemOverloadSpell { DamageAmount = OverloadDamage };

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

		// ── System Overload wind-up countdown ────────────────────────────────
		if (_overloadWindupTimer > 0f)
		{
			_overloadWindupTimer -= (float)delta;
			if (_overloadWindupTimer <= 0f)
				ExecuteOverload();
			return;
		}

		_meleeTimer -= (float)delta;
		_barrageTimer -= (float)delta;
		_pulseTimer -= (float)delta;
		_overloadTimer -= (float)delta;

		if (_pendingAttack != PendingAttack.None) return;

		if (_meleeTimer <= 0f)
		{
			_meleeTimer = MeleeInterval;
			PerformIronFist();
		}
		else if (_barrageTimer <= 0f)
		{
			_barrageTimer = BarrageInterval;
			CastRocketBarrage();
		}
		else if (_pulseTimer <= 0f)
		{
			_pulseTimer = PulseInterval;
			CastMagneticPulse();
		}
		else if (_overloadTimer <= 0f)
		{
			_overloadTimer = OverloadInterval;
			BeginOverload();
		}
	}

	// ── combat actions ────────────────────────────────────────────────────────

	void PerformIronFist()
	{
		var target = SelectCurrentMeleeTarget();
		if (target == null) return;
		_pendingTarget = target;
		_pendingAttack = PendingAttack.Melee;
		_visual.Play("attack");
	}

	void CastRocketBarrage()
	{
		// Barrage targets everyone — pass any alive member as the explicit target;
		// the spell's ResolveTargets will expand to the full party.
		var target = PickRandomPartyMember();
		if (target == null) return;
		_pendingTarget = target;
		_pendingAttack = PendingAttack.RocketBarrage;
		_visual.Play("cast");
	}

	void CastMagneticPulse()
	{
		var target = PickRandomPartyMember();
		if (target == null) return;
		_pendingTarget = target;
		_pendingAttack = PendingAttack.MagneticPulse;
		_visual.Play("cast");
	}

	/// <summary>
	/// Begins the System Overload wind-up.
	/// The "shield" animation plays as a visual telegraph — the Golem raises its
	/// shield plate and builds up a devastating internal charge.
	/// </summary>
	void BeginOverload()
	{
		_overloadWindupTimer = OverloadWindup;
		_riserPlayer.Play();
		ParryWindowManager.OpenWindow(_systemOverloadSpell.Name, _systemOverloadSpell.Icon, OverloadWindup);
		EmitSignalCastWindupStarted(_systemOverloadSpell.Name, _systemOverloadSpell.Icon, OverloadWindup);
		_pendingAttack = PendingAttack.None;
		_visual.Play("shield");
	}

	void ExecuteOverload()
	{
		EmitSignalCastWindupEnded();

		if (ParryWindowManager.ConsumeResult())
		{
			GD.Print("[MechaGolem] System Overload was deflected!");
			return;
		}

		var anyTarget = PickRandomPartyMember();
		if (anyTarget != null)
			SpellPipeline.Cast(_systemOverloadSpell, this, anyTarget);
	}

	void OnAnimationFinished()
	{
		if (_pendingTarget != null && _pendingTarget.IsAlive)
		{
			SpellResource spell = _pendingAttack switch
			{
				PendingAttack.Melee => _ironFistSpell,
				PendingAttack.RocketBarrage => _rocketBarrageSpell,
				PendingAttack.MagneticPulse => _crushedSpell,
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

	/// <summary>Rune of Midnight: scale all ability intervals by the haste multiplier.</summary>
	protected override void OnApplyHasteRune()
	{
		MeleeInterval /= GameConstants.RuneMidnightHasteMultiplier;
		BarrageInterval /= GameConstants.RuneMidnightHasteMultiplier;
		PulseInterval /= GameConstants.RuneMidnightHasteMultiplier;
		OverloadInterval /= GameConstants.RuneMidnightHasteMultiplier;
	}

}
