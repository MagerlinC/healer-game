using System.Collections.Generic;
using Godot;
using healerfantasy;
using healerfantasy.SpellResources;
using healerfantasy.SpellSystem;

/// <summary>
/// The Blood Prince — final boss of the Castle of Blood.
///
/// An ancient vampire lord of terrifying power. This boss introduces a wholly
/// unique mechanic not found in any other encounter: the Blood Covenant.
///
/// ════════════════════════════════════════════════════════════════════════════
/// THE BLOOD COVENANT — UNIQUE MECHANIC
/// ════════════════════════════════════════════════════════════════════════════
///
/// At 50% HP the Blood Prince reveals his true power and invokes the Blood
/// Covenant — a curse that binds the life energy of the party to himself.
///
/// From this point forward: EVERY POINT OF HEALING cast on a party member
/// is partially siphoned and used to heal the Blood Prince for
/// <see cref="BloodCovenantSiphonFraction"/> of the heal amount (default 40%).
///
/// This inverts the player's core incentive. In every other fight, the
/// healer casts as much healing as possible. Here, healing too aggressively
/// sustains the boss. The player must:
///   1. Accept that party members will sit at dangerously low health.
///   2. Cast only the minimum healing needed to keep party members alive.
///   3. Balance healing output against boss sustain to win the DPS race.
///
/// The siphon is implemented by tracking each party member's health between
/// frames in <see cref="_Process"/>. When a member's health increases (any
/// source of healing — spells, HoTs, etc.), the Blood Prince heals for 40%
/// of the delta. This is intentionally broad so that no heal bypasses the
/// covenant, regardless of how it was applied.
///
/// ════════════════════════════════════════════════════════════════════════════
/// PHASE 1 (100% → 50%)
/// ════════════════════════════════════════════════════════════════════════════
/// • <see cref="SlashInterval"/> — Regal Slash: melee on tank, 45 damage.
/// • <see cref="BloodBoltInterval"/> — Blood Bolt: ranged hit on random target, 48 damage.
/// • <see cref="SiphonInterval"/> — Sanguine Siphon: channeled spell leeching life from linked targets
///
/// ════════════════════════════════════════════════════════════════════════════
/// PHASE TRANSITION (at 50% HP)
/// ════════════════════════════════════════════════════════════════════════════
/// • The Blood Prince pauses briefly and emits <see cref="BloodCovenantActivated"/>.
/// • All attack timers are reset; Regal Slash damage increases to 60.
/// • Blood Covenant siphon begins immediately.
///
/// ════════════════════════════════════════════════════════════════════════════
/// PHASE 2 (50% → 0%)
/// ════════════════════════════════════════════════════════════════════════════
/// • Regal Slash: damage increased to 60.
/// • Blood Bolt: same as Phase 1.
/// • Sanguine Siphon: now applied to FOUR party members simultaneously.
/// • <see cref="VoidDrainInterval"/> — Void Drain: non-dispellable AoE DoT on
///   the entire party, 20 damage/sec for 10 seconds. Dramatically amplifies
///   the healing burden — and therefore the Blood Covenant siphon.
/// • Blood Covenant siphon (40% of all healing received by party).
///
/// ════════════════════════════════════════════════════════════════════════════
/// ANIMATIONS
/// ════════════════════════════════════════════════════════════════════════════
/// Loaded from res://assets/enemies/the-blood-prince/.
///
///   "idle"   — idle1–idle3   (looping, 4 fps)
///   "attack" — attack1–attack4 (one-shot → idle, 10 fps)
///   "cast"   — cast1–cast4   (one-shot → idle, 6 fps)
/// </summary>
public partial class TheBloodPrince : Character
{
	public TheBloodPrince()
	{
		MaxHealth = GameConstants.BossHealthBaseValuesByDungeonTier[GameConstants.CastleOfBloodTier][2];
	}

	// ── signals ───────────────────────────────────────────────────────────────

	/// <summary>
	/// Emitted the moment the Blood Covenant activates (phase transition at 50% HP).
	/// The UI can listen to this to display a dramatic overlay or announcement.
	/// </summary>
	[Signal]
	public delegate void BloodCovenantActivatedEventHandler();

