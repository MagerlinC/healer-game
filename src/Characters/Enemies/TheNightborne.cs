using System.Collections.Generic;
using Godot;
using healerfantasy;
using healerfantasy.CombatLog;
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
/// • Every <see cref="NightsAdvanceInterval"/> seconds — Night's Advance:
///   a 2-second cast bar gives the player time to pre-shield. On completion the
///   boss vanishes and blinks behind each party member in sequence, appearing in
///   a burst of star-dust and striking each target for 60 damage, before
///   snapping back to its origin. Not deflectable — the intended counter-play is
///   shielding or healing targets above the strike threshold before the cast
///   completes.
///
/// Animations loaded from individual PNGs extracted from the source GIFs:
///   res://assets/enemies/the-nightborne/frames/{anim}/{anim}_{n}.png
///   "idle"   — 9 frames,  looping
///   "attack" — 12 frames, one-shot → idle
///   "run"    — 6 frames,  one-shot → idle  (used as Umbral Eruption charge)
///   "hurt"   — 5 frames,  one-shot → idle
///   "death"  — 23 frames, one-shot, no return
/// </summary>
public partial class TheNightborne : EnemyCharacter
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

	[Export] public float NightsAdvanceInterval = 20.0f;

	[Export] public float ShadowStrikeDamage = 50f;
	[Export] public float VoidLanceDamage = 40f;
	[Export] public float UmbralDamage = 90f;
	[Export] public float NightsAdvanceDamage = 60f;

	// ── internal state ────────────────────────────────────────────────────────

	float _shadowStrikeTimer;
	float _voidLanceTimer;
	float _nightVeilTimer;
	float _umbralTimer;
	float _umbralWindupTimer;
	float _nightsAdvanceTimer;

	// ── Night's Advance state ─────────────────────────────────────────────────
	NightsAdvancePhase _nightsAdvancePhase;
	float _nightsAdvancePhaseTimer;
	readonly List<Character> _nightsAdvanceTargets = new();
	int _nightsAdvanceTargetIndex;
	Vector2 _nightsAdvanceOrigin;

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

	enum NightsAdvancePhase
	{
		None,
		Windup, // 2-second cast bar; player window to pre-shield
		Striking // sequentially blinking to each target
	}

	const float NightsAdvanceWindupDuration = 2.0f;

	// Time allocated per target — long enough for the attack animation (~1 s) plus a beat.
	const float NightsAdvanceStrikeDelay = 1.3f;
	const string StarDustTexturePath = "res://assets/enemies/the-nightborne/stardust.png";

	PendingAttack _pendingAttack;
	Character _pendingTarget;

	const string FrameBase = "res://assets/enemies/the-nightborne/frames/";

	// ── lifecycle ─────────────────────────────────────────────────────────────

	public override void _Ready()
	{
		base._Ready();
		CharacterName = GameConstants.SanctumBoss1Name;

		// Stagger initial timers so attacks don't all fire simultaneously.
		_shadowStrikeTimer = ShadowStrikeInterval;
		_voidLanceTimer = VoidLanceInterval;
		_nightVeilTimer = NightVeilInterval;
		_umbralTimer = UmbralEruptionInterval;
		_nightsAdvanceTimer = 8f;

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

		// ── Night's Advance state machine — blocks all other attacks while active ──
		if (_nightsAdvancePhase != NightsAdvancePhase.None)
		{
			UpdateNightsAdvance((float)delta);
			return;
		}

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
		_nightsAdvanceTimer -= (float)delta;

		if (_pendingAttack != PendingAttack.None) return;

		if (_nightsAdvanceTimer <= 0f)
		{
			_nightsAdvanceTimer = NightsAdvanceInterval;
			BeginNightsAdvance();
		}
		else if (_shadowStrikeTimer <= 0f)
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
		var target = SelectCurrentMeleeTarget();
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
		RaiseBossSpellTargets(target.CharacterName);
		_sprite.Play("attack");
	}

	void CastNightVeil()
	{
		var target = PickRandomPartyMember();
		if (target == null) return;
		_pendingTarget = target;
		_pendingAttack = PendingAttack.NightVeil;
		RaiseBossSpellTargets(target.CharacterName);
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

		// Don't return to idle during the death animation or Night's Advance strikes.
		if (IsAlive && _nightsAdvancePhase == NightsAdvancePhase.None)
			_sprite.Play("idle");
	}

	// ── targeting helpers ─────────────────────────────────────────────────────

	// ── Night's Advance ───────────────────────────────────────────────────────

	/// <summary>
	/// Kicks off Night's Advance — collects targets, shows a 2-second cast bar,
	/// then blinks to each party member in sequence.
	/// </summary>
	void BeginNightsAdvance()
	{
		// Snapshot alive party members now so the player knows who will be hit
		// and can pre-shield them during the 2-second cast window.
		_nightsAdvanceTargets.Clear();
		foreach (var node in GetTree().GetNodesInGroup("party"))
			if (node is Character c && c.IsAlive)
				_nightsAdvanceTargets.Add(c);

		if (_nightsAdvanceTargets.Count == 0) return;

		_nightsAdvanceTargetIndex = 0;
		_nightsAdvanceOrigin = GlobalPosition;
		_nightsAdvancePhase = NightsAdvancePhase.Windup;
		_nightsAdvancePhaseTimer = NightsAdvanceWindupDuration;

		// Announce ALL targets during the 2-second windup so the player can
		// see who will be struck and pre-shield them in time.
		var targetNames = new string[_nightsAdvanceTargets.Count];
		for (var i = 0; i < _nightsAdvanceTargets.Count; i++)
			targetNames[i] = _nightsAdvanceTargets[i].CharacterName;
		RaiseBossSpellTargets(targetNames);

		// Notify the UI so the cast bar is displayed (not parryable — the player
		// counter-play is shielding or healing targets before the cast ends).
		EmitSignalCastWindupStarted("Night's Advance", null, NightsAdvanceWindupDuration);
		_sprite.Play("idle"); // no dedicated cast animation — boss broods in place
	}

	/// <summary>Called every frame while Night's Advance is active.</summary>
	void UpdateNightsAdvance(float delta)
	{
		_nightsAdvancePhaseTimer -= delta;
		if (_nightsAdvancePhaseTimer > 0f) return;

		switch (_nightsAdvancePhase)
		{
			case NightsAdvancePhase.Windup:
				EmitSignalCastWindupEnded();
				ExecuteNightsAdvanceStrike();
				break;

			case NightsAdvancePhase.Striking:
				_nightsAdvanceTargetIndex++;
				if (_nightsAdvanceTargetIndex < _nightsAdvanceTargets.Count)
					ExecuteNightsAdvanceStrike();
				else
					FinishNightsAdvance();
				break;
		}
	}

	/// <summary>
	/// Teleports to the current target, spawns the star-dust burst, deals damage,
	/// then waits <see cref="NightsAdvanceStrikeDelay"/> before the next strike.
	/// </summary>
	void ExecuteNightsAdvanceStrike()
	{
		var target = _nightsAdvanceTargets[_nightsAdvanceTargetIndex];

		// Narrow the highlight to just this target as the boss blinks to them,
		// giving the player a clear visual cue for each sequential strike.
		RaiseBossSpellTargets(target.CharacterName);

		// Teleport the boss to the target's position, slightly behind them.
		GlobalPosition = target.GlobalPosition + new Vector2(-40f, 0f);
		SpawnStarDust(target.GlobalPosition);
		_sprite.Play("attack");

		if (target.IsAlive)
		{
			target.TakeDamage(NightsAdvanceDamage);
			target.RaiseFloatingCombatText(NightsAdvanceDamage, false, (int)SpellSchool.Void, false);
			CombatLog.Record(new CombatEventRecord
			{
				Timestamp = Time.GetTicksMsec() / 1000.0,
				SourceName = CharacterName,
				TargetName = target.CharacterName,
				AbilityName = "Night's Advance",
				Amount = NightsAdvanceDamage,
				Type = CombatEventType.Damage,
				IsCrit = false,
				Description =
					"The Nightborne blinks behind its target in a burst of star-dust, striking from the shadows."
			});
		}

		_nightsAdvancePhase = NightsAdvancePhase.Striking;
		_nightsAdvancePhaseTimer = NightsAdvanceStrikeDelay;
	}

	/// <summary>Boss snaps back to its original position and resumes idle.</summary>
	void FinishNightsAdvance()
	{
		_nightsAdvanceTargets.Clear();
		_nightsAdvancePhase = NightsAdvancePhase.None;
		GlobalPosition = _nightsAdvanceOrigin;
		_sprite.Play("idle");
		// Clear spell-target highlight; the default melee (Templar) outline resumes.
		RaiseBossSpellTargets();
	}

	/// <summary>
	/// Spawns a star-dust Sprite2D at <paramref name="worldPosition"/> that fades
	/// out over the strike delay window then removes itself.
	/// A circular radial shader masks the hard square edges of the texture.
	/// </summary>
	void SpawnStarDust(Vector2 worldPosition)
	{
		var texture = GD.Load<Texture2D>(StarDustTexturePath);

		// Circular soft-edge shader: fades the texture to transparent from 35 % radius
		// outward, so it reads as a cloud burst rather than a square decal.
		var shader = new Shader();
		shader.Code =
			"shader_type canvas_item;\n" +
			"void fragment() {\n" +
			"    float dist = length(UV - vec2(0.5));\n" +
			"    float mask = 1.0 - smoothstep(0.25, 0.5, dist);\n" +
			"    COLOR = texture(TEXTURE, UV) * vec4(1.0, 1.0, 1.0, mask);\n" +
			"}\n";
		var mat = new ShaderMaterial { Shader = shader };

		var dust = new Sprite2D
		{
			Texture = texture,
			GlobalPosition = worldPosition,
			ZIndex = ZIndex - 1,
			// 0.35 is a reasonable character-sized burst — tune if needed.
			Scale = new Vector2(0.35f, 0.35f),
			// Start semi-transparent so it blends rather than blasts.
			Modulate = new Color(1f, 1f, 1f, 0.80f),
			Material = mat
		};
		GetParent().AddChild(dust);

		// Stay briefly, then fade out over the rest of the window.
		var tween = dust.CreateTween();
		tween.TweenInterval(NightsAdvanceStrikeDelay * 0.25f);
		tween.TweenProperty(dust, "modulate:a", 0f, NightsAdvanceStrikeDelay * 0.75f);
		tween.TweenCallback(Callable.From(dust.QueueFree));
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
		NightsAdvanceInterval /= GameConstants.RuneTimeHasteMultiplier;
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
