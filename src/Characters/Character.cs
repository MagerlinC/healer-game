using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using healerfantasy;
using healerfantasy.Effects;
using healerfantasy.SpellSystem;

/// <summary>
/// Abstract base for every character in the game (player and NPCs alike).
/// Owns health, mana, passive drain, a collection of active
/// <see cref="CharacterEffect"/>s, a <see cref="Talent"/> list, a damage-
/// absorbing shield, and a <see cref="SpellHistory"/> record.
/// </summary>
public abstract partial class Character : CharacterBody2D
{
	// ── signals ──────────────────────────────────────────────────────────────
	[Signal]
	public delegate void HealthChangedEventHandler(string characterName, float current, float max);

	[Signal]
	public delegate void ManaChangedEventHandler(string characterName, float current, float max);

	[Signal]
	public delegate void DiedEventHandler(Character character);

	/// <summary>Emitted when an effect is applied (or refreshed) on this character.</summary>
	[Signal]
	public delegate void EffectAppliedEventHandler(string characterName, CharacterEffect effect);

	/// <summary>Emitted when an effect expires or is removed from this character.</summary>
	[Signal]
	public delegate void EffectRemovedEventHandler(string characterName, string effectId);

	/// <summary>
	/// Emitted whenever <see cref="CurrentShield"/> changes — on application,
	/// damage absorption, or expiry. Carries the new shield value and the
	/// character's MaxHealth so the UI can draw a proportional bar.
	/// </summary>
	[Signal]
	public delegate void ShieldChangedEventHandler(string characterName, float currentShield, float maxHealth);

	/// <summary>
	/// Emitted when a spell or periodic effect directly deals damage or restores
	/// health on this character. Used by <see cref="UI.FloatingCombatTextManager"/>
	/// to spawn floating numbers above the character model.
	/// <para>
	/// <paramref name="school"/> is the integer value of
	/// <see cref="SpellResources.SpellSchool"/> cast to <c>int</c>.
	/// </para>
	/// Does <em>not</em> fire for passive life-loss ticks.
	/// </summary>
	[Signal]
	public delegate void FloatingCombatTextEventHandler(float amount, bool isHealing, int school, bool isCrit);

	// ── exports ──────────────────────────────────────────────────────────────
	[Export] public string CharacterName = "Character";
	[Export] public float MaxHealth = 100.0f;
	[Export] public float MaxMana = 100.0f;
	[Export] public bool IsFriendly = true; // for conditional modifiers that check friend vs foe

	[Export] public float ManaRegenPerSecond = 1.0f;

	/// <summary>Base critical strike chance before talent modifiers are applied.</summary>
	[Export] public float BaseCritChance = 0.05f; // 5% chance

	// ── state ────────────────────────────────────────────────────────────────
	public float CurrentHealth { get; private set; }
	public float CurrentMana { get; private set; }
	public bool IsAlive => CurrentHealth > 0f;

	/// <summary>
	/// Current shield points. Damage is absorbed by the shield before
	/// reaching health. Modified via <see cref="AddShield"/> /
	/// <see cref="RemoveShield"/>; consumed automatically in
	/// <see cref="TakeDamage"/>.
	/// </summary>
	public float CurrentShield { get; private set; }

	// ── spell / talent system ─────────────────────────────────────────────────
	/// <summary>
	/// Talents assigned to this character. Call <see cref="GetCharacterStats"/>
	/// to obtain the aggregated stat snapshot, and <see cref="GetSpellModifiers"/>
	/// to collect all active spell modifier instances.
	/// </summary>
	public List<Talent> Talents { get; set; } = new();

	/// <summary>
	/// Persistent record of every completed spell cast made by this character.
	/// Written by <see cref="SpellPipeline"/> after each successful cast.
	/// </summary>
	public SpellHistory SpellHistory { get; } = new();

	// Keyed by CharacterEffect.EffectId for O(1) lookup and deduplication.
	readonly Dictionary<string, CharacterEffect> _effects = new();

