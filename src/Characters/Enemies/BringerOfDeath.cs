using System.Collections.Generic;
using Godot;
using healerfantasy;
using healerfantasy.SpellResources;
using healerfantasy.SpellSystem;

/// <summary>
/// The Bringer of Death — second boss encounter.
///
/// Behaviour
/// ─────────
/// • Stands still (no movement).
/// • Every <see cref="MeleeInterval"/> seconds — Deathbolt: necrotic melee
///   strike against the tank; uses the "attack" animation.
/// • Every <see cref="SoulRendInterval"/> seconds — Soul Rend: void-damage
///   bolt fired at a random party member; uses the "cast" animation.
/// • Every <see cref="DeathMarkInterval"/> seconds — Death Mark: brands a
///   random party member with a 10 dmg/s DoT for 12 seconds (dispellable);
///   uses the "spell" animation.
/// • Every <see cref="EmbraceInterval"/> seconds — Embrace of Death: a
///   3.5-second telegraphed AoE that hits all party members for 45 damage
///   unless the player casts Deflect in time; uses the "spell" animation.
///
/// Animations use individual PNG frames loaded at runtime from:
///   res://assets/enemies/bringer-of-death/Individual Sprite/{Anim}/
///   Bringer-of-Death_{Anim}_{n}.png
///
///   "idle"   — 8 frames, looping
///   "attack" — 10 frames, one-shot → idle
///   "cast"   — 9 frames, one-shot → idle
///   "spell"  — 16 frames, one-shot → idle
/// </summary>
public partial class BringerOfDeath : Character
{
	public BringerOfDeath()
	{
		MaxHealth = GameConstants.BossHealthBaseValuesByDungeonTier[GameConstants.AncientKeepTier][1];
	}
	// ── signals ───────────────────────────────────────────────────────────────

	[Signal]
	public delegate void CastWindupStartedEventHandler(string spellName, Texture2D icon, float duration);

	[Signal]
	public delegate void CastWindupEndedEventHandler();

	// ── tuneable exports ──────────────────────────────────────────────────────
	[Export] public float MeleeInterval = 3.0f;
	[Export] public float SoulRendInterval = 8.0f;
	[Export] public float DeathMarkInterval = 11.0f;
	[Export] public float EmbraceInterval = 15.0f;
	[Export] public float EmbraceWindupDuration = 3.5f;

	[Export] public float MeleeDamage = 40f;
	[Export] public float SoulRendDamage = 25f;
	[Export] public float EmbraceDamage = 60f;

	// ── internal state ────────────────────────────────────────────────────────
	float _meleeTimer;
	float _soulRendTimer;
	float _deathMarkTimer;
	float _embraceTimer;
	float _embraceWindupTimer;

	BossDeathboltSpell _deathboltSpell;
	BossSoulRendSpell _soulRendSpell;
	BossDeathMarkSpell _deathMarkSpell;
	BossEmbraceOfDeathSpell _embraceSpell;

	AnimatedSprite2D _sprite;
	AudioStreamPlayer _riserPlayer;

	enum PendingAttack
	{
		None,
		Melee,
		SoulRend,
		DeathMark
	}

	PendingAttack _pendingAttack;
	Character _pendingTarget;

	readonly string _riserSoundPath = AssetConstants.DeflectRiserSoundPath;

	// Individual-sprite base path
	const string SpritePath = "res://assets/enemies/bringer-of-death/Individual Sprite/";

	// ── lifecycle ─────────────────────────────────────────────────────────────
	public override void _Ready()
	{
		base._Ready();
		CharacterName = GameConstants.Boss2Name;
		RemoveFromGroup("party");
		AddToGroup(GameConstants.BossGroupName);
		IsFriendly = false;

		// Stagger first attacks so the player has a moment to react.
		_meleeTimer = MeleeInterval;
		_soulRendTimer = SoulRendInterval;
		_deathMarkTimer = DeathMarkInterval;
		_embraceTimer = EmbraceInterval;

		_deathboltSpell = new BossDeathboltSpell { DamageAmount = MeleeDamage };
		_soulRendSpell = new BossSoulRendSpell { DamageAmount = SoulRendDamage };
		_deathMarkSpell = new BossDeathMarkSpell();
		_embraceSpell = new BossEmbraceOfDeathSpell { DamageAmount = EmbraceDamage };

		GlobalAutoLoad.RegisterSignalEmitter(this, nameof(CastWindupStarted));
		GlobalAutoLoad.RegisterSignalEmitter(this, nameof(CastWindupEnded));

		_riserPlayer = new AudioStreamPlayer();
		_riserPlayer.Stream = GD.Load<AudioStream>(_riserSoundPath);
		AddChild(_riserPlayer);

		_sprite = GetNode<AnimatedSprite2D>("AnimatedSprite2D");
		_sprite.Scale = new Vector2(1.5f, 1.5f);
		SetupAnimations();
		_sprite.AnimationFinished += OnAnimationFinished;
		_sprite.Play("idle");
		ApplyRuneModifiers();
	}

