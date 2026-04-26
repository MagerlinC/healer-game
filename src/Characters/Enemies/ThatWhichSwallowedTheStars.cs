using System.Collections.Generic;
using Godot;
using healerfantasy;
using healerfantasy.SpellResources;
using healerfantasy.SpellSystem;

/// <summary>
/// That Which Swallowed the Stars — the final boss of the Sanctum of Stars
/// (and of the whole run).
///
/// An ancient cosmic horror of immeasurable scale, reduced here to a
/// battlefield manifestation that barely fits within mortal perception.
///
/// Behaviour
/// ─────────
/// • Every <see cref="BeamInterval"/> seconds — Stellar Beam: fires a sustained
///   beam of void-star energy at a random party member for heavy damage;
///   uses the "beam" animation.
///
/// • Every <see cref="CataclysmInterval"/> seconds — Void Cataclysm:
///   a three-part sequence, each part independently deflectable —
///
///   1. Wind-up (2.5 s): the "cast" animation plays; CastWindupStarted emitted
///      to show the boss cast bar.
///   2. Hit 1 (1.5 s parry window): CastWindupStarted re-emitted; parry window
///      opens; on resolution — deal 65 damage to the whole party unless deflected.
///   3. Hit 2 (1.5 s): same as above.
///   4. Hit 3 (1.5 s): same; CastWindupEnded emitted after resolution.
///
///   Between hits the boss plays the "cast" animation again as a brief visual.
///   The existing DeflectOverlay and riser sound fire for EACH of the three waves.
///
/// Animations loaded from individual PNGs at runtime:
///   res://assets/enemies/that-which-swallowed-the-stars/
///   "idle"    — idle1–idle2         (looping)
///   "beam"    — beam1–beam3         (one-shot → idle)
///   "cast"    — cast1–cast3         (used for both wind-up and inter-hit)
///   "default" — default.png         (single static frame, used for the initial
///                                    reveal beat; falls through to idle in-fight)
/// </summary>
public partial class ThatWhichSwallowedTheStars : Character
{
	public ThatWhichSwallowedTheStars()
	{
		MaxHealth = 2200f;
	}

	// ── signals ───────────────────────────────────────────────────────────────

	[Signal]
	public delegate void CastWindupStartedEventHandler(string spellName, Texture2D icon, float duration);

	[Signal]
	public delegate void CastWindupEndedEventHandler();

	// ── tuneable exports ──────────────────────────────────────────────────────

	[Export] public float BeamInterval         = 8.0f;
	[Export] public float CataclysmInterval    = 20.0f;
	[Export] public float CataclysmWindup      = 2.5f;    // initial wind-up before hit 1
	[Export] public float CataclysmHitWindow   = 1.5f;    // parry window duration per hit

	[Export] public float BeamDamage       = 75f;
	[Export] public float CataclysmDamage  = 65f;         // per hit

	// ── internal state ────────────────────────────────────────────────────────

	float _beamTimer;
	float _cataclysmTimer;

	BossTwstsBeamSpell          _beamSpell;
	BossTwstsVoidCataclysmSpell _cataclysmSpell;

	AnimatedSprite2D  _sprite;
	AudioStreamPlayer _riserPlayer;

	// Beam is a one-shot animation → spell.
	bool _beamPending;
	Character _beamTarget;

	// ── Void Cataclysm state machine ──────────────────────────────────────────

	enum CataclysmPhase
	{
		None,
		Windup,    // initial cast animation plays, boss bar counts down
		Hit1,      // parry window 1 — 1.5 s
		Hit2,      // parry window 2 — 1.5 s
		Hit3       // parry window 3 — 1.5 s
	}

	CataclysmPhase _cataclysmPhase;
	float          _cataclysmPhaseTimer;

	const string AssetBase = "res://assets/enemies/that-which-swallowed-the-stars/";

	// ── lifecycle ─────────────────────────────────────────────────────────────

	public override void _Ready()
	{
		base._Ready();
		CharacterName = GameConstants.SanctumBoss3Name;
		RemoveFromGroup("party");
		AddToGroup(GameConstants.BossGroupName);
		IsFriendly = false;

		// Stagger first attacks — give the player a brief moment before the onslaught.
		_beamTimer      = BeamInterval;
		_cataclysmTimer = CataclysmInterval;

		_beamSpell       = new BossTwstsBeamSpell          { DamageAmount = BeamDamage };
		_cataclysmSpell  = new BossTwstsVoidCataclysmSpell { DamageAmount = CataclysmDamage };

		GlobalAutoLoad.RegisterSignalEmitter(this, nameof(CastWindupStarted));
		GlobalAutoLoad.RegisterSignalEmitter(this, nameof(CastWindupEnded));

		_riserPlayer = new AudioStreamPlayer();
		_riserPlayer.Stream = GD.Load<AudioStream>(AssetConstants.ParryRiserSoundPath);
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

		// ── Void Cataclysm state machine ──────────────────────────────────────
		if (_cataclysmPhase != CataclysmPhase.None)
		{
			_cataclysmPhaseTimer -= (float)delta;
			if (_cataclysmPhaseTimer <= 0f)
				AdvanceCataclysm();
			return; // block other attacks while cataclysm runs
		}

		// ── Regular attack timers ─────────────────────────────────────────────
		_beamTimer      -= (float)delta;
		_cataclysmTimer -= (float)delta;

		if (_beamPending) return; // beam animation in flight

		if (_beamTimer <= 0f)
		{
			_beamTimer = BeamInterval;
			PerformBeam();
		}
		else if (_cataclysmTimer <= 0f)
		{
			_cataclysmTimer = CataclysmInterval;
			BeginCataclysm();
		}
	}