	[Signal]
	public delegate void CastWindupStartedEventHandler(string spellName, Texture2D icon, float duration);

	[Signal]
	public delegate void CastWindupEndedEventHandler();

	// ── Sanguine Siphon channel signals ───────────────────────────────────────

	/// <summary>
	/// Emitted when the Sanguine Siphon channel begins.
	/// The boss cast bar listens to this to display a reverse (draining) channel bar.
	/// </summary>
	[Signal]
	public delegate void SanguineChannelStartedEventHandler(BossBloodPrinceSanguineSiphonSpell spell);

	/// <summary>Emitted when the Sanguine Siphon channel ends (naturally or cancelled).</summary>
	[Signal]
	public delegate void SanguineChannelEndedEventHandler();

	/// <summary>
	/// Emitted at channel start with the health fraction (0 – 1) the boss must
	/// be damaged to in order to break the channel early.
	/// The boss health bar uses this to draw a golden break-threshold marker.
	/// </summary>
	[Signal]
	public delegate void SanguineHealthTargetSetEventHandler(float targetFraction);

	/// <summary>Emitted when the channel ends; removes the health-threshold marker from the UI.</summary>
	[Signal]
	public delegate void SanguineHealthTargetClearedEventHandler();

	// ── tuneable exports ──────────────────────────────────────────────────────

	// Phase 1 intervals
	[Export] public float SlashInterval = 2.5f;
	[Export] public float BloodBoltInterval = 5.0f;
	[Export] public float SiphonInterval = 14.0f;

	// Phase 2 additional intervals
	[Export] public float VoidDrainInterval = 18.0f;

	// Damage values
	[Export] public float Phase1SlashDamage = 45f;
	[Export] public float Phase2SlashDamage = 60f;
	[Export] public float BloodBoltDamage = 48f;

	/// <summary>
	/// Fraction of all healing applied to party members that is siphoned to
	/// heal the Blood Prince during Phase 2. Default 40%.
	/// </summary>
	[Export] public float BloodCovenantSiphonFraction = 0.40f;

	/// <summary>HP fraction (0–1) at which Phase 2 begins. Default 50%.</summary>
	[Export] public float PhaseTwoThreshold = 0.50f;

	// ── internal state ────────────────────────────────────────────────────────

	float _slashTimer;
	float _bloodBoltTimer;
	float _siphonTimer;
	float _voidDrainTimer;

	BossBloodPrinceSlashSpell _slashSpell;
	BossBloodPrinceBloodBoltSpell _bloodBoltSpell;
	BossBloodPrinceSanguineSiphonSpell _siphonSpell;
	BossBloodPrinceVoidDrainSpell _voidDrainSpell;

	AnimatedSprite2D _sprite;

	bool _phaseTwoActive;

	/// <summary>
	/// Targets pre-selected when <see cref="CastSanguineSiphon"/> fires.
	/// Stored here so the same targets are used when the animation ends
	/// (avoids picking a new random set between cast-start and cast-resolve).
	/// </summary>
	List<Character> _siphonTargets = new();

	/// <summary>
	/// True while a Sanguine Siphon channel is active.
	/// Prevents the siphon timer from triggering a new cast mid-channel.
	/// </summary>
	bool _isSiphonChanneling;

	// Blood Covenant — previous health snapshot for each party member.
	// Keyed by CharacterName for fast lookup each frame.
	readonly Dictionary<string, float> _prevPartyHealth = new();

	enum PendingAttack
	{
		None,
		Slash,
		BloodBolt,
		Siphon
	}

	PendingAttack _pendingAttack;
	Character _pendingTarget;

	const string AssetBase = "res://assets/enemies/the-blood-prince/";

	// ── lifecycle ─────────────────────────────────────────────────────────────

