using System.Collections.Generic;
using Godot;
using healerfantasy.LastLight;
using healerfantasy;
using healerfantasy.Effects;
using healerfantasy.Runes;
using healerfantasy.SpellResources;
using healerfantasy.SpellSystem;

public partial class TheTallowQueen : EnemyCharacter
{
	/// <summary>
	/// Animation clips (frames / fps from the original sprites). These are gameplay:
	/// an attack's spell is cast when its clip finishes, so a clip's length is its wind-up.
	/// </summary>
	static readonly Dictionary<string, CharacterVisual.Clip> Clips = new()
	{
		["idle"] = new(3 / 4f, Loop: true),
		["attack"] = new(4 / 10f),
		["cast"] = new(4 / 6f),
		["wax_shell"] = new(1 / 1f, Loop: true)
	};

	public TheTallowQueen()
	{
		MaxHealth = GameConstants.BossHealthBaseValuesByDungeonTier[GameConstants.GutteringSpireTier][0];
	}

	// ── signals ───────────────────────────────────────────────────────────────

	/// <summary>Emitted when the Queen begins or ends a pre-channel cast (shared cast bar).</summary>
	[Signal]
	public delegate void CastWindupStartedEventHandler(string spellName, Texture2D icon, float duration);

	[Signal]
	public delegate void CastWindupEndedEventHandler();

	/// <summary>Emitted when the Wax Rain channel begins — feeds the BossCastBar channel mode.</summary>
	[Signal]
	public delegate void WaxRainChannelStartedEventHandler(BossQueenWaxRainSpell spell);

	/// <summary>Emitted when the Wax Rain channel ends naturally or is interrupted.</summary>
	[Signal]
	public delegate void WaxRainChannelEndedEventHandler();

	// ── tuneable exports ──────────────────────────────────────────────────────

	[Export] public float MeleeAttackInterval = 3f;
	[Export] public float MeleeDamage = 25f;

	[Export] public float WaxRainInterval = 18f;
	[Export] public float MoltenDropInterval = 12f;
	[Export] public float ScaldInterval = 26f;
	[Export] public float WaxShellInterval = 32f;

	[Export] public float WaxRainDamagePerTick = 20f;
	[Export] public float MoltenDropDamagePerTick = 15f;
	[Export] public float ScaldDamage = 40f;
	[Export] public float WaxShellShieldAmount = 500f;
	[Export] public float SnuffOutDuration = 8f;

	// ── internal state ────────────────────────────────────────────────────────

	float _meleeTimer;
	float _waxRainTimer;
	float _moltenDropTimer;
	float _scaldTimer;
	float _waxShellTimer;

	bool _isWaxRainChanneling;
	bool _isInWaxShell;
	float _snuffOutTimer;

	// ── Setting Wax debuff ──────────────────────────────────────────────────────
	bool _settingWaxApplied;
	float _settingWaxStackTimer;
	SettingWaxEffect _settingWaxEffect;

	// ── Molten Cone phase ────────────────────────────────────────────────────
	bool _isInvulnerable;
	bool _isMidPhase;
	bool _phase1Triggered;
	bool _phase2Triggered;

	// ── Tallow Hulk mid-phase ───────────────────────────────────────────────
	bool _tallowHulkTriggered;

	/// <summary>
	/// Counts down the remaining wind-up time for the current cast.
	/// Resolution happens here in <c>_Process</c> — not in
	/// <c>OnAnimationFinished</c> — so that <see cref="SpellResource.CastTime"/>
	/// is the true arbiter of how long the cast takes, regardless of animation
	/// speed or frame count.
	/// </summary>
	float _castWindupTimer;

	BossQueenScaldingTouchSpell _meleeSpell;
	BossQueenWaxRainSpell _waxRainSpell;
	BossQueenMoltenDropSpell _moltenDropSpell;
	BossQueenScaldSpell _scaldSpell;
	BossQueenWaxShellSpell _waxShellSpell;
	BossQueenSnuffOutSpell _snuffOutSpell;

	CharacterVisual _visual = null!;

	enum PendingCast
	{
		None,
		WaxRain,
		MoltenDrop,
		Scald,
		Melee
	}