	// ── lifecycle ────────────────────────────────────────────────────────────
	public override void _Ready()
	{
		// Apply the persistent level-up health bonus to friendly characters.
		// IsFriendly is set by subclass constructors or scene exports before
		// _Ready() runs, so this check is safe here.
		if (IsFriendly)
			MaxHealth += PlayerProgressStore.MaxHealthBonus;

		CurrentHealth = MaxHealth;
		CurrentMana = MaxMana;
		GlobalAutoLoad.RegisterSignalEmitter(this, nameof(ManaChanged));
		GlobalAutoLoad.RegisterSignalEmitter(this, nameof(HealthChanged));
		GlobalAutoLoad.RegisterSignalEmitter(this, nameof(ShieldChanged));
		GlobalAutoLoad.RegisterSignalEmitter(this, nameof(EffectApplied));
		GlobalAutoLoad.RegisterSignalEmitter(this, nameof(EffectRemoved));
		GlobalAutoLoad.RegisterSignalEmitter(this, nameof(Died));
		GlobalAutoLoad.RegisterSignalEmitter(this, nameof(FloatingCombatText));
		EmitSignalHealthChanged(CharacterName, CurrentHealth, MaxHealth);
		EmitSignalManaChanged(CharacterName, CurrentMana, MaxMana);
		AddToGroup("party");
	}

	public override void _Process(double delta)
	{
		if (IsAlive)
		{
			RestoreMana(ManaRegenPerSecond * (float)delta);
		}

		TickEffects((float)delta);
	}

	// ── public API ───────────────────────────────────────────────────────────

	/// <summary>
	/// Apply damage. The shield absorbs damage first; any remainder reduces
	/// health. Triggers death on the first zero-health crossing.
	/// </summary>
	public void TakeDamage(float amount)
	{
		if (!IsAlive) return;

		// Apply any damage-taken multipliers from active effects/talents (e.g. Death Mark's +25%).
		var stats = GetCharacterStats();
		amount *= stats.DamageTakenMultiplier;

		// Shield absorbs damage before health is affected.
		if (CurrentShield > 0f)
		{
			var absorbed = Mathf.Min(CurrentShield, amount);
			CurrentShield -= absorbed;
			amount -= absorbed;
			EmitSignalShieldChanged(CharacterName, CurrentShield, MaxHealth);
			if (amount <= 0f)
			{
				EmitSignalHealthChanged(CharacterName, CurrentHealth, MaxHealth);
				return;
			}
		}

		CurrentHealth = Mathf.Max(0f, CurrentHealth - amount);
		EmitSignalHealthChanged(CharacterName, CurrentHealth, MaxHealth);

		if (CurrentHealth == 0f)
			OnDeath();
	}

	/// <summary>Restore health, clamped at MaxHealth.</summary>
	public void Heal(float amount)
	{
		if (!IsAlive) return;
		CurrentHealth = Mathf.Min(CurrentHealth + amount, MaxHealth);
		EmitSignalHealthChanged(CharacterName, CurrentHealth, MaxHealth);
	}

	/// <summary>
	/// Add shield points. Subsequent <see cref="TakeDamage"/> calls will drain
	/// the shield before touching health.
	/// </summary>
	public void AddShield(float amount)
	{
		CurrentShield += amount;
		EmitSignalShieldChanged(CharacterName, CurrentShield, MaxHealth);
	}

	/// <summary>
	/// Remove up to <paramref name="amount"/> shield points, clamped at 0.
	/// Called by <see cref="ShieldEffect.OnExpired"/> to clean up on expiry.
	/// </summary>
	public void RemoveShield(float amount)
	{
		CurrentShield = Mathf.Max(0f, CurrentShield - amount);
		EmitSignalShieldChanged(CharacterName, CurrentShield, MaxHealth);
	}

	/// <summary>
	/// Apply an effect to this character. If an effect with the same
	/// <see cref="CharacterEffect.EffectId"/> is already active it is
	/// replaced (refreshed), not stacked.
	/// </summary>
	public void ApplyEffect(CharacterEffect effect)
	{
		if (_effects.TryGetValue(effect.EffectId, out var existing))
		{
			// Let the existing instance handle the re-application (refresh or stack).
			// The new effect object is discarded; we keep the live instance so
			// stack counts and other state on it are preserved.
			existing.OnReapplied(this, effect);
			EmitSignalEffectApplied(CharacterName, existing);
			return;
		}

		_effects[effect.EffectId] = effect;
		effect.OnApplied(this);
		EmitSignalEffectApplied(CharacterName, effect);
	}

	/// <summary>
	/// Remove all active effects that are marked as harmful (i.e. debuffs).
	/// Called by the Dispel spell.
	/// </summary>
	public void RemoveHarmfulEffects()
	{
		var toRemove = new List<string>();
		foreach (var (id, effect) in _effects)
			if (effect.IsHarmful && effect.IsDispellable)
				toRemove.Add(id);
		foreach (var id in toRemove)
			RemoveEffect(id);
	}

