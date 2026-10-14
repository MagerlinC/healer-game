using System.Collections.Generic;
using Godot;
using healerfantasy.LastLight;
using healerfantasy;
using healerfantasy.SpellResources;
using healerfantasy.SpellSystem;

/// <summary>
/// The Demon Slime — third and final boss encounter.
///
/// Behaviour
/// ─────────
/// • Stands still (no movement).
/// • Every <see cref="MeleeInterval"/> seconds — Slime Slam: a crushing melee
///   strike against the tank; uses the "cleave" animation.
/// • Every <see cref="AcidSpitInterval"/> seconds — Acid Spit: a corrosive
///   projectile at a random party member; uses the "cleave" animation.
/// • Every <see cref="OozeInterval"/> seconds — Corrosive Ooze: coats a
///   random party member in acidic slime (12 dmg/s for 10 s, dispellable);
///   uses the "cleave" animation.
/// • Every <see cref="NovaInterval"/> seconds — Toxic Nova: a 3-second
///   telegraphed AoE that hits all party members for 50 damage unless
///   the player deflects; uses the "cleave" animation as wind-up visual.
///
///   "idle"   — 6 frames  (01_demon_idle/demon_idle_{n}.png),   looping
///   "cleave" — 15 frames (03_demon_cleave/demon_cleave_{n}.png), one-shot → idle
/// </summary>
public partial class DemonSlime : EnemyCharacter
{
	/// <summary>
	/// Animation clips (frames / fps from the original sprites). These are gameplay:
	/// an attack's spell is cast when its clip finishes, so a clip's length is its wind-up.
	/// </summary>
	static readonly Dictionary<string, CharacterVisual.Clip> Clips = new()
	{
		["idle"] = new(6 / 8f, Loop: true),
		["cleave"] = new(15 / 12f)
	};

	// ── signals ───────────────────────────────────────────────────────────────

	public DemonSlime()
	{
		MaxHealth = GameConstants.BossHealthBaseValuesByDungeonTier[GameConstants.AncientKeepTier][2];
	}

	[Signal]
	public delegate void CastWindupStartedEventHandler(string spellName, Texture2D icon, float duration);

	[Signal]
	public delegate void CastWindupEndedEventHandler();

	// ── tuneable exports ──────────────────────────────────────────────────────
	[Export] public float MeleeInterval = 3.0f;
	[Export] public float AcidSpitInterval = 8.0f;
	[Export] public float OozeInterval = 6.0f;
	[Export] public float NovaInterval = 8.0f;
	[Export] public float NovaWindupDuration = 3.0f;
	[Export] public float DetonationZoneInterval = 10.0f;

	[Export] public float MeleeDamage = 25f;
	[Export] public float AcidSpitDamage = 35f;
	[Export] public float NovaDamage = 75f;
	[Export] public float DetonationZoneDamage = 65f;

	// ── internal state ────────────────────────────────────────────────────────
	float _meleeTimer;
	float _acidSpitTimer;
	float _oozeTimer;
	float _novaTimer;
	float _novaWindupTimer;
	float _detonationZoneTimer;

	BossSlimeSlamSpell _slamSpell;
	BossAcidSpitSpell _acidSpitSpell;
	BossCorrosiveOozeSpell _oozeSpell;
	BossToxicNovaSpell _novaSpell;
	BossDetonationZoneSpell _detonationZoneSpell;

	CharacterVisual _visual = null!;
	AudioStreamPlayer _riserPlayer;

	enum PendingAttack
	{
		None,
		Melee,
		AcidSpit,
		Ooze,
		DetonationZone
	}

	PendingAttack _pendingAttack;
	Character _pendingTarget;


