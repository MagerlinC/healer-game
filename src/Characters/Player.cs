#nullable enable
using System.Collections.Generic;
using System.Linq;
using Godot;
using healerfantasy;
using healerfantasy.Items;
using healerfantasy.SpellResources;
using healerfantasy.SpellResources.Void;
using healerfantasy.SpellSystem;
using SpellResource = healerfantasy.SpellResources.SpellResource;


/// <summary>
/// The player-controlled healer.
/// Inherits health drain from Character and adds WASD movement + spell casting.
/// </summary>
public partial class Player : Character
{
	// ── movement ─────────────────────────────────────────────────────────────
	[Export] public float Speed = 80.0f;
	[Export] public float GlobalCooldown = 0.5f;

	/// <summary>Maximum number of spells the player can have equipped at once.</summary>
	public const int MaxSpellSlots = 6;

	public Player()
	{
		AddToGroup("party");
	}

	/// <summary>
	/// Always-available generic spells (Dispel, Deflect) sourced from
	/// <see cref="SpellRegistry.GenericSpells"/>. These live in their own
	/// action bar and cannot be removed from the loadout. Bound to input
	/// actions <c>dispel</c> and <c>deflect</c>.
	/// </summary>
	public SpellResource[] GenericSpells { get; } =
		SpellRegistry.GenericSpells.ToArray();

	/// <summary>
	/// The player's currently equipped spells, indexed by slot (0–5).
	/// Slot <c>i</c> maps to input action <c>spell_{i+1}</c> and is labelled
	/// with its keybind on the action bar. <c>null</c> entries are empty slots.
	///
	/// Populated from <see cref="SpellRegistry.AllSpells"/> in <see cref="_Ready"/>
	/// so that references match the registry — the Spellbook UI can then use
	/// reference equality when showing which spells are equipped.
	/// Updated by the Spellbook UI when the player changes their loadout.
	/// </summary>
	public SpellResource?[] EquippedSpells { get; } = new SpellResource?[MaxSpellSlots];

	/// <summary>
	/// The player's currently equipped ultimate spell (bound to the "ultimate" action,
	/// default key R). Null if no ultimate has been equipped.
	/// Loaded from <see cref="RunState.SelectedUltimate"/> in <see cref="_Ready"/>.
	/// </summary>
	public UltimateSpellResource? EquippedUltimate { get; set; } = null;

	/// <param name="spell">The spell being cast.</param>
	/// <param name="adjustedCastTime">
	/// The actual cast duration after applying the caster's
	/// <see cref="CharacterStats.IncreasedHaste"/>. Use this for
	/// UI timers rather than <see cref="SpellResources.SpellResource.CastTime"/>.
	/// </param>
	[Signal]
	public delegate void CastStartedEventHandler(SpellResource spell, float adjustedCastTime);

	[Signal]
	public delegate void CastCancelledEventHandler();

	/// <summary>
	/// Emitted when a spell with a non-zero <see cref="SpellResource.Cooldown"/>
	/// is successfully cast. <paramref name="duration"/> is the full cooldown in
	/// seconds — the same value the UI should count down from.
	/// </summary>
	[Signal]
	public delegate void CooldownStartedEventHandler(SpellResource spell, float duration);

	/// <summary>
	/// Emitted whenever the global cooldown is triggered (i.e. after every spell cast).
	/// <paramref name="duration"/> is <see cref="GlobalCooldown"/> in seconds.
	/// Used by the action bar to show the GCD sweep on all spell slots.
	/// </summary>
	[Signal]
	public delegate void GlobalCooldownStartedEventHandler(float duration);

	/// <summary>
	/// Emitted whenever the equipped ultimate spell's progress or state changes
	/// (after each spell cast and each process tick).
	/// <paramref name="progress"/> is the current accumulated progress.
	/// <paramref name="requirement"/> is the threshold needed to cast.
	/// Used by <see cref="UI.UltimateSlot"/> to refresh the progress display.
	/// </summary>
	[Signal]
	public delegate void UltimateProgressChangedEventHandler(float progress, float requirement);

	/// <summary>
	/// Set by World after the scene is ready.
	/// Used to resolve which party member the mouse is hovering at cast time.
	/// </summary>
	public GameUI GameUI { get; set; }

