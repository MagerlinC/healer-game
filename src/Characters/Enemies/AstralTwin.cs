using System.Collections.Generic;
using Godot;
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
/// completely immune — all incoming damage is discarded.  The shield breaks the
/// instant the PLAYER deals damage to the OTHER twin, which also causes the party
/// to switch focus.  This forces repeated target-swapping throughout the fight.
///
/// The "shielded" animation frame is displayed while the immunity is active.
///
/// Animations loaded from individual PNGs at runtime:
///   res://assets/enemies/astral-twins/{frame}.png
///   "idle"    — idle1–idle3      (looping)
///   "attack"  — attack1–attack3  (one-shot → idle)
///   "casting" — casting1–casting4 (one-shot → idle)
///   "shielded"— shielded.png     (single frame, looping)
/// </summary>
public partial class AstralTwin : Character
{
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

	AnimatedSprite2D _sprite;
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

	/// <summary>
	/// Threshold percentages at which this twin shields itself.
	/// Each threshold is used exactly once.
	/// </summary>
	static readonly float[] ShieldThresholds = { 0.75f, 0.50f, 0.25f };

	readonly bool[] _thresholdUsed = new bool[3];

	/// <summary>Reference to the sibling twin — wired up lazily after _Ready.</summary>
	AstralTwin _sibling;

	DiedEventHandler _siblingDiedHandler;

	const string AssetBase = "res://assets/enemies/astral-twins/";

	// ── lifecycle ─────────────────────────────────────────────────────────────

	public override void _Ready()
	{
		base._Ready();
		RemoveFromGroup("party");
		AddToGroup(GameConstants.BossGroupName);
		IsFriendly = false;

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

		_sprite = GetNode<AnimatedSprite2D>("AnimatedSprite2D");
		SetupAnimations();
		_sprite.AnimationFinished += OnAnimationFinished;
		_sprite.Play("idle");

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
	}

	public override void _Process(double delta)
	{
		base._Process(delta);
		if (!IsAlive) return;

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

		// Any successful hit on this twin immediately breaks the sibling's shield.
		if (IsInstanceValid(_sibling) && !_sibling.IsBeingRemoved && !_sibling.IsQueuedForDeletion())
			_sibling.NotifySiblingHit();

		// Check whether crossing a threshold triggers our OWN shield.
		CheckShieldThresholds(healthBefore);
	}

	/// <summary>
	/// Called by the sibling twin when it takes damage, giving this twin a
	/// chance to drop its own phase-shield if it is currently active.
	/// </summary>
	public void NotifySiblingHit()
	{
		if (_isPhaseShielded)
			ClearPhaseShield();
	}

	// ── combat actions ────────────────────────────────────────────────────────

	void PerformStrike()
	{
		var target = FindTank() ?? PickRandomPartyMember();
		if (target == null) return;
		_pendingTarget = target;
		_pendingAttack = PendingAttack.Strike;
		_sprite.Play("attack");
	}

	void CastStarfall()
	{
		var target = PickRandomPartyMember();
		if (target == null) return;
		_pendingTarget = target;
		_pendingAttack = PendingAttack.Starfall;
		_sprite.Play("casting");
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
			ParryWindowManager.OpenWindow();
			EmitSignalCastWindupStarted(_novaSpell.Name, _novaSpell.Icon, ConvergenceWindupDuration);
			_riserPlayer.Play();
		}

		_pendingAttack = PendingAttack.None;
		_sprite.Play("casting");
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
				_sprite.Play("idle");
				return;
			}

			var anyTarget = PickRandomPartyMember();
			if (anyTarget != null)
				SpellPipeline.Cast(_novaSpell, this, anyTarget);

			_ownsConvergenceWindow = false;
		}

		_sprite.Play("idle");
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
			_sprite.Play(_isPhaseShielded ? "shielded" : "idle");
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

		_sprite.Play("shielded");
		GD.Print($"[AstralTwin] {CharacterName} has raised a phase shield! Attack the other twin to break it.");
	}

	void ClearPhaseShield()
	{
		_isPhaseShielded = false;
		_sprite.Play("idle");
		GD.Print($"[AstralTwin] {CharacterName}'s phase shield has been broken!");
	}

	public override void _ExitTree()
	{
		if (_sprite != null)
			_sprite.AnimationFinished -= OnAnimationFinished;

		if (IsInstanceValid(_sibling) && _siblingDiedHandler != null)
			_sibling.Died -= _siblingDiedHandler;

		base._ExitTree();
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
	/// Loads individual PNG frames from res://assets/enemies/astral-twins/.
	/// idle1–3, attack1–3, casting1–4, shielded (single static frame).
	/// </summary>
	void SetupAnimations()
	{
		var frames = new SpriteFrames();
		frames.RemoveAnimation("default");

		AddAnimFromFiles(frames, "idle", "idle", 3, 6f, true);
		AddAnimFromFiles(frames, "attack", "attack", 3, 10f, false);
		AddAnimFromFiles(frames, "casting", "casting", 4, 8f, false);

		// Shielded: single static frame, looping so the sprite stays on it.
		frames.AddAnimation("shielded");
		frames.SetAnimationLoop("shielded", true);
		frames.SetAnimationSpeed("shielded", 1f);
		frames.AddFrame("shielded", GD.Load<Texture2D>(AssetBase + "shielded.png"));

		_sprite.SpriteFrames = frames;
		_sprite.Scale = new Vector2(0.3f, 0.3f);
	}

	static void AddAnimFromFiles(SpriteFrames frames, string animName,
		string filePrefix, int count, float fps, bool loop)
	{
		frames.AddAnimation(animName);
		frames.SetAnimationLoop(animName, loop);
		frames.SetAnimationSpeed(animName, fps);
		for (var i = 1; i <= count; i++)
		{
			var texture = GD.Load<Texture2D>(AssetBase + $"{filePrefix}{i}.png");
			frames.AddFrame(animName, texture);
		}
	}
}