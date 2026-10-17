using System.Collections.Generic;
using Godot;
using healerfantasy.LastLight;
using healerfantasy;
using healerfantasy.SpellResources;
using healerfantasy.SpellSystem;

/// <summary>
/// A single Astral Twin — two instances of this class are placed in the
/// AstralTwins scene (Dawn at x=-90, Dusk at x=+90 with the sprite flipped).
///
/// This is the second boss encounter of the Sanctum of Stars.
///
/// Behaviour
/// ─────────
/// • Every <see cref="StrikeInterval"/> seconds — Astral Strike: a melee hit
///   at the tank; uses the "attack" animation.
/// • Every <see cref="StarfallInterval"/> seconds — Starfall: a starlight bolt
///   at a random party member; uses the "casting" animation.
/// • Every <see cref="ConvergenceInterval"/> seconds — Celestial Convergence:
///   a <see cref="ConvergenceWindupDuration"/>-second telegraphed AoE that hits
///   the whole party — deflectable; uses the "casting" animation for the wind-up.
///
/// Phase-shield mechanic
/// ─────────────────────
/// Each twin shields itself at 75 %, 50 %, and 25 % of its own maximum health
/// (one shield per threshold, each used only once).  While shielded the twin is
/// completely immune — all incoming damage is discarded.  The shield breaks once
/// the OTHER twin has lost <see cref="ShieldBreakFraction"/> of its maximum health,
/// so a single stray hit (e.g. from a multi-target spell) is no longer enough —
/// the party has to genuinely swap focus.  The remaining break amount is shown as
/// a shield segment on the shielded twin's health bar, and on the models by a
/// <see cref="TwinTetherIndicator"/> — an aura tethered to the sibling.
///
/// Only one twin can hide at a time: if a twin raises its shield while its sibling
/// is still shielded, the sibling's shield breaks (otherwise both would be immune).
///
/// The twin holds its "shielded" pose while the immunity is active.
///
/// Bond of the Twins (enrage)
/// ──────────────────────────
/// When one twin dies the survivor enrages: its damage is multiplied by
/// <see cref="EnrageDamageMultiplier"/> and its abilities come
/// <see cref="EnrageHasteMultiplier"/>× faster.  This rewards bringing both
/// twins down close together.
/// </summary>
public partial class AstralTwin : EnemyCharacter
{
	/// <summary>
	/// Animation clips (frames / fps from the original sprites). These are gameplay:
	/// an attack's spell is cast when its clip finishes, so a clip's length is its wind-up.
	/// </summary>
	static readonly Dictionary<string, CharacterVisual.Clip> Clips = new()
	{
		["idle"] = new(3 / 6f, Loop: true),
		["attack"] = new(3 / 10f),
		["casting"] = new(4 / 8f),
		["shielded"] = new(1 / 1f, Loop: true)
	};

	public AstralTwin()
	{
		// Each twin has half the health pool
		MaxHealth = GameConstants.BossHealthBaseValuesByDungeonTier[GameConstants.SanctumOfStarsTier][1] / 2;
	}

	// ── signals ───────────────────────────────────────────────────────────────

	[Signal]
	public delegate void CastWindupStartedEventHandler(string spellName, Texture2D icon, float duration);

	[Signal]
	public delegate void CastWindupEndedEventHandler();

	// ── tuneable exports ──────────────────────────────────────────────────────

	[Export] public float StrikeInterval = 2.5f;
	[Export] public float StarfallInterval = 7.0f;
	[Export] public float ConvergenceInterval = 14.0f;
	[Export] public float ConvergenceWindupDuration = 3.0f;

	[Export] public float StrikeDamage = 20f;
	[Export] public float StarfallDamage = 40f;
	[Export] public float ConvergenceDamage = 80f;

	/// <summary>
	/// Fraction of the sibling's maximum health the party must strip from it to
	/// break this twin's phase shield.
	/// </summary>
	[Export] public float ShieldBreakFraction = 0.08f;

	/// <summary>Damage multiplier applied to the surviving twin's abilities.</summary>
	[Export] public float EnrageDamageMultiplier = 1.5f;

	/// <summary>Ability speed multiplier applied to the surviving twin (intervals are divided by this).</summary>
	[Export] public float EnrageHasteMultiplier = 1.3f;

	// ── internal state ────────────────────────────────────────────────────────

	float _strikeTimer;
	float _starfallTimer;
	float _convergenceTimer;
	float _convergenceWindupTimer;

	/// <summary>
	/// True when this twin is the one responsible for managing the parry window
	/// and dealing damage for the current Convergence cast. Because both twins
	/// share a static <see cref="ParryWindowManager"/>, only the first twin to
	/// call <see cref="BeginConvergence"/> in a given cycle opens the window;
	/// the second twin just mirrors the animation.
	/// </summary>
	bool _ownsConvergenceWindow;

