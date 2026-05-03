using Godot;
using healerfantasy;
using healerfantasy.Effects;
using healerfantasy.Runes;
using healerfantasy.SpellResources;
using healerfantasy.SpellSystem;

public partial class QueenOfTheFrozenWastes : Character
{
	public QueenOfTheFrozenWastes()
	{
		MaxHealth = GameConstants.BossHealthBaseValuesByDungeonTier[GameConstants.FrozenPeakTier][0];
	}

	// ── signals ───────────────────────────────────────────────────────────────

	/// <summary>Emitted when the Queen begins or ends a pre-channel cast (shared cast bar).</summary>
	[Signal]
	public delegate void CastWindupStartedEventHandler(string spellName, Texture2D icon, float duration);

	[Signal]
	public delegate void CastWindupEndedEventHandler();

	/// <summary>Emitted when the Snowstorm channel begins — feeds the BossCastBar channel mode.</summary>
	[Signal]
	public delegate void SnowstormChannelStartedEventHandler(BossQueenSnowstormSpell spell);

	/// <summary>Emitted when the Snowstorm channel ends naturally or is interrupted.</summary>
	[Signal]
	public delegate void SnowstormChannelEndedEventHandler();

	// ── tuneable exports ──────────────────────────────────────────────────────

	[Export] public float MeleeAttackInterval = 3f;
	[Export] public float MeleeDamage = 25f;

	[Export] public float SnowstormInterval = 18f;
	[Export] public float IcicleInterval = 12f;
	[Export] public float BurstOfWinterInterval = 26f;
	[Export] public float IceBlockInterval = 32f;

	[Export] public float SnowstormDamagePerTick = 20f;
	[Export] public float IcicleDamagePerTick = 15f;
	[Export] public float BurstOfWinterDamage = 50f;
	[Export] public float IceBlockShieldAmount = 500f;
	[Export] public float AbsoluteZeroDuration = 8f;

	// ── internal state ────────────────────────────────────────────────────────

	float _meleeTimer;
	float _snowstormTimer;
	float _icicleTimer;
	float _burstOfWinterTimer;
	float _iceBlockTimer;

	bool _isSnowstormChanneling;
	bool _isInIceBlock;
	float _absoluteZeroTimer;

	// ── Frostbite debuff ──────────────────────────────────────────────────────
	bool _frostbiteApplied;
	float _frostbiteStackTimer;
	FrostbiteEffect _frostbiteEffect;

	// ── Cone of Cold phase ────────────────────────────────────────────────────
	bool _isInvulnerable;
	bool _isMidPhase;
	bool _phase1Triggered;
	bool _phase2Triggered;

	/// <summary>
	/// Counts down the remaining wind-up time for the current cast.
	/// Resolution happens here in <c>_Process</c> — not in
	/// <c>OnAnimationFinished</c> — so that <see cref="SpellResource.CastTime"/>
	/// is the true arbiter of how long the cast takes, regardless of animation
	/// speed or frame count.
	/// </summary>
	float _castWindupTimer;

	BossQueenIcyStrikeSpell _meleeSpell;
	BossQueenSnowstormSpell _snowstormSpell;
	BossQueenVolatileIcicleSpell _icicleSpell;
	BossQueenBurstOfWinterSpell _burstOfWinterSpell;
	BossQueenIceBlockSpell _iceBlockSpell;
	BossQueenAbsoluteZeroSpell _absoluteZeroSpell;

	AnimatedSprite2D _sprite;

	enum PendingCast
	{
		None,
		Snowstorm,
		Icicle,
		BurstOfWinter,
		Melee
	}

	PendingCast _pendingCast;

	const string AssetBase = "res://assets/enemies/queen-of-the-frozen-wastes/";

	// ── invulnerability ───────────────────────────────────────────────────────

	/// <summary>
	/// Blocks all incoming damage while the Cone of Cold phase is active.
	/// The shield-break / Ice Block check still uses the raw shield value and
	/// is unaffected by this flag.
	/// </summary>
	public override void TakeDamage(float amount)
	{
		if (_isInvulnerable) return;
		base.TakeDamage(amount);
	}

	// ── lifecycle ─────────────────────────────────────────────────────────────