	bool _isCasting = false;
	float _castTimer = 0f;
	float _globalCooldownTimer = 0f;
	SpellResource? _castSpell;
	Character? _castTarget;

	/// <summary>
	/// Tracks the remaining cooldown (seconds) for each spell that was cast and
	/// has a non-zero <see cref="SpellResource.Cooldown"/>. Entries are kept at
	/// zero rather than removed so IsOnCooldown stays O(1).
	/// </summary>
	readonly Dictionary<SpellResource, float> _spellCooldowns = new();

	// ── animation ─────────────────────────────────────────────────────────────
	AnimatedSprite2D _sprite = null!;

	// ── float lift (mirrors OverworldPlayer) ──────────────────────────────────
	float _liftOffset = 0f;
	float _bobPhase   = 0f;
	const float FloatHeight   = 6f;
	const float BobAmplitude  = 2f;
	const float BobSpeed      = 0.7f;
	const float LiftSpeed     = 2.5f;

	// ── ultimate aura ─────────────────────────────────────────────────────────
	CpuParticles2D? _ultimateParticles;

	/// <summary>
	/// Duration of the cast animation at SpeedScale 1.0.
	/// Matches 4 frames at 4 FPS defined in the SpriteFrames resource.
	/// </summary>
	const float CastAnimBaseDuration = 1.0f;

	// ── casting audio ─────────────────────────────────────────────────────────
	AudioStreamPlayer _castingAudioPlayer = null!;
	AudioStreamPlayer _castFinishedAudioPlayer = null!;

	// ── lifecycle ────────────────────────────────────────────────────────────
	public override void _Ready()
	{
		base._Ready();
		_sprite = GetNode<AnimatedSprite2D>("AnimatedSprite2D");
		_sprite.Scale = new Vector2(GameConstants.HealerSpriteScale, GameConstants.HealerSpriteScale);

		// Add the float animation to the existing SpriteFrames (which already
		// contains "idle" and "cast" from the scene file).
		var frames = _sprite.SpriteFrames;
		if (!frames.HasAnimation("walk"))
		{
			frames.AddAnimation("walk");
			frames.SetAnimationSpeed("walk", 4.0);
			frames.SetAnimationLoop("walk", true);
			foreach (var i in new[] { 1, 2, 3 })
				frames.AddFrame("walk", GD.Load<Texture2D>($"res://assets/characters/healer/float{i}.png"));
		}

		_sprite.Play("idle");
		CharacterName = GameConstants.HealerName;
		GlobalAutoLoad.RegisterSignalEmitter(this, nameof(CastStarted));
		GlobalAutoLoad.RegisterSignalEmitter(this, nameof(CastCancelled));
		GlobalAutoLoad.RegisterSignalEmitter(this, nameof(CooldownStarted));
		GlobalAutoLoad.RegisterSignalEmitter(this, nameof(GlobalCooldownStarted));

		// Load spell loadout from RunState (set by the Overworld).
		// RunState always has a valid loadout (defaults if the Overworld was skipped).
		if (RunState.Instance?.HasLoadout == true)
		{
			System.Array.Copy(RunState.Instance.SelectedSpells, EquippedSpells, MaxSpellSlots);
		}

		// Load the ultimate spell (may be null if none was selected in the overworld).
		// Reset progress so accumulated state from a previous boss fight doesn't carry over.
		EquippedUltimate = RunState.Instance?.SelectedUltimate;
		EquippedUltimate?.ResetProgress();

		GlobalAutoLoad.RegisterSignalEmitter(this, nameof(UltimateProgressChanged));

		// Particle aura shown on the player while the ultimate is active.
		// Colour is driven by the equipped ultimate's spell school.
		_ultimateParticles = new CpuParticles2D();
		_ultimateParticles.Emitting = false;
		_ultimateParticles.Amount = 22;
		_ultimateParticles.Lifetime = 0.9f;
		_ultimateParticles.Explosiveness = 0f;
		_ultimateParticles.Randomness = 0.35f;
		_ultimateParticles.Direction = new Vector2(0f, -1f);
		_ultimateParticles.Spread = 65f;
		_ultimateParticles.Gravity = new Vector2(0f, -25f);
		_ultimateParticles.InitialVelocityMin = 12f;
		_ultimateParticles.InitialVelocityMax = 28f;
		_ultimateParticles.ScaleAmountMin = 2f;
		_ultimateParticles.ScaleAmountMax = 5f;
		_ultimateParticles.Color = EquippedUltimate != null
			? new Color(AssetConstants.SpellSchoolColor(EquippedUltimate.School), 0.85f)
			: new Color(1f, 1f, 1f, 0.85f);
		_ultimateParticles.ZIndex = 2;
		AddChild(_ultimateParticles);

		// Apply talents chosen in the Overworld
		if (RunState.Instance?.SelectedTalentDefs.Count > 0)
		{
			foreach (var def in RunState.Instance.SelectedTalentDefs)
				Talents.Add(def.CreateTalent());
		}

		// Apply equipped items from ItemStore.
		// ItemStore is updated by the Armory (in Camp) and the VictoryScreen equip button.
		// Each World scene load reads the current state, so changes made in Camp persist here.
		foreach (var item in ItemStore.GetEquippedItems())
			EquippedItems.Add(item);

		// Casting SFX — looping player restarts itself while _isCasting is true.
		_castingAudioPlayer = new AudioStreamPlayer();
		_castingAudioPlayer.Stream = GD.Load<AudioStream>(AssetConstants.CastingSfx);
		_castingAudioPlayer.VolumeDb = -4f;
		AddChild(_castingAudioPlayer);
		_castingAudioPlayer.Finished += OnCastingSfxFinished;

		// One-shot player for the cast-complete sound.
		_castFinishedAudioPlayer = new AudioStreamPlayer();
		_castFinishedAudioPlayer.VolumeDb = -18f;
		_castFinishedAudioPlayer.Stream = GD.Load<AudioStream>(AssetConstants.CastFinishedSfx);
		AddChild(_castFinishedAudioPlayer);
	}

