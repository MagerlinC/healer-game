using System.Collections.Generic;
using Godot;
using healerfantasy;
using healerfantasy.SpellResources;
using healerfantasy.SpellSystem;

/// <summary>
/// The Flying Skull — third and final boss of The Forsaken Citadel.
///
/// Behaviour
/// ─────────
/// • Stands still (no movement).
/// • Every <see cref="MeleeInterval"/> seconds — Death Chomp: the skull
///   snaps at the tank for heavy void damage; uses the "attack" animation.
/// • Every <see cref="ScreechInterval"/> seconds — Void Screech: fires a
///   bolt of void energy at a random party member; uses the "cast" animation.
/// • Every <see cref="PoolInterval"/> seconds — Necrotic Pool: plants a
///   lingering area-of-effect void pool on the healer's position that pulses
///   20 damage per second for 4 seconds; uses the "cast" animation.
/// • Every <see cref="WailInterval"/> seconds — Banshee Wail: a 3.5-second
///   telegraphed AoE that unleashes a devastating void wail across all party
///   members for 65 damage unless the player deflects in time;
///   uses the "cast" animation.
///
/// Animations use individual PNG frames from:
///   res://assets/enemies/flying-skull/{anim}{n}.png
///   idle   (3 frames, looping)
///   attack (5 frames, one-shot → idle)
///   cast   (4 frames, one-shot → idle)
/// </summary>
public partial class FlyingSkull : Character
{
	public FlyingSkull()
	{
		MaxHealth = GameConstants.BossHealthBaseValuesByDungeonTier[GameConstants.ForsakenCitadelTier][2];
	}

	// ── signals ───────────────────────────────────────────────────────────────

	[Signal]
	public delegate void CastWindupStartedEventHandler(string spellName, Texture2D icon, float duration);

	[Signal]
	public delegate void CastWindupEndedEventHandler();

	// ── tuneable exports ──────────────────────────────────────────────────────

	[Export] public float MeleeInterval = 2.0f;
	[Export] public float ScreechInterval = 5.0f;
	[Export] public float PoolInterval = 11.0f;
	[Export] public float WailInterval = 16.0f;
	[Export] public float WailWindup = 3.5f;

	[Export] public float MeleeDamage = 50f;
	[Export] public float ScreechDamage = 38f;
	[Export] public float PoolPulse = 20f;
	[Export] public float WailDamage = 65f;

	// ── internal state ────────────────────────────────────────────────────────

	float _meleeTimer;
	float _screechTimer;
	float _poolTimer;
	float _wailTimer;
	float _wailWindupTimer;

	BossDeathChompSpell _deathChompSpell;
	BossVoidScreechSpell _voidScreechSpell;
	BossNecroticPoolSpell _necroticPoolSpell;
	BossBansheeWailSpell _bansheeWailSpell;

	AnimatedSprite2D _sprite;
	AudioStreamPlayer _riserPlayer;

	enum PendingAttack
	{
		None,
		Melee,
		VoidScreech,
		NecroticPool
	}

	PendingAttack _pendingAttack;
	Character _pendingTarget;

	const string BasePath = "res://assets/enemies/flying-skull/";

	// ── lifecycle ─────────────────────────────────────────────────────────────