	public override void _Ready()
	{
		base._Ready();
		CharacterName = GameConstants.FrozenPeakBossName;
		RemoveFromGroup("party");
		AddToGroup(GameConstants.BossGroupName);
		IsFriendly = false;

		// Stagger first casts so the fight has a brief breathing window.
		_meleeTimer = MeleeAttackInterval;
		_snowstormTimer = 12f;
		_icicleTimer = 4f;
		_burstOfWinterTimer = 24f;
		_iceBlockTimer = 32f;

		// Frostbite: first stack-check fires after 1 second.
		_frostbiteStackTimer = 1f;

		_meleeSpell = new BossQueenIcyStrikeSpell { DamageAmount = MeleeDamage };
		_snowstormSpell = new BossQueenSnowstormSpell { Boss = this, DamagePerTick = SnowstormDamagePerTick };
		_icicleSpell = new BossQueenVolatileIcicleSpell { Boss = this, ZoneDamagePerTick = IcicleDamagePerTick };
		_burstOfWinterSpell = new BossQueenBurstOfWinterSpell { Boss = this, Damage = BurstOfWinterDamage };
		_iceBlockSpell = new BossQueenIceBlockSpell { Boss = this, IceBlockShield = IceBlockShieldAmount };
		_absoluteZeroSpell = new BossQueenAbsoluteZeroSpell { DamageAmount = 1000f, CastDuration = AbsoluteZeroDuration };

		GlobalAutoLoad.RegisterSignalEmitter(this, nameof(CastWindupStarted));
		GlobalAutoLoad.RegisterSignalEmitter(this, nameof(CastWindupEnded));
		GlobalAutoLoad.RegisterSignalEmitter(this, nameof(SnowstormChannelStarted));
		GlobalAutoLoad.RegisterSignalEmitter(this, nameof(SnowstormChannelEnded));

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

		// ── Frostbite — apply on first tick, then manage stacks ───────────────
		if (!_frostbiteApplied && RunState.Instance.IsRuneActive(RuneIndex.Purity))
			ApplyFrostbiteToHealer();
		UpdateFrostbiteStacks((float)delta);

		// ── Ice Block — track Absolute Zero countdown ────────────────────────
		if (_isInIceBlock)
		{
			// Shield broken by damage → cancel Ice Block.
			if (CurrentShield <= 0f)
			{
				EndIceBlock(false);
				return;
			}

			_absoluteZeroTimer -= (float)delta;
			if (_absoluteZeroTimer <= 0f)
				EndIceBlock(true);

			return; // no other abilities while encased
		}

		// ── Cone of Cold phase triggers ────────────────────────────────────────────
		// Check health thresholds before any ability logic. Once a phase begins
		// _isMidPhase suppresses the rest of _Process until the phase completes.
		if (!_isMidPhase && !_isInIceBlock)
		{
			if (!_phase1Triggered && CurrentHealth <= MaxHealth * 0.66f)
			{
				_phase1Triggered = true;
				TriggerConeOfColdPhase();
				return;
			}

			if (!_phase2Triggered && CurrentHealth <= MaxHealth * 0.33f)
			{
				_phase2Triggered = true;
				TriggerConeOfColdPhase();
				return;
			}
		}

		if (_isMidPhase) return;

		// ── Attack timers ─────────────────────────────────────────────────────
		_meleeTimer -= (float)delta;
		_snowstormTimer -= (float)delta;
		_icicleTimer -= (float)delta;
		_burstOfWinterTimer -= (float)delta;
		_iceBlockTimer -= (float)delta;

		// ── Cast wind-up timer ────────────────────────────────────────────────
		// Resolution is driven by the spell's CastTime, not the animation length.
		// OnAnimationFinished loops the cast animation while the timer counts down.
		if (_pendingCast != PendingCast.None)
		{
			_castWindupTimer -= (float)delta;
			if (_castWindupTimer <= 0f)
				ResolvePendingCast();
			return;
		}

		// Priority: Ice Block > Snowstorm > Burst of Winter > Volatile Icicle > Melee.
		if (_iceBlockTimer <= 0f && !_isSnowstormChanneling)
		{
			_iceBlockTimer = IceBlockInterval;
			CastIceBlock();
		}
		else if (_snowstormTimer <= 0f && !_isSnowstormChanneling)
		{
			_snowstormTimer = SnowstormInterval;
			BeginSnowstormCast();
		}
		else if (_burstOfWinterTimer <= 0f && !_isSnowstormChanneling)
		{
			_burstOfWinterTimer = BurstOfWinterInterval;
			BeginBurstOfWinterCast();
		}
		else if (_icicleTimer <= 0f && !_isSnowstormChanneling)
		{
			_icicleTimer = IcicleInterval;
			BeginIcicleCast();
		}
		else if (_meleeTimer <= 0f && !_isSnowstormChanneling)
		{
			_meleeTimer = MeleeAttackInterval;
			BeginMeleeAttack();
		}
	}