	/// <summary>Restarts the casting loop if the cast is still in progress.</summary>
	void OnCastingSfxFinished()
	{
		if (_isCasting)
			_castingAudioPlayer.Play();
	}

	// ── spell input ───────────────────────────────────────────────────────────

	/// <summary>
	/// Returns the generic spell (deflect or dispel) whose key was just pressed, or null.
	/// </summary>
	SpellResource? GetGenericSpellForInput()
	{
		if (Input.IsActionJustPressed("deflect"))
		{
			return GenericSpells.FirstOrDefault(s => s.Name == "Deflect");
		}

		if (Input.IsActionJustPressed("dispel"))
		{
			return GenericSpells.FirstOrDefault(s => s.Name == "Dispel");
		}

		return null;
	}

	/// <summary>
	/// Returns the equipped spell whose slot key was just pressed, or null.
	/// Slot i maps to action <c>spell_{i+1}</c>, which the player can rebind
	/// in the Godot InputMap without any code changes.
	/// </summary>
	SpellResource? GetSpellForInput()
	{
		for (var i = 0; i < MaxSpellSlots; i++)
			if (EquippedSpells[i] != null && Input.IsActionJustPressed($"spell_{i + 1}"))
				return EquippedSpells[i];
		return null;
	}

	/// <summary>
	/// Returns each slot's (spell, action-name) pair in slot order.
	/// Used to build and refresh the action bar.
	/// </summary>
	public (SpellResource? Spell, string Action)[] GetSpellBindings()
	{
		return Enumerable.Range(0, MaxSpellSlots)
			.Select(i => (EquippedSpells[i], $"spell_{i + 1}"))
			.ToArray();
	}

	// ── casting ───────────────────────────────────────────────────────────────