	public override void _Ready()
	{
		base._Ready();
		CharacterName = GameConstants.CastleBoss3Name;
		RemoveFromGroup("party");
		AddToGroup(GameConstants.BossGroupName);
		IsFriendly = false;

		// Stagger first attacks.
		_slashTimer = SlashInterval;
		_bloodBoltTimer = BloodBoltInterval;
		_siphonTimer = 10f; // Start after 10s, then every SiphonInterval.
		_voidDrainTimer = VoidDrainInterval;

		_slashSpell = new BossBloodPrinceSlashSpell { DamageAmount = Phase1SlashDamage };
		_bloodBoltSpell = new BossBloodPrinceBloodBoltSpell { DamageAmount = BloodBoltDamage };
		_siphonSpell = new BossBloodPrinceSanguineSiphonSpell { Boss = this };
		_voidDrainSpell = new BossBloodPrinceVoidDrainSpell { Boss = this };

		GlobalAutoLoad.RegisterSignalEmitter(this, nameof(BloodCovenantActivated));
		GlobalAutoLoad.RegisterSignalEmitter(this, nameof(CastWindupStarted));
		GlobalAutoLoad.RegisterSignalEmitter(this, nameof(CastWindupEnded));
		GlobalAutoLoad.RegisterSignalEmitter(this, nameof(SanguineChannelStarted));
		GlobalAutoLoad.RegisterSignalEmitter(this, nameof(SanguineChannelEnded));
		GlobalAutoLoad.RegisterSignalEmitter(this, nameof(SanguineHealthTargetSet));
		GlobalAutoLoad.RegisterSignalEmitter(this, nameof(SanguineHealthTargetCleared));

		_sprite = GetNode<AnimatedSprite2D>("AnimatedSprite2D");
		_sprite.Scale = new Vector2(0.4f, 0.4f);
		SetupAnimations();
		_sprite.AnimationFinished += OnAnimationFinished;
		_sprite.Play("idle");
		ApplyRuneModifiers();

		// Seed the health snapshot so the first frame has valid previous values.
		SeedPartyHealthSnapshot();
	}

	public override void _Process(double delta)
	{
		base._Process(delta);

		if (!IsAlive) return;

		// ── Phase 2 transition check ──────────────────────────────────────────
		if (!_phaseTwoActive && CurrentHealth / MaxHealth <= PhaseTwoThreshold)
			TriggerPhaseTwo();

		// ── Blood Covenant siphon ─────────────────────────────────────────────
		// Track every party member's health. If their health increased since last
		// frame, some healing reached them — siphon a fraction to this boss.
		if (_phaseTwoActive)
			ProcessBloodCovenant();

		// ── Regular attack timers ─────────────────────────────────────────────
		_slashTimer -= (float)delta;
		_bloodBoltTimer -= (float)delta;
		_siphonTimer -= (float)delta;

		if (_phaseTwoActive)
			_voidDrainTimer -= (float)delta;

		if (_pendingAttack != PendingAttack.None) return;

		// Priority: Void Drain > Siphon > Blood Bolt > Slash.
		if (_phaseTwoActive && _voidDrainTimer <= 0f)
		{
			_voidDrainTimer = VoidDrainInterval;
			CastVoidDrain();
		}
		else if (_siphonTimer <= 0f && !_isSiphonChanneling)
		{
			_siphonTimer = SiphonInterval;
			CastSanguineSiphon();
		}
		else if (_bloodBoltTimer <= 0f)
		{
			_bloodBoltTimer = BloodBoltInterval;
			CastBloodBolt();
		}
		else if (_slashTimer <= 0f)
		{
			_slashTimer = SlashInterval;
			PerformSlash();
		}
	}

	// ── phase transition ──────────────────────────────────────────────────────

	void TriggerPhaseTwo()
	{
		_phaseTwoActive = true;

		// Upgrade Regal Slash damage.
		_slashSpell.DamageAmount = Phase2SlashDamage;

		// Reset attack timers so Phase 2 opens with a quick flurry.
		_slashTimer = 1.0f;
		_bloodBoltTimer = 3.0f;
		_siphonTimer = 5.0f;
		_voidDrainTimer = VoidDrainInterval;

		// Initialise the health snapshot immediately so the first covenant
		// frame doesn't misfire on the boss's own heal-to-half.
		SeedPartyHealthSnapshot();

		EmitSignalBloodCovenantActivated();
		GD.Print("[BloodPrince] Blood Covenant activated! Siphon fraction: " +
		         $"{BloodCovenantSiphonFraction * 100f:F0}%");
	}

	// ── Blood Covenant ────────────────────────────────────────────────────────

