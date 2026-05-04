using System.Collections.Generic;
using Godot;
using healerfantasy;
using healerfantasy.SpellResources;
using healerfantasy.SpellSystem;

/// <summary>
/// The Crystal Knight boss enemy.
///
/// Behaviour
/// ─────────
/// • Stands still (no movement).
/// • Every <see cref="MeleeAttackInterval"/> seconds, performs a melee attack
///   (Crystal Slash) against the tank — the party member named "Templar".
///   Falls back to any alive party member if the Templar is dead.
/// • Every <see cref="SpellCastInterval"/> seconds, fires Crystal Blast at a
///   randomly chosen alive party member.
/// • Every <see cref="DecayInterval"/> seconds, applies Crystal Decay (a 10 HP/sec
///   DoT) to a random party member. Dispel removes it.
/// • Every <see cref="CrushInterval"/> seconds, begins a <see cref="CrushWindupDuration"/>
///   second wind-up for Structural Crush. Plays a riser sound and opens a parry
///   window. If the player casts Deflect during the wind-up, the attack is
///   negated; otherwise all party members take 35 damage on resolution.
///
/// Animations are driven by a padded uniform sprite sheet
/// (crystal_knight_sheet.png, 80×80 frames):
///   Row 0 — "idle"   (4 frames, looping)
///   Row 1 — "attack" (5 frames, one-shot → returns to idle)
///   Row 2 — "spell"  (2 frames, one-shot → returns to idle)
/// </summary>
public partial class CrystalKnight : EnemyCharacter
{
	public CrystalKnight()
	{
		MaxHealth = GameConstants.BossHealthBaseValuesByDungeonTier[GameConstants.AncientKeepTier][0];
	}
	// ── signals ───────────────────────────────────────────────────────────────

	/// <summary>
	/// Emitted when a telegraphed cast wind-up begins (currently: Structural Crush).
	/// <paramref name="spellName"/> is the display name; <paramref name="duration"/>
	/// is the full wind-up duration in seconds so the UI can show a countdown.
	/// </summary>
	[Signal]
	public delegate void CastWindupStartedEventHandler(string spellName, Texture2D icon, float duration);

	/// <summary>Emitted when the wind-up resolves — whether deflected or landed.</summary>
	[Signal]
	public delegate void CastWindupEndedEventHandler();

	// ── tuneable exports ──────────────────────────────────────────────────────
	[Export] public float MeleeAttackInterval = 2.0f;
	[Export] public float SpellCastInterval = 4.0f;
	[Export] public float DecayInterval = 8.0f;
	[Export] public float CrushInterval = 10.0f;
	[Export] public float CrushWindupDuration = 3.0f;

	[Export] public float MeleeDamage = 40f;
	[Export] public float BlastDamage = 20f;
	[Export] public float CrushDamage = 45f;

	// ── internal state ────────────────────────────────────────────────────────
	float _meleeTimer;
	float _spellTimer;
	float _decayTimer;
	float _crushTimer;
	float _crushWindupTimer; // counts down during the Structural Crush wind-up

	BossMeleeAttackSpell _meleeSpell;
	BossCrystalBlastSpell _blastSpell;
	BossCrystalDecaySpell _decaySpell;
	BossStructuralCrushSpell _crushSpell;

	AnimatedSprite2D _sprite;
	AudioStreamPlayer _riserPlayer;

	// Tracks which attack animation is in flight so OnAnimationFinished knows
	// which spell to fire.
	enum PendingAttack
	{
		None,
		Melee,
		CrystalBlast,
		CrystalDecay
	}

	PendingAttack _pendingAttack;
	Character _pendingTarget;

	// ── constants ─────────────────────────────────────────────────────────────
	const int FrameSize = 80; // uniform cell size in crystal_knight_sheet.png

	const string RiserSoundPath = "res://assets/sound-effects/riser.mp3";