	// ── combat actions ────────────────────────────────────────────────────────

	/// <summary>
	/// Begin the 1-second Snowstorm cast wind-up (plays cast animation and
	/// shows the cast bar). The channel node is spawned in OnAnimationFinished.
	/// </summary>
	void BeginSnowstormCast()
	{
		_pendingCast = PendingCast.Snowstorm;
		_castWindupTimer = _snowstormSpell.CastTime;
		_sprite.Play("cast");
		EmitSignalCastWindupStarted("Snowstorm", _snowstormSpell.Icon, _snowstormSpell.CastTime);
	}

	/// <summary>
	/// Begin the 1.5-second Burst of Winter cast wind-up (plays cast animation
	/// and shows the cast bar). The nova is spawned in OnAnimationFinished.
	/// </summary>
	void BeginBurstOfWinterCast()
	{
		_pendingCast = PendingCast.BurstOfWinter;
		_castWindupTimer = _burstOfWinterSpell.CastTime;
		_sprite.Play("cast");
		EmitSignalCastWindupStarted("Burst of Winter", _burstOfWinterSpell.Icon, _burstOfWinterSpell.CastTime);
	}

	/// <summary>
	/// Begin the 1-second Volatile Icicle cast wind-up (plays attack animation
	/// and shows the cast bar). The icicle is spawned in OnAnimationFinished.
	/// </summary>
	void BeginIcicleCast()
	{
		_pendingCast = PendingCast.Icicle;
		_castWindupTimer = _icicleSpell.CastTime;
		_sprite.Play("attack");
		EmitSignalCastWindupStarted("Volatile Icicle", _icicleSpell.Icon, _icicleSpell.CastTime);
	}

	/// <summary>
	/// Ice Block is instant — no animation gate. Immediately apply the shield,
	/// switch to the ice-block sprite, and start the Absolute Zero countdown.
	/// </summary>
	void CastIceBlock()
	{
		// Resolve the spell through the pipeline so the SpellContext is wired up
		// correctly, but since BossQueenIceBlockSpell.Apply delegates back to
		// StartIceBlock() the actual work happens there.
		var anyTarget = PickRandomPartyMember();
		if (anyTarget == null) return;
		SpellPipeline.Cast(_iceBlockSpell, this, anyTarget);
	}

	// ── Ice Block state machine ───────────────────────────────────────────────

	/// <summary>
	/// Called by <see cref="BossQueenIceBlockSpell.Apply"/> to activate the
	/// Ice Block state: apply shield, switch sprite, start Absolute Zero cast.
	/// </summary>
	public void StartIceBlock(float shieldAmount)
	{
		_isInIceBlock = true;
		_absoluteZeroTimer = AbsoluteZeroDuration;

		AddShield(shieldAmount);
		_sprite.Play("ice_block");

		// Show an Absolute Zero cast bar (8 seconds, fills from 0 → 1).
		EmitSignalCastWindupStarted("Absolute Zero", _absoluteZeroSpell.Icon, AbsoluteZeroDuration);

		GD.Print($"[QueenOfTheFrozenWastes] Ice Block activated — {shieldAmount:F0} shield, " +
		         $"{AbsoluteZeroDuration:F0}s until Absolute Zero.");
	}

