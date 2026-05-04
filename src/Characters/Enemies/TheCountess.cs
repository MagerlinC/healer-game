using System.Collections.Generic;
using Godot;
using healerfantasy;
using healerfantasy.Effects;
using healerfantasy.SpellResources;
using healerfantasy.SpellSystem;

/// <summary>
/// The Countess — second boss of the Castle of Blood.
///
/// An ancient vampiric noblewoman of terrifying elegance, wielding blood
/// sorcery to weaken her enemies and punish the healer's spell choices.
///
/// Behaviour
/// ─────────
/// • Every <see cref="MeleeAttackInterval"/> seconds — Noble Strike:
///   a swift melee attack at the tank (Templar).
///
/// • Every <see cref="BloodBoltInterval"/> seconds — Blood Bolt:
///   a ranged crimson projectile hurled at a random party member.
///
/// • Every <see cref="CurseInterval"/> seconds — Crimson Curse:
///   applies a 10-second dispellable debuff to a random party member that
///   deals 15 damage/sec AND reduces all healing received by 50%.
///   Forces a healing triage decision: dispel (removing the healing penalty)
///   or tank the DoT and heal through it normally.
///
/// • Every <see cref="NovaInterval"/> seconds — Sanguine Nova:
///   a telegraphed AoE blood explosion targeting the whole party.
///   Opens a <see cref="NovaWindupDuration"/>-second parry window.
///   Deflect cancels it; otherwise all party members take 60 damage.
///
/// • Blood Shield (once, at 35% HP) — the Countess conjures a large shield
///   of coagulated blood, absorbing the next 450 damage dealt to her.
///   Only triggers once per fight.
///
/// Court of Reflections (at 75%, 50%, 25% HP)
/// ─────────────────────────────────────────────
/// The Countess vanishes and spawns alongside X identical copies of herself.
/// A non-dispellable ramping DoT is applied to the whole party. The player
/// must identify and interact with the real Countess (by walking into her or
/// dispelling her) to end the mechanic. Interacting with a copy removes only
/// that copy. Hovering over any copy glows it gold as a targeting indicator.
///
/// Animations (individual PNGs in res://assets/enemies/the-countess/):
///   "idle"    — idle1–idle2     (looping)
///   "attack"  — attack1–attack4 (one-shot → idle)
///   "casting" — casting1–casting3 (one-shot → idle; used for spells and Nova wind-up)
/// </summary>
public partial class TheCountess : EnemyCharacter
{
	public TheCountess()
	{
		MaxHealth = GameConstants.BossHealthBaseValuesByDungeonTier[GameConstants.CastleOfBloodTier][1];
	}

	// ── signals ───────────────────────────────────────────────────────────────

	[Signal]
	public delegate void CastWindupStartedEventHandler(string spellName, Texture2D icon, float duration);

	[Signal]
	public delegate void CastWindupEndedEventHandler();

	/// <summary>Emitted when the Court of Reflections phase begins.</summary>
	[Signal]
	public delegate void CourtOfReflectionsStartedEventHandler();

	/// <summary>Emitted when the Court of Reflections phase ends (real boss found).</summary>
	[Signal]
	public delegate void CourtOfReflectionsEndedEventHandler();

	// ── tuneable exports ──────────────────────────────────────────────────────

	[Export] public float MeleeAttackInterval = 2.5f;
	[Export] public float BloodBoltInterval = 4.0f;
	[Export] public float CurseInterval = 9.0f;
	[Export] public float NovaInterval = 14.0f;
	[Export] public float NovaWindupDuration = 3.5f;

	[Export] public float MeleeDamage = 38f;
	[Export] public float BloodBoltDamage = 42f;
	[Export] public float NovaDamage = 60f;

	/// <summary>HP fraction (0–1) at which Blood Shield activates. Default 35%.</summary>
	[Export] public float ShieldThreshold = 0.35f;

	/// <summary>Base damage per tick for the Court of Reflections DoT.</summary>
	[Export] public float CourtDotBaseDamage = 8f;

	/// <summary>Additional damage added per tick (ramp) for the Court of Reflections DoT.</summary>
	[Export] public float CourtDotRampPerTick = 5f;

	// ── internal state ────────────────────────────────────────────────────────

	float _meleeTimer;
	float _bloodBoltTimer;
	float _curseTimer;
	float _novaTimer;
	float _novaWindupTimer;

	BossCountessStrikeSpell _meleeSpell;
	BossCountessBloodBoltSpell _bloodBoltSpell;
	BossCountessCrimsonCurseSpell _curseSpell;
	BossCountessSanguineNovaSpell _novaSpell;
	BossCountessBloodShieldSpell _shieldSpell;