	// ── lifecycle ─────────────────────────────────────────────────────────────
	public override void _Ready()
	{
		base._Ready();
		CharacterName = GameConstants.Boss1Name;
		// Character._Ready() adds every character to "party" — undo for enemies.
		RemoveFromGroup("party");
		AddToGroup(GameConstants.BossGroupName);
		IsFriendly = false;

		// Stagger first attacks so the player has a moment to react.
		_meleeTimer = MeleeAttackInterval;
		_spellTimer = SpellCastInterval;
		_decayTimer = DecayInterval;
		_crushTimer = CrushInterval;

		_meleeSpell = new BossMeleeAttackSpell { DamageAmount = MeleeDamage };
		_blastSpell = new BossCrystalBlastSpell { DamageAmount = BlastDamage };
		_decaySpell = new BossCrystalDecaySpell();
		_crushSpell = new BossStructuralCrushSpell { DamageAmount = CrushDamage };

		GlobalAutoLoad.RegisterSignalEmitter(this, nameof(CastWindupStarted));
		GlobalAutoLoad.RegisterSignalEmitter(this, nameof(CastWindupEnded));

		// Audio player for the Structural Crush riser telegraph.
		_riserPlayer = new AudioStreamPlayer();
		_riserPlayer.Stream = GD.Load<AudioStream>(RiserSoundPath);
		AddChild(_riserPlayer);

		// ── sprite setup ──────────────────────────────────────────────────────
		_sprite = GetNode<AnimatedSprite2D>("AnimatedSprite2D");
		_sprite.Scale = new Vector2(1.5f, 1.5f);
		SetupAnimations();
		_sprite.AnimationFinished += OnAnimationFinished;
		_sprite.Play("idle");
		ApplyRuneModifiers();
	}

