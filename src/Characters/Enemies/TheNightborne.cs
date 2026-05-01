using System.Collections.Generic;
using Godot;
using healerfantasy;
using healerfantasy.SpellResources;
using healerfantasy.SpellSystem;

/// <summary>
/// The Nightborne — first boss of the Sanctum of Stars.
///
/// A dark, armour-clad knight wreathed in shadow energy. The most straightforward
/// fight of the Sanctum — a ramp-up encounter to prepare the player for what follows.
///
/// Behaviour
/// ─────────
/// • Every <see cref="ShadowStrikeInterval"/> seconds — Shadow Strike: a heavy
///   necrotic melee hit aimed at the tank; uses the "attack" animation.
/// • Every <see cref="VoidLanceInterval"/> seconds — Void Lance: a bolt of void
///   energy hurled at a random party member; uses the "attack" animation.
/// • Every <see cref="NightVeilInterval"/> seconds — Night Veil: shrouds a random
///   party member in choking shadow (20 dmg/s for 10 s, dispellable);
///   uses the "attack" animation.
/// • Every <see cref="UmbralEruptionInterval"/> seconds — Umbral Eruption:
///   the knight charges with a <see cref="UmbralWindupDuration"/>-second wind-up
///   (the "run" animation plays as a visual charge), then erupts for 90 AoE
///   damage to the whole party — deflectable.
///
/// Animations loaded from individual PNGs extracted from the source GIFs:
///   res://assets/enemies/the-nightborne/frames/{anim}/{anim}_{n}.png
///   "idle"   — 9 frames,  looping
///   "attack" — 12 frames, one-shot → idle
///   "run"    — 6 frames,  one-shot → idle  (used as Umbral Eruption charge)
///   "hurt"   — 5 frames,  one-shot → idle
///   "death"  — 23 frames, one-shot, no return
/// </summary>
public partial class TheNightborne : Character
{
	public TheNightborne()
	{
		MaxHealth = GameConstants.BossHealthBaseValuesByDungeonTier[GameConstants.SanctumOfStarsTier][0];
	}

	// ── signals ───────────────────────────────────────────────────────────────

	[Signal]
	public delegate void CastWindupStartedEventHandler(string spellName, Texture2D icon, float duration);

	[Signal]
	public delegate void CastWindupEndedEventHandler();

	// ── tuneable exports ──────────────────────────────────────────────────────

	[Export] public float ShadowStrikeInterval = 2.5f;
	[Export] public float VoidLanceInterval = 7.0f;
	[Export] public float NightVeilInterval = 10.0f;
	[Export] public float UmbralEruptionInterval = 16.0f;
	[Export] public float UmbralWindupDuration = 3.5f;

	[Export] public float ShadowStrikeDamage = 60f;
	[Export] public float VoidLanceDamage = 45f;
	[Export] public float UmbralDamage = 90f;

	// ── internal state ────────────────────────────────────────────────────────

	float _shadowStrikeTimer;
	float _voidLanceTimer;
	float _nightVeilTimer;
	float _umbralTimer;
	float _umbralWindupTimer;

	BossNightborneShadowStrikeSpell _shadowStrikeSpell;
	BossNightborneVoidLanceSpell _voidLanceSpell;
	BossNightborneNightVeilSpell _nightVeilSpell;
	BossNightborneUmbralEruptionSpell _umbralEruptionSpell;

	AnimatedSprite2D _sprite;
	AudioStreamPlayer _riserPlayer;

	enum PendingAttack
	{
		None,
		ShadowStrike,
		VoidLance,
		NightVeil
	}

	PendingAttack _pendingAttack;
	Character _pendingTarget;

	const string FrameBase = "res://assets/enemies/the-nightborne/frames/";

	// ── lifecycle ─────────────────────────────────────────────────────────────

	public override void _Ready()
	{
		base._Ready();
		CharacterName = GameConstants.SanctumBoss1Name;
		RemoveFromGroup("party");
		AddToGroup(GameConstants.BossGroupName);
		IsFriendly = false;

		// Stagger initial timers so attacks don't all fire simultaneously.
		_shadowStrikeTimer = ShadowStrikeInterval;
		_voidLanceTimer = VoidLanceInterval;
		_nightVeilTimer = NightVeilInterval;
		_umbralTimer = UmbralEruptionInterval;

		_shadowStrikeSpell = new BossNightborneShadowStrikeSpell { DamageAmount = ShadowStrikeDamage };
		_voidLanceSpell = new BossNightborneVoidLanceSpell { DamageAmount = VoidLanceDamage };
		_nightVeilSpell = new BossNightborneNightVeilSpell();
		_umbralEruptionSpell = new BossNightborneUmbralEruptionSpell { DamageAmount = UmbralDamage };

		GlobalAutoLoad.RegisterSignalEmitter(this, nameof(CastWindupStarted));
		GlobalAutoLoad.RegisterSignalEmitter(this, nameof(CastWindupEnded));

		_riserPlayer = new AudioStreamPlayer();
		_riserPlayer.Stream = GD.Load<AudioStream>(AssetConstants.DeflectRiserSoundPath);
		AddChild(_riserPlayer);

		_sprite = GetNode<AnimatedSprite2D>("AnimatedSprite2D");
		SetupAnimations();
		_sprite.AnimationFinished += OnAnimationFinished;
		_sprite.Play("idle");
		ApplyRuneModifiers();
	}