	AnimatedSprite2D _sprite;
	AudioStreamPlayer _riserPlayer;

	bool _bloodShieldUsed;

	// ── Court of Reflections state ────────────────────────────────────────────

	bool _courtPhaseActive;

	/// <summary>
	/// Whether each health threshold (75 %, 50 %, 25 %) has already triggered
	/// the Court of Reflections mechanic for this fight.
	/// </summary>
	readonly bool[] _courtThresholdsTriggered = new bool[3];

	static readonly float[] CourtThresholds  = { 0.75f, 0.50f, 0.25f };

	/// <summary>Number of DECOY clones spawned at each threshold (real boss is always +1).</summary>
	static readonly int[] CourtDecoyCounts = { 2, 3, 4 };

	readonly List<CountessClone> _activeClones = new();

	/// <summary>
	/// Spread of clone spawn positions, relative to the boss's current global position.
	/// Indexed in order; the real boss is placed at a random slot among these.
	/// The array holds enough entries for the maximum number of clones (4 decoys + 1 real = 5).
	/// </summary>
	static readonly Vector2[] CloneOffsets =
	{
		new(-130f,    0f),
		new( 130f,    0f),
		new(   0f,  -90f),
		new( -75f,   90f),
		new(  75f,   90f),
	};

	// ─────────────────────────────────────────────────────────────────────────

	enum PendingAttack
	{
		None,
		Melee,
		BloodBolt,
		Curse
	}

	PendingAttack _pendingAttack;
	Character _pendingTarget;

	const string AssetBase = "res://assets/enemies/the-countess/";

	// ── lifecycle ─────────────────────────────────────────────────────────────

	public override void _Ready()
	{
		base._Ready();
		CharacterName = GameConstants.CastleBoss2Name;
		RemoveFromGroup("party");
		AddToGroup(GameConstants.BossGroupName);
		IsFriendly = false;

		// Stagger first attacks.
		_meleeTimer = MeleeAttackInterval;
		_bloodBoltTimer = BloodBoltInterval;
		_curseTimer = CurseInterval;
		_novaTimer = NovaInterval;

		_meleeSpell = new BossCountessStrikeSpell { DamageAmount = MeleeDamage };
		_bloodBoltSpell = new BossCountessBloodBoltSpell { DamageAmount = BloodBoltDamage };
		_curseSpell = new BossCountessCrimsonCurseSpell();
		_novaSpell = new BossCountessSanguineNovaSpell { DamageAmount = NovaDamage };
		_shieldSpell = new BossCountessBloodShieldSpell();

		GlobalAutoLoad.RegisterSignalEmitter(this, nameof(CastWindupStarted));
		GlobalAutoLoad.RegisterSignalEmitter(this, nameof(CastWindupEnded));
		GlobalAutoLoad.RegisterSignalEmitter(this, nameof(CourtOfReflectionsStarted));
		GlobalAutoLoad.RegisterSignalEmitter(this, nameof(CourtOfReflectionsEnded));

		_riserPlayer = new AudioStreamPlayer();
		_riserPlayer.Stream = GD.Load<AudioStream>(AssetConstants.DeflectRiserSoundPath);
		AddChild(_riserPlayer);

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

		// ── Suspend all combat logic during the Court of Reflections ─────────
		if (_courtPhaseActive) return;

		// ── Blood Shield — once at 35% HP ─────────────────────────────────────
		if (!_bloodShieldUsed && CurrentHealth / MaxHealth <= ShieldThreshold)
		{
			_bloodShieldUsed = true;
			SpellPipeline.Cast(_shieldSpell, this, this);
			GD.Print("[TheCountess] Blood Shield activated!");
		}

		// ── Sanguine Nova wind-up countdown ──────────────────────────────────
		if (_novaWindupTimer > 0f)
		{
			_novaWindupTimer -= (float)delta;
			if (_novaWindupTimer <= 0f)
				ExecuteSanguineNova();
			return;
		}

		// ── Regular attack timers ─────────────────────────────────────────────
		_meleeTimer -= (float)delta;
		_bloodBoltTimer -= (float)delta;
		_curseTimer -= (float)delta;
		_novaTimer -= (float)delta;

		if (_pendingAttack != PendingAttack.None) return;

		if (_novaTimer <= 0f)
		{
			_novaTimer = NovaInterval;
			BeginSanguineNova();
		}
		else if (_curseTimer <= 0f)
		{
			_curseTimer = CurseInterval;
			CastCrimsonCurse();
		}
		else if (_bloodBoltTimer <= 0f)
		{
			_bloodBoltTimer = BloodBoltInterval;
			CastBloodBolt();
		}
		else if (_meleeTimer <= 0f)
		{
			_meleeTimer = MeleeAttackInterval;
			PerformMeleeAttack();
		}
	}