	// ── combat actions ────────────────────────────────────────────────────────

	void PerformBeam()
	{
		var target = PickRandomPartyMember();
		if (target == null) return;
		_beamTarget  = target;
		_beamPending = true;
		_sprite.Play("beam");
	}

	/// <summary>Kicks off the Void Cataclysm wind-up (phase: Windup).</summary>
	void BeginCataclysm()
	{
		_cataclysmPhase      = CataclysmPhase.Windup;
		_cataclysmPhaseTimer = CataclysmWindup;

		// One cast bar for the full wind-up.  The three hits that follow fire
		// without additional cast bars — each is heralded only by the riser sound.
		_riserPlayer.Play();
		EmitSignalCastWindupStarted(_cataclysmSpell.Name, _cataclysmSpell.Icon, CataclysmWindup);
		_sprite.Play("cast");
	}

	/// <summary>
	/// Advances the Cataclysm state machine one phase.
	/// Called when the current phase timer expires.
	/// </summary>
	void AdvanceCataclysm()
	{
		switch (_cataclysmPhase)
		{
			case CataclysmPhase.Windup:
				// Wind-up cast bar ends → close it, then immediately open the first
				// parry window.  No new cast bar is shown for the individual hits.
				EmitSignalCastWindupEnded();
				StartCataclysmHit(CataclysmPhase.Hit1);
				break;

			case CataclysmPhase.Hit1:
				ResolveCataclysmHit(1);
				StartCataclysmHit(CataclysmPhase.Hit2);
				break;

			case CataclysmPhase.Hit2:
				ResolveCataclysmHit(2);
				StartCataclysmHit(CataclysmPhase.Hit3);
				break;

			case CataclysmPhase.Hit3:
				ResolveCataclysmHit(3);
				_cataclysmPhase = CataclysmPhase.None;
				_sprite.Play("idle");
				break;
		}
	}

	/// <summary>
	/// Opens a parry window for one Cataclysm hit.
	/// Deliberately does NOT emit CastWindupStarted — the single cast bar shown
	/// during the wind-up phase is sufficient; we don't want three extra bars.
	/// </summary>
	void StartCataclysmHit(CataclysmPhase phase)
	{
		_cataclysmPhase      = phase;
		_cataclysmPhaseTimer = CataclysmHitWindow;

		_riserPlayer.Play();
		ParryWindowManager.OpenWindow();
		_sprite.Play("cast");
	}

	/// <summary>Resolves one Cataclysm hit — deflected or lands for full damage.</summary>
	void ResolveCataclysmHit(int hitNumber)
	{
		if (ParryWindowManager.ConsumeResult())
		{
			GD.Print($"[TWSTS] Void Cataclysm hit {hitNumber} was deflected!");
			return;
		}

		var anyTarget = PickRandomPartyMember();
		if (anyTarget != null)
			SpellPipeline.Cast(_cataclysmSpell, this, anyTarget);
	}

	void OnAnimationFinished()
	{
		// Beam animation completes → deal damage.
		if (_beamPending)
		{
			if (_beamTarget != null && _beamTarget.IsAlive)
				SpellPipeline.Cast(_beamSpell, this, _beamTarget);

			_beamTarget  = null;
			_beamPending = false;
		}

		// Return to idle only when no cataclysm is running (it manages the sprite directly).
		if (_cataclysmPhase == CataclysmPhase.None && IsAlive)
			_sprite.Play("idle");
	}

	// ── targeting helper ──────────────────────────────────────────────────────

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
	/// Loads individual PNG frames from:
	///   res://assets/enemies/that-which-swallowed-the-stars/
	///
	/// idle1–2 (looping), beam1–3 (one-shot), cast1–3 (one-shot).
	/// "default" frame is added as a single-frame "default" animation for the
	/// reveal beat; the boss transitions straight to "idle" during normal combat.
	/// </summary>
	void SetupAnimations()
	{
		var frames = new SpriteFrames();
		frames.RemoveAnimation("default");

		AddAnimFromFiles(frames, "idle",    "idle",    2, 4f,  true);
		AddAnimFromFiles(frames, "beam",    "beam",    3, 10f, false);
		AddAnimFromFiles(frames, "cast",    "cast",    3, 8f,  false);

		// Single-frame "default" pose for the initial reveal.
		var defaultAnim = "reveal";
		frames.AddAnimation(defaultAnim);
		frames.SetAnimationLoop(defaultAnim, false);
		frames.SetAnimationSpeed(defaultAnim, 1f);
		frames.AddFrame(defaultAnim, GD.Load<Texture2D>(AssetBase + "default.png"));

		_sprite.SpriteFrames = frames;
		_sprite.Scale = new Vector2(0.35f, 0.35f);
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