	void FireSpell(SpellResource spell, Character target)
	{
		// Clear _isCasting BEFORE Stop() — Godot emits Finished even on manual
		// Stop() calls, and OnCastingSfxFinished must not restart the loop.
		_isCasting = false;
		_castingAudioPlayer.Stop();
		_castFinishedAudioPlayer.Play();
		_sprite.SpeedScale = 1.0f;
		_sprite.Play("idle");
		_sprite.Position = new Vector2(0f, _liftOffset); // keep lift smooth; _PhysicsProcess descends it

		// Tell party members which boss to focus when the player attacks one directly.
		if (spell.EffectType == EffectType.Harmful && target != null && target.IsInGroup(GameConstants.BossGroupName))
			PartyMember.NotifyPlayerAttackedBoss(target);

		var stats = GetCharacterStats();
		var adjustManaCost = stats.ManaCostMultiplier * spell.ManaCost;
		var adjustLifeCost = stats.LifeCostMultiplier * spell.HealthCost;
		SpendMana(spell.ManaCost);
		SpendLife(spell.HealthCost);
		var ctx = SpellPipeline.Cast(spell, this, target);

		// Notify the ultimate spell about this cast so it can track progress.
		// Also check if this was the ultimate itself — if so, reset progress.
		if (EquippedUltimate != null)
		{
			if (ReferenceEquals(spell, EquippedUltimate))
			{
				EquippedUltimate.ResetProgress();
			}
			else if (!IsUltimateActive())
			{
				EquippedUltimate.OnRegularSpellCast(ctx);
			}

			EmitSignalUltimateProgressChanged(EquippedUltimate.Progress, EquippedUltimate.Requirement);
		}

		if (spell.Cooldown > 0f)
		{
			_spellCooldowns[spell] = spell.Cooldown;
			EmitSignalCooldownStarted(spell, spell.Cooldown);
		}

		_castSpell = null;
		_castTarget = null;
	}

	/// <summary>
	/// Fire a generic (off-GCD) spell without cancelling any active cast.
	/// Generic spells have no mana cost and do not trigger the GCD.
	/// The cooldown is only started if the spell reports itself as effective
	/// (e.g. Dispel does nothing when the target has no dispellable debuffs).
	/// </summary>
	void FireGenericSpell(SpellResource spell, Character target)
	{
		var ctx = SpellPipeline.Cast(spell, this, target);

		if (spell.Cooldown > 0f && ctx.WasEffective)
		{
			_spellCooldowns[spell] = spell.Cooldown;
			EmitSignalCooldownStarted(spell, spell.Cooldown);
		}
	}

	/// <summary>Returns true if <paramref name="spell"/> still has time remaining on its cooldown.</summary>
	public bool IsOnCooldown(SpellResource spell)
	{
		return _spellCooldowns.TryGetValue(spell, out var cd) && cd > 0f;
	}