	// ── lifecycle ─────────────────────────────────────────────────────────────
	public override void _Ready()
	{
		base._Ready();
		CharacterName = GameConstants.Boss3Name;

		_meleeTimer = MeleeInterval;
		_acidSpitTimer = AcidSpitInterval;
		_oozeTimer = OozeInterval;
		_novaTimer = NovaInterval;
		_detonationZoneTimer = DetonationZoneInterval;

		_slamSpell = new BossSlimeSlamSpell { DamageAmount = MeleeDamage };
		_acidSpitSpell = new BossAcidSpitSpell { DamageAmount = AcidSpitDamage };
		_oozeSpell = new BossCorrosiveOozeSpell();
		_novaSpell = new BossToxicNovaSpell { DamageAmount = NovaDamage };
		_detonationZoneSpell = new BossDetonationZoneSpell { DamageAmount = DetonationZoneDamage };

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

		// ── Toxic Nova wind-up countdown ──────────────────────────────────────
		if (_novaWindupTimer > 0f)
		{
			_novaWindupTimer -= (float)delta;
			if (_novaWindupTimer <= 0f)
				ExecuteNova();
		}

		if (_novaWindupTimer > 0f) return;

		_meleeTimer -= (float)delta;
		_acidSpitTimer -= (float)delta;
		_oozeTimer -= (float)delta;
		_novaTimer -= (float)delta;
		_detonationZoneTimer -= (float)delta;

		if (_pendingAttack != PendingAttack.None) return;

		if (_meleeTimer <= 0f)
		{
			_meleeTimer = MeleeInterval;
			PerformSlam();
		}
		else if (_acidSpitTimer <= 0f)
		{
			_acidSpitTimer = AcidSpitInterval;
			CastAcidSpit();
		}
		else if (_oozeTimer <= 0f)
		{
			_oozeTimer = OozeInterval;
			CastOoze();
		}
		else if (_novaTimer <= 0f)
		{
			_novaTimer = NovaInterval;
			BeginNova();
		}
		else if (_detonationZoneTimer <= 0f)
		{
			_detonationZoneTimer = DetonationZoneInterval;
			CastDetonationZone();
		}
	}

	// ── combat actions ────────────────────────────────────────────────────────

	void PerformSlam()
	{
		var target = SelectCurrentMeleeTarget();
		if (target == null) return;
		_pendingTarget = target;
		_pendingAttack = PendingAttack.Melee;
		_visual.Play("cleave");
	}

	void CastAcidSpit()
	{
		var target = PickRandomPartyMember();
		if (target == null) return;
		_pendingTarget = target;
		_pendingAttack = PendingAttack.AcidSpit;
		_visual.Play("cleave");
	}

	void CastOoze()
	{
		var target = PickRandomPartyMember();
		if (target == null) return;
		_pendingTarget = target;
		_pendingAttack = PendingAttack.Ooze;
		_visual.Play("cleave");
	}

	void CastDetonationZone()
	{
		// The spell always targets the player via ResolveTargets; we still need
		// a non-null explicit target for the pipeline — any alive party member works.
		var anyTarget = FindHealer();
		if (anyTarget == null) return;
		_pendingTarget = anyTarget;
		_pendingAttack = PendingAttack.DetonationZone;
		_visual.Play("cleave");
	}

	void BeginNova()
	{
		_novaWindupTimer = NovaWindupDuration;
		_riserPlayer.Play();
		ParryWindowManager.OpenWindow(_novaSpell.Name, _novaSpell.Icon, NovaWindupDuration);
		EmitSignalCastWindupStarted(_novaSpell.Name, _novaSpell.Icon, NovaWindupDuration);
		_pendingAttack = PendingAttack.None;
		_visual.Play("cleave");
	}

	void ExecuteNova()
	{
		EmitSignalCastWindupEnded();

		if (ParryWindowManager.ConsumeResult())
		{
			GD.Print("[DemonSlime] Toxic Nova was deflected!");
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
				PendingAttack.Melee => _slamSpell,
				PendingAttack.AcidSpit => _acidSpitSpell,
				PendingAttack.Ooze => _oozeSpell,
				PendingAttack.DetonationZone => _detonationZoneSpell,
				_ => null
			};
			if (spell != null)
				SpellPipeline.Cast(spell, this, _pendingTarget);
		}

		_pendingTarget = null;
		_pendingAttack = PendingAttack.None;
		_visual.Play("idle");
	}

	/// <summary>Rune of Time: scale all ability intervals by the haste multiplier.</summary>
	protected override void OnApplyHasteRune()
	{
		MeleeInterval /= GameConstants.RuneTimeHasteMultiplier;
		AcidSpitInterval /= GameConstants.RuneTimeHasteMultiplier;
		OozeInterval /= GameConstants.RuneTimeHasteMultiplier;
		NovaInterval /= GameConstants.RuneTimeHasteMultiplier;
		DetonationZoneInterval /= GameConstants.RuneTimeHasteMultiplier;
	}

}