	public override void _Process(double delta)
	{
		base._Process(delta);
		if (!IsAlive) return;

		// ── Embrace of Death wind-up countdown ────────────────────────────────
		if (_embraceWindupTimer > 0f)
		{
			_embraceWindupTimer -= (float)delta;
			if (_embraceWindupTimer <= 0f)
				ExecuteEmbrace();
		}

		if (_embraceWindupTimer > 0f) return;

		_meleeTimer -= (float)delta;
		_soulRendTimer -= (float)delta;
		_deathMarkTimer -= (float)delta;
		_embraceTimer -= (float)delta;

		if (_pendingAttack != PendingAttack.None) return;

		if (_meleeTimer <= 0f)
		{
			_meleeTimer = MeleeInterval;
			PerformDeathbolt();
		}
		else if (_soulRendTimer <= 0f)
		{
			_soulRendTimer = SoulRendInterval;
			CastSoulRend();
		}
		else if (_deathMarkTimer <= 0f)
		{
			_deathMarkTimer = DeathMarkInterval;
			CastDeathMark();
		}
		else if (_embraceTimer <= 0f)
		{
			_embraceTimer = EmbraceInterval;
			BeginEmbrace();
		}
	}

	// ── combat actions ────────────────────────────────────────────────────────

	void PerformDeathbolt()
	{
		var target = FindTank() ?? PickRandomPartyMember();
		if (target == null) return;
		_pendingTarget = target;
		_pendingAttack = PendingAttack.Melee;
		_sprite.Play("attack");
	}

	void CastSoulRend()
	{
		var target = PickRandomPartyMember();
		if (target == null) return;
		_pendingTarget = target;
		_pendingAttack = PendingAttack.SoulRend;
		_sprite.Play("cast");
	}

	void CastDeathMark()
	{
		var target = PickRandomPartyMember();
		if (target == null) return;
		_pendingTarget = target;
		_pendingAttack = PendingAttack.DeathMark;
		_sprite.Play("spell");
	}

	void BeginEmbrace()
	{
		_embraceWindupTimer = EmbraceWindupDuration;
		_riserPlayer.Play();
		ParryWindowManager.OpenWindow(_embraceSpell.Name, _embraceSpell.Icon, EmbraceWindupDuration);
		EmitSignalCastWindupStarted(_embraceSpell.Name, _embraceSpell.Icon, EmbraceWindupDuration);
		_pendingAttack = PendingAttack.None;
		_sprite.Play("spell");
	}

	void ExecuteEmbrace()
	{
		EmitSignalCastWindupEnded();

		if (ParryWindowManager.ConsumeResult())
		{
			GD.Print("[BringerOfDeath] Embrace of Death was deflected!");
			return;
		}

		var anyTarget = PickRandomPartyMember();
		if (anyTarget != null)
			SpellPipeline.Cast(_embraceSpell, this, anyTarget);
	}

	void OnAnimationFinished()
	{
		if (_pendingTarget != null && _pendingTarget.IsAlive)
		{
			SpellResource spell = _pendingAttack switch
			{
				PendingAttack.Melee => _deathboltSpell,
				PendingAttack.SoulRend => _soulRendSpell,
				PendingAttack.DeathMark => _deathMarkSpell,
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
			if (node is Character c && c.CharacterName == "Templar" && c.IsAlive)
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
	/// Builds SpriteFrames from individual PNG files at runtime.
	/// File pattern: {SpritePath}{AnimName}/Bringer-of-Death_{AnimName}_{n}.png
	/// </summary>
	/// <summary>Rune of Time: scale all ability intervals by the haste multiplier.</summary>
	protected override void OnApplyHasteRune()
	{
		MeleeInterval /= GameConstants.RuneTimeHasteMultiplier;
		SoulRendInterval /= GameConstants.RuneTimeHasteMultiplier;
		DeathMarkInterval /= GameConstants.RuneTimeHasteMultiplier;
		EmbraceInterval /= GameConstants.RuneTimeHasteMultiplier;
	}

		void SetupAnimations()
	{
		var frames = new SpriteFrames();
		frames.RemoveAnimation("default");

		AddAnimFromFiles(frames, "idle", "Idle", 8, 8f, true);
		AddAnimFromFiles(frames, "attack", "Attack", 10, 12f, false);
		AddAnimFromFiles(frames, "cast", "Cast", 9, 10f, false);
		AddAnimFromFiles(frames, "spell", "Spell", 16, 10f, false);

		_sprite.SpriteFrames = frames;
		_sprite.Scale = new Vector2(1.2f, 1.2f);
	}

	static void AddAnimFromFiles(SpriteFrames frames, string animName, string folderName,
		int count, float fps, bool loop)
	{
		frames.AddAnimation(animName);
		frames.SetAnimationLoop(animName, loop);
		frames.SetAnimationSpeed(animName, fps);
		for (var i = 1; i <= count; i++)
		{
			var path = $"{SpritePath}{folderName}/Bringer-of-Death_{folderName}_{i}.png";
			var texture = GD.Load<Texture2D>(path);
			frames.AddFrame(animName, texture);
		}
	}
}
