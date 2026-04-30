using System.Collections.Generic;
using Godot;
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
/// Animations use individual PNG frames loaded at runtime from:
///   res://assets/enemies/demon-slime/individual sprites/
///
///   "idle"   — 6 frames  (01_demon_idle/demon_idle_{n}.png),   looping
///   "cleave" — 15 frames (03_demon_cleave/demon_cleave_{n}.png), one-shot → idle
/// </summary>
public partial class DemonSlime : Character
{
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

	[Export] public float MeleeDamage = 40f;
	[Export] public float AcidSpitDamage = 55f;
	[Export] public float NovaDamage = 75f;
	[Export] public float DetonationZoneDamage = 80f;

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

	AnimatedSprite2D _sprite;
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

	const string IdlePath = "res://assets/enemies/demon-slime/individual sprites/01_demon_idle/demon_idle_";
	const string CleavePath = "res://assets/enemies/demon-slime/individual sprites/03_demon_cleave/demon_cleave_";

	// ── lifecycle ─────────────────────────────────────────────────────────────
	public override void _Ready()
	{
		base._Ready();
		CharacterName = GameConstants.Boss3Name;
		RemoveFromGroup("party");
		AddToGroup(GameConstants.BossGroupName);
		IsFriendly = false;

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

		_sprite = GetNode<AnimatedSprite2D>("AnimatedSprite2D");
		SetupAnimations();
		_sprite.AnimationFinished += OnAnimationFinished;
		_sprite.Play("idle");
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
		var target = FindTank() ?? PickRandomPartyMember();
		if (target == null) return;
		_pendingTarget = target;
		_pendingAttack = PendingAttack.Melee;
		_sprite.Play("cleave");
	}

	void CastAcidSpit()
	{
		var target = PickRandomPartyMember();
		if (target == null) return;
		_pendingTarget = target;
		_pendingAttack = PendingAttack.AcidSpit;
		_sprite.Play("cleave");
	}

	void CastOoze()
	{
		var target = PickRandomPartyMember();
		if (target == null) return;
		_pendingTarget = target;
		_pendingAttack = PendingAttack.Ooze;
		_sprite.Play("cleave");
	}

	void CastDetonationZone()
	{
		// The spell always targets the player via ResolveTargets; we still need
		// a non-null explicit target for the pipeline — any alive party member works.
		var anyTarget = FindHealer();
		if (anyTarget == null) return;
		_pendingTarget = anyTarget;
		_pendingAttack = PendingAttack.DetonationZone;
		_sprite.Play("cleave");
	}

	void BeginNova()
	{
		_novaWindupTimer = NovaWindupDuration;
		_riserPlayer.Play();
		ParryWindowManager.OpenWindow(_novaSpell.Name, _novaSpell.Icon, NovaWindupDuration);
		EmitSignalCastWindupStarted(_novaSpell.Name, _novaSpell.Icon, NovaWindupDuration);
		_pendingAttack = PendingAttack.None;
		_sprite.Play("cleave");
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
		_sprite.Play("idle");
	}

	// ── targeting helpers ─────────────────────────────────────────────────────
	Character FindHealer()
	{
		foreach (var node in GetTree().GetNodesInGroup("party"))
			if (node is Character c && c.CharacterName == GameConstants.HealerName && c.IsAlive)
				return c;
		return null;
	}

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

	void SetupAnimations()
	{
		var frames = new SpriteFrames();
		frames.RemoveAnimation("default");

		AddAnimFromFiles(frames, "idle", IdlePath, 6, 8f, true);
		AddAnimFromFiles(frames, "cleave", CleavePath, 15, 12f, false);

		_sprite.SpriteFrames = frames;
		_sprite.Scale = new Vector2(1.1f, 1.1f);
	}

	static void AddAnimFromFiles(SpriteFrames frames, string animName, string basePath,
		int count, float fps, bool loop)
	{
		frames.AddAnimation(animName);
		frames.SetAnimationLoop(animName, loop);
		frames.SetAnimationSpeed(animName, fps);
		for (var i = 1; i <= count; i++)
		{
			var texture = GD.Load<Texture2D>($"{basePath}{i}.png");
			frames.AddFrame(animName, texture);
		}
	}
}