	public override void _Ready()
	{
		base._Ready();
		CharacterName = GameConstants.ForsakenBoss3Name;
		RemoveFromGroup("party");
		AddToGroup(GameConstants.BossGroupName);
		IsFriendly = false;

		_meleeTimer = MeleeInterval;
		_screechTimer = ScreechInterval;
		_poolTimer = PoolInterval;
		_wailTimer = WailInterval;

		_deathChompSpell = new BossDeathChompSpell { DamageAmount = MeleeDamage };
		_voidScreechSpell = new BossVoidScreechSpell { DamageAmount = ScreechDamage };
		_necroticPoolSpell = new BossNecroticPoolSpell { DamagePerPulse = PoolPulse };
		_bansheeWailSpell = new BossBansheeWailSpell { DamageAmount = WailDamage };

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

		// ── Banshee Wail wind-up countdown ────────────────────────────────────
		if (_wailWindupTimer > 0f)
		{
			_wailWindupTimer -= (float)delta;
			if (_wailWindupTimer <= 0f)
				ExecuteWail();
			return;
		}

		_meleeTimer -= (float)delta;
		_screechTimer -= (float)delta;
		_poolTimer -= (float)delta;
		_wailTimer -= (float)delta;

		if (_pendingAttack != PendingAttack.None) return;

		if (_meleeTimer <= 0f)
		{
			_meleeTimer = MeleeInterval;
			PerformDeathChomp();
		}
		else if (_screechTimer <= 0f)
		{
			_screechTimer = ScreechInterval;
			CastVoidScreech();
		}
		else if (_poolTimer <= 0f)
		{
			_poolTimer = PoolInterval;
			CastNecroticPool();
		}
		else if (_wailTimer <= 0f)
		{
			_wailTimer = WailInterval;
			BeginWail();
		}
	}

	// ── combat actions ────────────────────────────────────────────────────────

	void PerformDeathChomp()
	{
		var target = FindTank() ?? PickRandomPartyMember();
		if (target == null) return;
		_pendingTarget = target;
		_pendingAttack = PendingAttack.Melee;
		_sprite.Play("attack");
	}

	void CastVoidScreech()
	{
		var target = PickRandomPartyMember();
		if (target == null) return;
		_pendingTarget = target;
		_pendingAttack = PendingAttack.VoidScreech;
		_sprite.Play("cast");
	}

	void CastNecroticPool()
	{
		// Necrotic Pool always targets the healer (handled in ResolveTargets),
		// but we pass any alive member as the nominal target to satisfy the pipeline.
		var target = PickRandomPartyMember();
		if (target == null) return;
		_pendingTarget = target;
		_pendingAttack = PendingAttack.NecroticPool;
		_sprite.Play("cast");
	}

	void BeginWail()
	{
		_wailWindupTimer = WailWindup;
		_riserPlayer.Play();
		ParryWindowManager.OpenWindow();
		EmitSignalCastWindupStarted(_bansheeWailSpell.Name, _bansheeWailSpell.Icon, WailWindup);
		_pendingAttack = PendingAttack.None;
		_sprite.Play("cast");
	}

	void ExecuteWail()
	{
		EmitSignalCastWindupEnded();

		if (ParryWindowManager.ConsumeResult())
		{
			GD.Print("[FlyingSkull] Banshee Wail was deflected!");
			return;
		}

		var anyTarget = PickRandomPartyMember();
		if (anyTarget != null)
			SpellPipeline.Cast(_bansheeWailSpell, this, anyTarget);
	}

	void OnAnimationFinished()
	{
		if (_pendingTarget != null && _pendingTarget.IsAlive)
		{
			SpellResource spell = _pendingAttack switch
			{
				PendingAttack.Melee => _deathChompSpell,
				PendingAttack.VoidScreech => _voidScreechSpell,
				PendingAttack.NecroticPool => _necroticPoolSpell,
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

		AddAnimFromFiles(frames, "idle", 3, 6f, true);
		AddAnimFromFiles(frames, "attack", 5, 10f, false);
		AddAnimFromFiles(frames, "cast", 4, 8f, false);

		_sprite.SpriteFrames = frames;
	}

	void AddAnimFromFiles(SpriteFrames frames, string animName, int count, float fps, bool loop)
	{
		frames.AddAnimation(animName);
		frames.SetAnimationLoop(animName, loop);
		frames.SetAnimationSpeed(animName, fps);
		for (var i = 1; i <= count; i++)
		{
			var path = $"{BasePath}{animName}{i}.png";
			var texture = GD.Load<Texture2D>(path);
			frames.AddFrame(animName, texture);
		}
	}
}