	/// <summary>
	/// Each frame during Phase 2, compare each living party member's current
	/// health against the snapshot from the previous frame. Any increase in
	/// health represents healing — siphon <see cref="BloodCovenantSiphonFraction"/>
	/// of that delta directly to the Blood Prince.
	/// </summary>
	void ProcessBloodCovenant()
	{
		foreach (var node in GetTree().GetNodesInGroup("party"))
		{
			if (node is not Character member || !member.IsAlive) continue;

			_prevPartyHealth.TryGetValue(member.CharacterName, out var prev);
			var current = member.CurrentHealth;

			if (current > prev)
			{
				// Healing detected — siphon a fraction.
				var healingDelta = current - prev;
				var siphon = healingDelta * BloodCovenantSiphonFraction;

				if (siphon > 0f && IsAlive)
				{
					Heal(siphon);
					RaiseFloatingCombatText(siphon, true, (int)SpellSchool.Void, false);
				}
			}

			// Update snapshot regardless of direction so damage is not mistaken
			// for a subsequent heal next frame.
			_prevPartyHealth[member.CharacterName] = current;
		}
	}

	/// <summary>
	/// Initialise (or re-seed) the health snapshot to current party health.
	/// Call at the start of Phase 2 to avoid a spurious siphon on the first frame.
	/// </summary>
	void SeedPartyHealthSnapshot()
	{
		_prevPartyHealth.Clear();
		foreach (var node in GetTree().GetNodesInGroup("party"))
			if (node is Character c && c.IsAlive)
				_prevPartyHealth[c.CharacterName] = c.CurrentHealth;
	}

	// ── combat actions ────────────────────────────────────────────────────────

	void PerformSlash()
	{
		var target = FindTank() ?? PickRandomPartyMember();
		if (target == null) return;
		_pendingTarget = target;
		_pendingAttack = PendingAttack.Slash;
		_sprite.Play("attack");
	}

	void CastBloodBolt()
	{
		var target = PickRandomPartyMember();
		if (target == null) return;
		_pendingTarget = target;
		_pendingAttack = PendingAttack.BloodBolt;
		_sprite.Play("cast");
	}

	/// <summary>
	/// Casts SanguineSiphon. In Phase 1 two targets are chosen; in Phase 2 four.
	/// Targets are locked in here so the same set is used when the animation ends.
	/// Emits <see cref="CastWindupStarted"/> so the cast bar shows during the wind-up.
	/// </summary>
	void CastSanguineSiphon()
	{
		_siphonTargets = PickSiphonTargets();
		if (_siphonTargets.Count == 0) return;

		// Use the first target as the animation pivot; the spell resolves all targets.
		_pendingTarget = _siphonTargets[0];
		_pendingAttack = PendingAttack.Siphon;
		_sprite.Play("cast");

		// Show the pre-channel cast bar (1 s cast time).
		EmitSignalCastWindupStarted("Sanguine Siphon", _siphonSpell.Icon, _siphonSpell.CastTime);
	}

	/// <summary>
	/// Void Drain fires immediately (no animation gate) — the AoE DoT is
	/// applied to every party member regardless of what animation is playing.
	/// Reset pending state so melee/bolt can continue normally.
	/// </summary>
	void CastVoidDrain()
	{
		var anyTarget = PickRandomPartyMember();
		if (anyTarget == null) return;
		SpellPipeline.Cast(_voidDrainSpell, this, anyTarget);
		GD.Print("[BloodPrince] Void Drain cast on entire party.");
	}

	void OnAnimationFinished()
	{
		if (_pendingTarget != null && _pendingTarget.IsAlive)
		{
			switch (_pendingAttack)
			{
				case PendingAttack.Slash:
					SpellPipeline.Cast(_slashSpell, this, _pendingTarget);
					break;

				case PendingAttack.BloodBolt:
					SpellPipeline.Cast(_bloodBoltSpell, this, _pendingTarget);
					break;

				case PendingAttack.Siphon:
					// End the cast-windup bar; the channel bar will appear via
					// OnSanguineSiphonChannelStarted once Apply runs.
					EmitSignalCastWindupEnded();

					// Filter out targets that died during the cast wind-up.
					_siphonTargets.RemoveAll(t => !IsInstanceValid(t) || !t.IsAlive);
					if (_siphonTargets.Count > 0)
					{
						// Hand the pre-selected target list to the spell so ResolveTargets
						// returns all of them in a single pipeline pass.
						_siphonSpell.PendingTargets = _siphonTargets;
						SpellPipeline.Cast(_siphonSpell, this, _siphonTargets[0]);
					}

					_siphonTargets = new List<Character>();
					break;
			}
		}

		_pendingTarget = null;
		_pendingAttack = PendingAttack.None;

		if (IsAlive)
			_sprite.Play("idle");
	}