	public override void _Process(double delta)
	{
		base._Process(delta); // runs health drain from Character
		if (_globalCooldownTimer > 0f)
			_globalCooldownTimer = Mathf.Max(_globalCooldownTimer - (float)delta, 0.0f);

		// Tick per-spell cooldowns.
		var cdKeys = new List<SpellResource>(_spellCooldowns.Keys);
		foreach (var key in cdKeys)
			_spellCooldowns[key] = Mathf.Max(_spellCooldowns[key] - (float)delta, 0f);

		if (!IsAlive) return;

		// ── Tick ultimate progress every frame while alive ────────────────────────
		if (EquippedUltimate != null)
		{
			var ultimateActive = IsUltimateActive();

			var prevProgress = EquippedUltimate.Progress;
			if (!ultimateActive)
				EquippedUltimate.OnProcessTick(this, (float)delta);
			if (!Mathf.IsEqualApprox(EquippedUltimate.Progress, prevProgress))
				EmitSignalUltimateProgressChanged(EquippedUltimate.Progress, EquippedUltimate.Requirement);

			// Drive the spell-school particle aura — emit only while the ultimate is active.
			if (_ultimateParticles != null && _ultimateParticles.Emitting != ultimateActive)
				_ultimateParticles.Emitting = ultimateActive;
		}

		// ── Generic spells (off-GCD, castable even while casting another spell) ──
		// Checked before the _isCasting early-return so Deflect can be triggered
		// at any time during a cast.
		if (IsAlive)
		{
			var genericSpell = GetGenericSpellForInput();
			if (genericSpell != null && !IsOnCooldown(genericSpell))
			{
				var target = ResolveTargetWithFallback(GameUI?.GetHoveredCharacter(), genericSpell);
				if (target != null)
					FireGenericSpell(genericSpell, target);
			}
		}

		// Tick cast timer — any movement input interrupts the cast
		if (_isCasting)
		{
			if (Input.GetVector("move_left", "move_right", "move_up", "move_down") != Vector2.Zero)
			{
				CancelCast();
			}
			else
			{
				_castTimer -= (float)delta;
				if (_castTimer <= 0f)
					FireSpell(_castSpell, _castTarget);
			}

			return;
		}

		var canCast = IsAlive && _globalCooldownTimer == 0f;
		if (!canCast) return;

		// ── Ultimate spell (bound to "ultimate", default key R) ───────────────────
		if (EquippedUltimate != null && Input.IsActionJustPressed("ultimate"))
		{
			var ultimate = EquippedUltimate;
			var canPayCost = CurrentMana >= ultimate.ManaCost && CurrentHealth > ultimate.HealthCost;
			if (canPayCost && !IsOnCooldown(ultimate) && ultimate.IsRequirementMet && !IsUltimateActive())
			{
				var hoveredCharacter = ResolveTargetWithFallback(GameUI?.GetHoveredCharacter(), ultimate);
				if (hoveredCharacter != null)
				{
					_castTarget = hoveredCharacter;
					_castSpell = ultimate;

					var stats = GetCharacterStats();
					var isInstant = ultimate.CastTime == 0.0f
					                || stats.NextCastIsInstant && ultimate.School != SpellSchool.Chronomancy;

					if (isInstant)
					{
						FireSpell(ultimate, hoveredCharacter);
					}
					else
					{
						var adjustedCastTime = ultimate.CastTime - ultimate.CastTime * stats.IncreasedHaste;
						EmitSignalCastStarted(ultimate, adjustedCastTime);
						_isCasting = true;
						_castTimer = adjustedCastTime;
						_castingAudioPlayer.Play();
						_sprite.SpeedScale = CastAnimBaseDuration / adjustedCastTime;
						_sprite.Play("cast");
					}

					var adjustedGcd = Mathf.Max(GlobalCooldown * (1f - stats.IncreasedHaste), 0.1f);
					_globalCooldownTimer = adjustedGcd;
					EmitSignalGlobalCooldownStarted(adjustedGcd);
				}
			}

			return;
		}

		var spellToCast = GetSpellForInput();

		if (spellToCast is not null)
		{

			var stats = GetCharacterStats();
			var adjustedManaCost = spellToCast.ManaCost * stats.ManaCostMultiplier;
			var adjustedLifeCost = spellToCast.ManaCost * stats.ManaCostMultiplier;
			var canPayCost = CurrentMana >= adjustedManaCost && CurrentHealth > adjustedLifeCost;
			if (canPayCost && !IsOnCooldown(spellToCast))
			{
				// Lock in the target at cast-start: whichever party frame is under
				// the cursor, or self if the cursor is not over any frame.
				var hoveredCharacter = ResolveTargetWithFallback(GameUI?.GetHoveredCharacter(), spellToCast);
				if (hoveredCharacter == null) return; // no valid target (all bosses dead, etc.)
				if (spellToCast.EffectType == EffectType.Harmful && hoveredCharacter.IsFriendly)
					return;

				_castTarget = hoveredCharacter;
				_castSpell = spellToCast;

				var isInstant = spellToCast.CastTime == 0.0f
				                || stats.NextCastIsInstant && spellToCast.School != SpellSchool.Chronomancy;

				if (isInstant)
				{
					FireSpell(_castSpell, _castTarget);
				}
				else
				{
					// Increase by haste
					var adjustedCastTime = spellToCast.CastTime - spellToCast.CastTime * stats.IncreasedHaste;
					EmitSignalCastStarted(spellToCast, adjustedCastTime);

					_isCasting = true;
					_castTimer = adjustedCastTime;
					_castingAudioPlayer.Play();

					// Scale the cast animation so one full cycle = adjustedCastTime.
					// Longer casts play slower; shorter casts (or hasted casts) play faster.
					_sprite.SpeedScale = CastAnimBaseDuration / adjustedCastTime;
					_sprite.Play("cast");
				}

				var adjustedGcd = Mathf.Max(GlobalCooldown * (1f - stats.IncreasedHaste), 0.1f);
				_globalCooldownTimer = adjustedGcd;
				EmitSignalGlobalCooldownStarted(adjustedGcd);
			}
		}
	}