	BossAstralStrikeSpell _strikeSpell;
	BossAstralStarfallSpell _starfallSpell;
	BossCelestialConvergenceSpell _novaSpell;

	CharacterVisual _visual = null!;
	AudioStreamPlayer _riserPlayer;

	enum PendingAttack
	{
		None,
		Strike,
		Starfall
	}

	PendingAttack _pendingAttack;
	Character _pendingTarget;

	// ── Phase-shield state ────────────────────────────────────────────────────

	bool _isPhaseShielded;

	/// <summary>Sibling health still to be removed before this twin's shield breaks.</summary>
	float _shieldBreakRemaining;
	float _shieldBreakTotal;

	bool _enraged;
	float _enrageTime;
	// Kept at or below 1 — the rig treats a brightened visual as "hover highlighted",
	// and tinting the twin node itself (not its visual) leaves hover untouched.
	static readonly Color EnrageTint = new(1f, 0.45f, 0.45f);

	/// <summary>
	/// Threshold percentages at which this twin shields itself.
	/// Each threshold is used exactly once.
	/// </summary>
	static readonly float[] ShieldThresholds = { 0.75f, 0.50f, 0.25f };

	readonly bool[] _thresholdUsed = new bool[3];

	/// <summary>Reference to the sibling twin — wired up lazily after _Ready.</summary>
	AstralTwin _sibling;

	DiedEventHandler _siblingDiedHandler;


	// ── lifecycle ─────────────────────────────────────────────────────────────

	public override void _Ready()
	{
		base._Ready();

		_strikeTimer = StrikeInterval;
		_starfallTimer = StarfallInterval;
		_convergenceTimer = ConvergenceInterval;

		_strikeSpell = new BossAstralStrikeSpell { DamageAmount = StrikeDamage };
		_starfallSpell = new BossAstralStarfallSpell { DamageAmount = StarfallDamage };
		_novaSpell = new BossCelestialConvergenceSpell { DamageAmount = ConvergenceDamage };

		GlobalAutoLoad.RegisterSignalEmitter(this, nameof(CastWindupStarted));
		GlobalAutoLoad.RegisterSignalEmitter(this, nameof(CastWindupEnded));

		_riserPlayer = new AudioStreamPlayer();
		_riserPlayer.Stream = GD.Load<AudioStream>(AssetConstants.DeflectRiserSoundPath);
		AddChild(_riserPlayer);

		_visual = CharacterVisual.Attach(this, Clips);
		_visual.AnimationFinished += OnAnimationFinished;
		_visual.Play("idle");
		ApplyRuneModifiers();

		// Wire sibling reference after all nodes in the scene are ready.
		CallDeferred(nameof(FindSibling));
	}

	void FindSibling()
	{
		if (IsBeingRemoved || !IsInstanceValid(this)) return;

		foreach (var node in GetTree().GetNodesInGroup(GameConstants.BossGroupName))
			if (node is AstralTwin twin && twin != this)
			{
				_sibling = twin;
				// When the sibling dies any active phase shield on this twin becomes
				// unbreakable (nothing can call NotifySiblingHit any more), so we
				// clear it immediately and retire all remaining thresholds.
				_siblingDiedHandler = OnSiblingDiedFromSignal;
				_sibling.Died += _siblingDiedHandler;
				break;
			}
	}

	void OnSiblingDiedFromSignal(Character _)
	{
		if (IsBeingRemoved) return;
		OnSiblingDied();
	}

	void OnSiblingDied()
	{
		// Retire every remaining shield threshold — the mechanic requires two
		// living twins and makes no sense in a solo fight.
		for (var i = 0; i < _thresholdUsed.Length; i++)
			_thresholdUsed[i] = true;

		if (_isPhaseShielded)
			ClearPhaseShield();

		if (IsAlive)
			TriggerEnrage();
	}

	void TriggerEnrage()
	{
		if (_enraged) return;
		_enraged = true;

		_strikeSpell.DamageAmount *= EnrageDamageMultiplier;
		_starfallSpell.DamageAmount *= EnrageDamageMultiplier;
		_novaSpell.DamageAmount *= EnrageDamageMultiplier;

		StrikeInterval /= EnrageHasteMultiplier;
		StarfallInterval /= EnrageHasteMultiplier;
		ConvergenceInterval /= EnrageHasteMultiplier;
		_strikeTimer = Mathf.Min(_strikeTimer, StrikeInterval);
		_starfallTimer = Mathf.Min(_starfallTimer, StarfallInterval);
		_convergenceTimer = Mathf.Min(_convergenceTimer, ConvergenceInterval);

		GD.Print($"[AstralTwin] {CharacterName} is ENRAGED by the loss of its twin!");
	}