	/// <summary>
	/// Ends the Ice Block state.
	/// If <paramref name="absoluteZeroCompleted"/> is true, the 8-second cast
	/// finished without the shield breaking — deal 1 000 damage to the entire party.
	/// Otherwise the shield was destroyed and Absolute Zero is cancelled.
	/// </summary>
	public void EndIceBlock(bool absoluteZeroCompleted)
	{
		if (!_isInIceBlock) return;
		_isInIceBlock = false;

		// Remove any remaining shield.
		if (CurrentShield > 0f)
			RemoveShield(CurrentShield);

		// Clear the cast bar.
		EmitSignalCastWindupEnded();

		if (absoluteZeroCompleted)
		{
			GD.Print("[QueenOfTheFrozenWastes] Absolute Zero completed — wiping party!");
			_absoluteZeroSpell.ResolveNow(this);
		}
		else
		{
			GD.Print("[QueenOfTheFrozenWastes] Ice Block shattered — Absolute Zero cancelled.");
		}

		// Return to idle animation.
		if (IsAlive)
			_sprite.Play("idle");
	}

	// ── Snowstorm channel callbacks ───────────────────────────────────────────

	/// <summary>
	/// Called by <see cref="BossQueenSnowstormSpell.Apply"/> immediately after
	/// the SnowstormChannelNode is added to the scene.
	/// Clears the cast wind-up bar and emits <see cref="SnowstormChannelStarted"/>
	/// so the BossCastBar switches to channel mode.
	/// </summary>
	public void OnSnowstormChannelStarted(BossQueenSnowstormSpell spell)
	{
		_isSnowstormChanneling = true;
		// NOTE: CastWindupEnded was already emitted in OnAnimationFinished before
		// SpellPipeline.Cast was called — no need to emit it again here.
		EmitSignalSnowstormChannelStarted(spell); // switches BossCastBar to channel mode

		GD.Print($"[QueenOfTheFrozenWastes] Snowstorm channel started ({spell.ChannelDuration:F0}s).");
	}

	/// <summary>
	/// Called by <see cref="SnowstormChannelNode.OnChannelFinished"/> when the
	/// channel ends. Clears the channel bar and allows the next cast.
	/// </summary>
	public void OnSnowstormChannelEnded()
	{
		_isSnowstormChanneling = false;
		EmitSignalSnowstormChannelEnded();

		GD.Print("[QueenOfTheFrozenWastes] Snowstorm channel ended.");
	}

	// ── cast resolution ───────────────────────────────────────────────────────

	/// <summary>
	/// Called by <c>_Process</c> when <see cref="_castWindupTimer"/> reaches zero.
	/// Resolves the pending spell and returns the boss to idle (or channel) state.
	/// Keeping resolution here — rather than in <c>OnAnimationFinished</c> —
	/// means <see cref="SpellResource.CastTime"/> is the true cast duration.
	/// </summary>
	void ResolvePendingCast()
	{
		switch (_pendingCast)
		{
			case PendingCast.Melee:
				// No cast bar to clear — melee auto-attacks are unannounced.
				var meleeTarget = FindTank() ?? PickRandomPartyMember();
				if (meleeTarget != null)
					SpellPipeline.Cast(_meleeSpell, this, meleeTarget);
				break;

			case PendingCast.Snowstorm:
				EmitSignalCastWindupEnded();
				var snowTarget = PickRandomPartyMember();
				if (snowTarget != null)
					SpellPipeline.Cast(_snowstormSpell, this, snowTarget);
				break;

			case PendingCast.Icicle:
				EmitSignalCastWindupEnded();
				var icicleTarget = PickRandomPartyMember();
				if (icicleTarget != null)
					SpellPipeline.Cast(_icicleSpell, this, icicleTarget);
				break;

			case PendingCast.BurstOfWinter:
				EmitSignalCastWindupEnded();
				var burstTarget = PickRandomPartyMember();
				if (burstTarget != null)
					SpellPipeline.Cast(_burstOfWinterSpell, this, burstTarget);
				break;
		}

		_pendingCast = PendingCast.None;

		if (IsAlive && !_isInIceBlock)
			_sprite.Play(_isSnowstormChanneling ? "cast" : "idle");
	}

	// ── animation callbacks ───────────────────────────────────────────────────

	void OnAnimationFinished()
	{
		// While a cast is still counting down, loop the current animation so
		// the boss visually holds the casting pose for the full CastTime duration.
		if (_pendingCast != PendingCast.None)
		{
			_sprite.Play(_sprite.Animation);
			return;
		}

		// No cast in progress — return to idle (or channel anim / ice block).
		if (IsAlive && !_isInIceBlock)
			_sprite.Play(_isSnowstormChanneling ? "cast" : "idle");
	}