	/// <summary>Remove an active effect by id, if present.</summary>
	public void RemoveEffect(string effectId)
	{
		if (_effects.TryGetValue(effectId, out var effect))
		{
			effect.OnExpired(this);
			_effects.Remove(effectId);
			EmitSignalEffectRemoved(CharacterName, effectId);
		}
	}

	public void RefreshAllPlayerEffects()
	{
		if (_effects.Count == 0) return;

		foreach (var effect in _effects.Values)
		{
			if (effect.SourceCharacterName == GameConstants.PlayerName)
			{
				effect.Refresh();
			}
		}
	}

	// ── talent / stat system ─────────────────────────────────────────────────

	/// <summary>
	/// Compute this character's final <see cref="CharacterStats"/> by starting
	/// from the base exported values and applying every <see cref="ICharacterModifier"/>
	/// contributed by the character's talents, in order.
	/// </summary>
	public CharacterStats GetCharacterStats()
	{
		var stats = new CharacterStats
		{
			MaxHealth = MaxHealth,
			MaxMana = MaxMana,
			CritChance = BaseCritChance,
			CritMultiplier = 1.5f,
			DamageMultiplier = 1.0f,
			HealingMultiplier = 1.0f
		};

		foreach (var talent in Talents)
		foreach (var mod in talent.CharacterModifiers)
			mod.Modify(stats);

		// Also apply any active effect that acts as a character modifier
		// (e.g. AccelerationEffect contributing to CastSpeedMultiplier).
		// Mirrors how GetSpellModifiers handles ISpellModifier effects.
		foreach (var eff in _effects.Values)
			if (eff is ICharacterModifier effMod)
				effMod.Modify(stats);

		return stats;
	}

	/// <summary>
	/// Collect all <see cref="ISpellModifier"/> instances that apply to this
	/// character's casts. Includes modifiers from talents AND from any currently
	/// active <see cref="CharacterEffect"/> that implements
	/// <see cref="ISpellModifier"/> (e.g. <see cref="CriticalInfusionBuff"/>).
	/// </summary>
	public IEnumerable<ISpellModifier> GetSpellModifiers()
	{
		foreach (var talent in Talents)
		foreach (var mod in talent.SpellModifiers)
			yield return mod;

		foreach (var effect in _effects.Values)
			if (effect is ISpellModifier mod)
				yield return mod;
	}

	// ── protected helpers ────────────────────────────────────────────────────

	/// <summary>
	/// Public wrapper so external systems (SpellPipeline, effects, spells) can
	/// fire the <see cref="FloatingCombatText"/> signal, which is protected by
	/// the Godot source generator.
	/// </summary>
	public void RaiseFloatingCombatText(float amount, bool isHealing, int school, bool isCrit)
	{
		EmitSignalFloatingCombatText(amount, isHealing, school, isCrit);
	}

	/// <summary>Subtract mana, clamped at 0.</summary>
	public void SpendMana(float amount)
	{
		CurrentMana = Mathf.Max(0f, CurrentMana - amount);
		EmitSignalManaChanged(CharacterName, CurrentMana, MaxMana);
	}

	/// <summary>Restore mana, clamped at MaxMana.</summary>
	public void RestoreMana(float amount)
	{
		CurrentMana = Mathf.Min(CurrentMana + amount, MaxMana);
		EmitSignalManaChanged(CharacterName, CurrentMana, MaxMana);
	}

	// ── private helpers ──────────────────────────────────────────────────────
	void TickEffects(float delta)
	{
		if (_effects.Count == 0) return;

		List<string> expired = null;
		foreach (var (id, effect) in _effects)
		{
			effect.Update(this, delta);
			if (effect.IsExpired)
				(expired ??= new List<string>()).Add(id);
		}

		if (expired == null) return;
		foreach (var id in expired)
		{
			_effects[id].OnExpired(this);
			_effects.Remove(id);
			EmitSignalEffectRemoved(CharacterName, id);
		}
	}

	public CharacterEffect GetEffectById(string effectId)
	{
		_effects.TryGetValue(effectId, out var effect);

		return effect;
	}

	void OnDeath()
	{
		EmitSignalDied(this);
	}
}