	// ── Sanguine Siphon channel callbacks ────────────────────────────────────

	/// <summary>
	/// Called by <see cref="healerfantasy.SpellResources.BossBloodPrinceSanguineSiphonSpell"/>
	/// immediately after the channel node has been added to the scene and the drain
	/// debuff has recorded the boss's current health.
	/// Emits UI signals so the cast bar switches to channel mode and the health bar
	/// shows the break-threshold marker.
	/// </summary>
	public void OnSanguineSiphonChannelStarted(BossBloodPrinceSanguineSiphonSpell spell, float healthTargetFraction)
	{
		_isSiphonChanneling = true;
		EmitSignalSanguineChannelStarted(spell);
		EmitSignalSanguineHealthTargetSet(healthTargetFraction);

		GD.Print($"[BloodPrince] Sanguine Siphon channel started. " +
		         $"Duration: {spell.ChannelDuration:F1}s, break at {healthTargetFraction * 100f:F0}% HP.");
	}

	/// <summary>
	/// Called by <see cref="SanguineSiphonChannelNode"/> when the channel ends
	/// (naturally or cancelled). Clears the channeling guard and UI signals.
	/// </summary>
	public void OnSanguineSiphonChannelEnded()
	{
		_isSiphonChanneling = false;
		EmitSignalSanguineChannelEnded();
		EmitSignalSanguineHealthTargetCleared();

		GD.Print("[BloodPrince] Sanguine Siphon channel ended.");
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

	/// <summary>
	/// Picks mark targets: one in Phase 1, two distinct members in Phase 2.
	/// Shuffles the living party list and takes the first N.
	/// </summary>
	List<Character> PickSiphonTargets()
	{
		var alive = new List<Character>();
		foreach (var node in GetTree().GetNodesInGroup("party"))
			if (node is Character c && c.IsAlive)
				alive.Add(c);

		// Fisher-Yates shuffle (two-pass is sufficient for small lists).
		for (var i = alive.Count - 1; i > 0; i--)
		{
			var j = (int)(GD.Randi() % (uint)(i + 1));
			(alive[i], alive[j]) = (alive[j], alive[i]);
		}

		var count = _phaseTwoActive ? Mathf.Min(4, alive.Count) : Mathf.Min(2, alive.Count);
		return alive.GetRange(0, count);
	}

	// ── animation setup ───────────────────────────────────────────────────────

	/// <summary>
	/// Loads individual PNG frames from res://assets/enemies/the-blood-prince/.
	///
	/// CURRENT ASSETS: only idle1–3 exist.
	///
	/// "attack" and "cast" are placeholders that reuse idle frames as one-shot
	/// animations. Replace the placeholder blocks below with real asset paths
	/// once the attack and cast sprite sheets are finalised.
	/// </summary>
	/// <summary>Rune of Time: scale all ability intervals by the haste multiplier.</summary>
	protected override void OnApplyHasteRune()
	{
		SlashInterval /= GameConstants.RuneTimeHasteMultiplier;
		BloodBoltInterval /= GameConstants.RuneTimeHasteMultiplier;
		SiphonInterval /= GameConstants.RuneTimeHasteMultiplier;
		VoidDrainInterval /= GameConstants.RuneTimeHasteMultiplier;
	}

		void SetupAnimations()
	{
		var frames = new SpriteFrames();
		frames.RemoveAnimation("default");

		AddAnimFromFiles(frames, "idle", "idle", 3, 4f, true);
		AddAnimFromFiles(frames, "attack", "attack", 4, 10f, false);
		AddAnimFromFiles(frames, "cast", "cast", 4, 6f, false);

		_sprite.SpriteFrames = frames;
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