	// ── Cone of Cold phase ───────────────────────────────────────────────────

	/// <summary>
	/// Begins the Cone of Cold phase. The boss becomes invulnerable, any active
	/// cast is interrupted, and a self-contained <see cref="ConeOfColdPhase"/>
	/// node drives the sequence before calling back to <see cref="OnConeOfColdPhaseComplete"/>.
	/// </summary>
	void TriggerConeOfColdPhase()
	{
		GD.Print("[QueenOfTheFrozenWastes] Triggering Cone of Cold phase.");

		_isInvulnerable = true;
		_isMidPhase = true;

		// Cancel any in-progress cast wind-up so the cast bar is cleared cleanly.
		if (_pendingCast != PendingCast.None)
		{
			_pendingCast = PendingCast.None;
			_castWindupTimer = 0f;
			EmitSignalCastWindupEnded();
		}

		// Cancel an active Snowstorm channel — EndChannel fires OnChannelFinished
		// which calls OnSnowstormChannelEnded, resetting the flag and clearing the bar.
		if (_isSnowstormChanneling)
		{
			foreach (var child in GetParent().GetChildren())
			{
				if (child is SnowstormChannelNode chan)
				{
					chan.Cancel();
					break;
				}
			}
		}

		var phase = new ConeOfColdPhase();
		phase.ShowCastBar = (name, icon, dur) => EmitSignalCastWindupStarted(name, icon, dur);
		phase.HideCastBar = () => EmitSignalCastWindupEnded();
		phase.OnPhaseComplete = OnConeOfColdPhaseComplete;

		// Add as sibling so the phase node can access the full scene tree.
		GetParent().AddChild(phase);
		phase.Start(this);
	}

	/// <summary>
	/// Fired by <see cref="ConeOfColdPhase"/> when the full sequence has finished.
	/// Restores normal ability rotation and clears the invulnerability flag.
	/// </summary>
	void OnConeOfColdPhaseComplete()
	{
		GD.Print("[QueenOfTheFrozenWastes] Cone of Cold phase complete — resuming normal rotation.");

		_isInvulnerable = false;
		_isMidPhase = false;

		// Stagger next abilities so the fight doesn't immediately spike after
		// the phase ends.
		_snowstormTimer = Mathf.Max(_snowstormTimer, 8f);
		_icicleTimer = Mathf.Max(_icicleTimer, 4f);
		_burstOfWinterTimer = Mathf.Max(_burstOfWinterTimer, 10f);
		_iceBlockTimer = Mathf.Max(_iceBlockTimer, 12f);

		if (IsAlive)
			_sprite.Play("idle");
	}

	// ── combat actions (melee) ────────────────────────────────────────────────

	/// <summary>
	/// Begins the melee auto-attack. Plays the "attack" animation and sets a
	/// short resolution timer. No cast bar is shown — melee is unannounced.
	/// </summary>
	void BeginMeleeAttack()
	{
		_pendingCast = PendingCast.Melee;
		_castWindupTimer = 0.5f; // visual delay; matches attack animation length
		_sprite.Play("attack");
	}

	// ── Frostbite helpers ─────────────────────────────────────────────────────

	/// <summary>
	/// Applies the <see cref="FrostbiteEffect"/> to the healer on the first
	/// <c>_Process</c> frame, ensuring the scene tree is fully ready.
	/// </summary>
	void ApplyFrostbiteToHealer()
	{
		_frostbiteApplied = true; // set immediately so we never retry on failure

		var healer = FindHealer();
		if (healer == null)
		{
			GD.PrintErr("[QueenOfTheFrozenWastes] Could not find healer to apply Frostbite.");
			return;
		}

		_frostbiteEffect = new FrostbiteEffect
		{
			AbilityName = "Frostbite",
			Description = "The Queen's icy presence seeps into the healer's bones. " +
			              "Deals 5 damage per second per stack. " +
			              "Gains a stack each second spent standing still; " +
			              "loses a stack each second spent moving (minimum 1 stack).",
			SourceCharacterName = CharacterName,
			Icon = GD.Load<Texture2D>(
				AssetConstants.SpellIconAssets + "enemy/queen-of-the-frozen-wastes/frostbite.png")
		};

		healer.ApplyEffect(_frostbiteEffect);
		GD.Print($"[QueenOfTheFrozenWastes] Frostbite applied to {healer.CharacterName}.");
	}