	public override void _Process(double delta)
	{
		// Runs mana regen, effect ticking, and the 0-damage life-loss tick.
		base._Process(delta);

		if (!IsAlive) return;

		// ── Structural Crush wind-up countdown ────────────────────────────────
		if (_crushWindupTimer > 0f)
		{
			_crushWindupTimer -= (float)delta;
			if (_crushWindupTimer <= 0f)
				ExecuteStructuralCrush();
		}

		// ── Regular attack timers ─────────────────────────────────────────────
		// Always tick every timer so none of them fall arbitrarily far behind
		// while another attack is in progress.  But only *fire* the next attack
		// once the current animation has fully finished (pendingAttack returns to
		// None in OnAnimationFinished) AND no Structural Crush wind-up is active.
		// Without this guard, two timers expiring in the same frame would both
		// call their start-attack method, the second overwriting _pendingTarget /
		// _pendingAttack and causing the first attack to be silently dropped.
		if (_crushWindupTimer > 0f) return;

		_meleeTimer -= (float)delta;
		_spellTimer -= (float)delta;
		_decayTimer -= (float)delta;
		_crushTimer -= (float)delta;

		// Don't start a new attack while an animation is still playing.
		if (_pendingAttack != PendingAttack.None) return;

		if (_meleeTimer <= 0f)
		{
			_meleeTimer = MeleeAttackInterval;
			PerformMeleeAttack();
		}
		else if (_spellTimer <= 0f)
		{
			_spellTimer = SpellCastInterval;
			CastCrystalBlast();
		}
		else if (_decayTimer <= 0f)
		{
			_decayTimer = DecayInterval;
			CastCrystalDecay();
		}
		else if (_crushTimer <= 0f)
		{
			_crushTimer = CrushInterval;
			BeginStructuralCrush();
		}
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

	void CastCrystalBlast()
	{
		var target = PickRandomPartyMember();
		if (target == null) return;
		_pendingTarget = target;
		_pendingAttack = PendingAttack.CrystalBlast;
		_sprite.Play("spell");
	}

	void CastCrystalDecay()
	{
		var target = PickRandomPartyMember();
		if (target == null) return;
		_pendingTarget = target;
		_pendingAttack = PendingAttack.CrystalDecay;
		_sprite.Play("spell");
	}

	/// <summary>
	/// Begins the Structural Crush wind-up:
	/// plays the riser sound, opens the parry window, and starts the countdown.
	/// The attack resolves in <see cref="ExecuteStructuralCrush"/> when the
	/// wind-up timer expires.
	/// </summary>
	void BeginStructuralCrush()
	{
		_crushWindupTimer = CrushWindupDuration;
		_riserPlayer.Play();
		ParryWindowManager.OpenWindow(_crushSpell.Name, _crushSpell.Icon, CrushWindupDuration);
		EmitSignalCastWindupStarted(_crushSpell.Name, _crushSpell.Icon, CrushWindupDuration);
		// Play the spell animation as a visual telegraph; the actual hit comes
		// from the wind-up timer rather than OnAnimationFinished.
		_pendingAttack = PendingAttack.None; // animation finish won't fire a spell
		_sprite.Play("spell");
	}

	/// <summary>
	/// Resolves the Structural Crush at the end of the wind-up.
	/// If the player deflected in time the attack is cancelled; otherwise
	/// all party members take <see cref="CrushDamage"/> damage.
	/// </summary>
	void ExecuteStructuralCrush()
	{
		EmitSignalCastWindupEnded(); // hides the boss cast bar regardless of outcome

		var wasDeflected = ParryWindowManager.ConsumeResult();
		if (wasDeflected)
		{
			GD.Print("[CrystalKnight] Structural Crush was deflected!");
			return;
		}

		// Pick any alive party member as the explicit target — the spell's
		// ResolveTargets will expand it to the whole party.
		var anyTarget = PickRandomPartyMember();
		if (anyTarget != null)
			SpellPipeline.Cast(_crushSpell, this, anyTarget);
	}

	// Damage lands on the last frame for melee/blast; then we return to idle.
	void OnAnimationFinished()
	{
		if (_pendingTarget != null && _pendingTarget.IsAlive)
		{
			SpellResource spell = _pendingAttack switch
			{
				PendingAttack.Melee => _meleeSpell,
				PendingAttack.CrystalBlast => _blastSpell,
				PendingAttack.CrystalDecay => _decaySpell,
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



	// ── animation setup ───────────────────────────────────────────────────────

	/// <summary>
	/// Builds the SpriteFrames resource from crystal_knight_sheet.png at runtime.
	///
	/// Sheet layout (each cell is 80×80 px):
	///   Row 0 — idle   (4 frames)
	///   Row 1 — attack (5 frames)
	///   Row 2 — spell  (2 frames)
	/// </summary>
	/// <summary>Rune of Time: scale all ability intervals by the haste multiplier.</summary>
	protected override void OnApplyHasteRune()
	{
		MeleeAttackInterval /= GameConstants.RuneTimeHasteMultiplier;
		SpellCastInterval /= GameConstants.RuneTimeHasteMultiplier;
		DecayInterval /= GameConstants.RuneTimeHasteMultiplier;
		CrushInterval /= GameConstants.RuneTimeHasteMultiplier;
	}

		void SetupAnimations()
	{
		var texture = GD.Load<Texture2D>(AssetConstants.EnemyAssets + "/crystal-knight/crystal_knight_sheet.png");
		var frames = new SpriteFrames();
		frames.RemoveAnimation("default");

		AddAnim(frames, "idle", texture, 0, 4, 8f, true);
		AddAnim(frames, "attack", texture, 1, 5, 10f, false);
		AddAnim(frames, "spell", texture, 2, 2, 4f, false);

		_sprite.SpriteFrames = frames;
	}

	static void AddAnim(SpriteFrames frames, string name, Texture2D texture,
		int row, int count, float fps, bool loop)
	{
		frames.AddAnimation(name);
		frames.SetAnimationLoop(name, loop);
		frames.SetAnimationSpeed(name, fps);
		for (var i = 0; i < count; i++)
		{
			var atlas = new AtlasTexture
			{
				Atlas = texture,
				Region = new Rect2(i * FrameSize, row * FrameSize, FrameSize, FrameSize)
			};
			frames.AddFrame(name, atlas);
		}
	}
}