	public override void _Process(double delta)
	{
		base._Process(delta);
		if (!IsAlive) return;

		// ── Umbral Eruption wind-up countdown ─────────────────────────────────
		if (_umbralWindupTimer > 0f)
		{
			_umbralWindupTimer -= (float)delta;
			if (_umbralWindupTimer <= 0f)
				ExecuteUmbralEruption();
		}

		if (_umbralWindupTimer > 0f) return;

		_shadowStrikeTimer -= (float)delta;
		_voidLanceTimer -= (float)delta;
		_nightVeilTimer -= (float)delta;
		_umbralTimer -= (float)delta;

		if (_pendingAttack != PendingAttack.None) return;

		if (_shadowStrikeTimer <= 0f)
		{
			_shadowStrikeTimer = ShadowStrikeInterval;
			PerformShadowStrike();
		}
		else if (_voidLanceTimer <= 0f)
		{
			_voidLanceTimer = VoidLanceInterval;
			CastVoidLance();
		}
		else if (_nightVeilTimer <= 0f)
		{
			_nightVeilTimer = NightVeilInterval;
			CastNightVeil();
		}
		else if (_umbralTimer <= 0f)
		{
			_umbralTimer = UmbralEruptionInterval;
			BeginUmbralEruption();
		}
	}

	// ── combat actions ────────────────────────────────────────────────────────

	void PerformShadowStrike()
	{
		var target = FindTank() ?? PickRandomPartyMember();
		if (target == null) return;
		_pendingTarget = target;
		_pendingAttack = PendingAttack.ShadowStrike;
		_sprite.Play("attack");
	}

	void CastVoidLance()
	{
		var target = PickRandomPartyMember();
		if (target == null) return;
		_pendingTarget = target;
		_pendingAttack = PendingAttack.VoidLance;
		_sprite.Play("attack");
	}

	void CastNightVeil()
	{
		var target = PickRandomPartyMember();
		if (target == null) return;
		_pendingTarget = target;
		_pendingAttack = PendingAttack.NightVeil;
		_sprite.Play("attack");
	}

	void BeginUmbralEruption()
	{
		_umbralWindupTimer = UmbralWindupDuration;
		_riserPlayer.Play();
		ParryWindowManager.OpenWindow(_umbralEruptionSpell.Name, _umbralEruptionSpell.Icon, UmbralWindupDuration);
		EmitSignalCastWindupStarted(_umbralEruptionSpell.Name, _umbralEruptionSpell.Icon, UmbralWindupDuration);
		_pendingAttack = PendingAttack.None; // wind-up timer drives this, not animation finish
		_sprite.Play("run"); // charge-up visual: the knight surges forward
	}

	void ExecuteUmbralEruption()
	{
		EmitSignalCastWindupEnded();

		if (ParryWindowManager.ConsumeResult())
		{
			GD.Print("[TheNightborne] Umbral Eruption was deflected!");
			_sprite.Play("idle");
			return;
		}

		var anyTarget = PickRandomPartyMember();
		if (anyTarget != null)
			SpellPipeline.Cast(_umbralEruptionSpell, this, anyTarget);

		_sprite.Play("idle");
	}

	void OnAnimationFinished()
	{
		if (_pendingTarget != null && _pendingTarget.IsAlive)
		{
			SpellResource spell = _pendingAttack switch
			{
				PendingAttack.ShadowStrike => _shadowStrikeSpell,
				PendingAttack.VoidLance => _voidLanceSpell,
				PendingAttack.NightVeil => _nightVeilSpell,
				_ => null
			};
			if (spell != null)
				SpellPipeline.Cast(spell, this, _pendingTarget);
		}

		_pendingTarget = null;
		_pendingAttack = PendingAttack.None;

		// Don't return to idle during the death animation.
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
	/// Builds SpriteFrames from individual PNGs extracted from the source GIFs.
	/// Path pattern: res://assets/enemies/the-nightborne/frames/{anim}/{anim}_{n}.png
	/// </summary>
	/// <summary>Rune of Time: scale all ability intervals by the haste multiplier.</summary>
	protected override void OnApplyHasteRune()
	{
		ShadowStrikeInterval /= GameConstants.RuneTimeHasteMultiplier;
		VoidLanceInterval /= GameConstants.RuneTimeHasteMultiplier;
		NightVeilInterval /= GameConstants.RuneTimeHasteMultiplier;
		UmbralEruptionInterval /= GameConstants.RuneTimeHasteMultiplier;
	}

		void SetupAnimations()
	{
		var frames = new SpriteFrames();
		frames.RemoveAnimation("default");

		AddAnimFromFiles(frames, "idle", 9, 8f, true);
		AddAnimFromFiles(frames, "attack", 12, 12f, false);
		AddAnimFromFiles(frames, "run", 6, 10f, false);
		AddAnimFromFiles(frames, "hurt", 5, 10f, false);
		AddAnimFromFiles(frames, "death", 23, 10f, false);

		_sprite.SpriteFrames = frames;
		_sprite.Scale = new Vector2(1.2f, 1.2f);
	}

	static void AddAnimFromFiles(SpriteFrames frames, string animName,
		int count, float fps, bool loop)
	{
		frames.AddAnimation(animName);
		frames.SetAnimationLoop(animName, loop);
		frames.SetAnimationSpeed(animName, fps);
		for (var i = 1; i <= count; i++)
		{
			var path = $"{FrameBase}{animName}/{animName}_{i}.png";
			var texture = GD.Load<Texture2D>(path);
			frames.AddFrame(animName, texture);
		}
	}
}