	/// <summary>
	/// Called every frame. Every second, checks whether the healer is moving
	/// and adds or removes a Frostbite stack accordingly.
	/// </summary>
	void UpdateFrostbiteStacks(float delta)
	{
		if (_frostbiteEffect == null) return;

		_frostbiteStackTimer -= delta;
		if (_frostbiteStackTimer > 0f) return;
		_frostbiteStackTimer += 1f;

		var healer = FindHealer();
		if (healer == null) return;

		// CharacterBody2D.Velocity is zero when the player isn't pressing movement keys.
		var isMoving = healer is CharacterBody2D body && body.Velocity.LengthSquared() > 0f;
		if (isMoving)
			_frostbiteEffect.LoseStack();
		else
			_frostbiteEffect.GainStack();

		GD.Print($"[QueenOfTheFrozenWastes] Frostbite — stacks: {_frostbiteEffect.CurrentStacks} (moving: {isMoving}).");
	}

	// ── targeting helpers ─────────────────────────────────────────────────────

	/// <summary>
	/// Returns the alive healer (player character), or null if not found.
	/// </summary>
	Character FindHealer()
	{
		foreach (var node in GetTree().GetNodesInGroup("party"))
			if (node is Character c && c.CharacterName == GameConstants.HealerName && c.IsAlive)
				return c;
		return null;
	}

	/// <summary>
	/// Returns the alive Templar (the tank), or null if not found.
	/// </summary>
	Character FindTank()
	{
		foreach (var node in GetTree().GetNodesInGroup("party"))
			if (node is Character c && c.CharacterName == "Templar" && c.IsAlive)
				return c;
		return null;
	}

	Character PickRandomPartyMember()
	{
		var alive = new System.Collections.Generic.List<Character>();
		foreach (var node in GetTree().GetNodesInGroup("party"))
			if (node is Character c && c.IsAlive)
				alive.Add(c);
		if (alive.Count == 0) return null;
		return alive[(int)(GD.Randi() % (uint)alive.Count)];
	}

	// ── animation setup ───────────────────────────────────────────────────────

	/// <summary>Rune of Time: scale all ability intervals by the haste multiplier.</summary>
	protected override void OnApplyHasteRune()
	{
		MeleeAttackInterval /= GameConstants.RuneTimeHasteMultiplier;
		SnowstormInterval /= GameConstants.RuneTimeHasteMultiplier;
		IcicleInterval /= GameConstants.RuneTimeHasteMultiplier;
		BurstOfWinterInterval /= GameConstants.RuneTimeHasteMultiplier;
		IceBlockInterval /= GameConstants.RuneTimeHasteMultiplier;
	}

	void SetupAnimations()
	{
		var frames = new SpriteFrames();
		frames.RemoveAnimation("default");

		AddAnimFromFiles(frames, "idle", "idle", 3, 4f, true);
		AddAnimFromFiles(frames, "attack", "attack", 3, 10f, false);
		AddAnimFromFiles(frames, "cast", "cast", 3, 6f, false);

		// Ice Block — single static frame; looping so it holds indefinitely.
		frames.AddAnimation("ice_block");
		frames.SetAnimationLoop("ice_block", true);
		frames.SetAnimationSpeed("ice_block", 1f);
		var iceBlockTex = GD.Load<Texture2D>(AssetBase + "queen-ice-block.png");
		if (iceBlockTex != null)
			frames.AddFrame("ice_block", iceBlockTex);

		_sprite.SpriteFrames = frames;
	}

	static void AddAnimFromFiles(SpriteFrames frames, string animName,
		string filePrefix, int count, float fps, bool looping)
	{
		frames.AddAnimation(animName);
		frames.SetAnimationLoop(animName, looping);
		frames.SetAnimationSpeed(animName, fps);
		for (var i = 1; i <= count; i++)
		{
			var texture = GD.Load<Texture2D>(AssetBase + $"{filePrefix}{i}.png");
			frames.AddFrame(animName, texture);
		}
	}
}