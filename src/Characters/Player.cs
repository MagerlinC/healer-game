#nullable enable
using System.Collections.Generic;
using System.Linq;
using Godot;
using healerfantasy;
using healerfantasy.SpellResources;
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

	/// <param name="spell">The spell being cast.</param>
	/// <param name="adjustedCastTime">
	/// The actual cast duration after applying the caster's
	/// <see cref="CharacterStats.CastSpeedMultiplier"/>. Use this for
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

	// ── lifecycle ────────────────────────────────────────────────────────────
	public override void _Ready()
	{
		base._Ready();
		CharacterName = GameConstants.PlayerName;
		GlobalAutoLoad.RegisterSignalEmitter(this, nameof(CastStarted));
		GlobalAutoLoad.RegisterSignalEmitter(this, nameof(CastCancelled));
		GlobalAutoLoad.RegisterSignalEmitter(this, nameof(CooldownStarted));

		// Load spell loadout from RunState (set by the Overworld).
		// RunState always has a valid loadout (defaults if the Overworld was skipped).
		if (RunState.Instance?.HasLoadout == true)
		{
			System.Array.Copy(RunState.Instance.SelectedSpells, EquippedSpells, MaxSpellSlots);
		}
		else
		{
			// Fallback: hardcoded defaults for direct editor launches
			var defaults = new[]
			{
				"Touch of Light", "Wave of Incandescence", "Renewing Bloom",
				"Reinvigorate", "Burst of Light", "Decay"
			};
			for (var i = 0; i < defaults.Length && i < MaxSpellSlots; i++)
				EquippedSpells[i] = SpellRegistry.AllSpells.FirstOrDefault(s => s.Name == defaults[i]);
		}

		// Apply talents chosen in the Overworld
		if (RunState.Instance?.SelectedTalentDefs.Count > 0)
		{
			foreach (var def in RunState.Instance.SelectedTalentDefs)
				Talents.Add(def.CreateTalent());
		}
	}

	// ── spell input ───────────────────────────────────────────────────────────

	/// <summary>
	/// Returns the generic spell whose key was just pressed, or null.
	/// Slot 0 → <c>generic_1</c>, slot 1 → <c>generic_2</c>.
	/// </summary>
	SpellResource? GetGenericSpellForInput()
	{
		if (Input.IsActionJustPressed("deflect"))
		{
			return GenericSpells.FirstOrDefault(s => s.Name == "deflect");
		}

		if (Input.IsActionJustPressed("dispel"))
		{
			return GenericSpells.FirstOrDefault(s => s.Name == "dispel");
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
		SpendMana(spell.ManaCost);
		SpellPipeline.Cast(spell, this, target);

		if (spell.Cooldown > 0f)
		{
			_spellCooldowns[spell] = spell.Cooldown;
			EmitSignalCooldownStarted(spell, spell.Cooldown);
		}

		_isCasting = false;
		_castSpell = null;
		_castTarget = null;
	}

	/// <summary>
	/// Fire a generic (off-GCD) spell without cancelling any active cast.
	/// Generic spells have no mana cost and do not trigger the GCD.
	/// </summary>
	void FireGenericSpell(SpellResource spell, Character target)
	{
		SpellPipeline.Cast(spell, this, target);

		if (spell.Cooldown > 0f)
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

		if (!IsAlive)
		{
			CancelCast();
			return;
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

		var spellToCast = GetSpellForInput();

		if (spellToCast is not null)
		{
			var hasMana = CurrentMana >= spellToCast.ManaCost;
			if (hasMana && !IsOnCooldown(spellToCast))
			{
				// Lock in the target at cast-start: whichever party frame is under
				// the cursor, or self if the cursor is not over any frame.
				var hoveredCharacter = ResolveTargetWithFallback(GameUI?.GetHoveredCharacter(), spellToCast);
				if (spellToCast.EffectType == EffectType.Harmful && hoveredCharacter.IsFriendly)
					return;

				_castTarget = hoveredCharacter;
				_castSpell = spellToCast;

				if (spellToCast.CastTime == 0.0f)
				{
					FireSpell(_castSpell, _castTarget);
				}
				else
				{
					// Divide by the cast-speed multiplier so e.g. 2× Acceleration
					// turns a 2 s cast into ~1.67 s.
					var adjustedCastTime = spellToCast.CastTime / GetCharacterStats().CastSpeedMultiplier;
					EmitSignalCastStarted(spellToCast, adjustedCastTime);
					_isCasting = true;
					_castTimer = adjustedCastTime;
				}

				_globalCooldownTimer = GlobalCooldown;
			}
		}
	}

	Character ResolveTargetWithFallback(Character? target, SpellResource spell)
	{
		if (target == null)
		{
			return spell.EffectType == EffectType.Helpful
				? this
				: GetTree().GetFirstNodeInGroup(GameConstants.BossGroupName) as Character;
		}

		return target;
	}

	// ── private helpers ──────────────────────────────────────────────────────

	/// <summary>Abort the current cast and notify listeners. No mana is refunded.</summary>
	void CancelCast()
	{
		EmitSignalCastCancelled();
		_isCasting = false;
		_castSpell = null;
		_castTarget = null;
		_castTimer = 0f;
	}

	public override void _PhysicsProcess(double delta)
	{
		if (!IsAlive) return;
		var direction = Input.GetVector("move_left", "move_right", "move_up", "move_down");
		Velocity = direction != Vector2.Zero ? direction * Speed : Vector2.Zero;
		MoveAndSlide();
	}
}