	PendingCast _pendingCast;


	// ── invulnerability ───────────────────────────────────────────────────────

	/// <summary>
	/// Blocks all incoming damage while the Molten Cone phase is active.
	/// The shield-break / Wax Shell check still uses the raw shield value and
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
		CharacterName = GameConstants.GutteringSpireBossName;
		IsFriendly = false;

		// Stagger first casts so the fight has a brief breathing window.
		_meleeTimer = MeleeAttackInterval;
		_waxRainTimer = 12f;
		_moltenDropTimer = 4f;
		_scaldTimer = 24f;
		_waxShellTimer = 32f;

		// SettingWax: first stack-check fires after 1 second.
		_settingWaxStackTimer = 1f;

		_meleeSpell = new BossQueenScaldingTouchSpell { DamageAmount = MeleeDamage };
		_waxRainSpell = new BossQueenWaxRainSpell { Boss = this, DamagePerTick = WaxRainDamagePerTick };
		_moltenDropSpell = new BossQueenMoltenDropSpell { Boss = this, ZoneDamagePerTick = MoltenDropDamagePerTick };
		_scaldSpell = new BossQueenScaldSpell { Boss = this, Damage = ScaldDamage };
		_waxShellSpell = new BossQueenWaxShellSpell { Boss = this, WaxShellShield = WaxShellShieldAmount };
		_snuffOutSpell = new BossQueenSnuffOutSpell { DamageAmount = 1000f, CastDuration = SnuffOutDuration };

		GlobalAutoLoad.RegisterSignalEmitter(this, nameof(CastWindupStarted));
		GlobalAutoLoad.RegisterSignalEmitter(this, nameof(CastWindupEnded));
		GlobalAutoLoad.RegisterSignalEmitter(this, nameof(WaxRainChannelStarted));
		GlobalAutoLoad.RegisterSignalEmitter(this, nameof(WaxRainChannelEnded));