	// ── Damage override — immunity + threshold detection ──────────────────────

	public override void TakeDamage(float amount)
	{
		// Completely immune during the Court of Reflections phase.
		if (_courtPhaseActive) return;

		var healthBefore = CurrentHealth;
		base.TakeDamage(amount);

		// Check whether we just crossed a Court of Reflections threshold.
		CheckCourtThresholds(healthBefore);
	}

	// ── Court of Reflections ──────────────────────────────────────────────────

	void CheckCourtThresholds(float healthBefore)
	{
		if (!IsAlive) return;
		for (var i = 0; i < CourtThresholds.Length; i++)
		{
			if (_courtThresholdsTriggered[i]) continue;
			var pct = CourtThresholds[i];
			if (healthBefore / MaxHealth > pct && CurrentHealth / MaxHealth <= pct)
			{
				_courtThresholdsTriggered[i] = true;
				BeginCourtOfReflections(CourtDecoyCounts[i]);
				break;
			}
		}
	}

	/// <summary>
	/// Starts the Court of Reflections mechanic:
	/// - Makes the Countess invulnerable and invisible.
	/// - Applies a ramping non-dispellable DoT to the whole party.
	/// - Spawns <paramref name="decoyCount"/> decoy clones plus one real-boss clone
	///   at randomised positions around the arena.
	/// </summary>
	void BeginCourtOfReflections(int decoyCount)
	{
		GD.Print($"[TheCountess] Court of Reflections begins — {decoyCount} decoys.");

		_courtPhaseActive = true;

		// Cancel any in-flight cast or nova wind-up cleanly.
		if (_novaWindupTimer > 0f)
		{
			_novaWindupTimer = 0f;
			_riserPlayer.Stop();
			ParryWindowManager.ConsumeResult();
			EmitSignalCastWindupEnded();
		}
		_pendingAttack = PendingAttack.None;
		_pendingTarget = null;

		// Hide the boss sprite — she "vanishes".
		_sprite.Visible = false;

		// Notify UI to hide the boss health bar.
		EmitSignalCourtOfReflectionsStarted();

		// Apply ramping DoT to every living party member.
		foreach (var node in GetTree().GetNodesInGroup("party"))
		{
			if (node is Character c && c.IsAlive)
			{
				c.ApplyEffect(new CourtOfReflectionsEffect(CourtDotBaseDamage, CourtDotRampPerTick)
				{
					Icon = null, // set an icon if one is available
					SourceCharacterName = CharacterName
				});
			}
		}

		// Build the spawn position list — take only as many offsets as we need.
		var totalClones = decoyCount + 1; // decoys + one real boss
		var bossPos = GlobalPosition;

		// Shuffle which offset slot gets the real boss.
		var realIndex = (int)(GD.Randi() % (uint)totalClones);

		_activeClones.Clear();
		for (var i = 0; i < totalClones; i++)
		{
			var isReal = i == realIndex;
			var clone  = new CountessClone(this, isReal);
			clone.Position = bossPos + CloneOffsets[i];
			GetParent().AddChild(clone);
			_activeClones.Add(clone);
		}
	}

	/// <summary>
	/// Called by a <see cref="CountessClone"/> when the player walks into it or
	/// dispels it. If the clone is the real boss the mechanic resolves; otherwise
	/// only that clone is removed.
	/// </summary>
	public void OnCloneInteracted(CountessClone clone)
	{
		if (!_courtPhaseActive) return;

		if (clone.IsRealBoss)
		{
			GD.Print("[TheCountess] Real boss found — Court of Reflections ends!");
			EndCourtOfReflections();
		}
		else
		{
			GD.Print("[TheCountess] Decoy dismissed.");
			_activeClones.Remove(clone);
			clone.QueueFree();
		}
	}

	/// <summary>
	/// Ends the Court of Reflections mechanic:
	/// - Removes all remaining clones.
	/// - Removes the party DoT.
	/// - Makes the Countess visible and vulnerable again.
	/// - Resets attack timers so she doesn't immediately barrage the party.
	/// </summary>
	void EndCourtOfReflections()
	{
		_courtPhaseActive = false;

		// Remove every remaining clone (including the real-boss one that was just found).
		foreach (var clone in _activeClones)
			if (IsInstanceValid(clone) && !clone.IsQueuedForDeletion())
				clone.QueueFree();
		_activeClones.Clear();

		// Wipe the world-space hover registry — no more clones exist.
		CourtOfReflectionsRegistry.Clear();

		// Remove the DoT from all party members.
		foreach (var node in GetTree().GetNodesInGroup("party"))
			if (node is Character c)
				c.RemoveEffect("CourtOfReflections");

		// Make the Countess visible and attackable again.
		_sprite.Visible = true;
		_sprite.Play("idle");

		// Notify UI to re-show the boss health bar.
		EmitSignalCourtOfReflectionsEnded();

		// Give the party a brief breather before attacks resume.
		_meleeTimer    = MeleeAttackInterval;
		_bloodBoltTimer = BloodBoltInterval;
		_curseTimer    = CurseInterval;
		_novaTimer     = NovaInterval;

		GD.Print("[TheCountess] Court of Reflections resolved — resuming normal combat.");
	}