	public override void _Process(double delta)
	{
		base._Process(delta);
		if (!IsAlive) return;

		if (_enraged)
		{
			// Slow red pulse so the enrage reads on the model itself.
			_enrageTime += (float)delta;
			var pulse = 0.5f + 0.5f * Mathf.Sin(_enrageTime * 4f);
			Modulate = Colors.White.Lerp(EnrageTint, 0.5f + 0.5f * pulse);
		}

		// ── Celestial Convergence wind-up countdown ───────────────────────────
		if (_convergenceWindupTimer > 0f)
		{
			_convergenceWindupTimer -= (float)delta;
			if (_convergenceWindupTimer <= 0f)
				ExecuteConvergence();
		}

		if (_convergenceWindupTimer > 0f) return;

		_strikeTimer -= (float)delta;
		_starfallTimer -= (float)delta;
		_convergenceTimer -= (float)delta;

		if (_pendingAttack != PendingAttack.None) return;

		if (_strikeTimer <= 0f)
		{
			_strikeTimer = StrikeInterval;
			PerformStrike();
		}
		else if (_starfallTimer <= 0f)
		{
			_starfallTimer = StarfallInterval;
			CastStarfall();
		}
		else if (_convergenceTimer <= 0f)
		{
			_convergenceTimer = ConvergenceInterval;
			BeginConvergence();
		}
	}

	// ── Damage interception — phase shield ────────────────────────────────────

	/// <summary>
	/// While this twin is phase-shielded all incoming damage is blocked.
	/// A successful hit on the OTHER twin breaks this twin's shield.
	/// </summary>
	public override void TakeDamage(float amount)
	{
		if (_isPhaseShielded)
		{
			GD.Print($"[AstralTwin] {CharacterName} is shielded — damage blocked.");
			return;
		}

		var healthBefore = CurrentHealth;
		base.TakeDamage(amount);

		// Health actually lost on this twin wears down the sibling's shield.
		var healthLost = healthBefore - CurrentHealth;
		if (healthLost > 0f && IsInstanceValid(_sibling) && !_sibling.IsBeingRemoved && !_sibling.IsQueuedForDeletion())
			_sibling.NotifySiblingHit(healthLost);

		// Check whether crossing a threshold triggers our OWN shield.
		CheckShieldThresholds(healthBefore);
	}

	/// <summary>
	/// Called by the sibling twin when it loses <paramref name="healthLost"/>
	/// health. Wears down this twin's phase shield, breaking it once enough
	/// damage has been dealt to the sibling.
	/// </summary>
	public void NotifySiblingHit(float healthLost)
	{
		if (!_isPhaseShielded) return;

		_shieldBreakRemaining -= healthLost;
		if (_shieldBreakRemaining <= 0f)
			ClearPhaseShield();
		else
			EmitSignalShieldChanged(CharacterName, _shieldBreakRemaining, MaxHealth);
	}

	// ── combat actions ────────────────────────────────────────────────────────

	void PerformStrike()
	{
		var target = SelectCurrentMeleeTarget();
		if (target == null) return;
		_pendingTarget = target;
		_pendingAttack = PendingAttack.Strike;
		_visual.Play("attack");
	}

	void CastStarfall()
	{
		var target = PickRandomPartyMember();
		if (target == null) return;
		_pendingTarget = target;
		_pendingAttack = PendingAttack.Starfall;
		_visual.Play("casting");
	}

	void BeginConvergence()
	{
		_convergenceWindupTimer = ConvergenceWindupDuration;

		// Only the first twin to enter the wind-up owns the parry window.
		// The second twin (whose timer fires in the same frame or shortly after)
		// simply plays the animation — it must not open a second window, which
		// would reset _wasDeflected and cause a successful deflect to be ignored.
		_ownsConvergenceWindow = !ParryWindowManager.IsOpen;
		if (_ownsConvergenceWindow)
		{
			ParryWindowManager.OpenWindow(_novaSpell.Name, _novaSpell.Icon, ConvergenceWindupDuration);
			EmitSignalCastWindupStarted(_novaSpell.Name, _novaSpell.Icon, ConvergenceWindupDuration);
			_riserPlayer.Play();
		}

		_pendingAttack = PendingAttack.None;
		_visual.Play("casting");
	}

	void ExecuteConvergence()
	{
		if (_ownsConvergenceWindow)
		{
			_riserPlayer.Stop();
			EmitSignalCastWindupEnded();

			if (ParryWindowManager.ConsumeResult())
			{
				GD.Print($"[AstralTwin] Celestial Convergence was deflected!");
				_ownsConvergenceWindow = false;
				_visual.Play("idle");
				return;
			}

			var anyTarget = PickRandomPartyMember();
			if (anyTarget != null)
				SpellPipeline.Cast(_novaSpell, this, anyTarget);

			_ownsConvergenceWindow = false;
		}

		_visual.Play("idle");
	}