		_visual = CharacterVisual.Attach(this, Clips);
		_visual.AnimationFinished += OnAnimationFinished;
		_visual.Play("idle");
		ApplyRuneModifiers();
	}

	public override void _Process(double delta)
	{
		base._Process(delta);
		if (!IsAlive) return;

		// ── Setting Wax — apply on first tick, then manage stacks ───────────────
		if (!_settingWaxApplied && RunState.Instance.IsRuneActive(RuneIndex.StarlessNight))
			ApplySettingWaxToHealer();
		UpdateSettingWaxStacks((float)delta);

		// ── Wax Shell — track Snuff Out countdown ────────────────────────
		if (_isInWaxShell)
		{
			// Shield broken by damage → cancel Wax Shell.
			if (CurrentShield <= 0f)
			{
				EndWaxShell(false);
				return;
			}

			_snuffOutTimer -= (float)delta;
			if (_snuffOutTimer <= 0f)
				EndWaxShell(true);

			return; // no other abilities while encased
		}

		// ── Phase triggers ────────────────────────────────────────────────────────
		// Check health thresholds before any ability logic. Once a phase begins
		// _isMidPhase suppresses the rest of _Process until the phase completes.
		if (!_isMidPhase && !_isInWaxShell)
		{
			// ── Tallow Hulk spawns at 50% ──────────────────────────────────────
			if (!_tallowHulkTriggered && CurrentHealth <= MaxHealth * 0.50f)
			{
				_tallowHulkTriggered = true;
				TriggerTallowHulkPhase();
				return;
			}

			// ── Molten Cone phases (66% and 33%) ────────────────────────────────
			if (!_phase1Triggered && CurrentHealth <= MaxHealth * 0.66f)
			{
				_phase1Triggered = true;
				TriggerMoltenConePhase();
				return;
			}

			if (!_phase2Triggered && CurrentHealth <= MaxHealth * 0.33f)
			{
				_phase2Triggered = true;
				TriggerMoltenConePhase();
				return;
			}
		}

		if (_isMidPhase) return;

		// ── Attack timers ─────────────────────────────────────────────────────
		_meleeTimer -= (float)delta;
		_waxRainTimer -= (float)delta;
		_moltenDropTimer -= (float)delta;
		_scaldTimer -= (float)delta;
		_waxShellTimer -= (float)delta;

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

		// Priority: Wax Shell > Wax Rain > Scald > Molten Drop > Melee.
		if (_waxShellTimer <= 0f && !_isWaxRainChanneling)
		{
			_waxShellTimer = WaxShellInterval;
			CastWaxShell();
		}
		else if (_waxRainTimer <= 0f && !_isWaxRainChanneling)
		{
			_waxRainTimer = WaxRainInterval;
			BeginWaxRainCast();
		}
		else if (_scaldTimer <= 0f && !_isWaxRainChanneling)
		{
			_scaldTimer = ScaldInterval;
			BeginScaldCast();
		}
		else if (_moltenDropTimer <= 0f && !_isWaxRainChanneling)
		{
			_moltenDropTimer = MoltenDropInterval;
			BeginMoltenDropCast();
		}
		else if (_meleeTimer <= 0f && !_isWaxRainChanneling)
		{
			_meleeTimer = MeleeAttackInterval;
			BeginMeleeAttack();
		}
	}

	// ── combat actions ────────────────────────────────────────────────────────

	/// <summary>
	/// Begin the 1-second Wax Rain cast wind-up (plays cast animation and
	/// shows the cast bar). The channel node is spawned in OnAnimationFinished.
	/// </summary>
	void BeginWaxRainCast()
	{
		_pendingCast = PendingCast.WaxRain;
		_castWindupTimer = _waxRainSpell.CastTime;
		_visual.Play("cast");
		EmitSignalCastWindupStarted("Wax Rain", _waxRainSpell.Icon, _waxRainSpell.CastTime);
	}

	/// <summary>
	/// Begin the 1.5-second Scald cast wind-up (plays cast animation
	/// and shows the cast bar). The nova is spawned in OnAnimationFinished.
	/// </summary>
	void BeginScaldCast()
	{
		_pendingCast = PendingCast.Scald;
		_castWindupTimer = _scaldSpell.CastTime;
		_visual.Play("cast");
		EmitSignalCastWindupStarted("Scald", _scaldSpell.Icon, _scaldSpell.CastTime);
	}

	/// <summary>
	/// Begin the 1-second Molten Drop cast wind-up (plays attack animation
	/// and shows the cast bar). The molten drop is spawned in OnAnimationFinished.
	/// </summary>
	void BeginMoltenDropCast()
	{
		_pendingCast = PendingCast.MoltenDrop;
		_castWindupTimer = _moltenDropSpell.CastTime;
		_visual.Play("attack");
		EmitSignalCastWindupStarted("Molten Drop", _moltenDropSpell.Icon, _moltenDropSpell.CastTime);
	}

	/// <summary>
	/// Wax Shell is instant — no animation gate. Immediately apply the shield,
	/// switch to the wax-shell sprite, and start the Snuff Out countdown.
	/// </summary>
	void CastWaxShell()
	{
		// Resolve the spell through the pipeline so the SpellContext is wired up
		// correctly, but since BossQueenWaxShellSpell.Apply delegates back to
		// StartWaxShell() the actual work happens there.
		var anyTarget = PickRandomPartyMember();
		if (anyTarget == null) return;
		SpellPipeline.Cast(_waxShellSpell, this, anyTarget);
	}

	// ── Wax Shell state machine ───────────────────────────────────────────────

	/// <summary>
	/// Called by <see cref="BossQueenWaxShellSpell.Apply"/> to activate the
	/// Wax Shell state: apply shield, switch sprite, start Snuff Out cast.
	/// </summary>
	public void StartWaxShell(float shieldAmount)
	{
		_isInWaxShell = true;
		_snuffOutTimer = SnuffOutDuration;

		AddShield(shieldAmount);
		_visual.Play("wax_shell");

		// Show an Snuff Out cast bar (8 seconds, fills from 0 → 1).
		EmitSignalCastWindupStarted("Snuff Out", _snuffOutSpell.Icon, SnuffOutDuration);

		GD.Print($"[TheTallowQueen] Wax Shell activated — {shieldAmount:F0} shield, " +
		         $"{SnuffOutDuration:F0}s until Snuff Out.");
	}

	/// <summary>
	/// Ends the Wax Shell state.
	/// If <paramref name="snuffOutCompleted"/> is true, the 8-second cast
	/// finished without the shield breaking — deal 1 000 damage to the entire party.
	/// Otherwise the shield was destroyed and Snuff Out is cancelled.
	/// </summary>
	public void EndWaxShell(bool snuffOutCompleted)
	{
		if (!_isInWaxShell) return;
		_isInWaxShell = false;

		// Remove any remaining shield.
		if (CurrentShield > 0f)
			RemoveShield(CurrentShield);

		// Clear the cast bar.
		EmitSignalCastWindupEnded();

		if (snuffOutCompleted)
		{
			GD.Print("[TheTallowQueen] Snuff Out completed — wiping party!");
			_snuffOutSpell.ResolveNow(this);
		}
		else
		{
			GD.Print("[TheTallowQueen] Wax Shell cracked — Snuff Out cancelled.");
		}

		// Return to idle animation.
		if (IsAlive)
			_visual.Play("idle");
	}

	// ── Wax Rain channel callbacks ───────────────────────────────────────────

	/// <summary>
	/// Called by <see cref="BossQueenWaxRainSpell.Apply"/> immediately after
	/// the WaxRainChannelNode is added to the scene.
	/// Clears the cast wind-up bar and emits <see cref="WaxRainChannelStarted"/>
	/// so the BossCastBar switches to channel mode.
	/// </summary>
	public void OnWaxRainChannelStarted(BossQueenWaxRainSpell spell)
	{
		_isWaxRainChanneling = true;
		// NOTE: CastWindupEnded was already emitted in OnAnimationFinished before
		// SpellPipeline.Cast was called — no need to emit it again here.
		EmitSignalWaxRainChannelStarted(spell); // switches BossCastBar to channel mode

		GD.Print($"[TheTallowQueen] Wax Rain channel started ({spell.ChannelDuration:F0}s).");
	}

	/// <summary>
	/// Called by <see cref="WaxRainChannelNode.OnChannelFinished"/> when the
	/// channel ends. Clears the channel bar and allows the next cast.
	/// </summary>
	public void OnWaxRainChannelEnded()
	{
		_isWaxRainChanneling = false;
		EmitSignalWaxRainChannelEnded();

		GD.Print("[TheTallowQueen] Wax Rain channel ended.");
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
				var meleeTarget = SelectCurrentMeleeTarget();
				if (meleeTarget != null)
					SpellPipeline.Cast(_meleeSpell, this, meleeTarget);
				break;

			case PendingCast.WaxRain:
				EmitSignalCastWindupEnded();
				var rainTarget = PickRandomPartyMember();
				if (rainTarget != null)
					SpellPipeline.Cast(_waxRainSpell, this, rainTarget);
				break;

			case PendingCast.MoltenDrop:
				EmitSignalCastWindupEnded();
				var dropTarget = PickRandomPartyMember();
				if (dropTarget != null)
					SpellPipeline.Cast(_moltenDropSpell, this, dropTarget);
				break;

			case PendingCast.Scald:
				EmitSignalCastWindupEnded();
				var burstTarget = PickRandomPartyMember();
				if (burstTarget != null)
					SpellPipeline.Cast(_scaldSpell, this, burstTarget);
				break;
		}

		_pendingCast = PendingCast.None;

		if (IsAlive && !_isInWaxShell)
			_visual.Play(_isWaxRainChanneling ? "cast" : "idle");
	}

	// ── animation callbacks ───────────────────────────────────────────────────

	void OnAnimationFinished()
	{
		// While a cast is still counting down, loop the current animation so
		// the boss visually holds the casting pose for the full CastTime duration.
		if (_pendingCast != PendingCast.None)
		{
			_visual.Play(_visual.Animation);
			return;
		}

		// No cast in progress — return to idle (or channel anim / wax shell).
		if (IsAlive && !_isInWaxShell)
			_visual.Play(_isWaxRainChanneling ? "cast" : "idle");
	}

	// ── Tallow Hulk phase ───────────────────────────────────────────────────

	/// <summary>
	/// Triggers the dramatic mid-fight entrance of The Tallow Hulk when the
	/// Queen reaches 50% health. Briefly suppresses the Queen's ability rotation
	/// while the entrance sequence plays, then resumes normally.
	/// </summary>
	void TriggerTallowHulkPhase()
	{
		GD.Print("[TheTallowQueen] Triggering Tallow Hulk entrance at 50% health.");

		_isInvulnerable = true;
		_isMidPhase = true;

		// Cancel any in-progress cast wind-up cleanly.
		if (_pendingCast != PendingCast.None)
		{
			_pendingCast = PendingCast.None;
			_castWindupTimer = 0f;
			EmitSignalCastWindupEnded();
		}

		// Cancel an active Wax Rain channel.
		if (_isWaxRainChanneling)
		{
			foreach (var child in GetParent().GetChildren())
			{
				if (child is WaxRainChannelNode chan)
				{
					chan.Cancel();
					break;
				}
			}
		}

		var phase = new TallowHulkEmergePhase();
		phase.OnPhaseComplete = OnTallowHulkPhaseComplete;
		// Uncomment and set PhaseMusic if you want music to change here, e.g.:
		// phase.PhaseMusic = GD.Load<AudioStreamOggVorbis>(AssetConstants.FinalBossPhase2MusicPath);
		// phase.PhaseMusic.Loop = true;

		GetParent().AddChild(phase);
		phase.Start(this);
	}

	/// <summary>
	/// Called by <see cref="TallowHulkEmergePhase"/> when the entrance animation
	/// completes and the Tallow Hulk has begun combat. The Queen resumes her
	/// normal ability rotation.
	/// </summary>
	void OnTallowHulkPhaseComplete()
	{
		GD.Print("[TheTallowQueen] Tallow Hulk entrance complete — Queen resumes combat.");

		_isInvulnerable = false;
		_isMidPhase = false;

		// Brief breather after the dramatic entrance.
		_waxRainTimer = Mathf.Max(_waxRainTimer, 6f);
		_moltenDropTimer = Mathf.Max(_moltenDropTimer, 3f);
		_scaldTimer = Mathf.Max(_scaldTimer, 8f);
		_waxShellTimer = Mathf.Max(_waxShellTimer, 10f);

		if (IsAlive)
			_visual.Play("idle");
	}

	// ── Molten Cone phase ───────────────────────────────────────────────────

	/// <summary>
	/// Begins the Molten Cone phase. The boss becomes invulnerable, any active
	/// cast is interrupted, and a self-contained <see cref="MoltenConePhase"/>
	/// node drives the sequence before calling back to <see cref="OnMoltenConePhaseComplete"/>.
	/// </summary>
	void TriggerMoltenConePhase()
	{
		GD.Print("[TheTallowQueen] Triggering Molten Cone phase.");

		_isInvulnerable = true;
		HoldTallowHulk(true); // its slams and beams can't be dodged while the party is entombed
		_isMidPhase = true;

		// Cancel any in-progress cast wind-up so the cast bar is cleared cleanly.
		if (_pendingCast != PendingCast.None)
		{
			_pendingCast = PendingCast.None;
			_castWindupTimer = 0f;
			EmitSignalCastWindupEnded();
		}

		// Cancel an active Wax Rain channel — EndChannel fires OnChannelFinished
		// which calls OnWaxRainChannelEnded, resetting the flag and clearing the bar.
		if (_isWaxRainChanneling)
		{
			foreach (var child in GetParent().GetChildren())
			{
				if (child is WaxRainChannelNode chan)
				{
					chan.Cancel();
					break;
				}
			}
		}

		var phase = new MoltenConePhase();
		phase.ShowCastBar = (name, icon, dur) => EmitSignalCastWindupStarted(name, icon, dur);
		phase.HideCastBar = () => EmitSignalCastWindupEnded();
		phase.OnPhaseComplete = OnMoltenConePhaseComplete;

		// Add as sibling so the phase node can access the full scene tree.
		GetParent().AddChild(phase);
		phase.Start(this);
	}

	/// <summary>
	/// Fired by <see cref="MoltenConePhase"/> when the full sequence has finished.
	/// Restores normal ability rotation and clears the invulnerability flag.
	/// </summary>
	void OnMoltenConePhaseComplete()
	{
		GD.Print("[TheTallowQueen] Molten Cone phase complete — resuming normal rotation.");

		_isInvulnerable = false;
		HoldTallowHulk(false);
		_isMidPhase = false;

		// Stagger next abilities so the fight doesn't immediately spike after
		// the phase ends.
		_waxRainTimer = Mathf.Max(_waxRainTimer, 8f);
		_moltenDropTimer = Mathf.Max(_moltenDropTimer, 4f);
		_scaldTimer = Mathf.Max(_scaldTimer, 10f);
		_waxShellTimer = Mathf.Max(_waxShellTimer, 12f);

		if (IsAlive)
			_visual.Play("idle");
	}

	/// <summary>Pauses (or resumes) the Tallow Hulk's attacks, if it has risen.</summary>
	void HoldTallowHulk(bool hold)
	{
		foreach (var child in GetParent().GetChildren())
			if (child is TheTallowHulk hulk)
				hulk.HoldAbilities = hold;
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
		_visual.Play("attack");
	}

	// ── Setting Wax helpers ─────────────────────────────────────────────────────

	/// <summary>
	/// Applies the <see cref="SettingWaxEffect"/> to the healer on the first
	/// <c>_Process</c> frame, ensuring the scene tree is fully ready.
	/// </summary>
	void ApplySettingWaxToHealer()
	{
		_settingWaxApplied = true; // set immediately so we never retry on failure

		var healer = FindHealer();
		if (healer == null)
		{
			GD.PrintErr("[TheTallowQueen] Could not find healer to apply Setting Wax.");
			return;
		}

		_settingWaxEffect = new SettingWaxEffect
		{
			AbilityName = "Setting Wax",
			Description = "Wax drips onto the Lightkeeper and hardens where they stand. " +
			              "Deals 5 damage per second per stack. " +
			              "Gains a stack each second spent standing still; " +
			              "loses a stack each second spent moving (minimum 1 stack).",
			SourceCharacterName = CharacterName,
			Icon = GD.Load<Texture2D>(
				AssetConstants.SpellIconAssets + "enemy/the-tallow-queen/setting-wax.png")
		};

		healer.ApplyEffect(_settingWaxEffect);
		GD.Print($"[TheTallowQueen] Setting Wax applied to {healer.CharacterName}.");
	}

	/// <summary>
	/// Called every frame. Every second, checks whether the healer is moving
	/// and adds or removes a Setting Wax stack accordingly.
	/// </summary>
	void UpdateSettingWaxStacks(float delta)
	{
		if (_settingWaxEffect == null) return;

		_settingWaxStackTimer -= delta;
		if (_settingWaxStackTimer > 0f) return;
		_settingWaxStackTimer += 1f;

		var healer = FindHealer();
		if (healer == null) return;

		// CharacterBody2D.Velocity is zero when the player isn't pressing movement keys.
		var isMoving = healer is CharacterBody2D body && body.Velocity.LengthSquared() > 0f;
		if (isMoving)
			_settingWaxEffect.LoseStack();
		else
			_settingWaxEffect.GainStack();

		GD.Print($"[TheTallowQueen] Setting Wax — stacks: {_settingWaxEffect.CurrentStacks} (moving: {isMoving}).");
	}

	/// <summary>Rune of Midnight: scale all ability intervals by the haste multiplier.</summary>
	protected override void OnApplyHasteRune()
	{
		MeleeAttackInterval /= GameConstants.RuneMidnightHasteMultiplier;
		WaxRainInterval /= GameConstants.RuneMidnightHasteMultiplier;
		MoltenDropInterval /= GameConstants.RuneMidnightHasteMultiplier;
		ScaldInterval /= GameConstants.RuneMidnightHasteMultiplier;
		WaxShellInterval /= GameConstants.RuneMidnightHasteMultiplier;
	}

}