	// ── combat actions ────────────────────────────────────────────────────────

	void PerformMeleeAttack()
	{
		var target = FindTank() ?? PickRandomPartyMember();
		if (target == null) return;
		_pendingTarget = target;
		_pendingAttack = PendingAttack.Melee;
		_sprite.Play("attack");
	}

	void CastBloodBolt()
	{
		var target = PickRandomPartyMember();
		if (target == null) return;
		_pendingTarget = target;
		_pendingAttack = PendingAttack.BloodBolt;
		_sprite.Play("casting");
	}

	void CastCrimsonCurse()
	{
		var target = PickRandomPartyMember();
		if (target == null) return;
		_pendingTarget = target;
		_pendingAttack = PendingAttack.Curse;
		_sprite.Play("casting");
	}

	/// <summary>
	/// Begins the Sanguine Nova wind-up: plays the riser, opens the parry
	/// window, and starts the countdown.
	/// </summary>
	void BeginSanguineNova()
	{
		_novaWindupTimer = NovaWindupDuration;
		_riserPlayer.Play();
		ParryWindowManager.OpenWindow(_novaSpell.Name, _novaSpell.Icon, NovaWindupDuration);
		EmitSignalCastWindupStarted(_novaSpell.Name, _novaSpell.Icon, NovaWindupDuration);
		_pendingAttack = PendingAttack.None;
		_sprite.Play("casting");
	}

	/// <summary>
	/// Resolves Sanguine Nova at the end of the wind-up.
	/// Deflection cancels the blast; otherwise all party members take damage.
	/// </summary>
	void ExecuteSanguineNova()
	{
		EmitSignalCastWindupEnded();

		var wasDeflected = ParryWindowManager.ConsumeResult();
		if (wasDeflected)
		{
			GD.Print("[TheCountess] Sanguine Nova was deflected!");
			return;
		}

		var anyTarget = PickRandomPartyMember();
		if (anyTarget != null)
			SpellPipeline.Cast(_novaSpell, this, anyTarget);
	}

	void OnAnimationFinished()
	{
		if (_pendingTarget != null && _pendingTarget.IsAlive)
		{
			SpellResource spell = _pendingAttack switch
			{
				PendingAttack.Melee     => _meleeSpell,
				PendingAttack.BloodBolt => _bloodBoltSpell,
				PendingAttack.Curse     => _curseSpell,
				_                       => null
			};

			if (spell != null)
				SpellPipeline.Cast(spell, this, _pendingTarget);
		}

		_pendingTarget = null;
		_pendingAttack = PendingAttack.None;

		if (IsAlive)
			_sprite.Play("idle");
	}

	// ── targeting helpers ─────────────────────────────────────────────────────



	// ── animation setup ───────────────────────────────────────────────────────

	/// <summary>
	/// Loads individual PNG frames from res://assets/enemies/the-countess/.
	///
	/// idle1–2     (looping, 3 fps)
	/// attack1–4   (one-shot, 10 fps)
	/// casting1–3  (one-shot, 6 fps; used for spells and Nova wind-up)
	/// </summary>
	/// <summary>Rune of Time: scale all ability intervals by the haste multiplier.</summary>
	protected override void OnApplyHasteRune()
	{
		MeleeAttackInterval /= GameConstants.RuneTimeHasteMultiplier;
		BloodBoltInterval /= GameConstants.RuneTimeHasteMultiplier;
		CurseInterval /= GameConstants.RuneTimeHasteMultiplier;
		NovaInterval /= GameConstants.RuneTimeHasteMultiplier;
	}

		void SetupAnimations()
	{
		var frames = new SpriteFrames();
		frames.RemoveAnimation("default");

		AddAnimFromFiles(frames, "idle", AssetBase + "idle",    2, 3f,  true);
		AddAnimFromFiles(frames, "attack", AssetBase + "attack",  4, 10f, false);
		AddAnimFromFiles(frames, "casting", AssetBase + "casting", 3, 6f,  false);

		_sprite.SpriteFrames = frames;
	}

}