	void OnAnimationFinished()
	{
		if (_pendingTarget != null && _pendingTarget.IsAlive)
		{
			SpellResource spell = _pendingAttack switch
			{
				PendingAttack.Strike => _strikeSpell,
				PendingAttack.Starfall => _starfallSpell,
				_ => null
			};
			if (spell != null)
				SpellPipeline.Cast(spell, this, _pendingTarget);
		}

		_pendingTarget = null;
		_pendingAttack = PendingAttack.None;

		if (IsAlive)
			_visual.Play(_isPhaseShielded ? "shielded" : "idle");
	}

	// ── phase-shield helpers ──────────────────────────────────────────────────

	void CheckShieldThresholds(float healthBefore)
	{
		if (!IsAlive) return;
		// Phase shields require a living sibling to break them — skip entirely
		// once the sibling is gone (OnSiblingDied will have retired the thresholds,
		// but this guard catches any race where the signal fires late).
		if (!IsInstanceValid(_sibling) || _sibling.IsBeingRemoved || _sibling.IsQueuedForDeletion() || !_sibling.IsAlive) return;
		for (var i = 0; i < ShieldThresholds.Length; i++)
		{
			if (_thresholdUsed[i]) continue;
			var pct = ShieldThresholds[i];
			if (healthBefore / MaxHealth > pct && CurrentHealth / MaxHealth <= pct)
			{
				_thresholdUsed[i] = true;
				ApplyPhaseShield();
				break;
			}
		}
	}

	void ApplyPhaseShield()
	{
		_isPhaseShielded = true;
		_shieldBreakTotal = _shieldBreakRemaining = ShieldBreakFraction * _sibling.MaxHealth;
		EmitSignalShieldChanged(CharacterName, _shieldBreakRemaining, MaxHealth);
		TwinTetherIndicator.Attach(this, _sibling);

		// Both twins shielded at once would leave nothing damageable — the newer
		// shield takes over and the sibling's breaks.
		if (_sibling.IsPhaseShielded)
			_sibling.ClearPhaseShield();

		// If a one-shot animation (attack/casting) was in flight, interrupting it
		// means AnimationFinished will never fire — clear pending state now so the
		// boss can resume attacking after the shield breaks.
		_pendingAttack = PendingAttack.None;
		_pendingTarget = null;

		// If a Convergence wind-up was counting down, cancel it cleanly.
		// Only consume the parry window if this twin opened it — the sibling's
		// shield interrupt must not steal the other twin's window.
		if (_convergenceWindupTimer > 0f)
		{
			_convergenceWindupTimer = 0f;
			if (_ownsConvergenceWindow)
			{
				_riserPlayer.Stop();
				ParryWindowManager.ConsumeResult(); // discard — window cancelled by shield
				EmitSignalCastWindupEnded();
				_ownsConvergenceWindow = false;
			}
		}

		_visual.Play("shielded");
		GD.Print($"[AstralTwin] {CharacterName} has raised a phase shield! Attack the other twin to break it.");
	}

	public bool IsPhaseShielded => _isPhaseShielded;

	/// <summary>How much of the current phase shield is left (1 = fresh, 0 = broken or none).</summary>
	public float ShieldStrength => _isPhaseShielded && _shieldBreakTotal > 0f
		? Mathf.Clamp(_shieldBreakRemaining / _shieldBreakTotal, 0f, 1f)
		: 0f;

	void ClearPhaseShield()
	{
		_isPhaseShielded = false;
		_shieldBreakRemaining = 0f;
		EmitSignalShieldChanged(CharacterName, 0f, MaxHealth);
		_visual.Play("idle");
		GD.Print($"[AstralTwin] {CharacterName}'s phase shield has been broken!");
	}

	public override void _ExitTree()
	{
		if (_visual != null)
			_visual.AnimationFinished -= OnAnimationFinished;

		if (IsInstanceValid(_sibling) && _siblingDiedHandler != null)
			_sibling.Died -= _siblingDiedHandler;

		base._ExitTree();
	}

	// ── targeting helpers ─────────────────────────────────────────────────────

	/// <summary>Rune of Midnight: scale all ability intervals by the haste multiplier.</summary>
	protected override void OnApplyHasteRune()
	{
		StrikeInterval /= GameConstants.RuneMidnightHasteMultiplier;
		StarfallInterval /= GameConstants.RuneMidnightHasteMultiplier;
		ConvergenceInterval /= GameConstants.RuneMidnightHasteMultiplier;
	}

}