	Character? ResolveTargetWithFallback(Character? target, SpellResource spell)
	{
		if (target == null)
		{
			if (spell.EffectType == EffectType.Helpful)
				return this;

			// Return the first *alive* boss — GetFirstNodeInGroup gives an arbitrary
			// ordering and may return a dead character in multi-boss encounters.
			foreach (var node in GetTree().GetNodesInGroup(GameConstants.BossGroupName))
				if (node is Character c && c.IsAlive)
					return c;

			return null;
		}

		// If the resolved target is a dead boss (e.g. player had the dead twin's
		// health bar hovered), fall back to any alive boss.
		if (!target.IsAlive && target.IsInGroup(GameConstants.BossGroupName))
		{
			foreach (var node in GetTree().GetNodesInGroup(GameConstants.BossGroupName))
				if (node is Character c && c.IsAlive)
					return c;
			return null;
		}

		return target;
	}

	// ── private helpers ──────────────────────────────────────────────────────

	/// <summary>
	/// Returns true when the equipped ultimate's persistent buff is currently active on the player.
	/// False when no ultimate is equipped, the ultimate has no ActiveEffectId, or the buff is absent.
	/// </summary>
	bool IsUltimateActive()
	{
		if (EquippedUltimate == null) return false;
		var id = EquippedUltimate.ActiveEffectId;
		return !string.IsNullOrEmpty(id) && GetEffectById(id) != null;
	}

	/// <summary>Abort the current cast and notify listeners. No mana is refunded.</summary>
	void CancelCast()
	{
		// Clear _isCasting BEFORE Stop() — same reason as FireSpell.
		_isCasting = false;
		_castingAudioPlayer.Stop();
		EmitSignalCastCancelled();
		_sprite.SpeedScale = 1.0f;
		_sprite.Play("idle");
		_sprite.Position = new Vector2(0f, _liftOffset); // keep lift smooth; _PhysicsProcess descends it
		_castSpell = null;
		_castTarget = null;
		_castTimer = 0f;
	}

	protected override void ApplyDeathVisuals()
	{
		// Cancel any in-progress cast immediately.
		_isCasting = false;
		_castingAudioPlayer?.Stop();

		// Stop the animation where it is.
		_sprite.Stop();

		// Rotate 90° clockwise so the character appears to fall/lie on the ground.
		_sprite.Rotation = Mathf.Pi / 2f;

		// Apply a greyscale shader to indicate death.
		var shader = new Shader();
		shader.Code = """
		              shader_type canvas_item;
		              void fragment() {
		              	vec4 col = texture(TEXTURE, UV);
		              	float grey = dot(col.rgb, vec3(0.299, 0.587, 0.114));
		              	COLOR = vec4(grey, grey, grey, col.a);
		              }
		              """;
		var mat = new ShaderMaterial();
		mat.Shader = shader;
		_sprite.Material = mat;
	}

	public override void _PhysicsProcess(double delta)
	{
		if (!IsAlive) return;

		var dt  = (float)delta;
		var dir = Input.GetVector("move_left", "move_right", "move_up", "move_down");

		if (dir != Vector2.Zero)
		{
			// Flip sprite to face horizontal movement direction.
			// The healer sprite is drawn facing left, so FlipH=true → right.
			if (dir.X != 0f)
				_sprite.FlipH = dir.X > 0f;

			// Switch to float animation while moving, but don't interrupt a cast.
			if (!_isCasting && _sprite.Animation != "walk")
				_sprite.Play("walk");

			// Slowly ascend.
			_liftOffset = Mathf.Lerp(_liftOffset, -FloatHeight, LiftSpeed * dt);
			_bobPhase  += BobSpeed * Mathf.Tau * dt;
			_sprite.Position = new Vector2(0f, _liftOffset + Mathf.Sin(_bobPhase) * BobAmplitude);
		}
		else
		{
			// Return to idle only when not casting (cast animation takes priority).
			if (!_isCasting && _sprite.Animation != "idle")
				_sprite.Play("idle");

			// Slowly descend back to the ground.
			_liftOffset = Mathf.Lerp(_liftOffset, 0f, LiftSpeed * dt);
			_sprite.Position = new Vector2(0f, _liftOffset);
		}

		var stats = GetCharacterStats();
		var effectiveSpeed = Speed * (1f + stats.IncreasedMovementSpeed);
		Velocity = dir != Vector2.Zero ? dir * effectiveSpeed : Vector2.Zero;
		MoveAndSlide();
	}
}