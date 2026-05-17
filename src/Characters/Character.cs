using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using healerfantasy;
using healerfantasy.Effects;
using healerfantasy.Items;
using healerfantasy.Merchant;
using healerfantasy.Runes;
using healerfantasy.SpellResources;
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
	/// Emitted whenever <see cref="CurrentHealAbsorption"/> changes.
	/// Carries the remaining absorption and MaxHealth so the UI can draw a
	/// proportional dark-purple overlay bar on the party frame.
	/// Only meaningful for friendly characters (Rune of the Void).
	/// </summary>
	[Signal]
	public delegate void HealAbsorptionChangedEventHandler(string characterName, float absorptionRemaining, float maxHealth);

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

	/// <summary>
	/// Emitted by bosses whenever they choose a party member as their current
	/// melee target. Carries the target character name, or an empty string when
	/// no valid melee target exists.
	/// </summary>
	[Signal]
	public delegate void BossMeleeTargetChangedEventHandler(string characterName);

	// ── exports ──────────────────────────────────────────────────────────────
	[Export] public string CharacterName = "Character";
	[Export] public float MaxHealth = 100.0f;
	[Export] public float MaxMana = 100.0f;
	[Export] public bool IsFriendly = true; // for conditional modifiers that check friend vs foe

	/// <summary>Base critical strike chance before talent modifiers are applied.</summary>
	[Export] public float BaseCritChance = 0.05f; // 5% chance

	// ── state ────────────────────────────────────────────────────────────────
	public float CurrentHealth { get; private set; }
	public float CurrentMana { get; private set; }
	public bool IsAlive => CurrentHealth > 0f;
	public bool IsBeingRemoved { get; private set; }

	/// <summary>
	/// Current shield points. Damage is absorbed by the shield before
	/// reaching health. Modified via <see cref="AddShield"/> /
	/// <see cref="RemoveShield"/>; consumed automatically in
	/// <see cref="TakeDamage"/>.
	/// </summary>
	public float CurrentShield { get; private set; }

	/// <summary>
	/// Pending heal-absorption points (Rune of the Void).
	/// When greater than zero, incoming heals are consumed by this amount first
	/// before any health is actually restored.
	/// </summary>
	public float CurrentHealAbsorption { get; private set; }

	// ── spell / talent / item system ──────────────────────────────────────────
	/// <summary>
	/// Talents assigned to this character. Call <see cref="GetCharacterStats"/>
	/// to obtain the aggregated stat snapshot, and <see cref="GetSpellModifiers"/>
	/// to collect all active spell modifier instances.
	/// </summary>
	public List<Talent> Talents { get; set; } = new();

	/// <summary>
	/// Items currently equipped on this character for the active run.
	/// Their <see cref="EquippableItem.CharacterModifiers"/> feed into
	/// <see cref="GetCharacterStats"/> and their
	/// <see cref="EquippableItem.SpellModifiers"/> into
	/// <see cref="GetSpellModifiers"/>, using the same pipelines as Talents.
	///
	/// For the player character, populated from <see cref="ItemStore"/> in
	/// <c>Player._Ready()</c>. NPC characters leave this empty.
	/// </summary>
	public List<EquippableItem> EquippedItems { get; set; } = new();

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
		// Scale enemy MaxHealth by 10% per active rune (Rune system baseline).
		if (!IsFriendly && RunState.Instance != null && RunState.Instance.ActiveRuneCount > 0)
			MaxHealth *= 1f + RunState.Instance.ActiveRuneCount * GameConstants.RuneHealthBonusPerRune;

		CurrentHealth = MaxHealth;
		CurrentMana = MaxMana;
		GlobalAutoLoad.RegisterSignalEmitter(this, nameof(ManaChanged));
		GlobalAutoLoad.RegisterSignalEmitter(this, nameof(HealthChanged));
		GlobalAutoLoad.RegisterSignalEmitter(this, nameof(ShieldChanged));
		GlobalAutoLoad.RegisterSignalEmitter(this, nameof(HealAbsorptionChanged));
		GlobalAutoLoad.RegisterSignalEmitter(this, nameof(EffectApplied));
		GlobalAutoLoad.RegisterSignalEmitter(this, nameof(EffectRemoved));
		GlobalAutoLoad.RegisterSignalEmitter(this, nameof(Died));
		GlobalAutoLoad.RegisterSignalEmitter(this, nameof(FloatingCombatText));
		GlobalAutoLoad.RegisterSignalEmitter(this, nameof(BossMeleeTargetChanged));
		EmitSignalHealthChanged(CharacterName, CurrentHealth, MaxHealth);
		EmitSignalManaChanged(CharacterName, CurrentMana, MaxMana);
	}

	public override void _Process(double delta)
	{
		if (IsBeingRemoved) return;

		if (IsAlive)
		{
			var stats = GetCharacterStats();
			RestoreMana(stats.ManaRegenPerSecond * (float)delta);
		}

		TickEffects((float)delta);
	}

	// ── public API ───────────────────────────────────────────────────────────

	/// <summary>
	/// Apply damage. The shield absorbs damage first; any remainder reduces
	/// health. Triggers death on the first zero-health crossing.
	/// Override in subclasses to intercept damage (e.g. boss immunity phases).
	/// </summary>
	public virtual void TakeDamage(float amount)
	{
		if (!IsAlive) return;

		// Apply any damage-taken multipliers from active effects/talents (e.g. Death Mark's +25%).
		var stats = GetCharacterStats();
		amount *= stats.DamageTakenMultiplier;

		// Rune of the Void: apply healing absorption equal to 10% of all
		// damage taken by friendly characters.
		if (IsFriendly && amount > 0f &&
		    RunState.Instance?.IsRuneActive(RuneIndex.Void) == true)
		{
			AddHealAbsorption(amount * GameConstants.RuneVoidAbsorptionFraction);
		}

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

	/// <summary>
	/// Restore health, clamped at MaxHealth.
	/// Applies any <see cref="CharacterStats.HealingReceivedMultiplier"/> from active
	/// effects (e.g. Crimson Curse) before adding the amount.
	/// If there is active heal-absorption (Rune of the Void), incoming healing
	/// is consumed by the absorption first before reaching health.
	/// </summary>
	public void Heal(float amount)
	{
		if (!IsAlive) return;

		var stats = GetCharacterStats();
		amount *= Mathf.Max(0f, stats.HealingReceivedMultiplier);
		if (amount <= 0f) return;

		// Rune of the Void: consume heal absorption before the heal reaches health.
		if (CurrentHealAbsorption > 0f)
		{
			var consumed = Mathf.Min(CurrentHealAbsorption, amount);
			CurrentHealAbsorption -= consumed;
			amount -= consumed;
			EmitSignalHealAbsorptionChanged(CharacterName, CurrentHealAbsorption, MaxHealth);
			if (amount <= 0f) return;
		}

		CurrentHealth = Mathf.Min(CurrentHealth + amount, MaxHealth);
		EmitSignalHealthChanged(CharacterName, CurrentHealth, MaxHealth);
	}

	/// <summary>
	/// Bring a dead character back to life at the given health amount.
	/// Unlike <see cref="Heal"/>, this bypasses the <c>IsAlive</c> guard and
	/// sets health directly — intended only for resurrection effects such as the
	/// Stone of Rebirth.  Emits <see cref="HealthChanged"/> so the UI updates.
	/// </summary>
	public void Revive(float amount)
	{
		SetCurrentHealthDirect(Mathf.Clamp(amount, 1f, MaxHealth));
	}

	/// <summary>
	/// Permanently adjust this character's <see cref="MaxHealth"/> by
	/// <paramref name="delta"/> (positive to increase, negative to decrease)
	/// and keep <see cref="CurrentHealth"/> in sync.
	/// <para>
	/// When increasing: current health rises by the same amount so the bar
	/// stays proportionally full (matching the convention used by
	/// WoW-style max-health buffs such as Rallying Cry).
	/// When decreasing: current health is clamped to the new max so it
	/// can never exceed it.
	/// </para>
	/// Emits <see cref="HealthChanged"/> so the UI updates immediately.
	/// </summary>
	public void ModifyMaxHealth(float delta)
	{
		MaxHealth += delta;
		CurrentHealth = delta > 0f
			? Mathf.Min(CurrentHealth + delta, MaxHealth)
			: Mathf.Min(CurrentHealth, MaxHealth);
		EmitSignalHealthChanged(CharacterName, CurrentHealth, MaxHealth);
	}

	/// <summary>
	/// Force the character's current health to a specific value without running
	/// the normal damage / death pipeline. Intended for encounter scripts that
	/// need bespoke phase-transition behaviour.
	/// </summary>
	protected void SetCurrentHealthDirect(float amount)
	{
		CurrentHealth = Mathf.Clamp(amount, 0f, MaxHealth);
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

	// ── heal absorption (Rune of the Void) ───────────────────────────────────

	/// <summary>
	/// Adds <paramref name="amount"/> to the character's pending heal absorption.
	/// The next incoming heal(s) will be consumed by this amount before reaching
	/// health, producing the dark-purple overlay on the party frame.
	/// </summary>
	public void AddHealAbsorption(float amount)
	{
		CurrentHealAbsorption += amount;
		EmitSignalHealAbsorptionChanged(CharacterName, CurrentHealAbsorption, MaxHealth);
	}

	/// <summary>
	/// Clears all pending heal absorption (e.g. on death or fight reset).
	/// </summary>
	public void ClearHealAbsorption()
	{
		if (CurrentHealAbsorption <= 0f) return;
		CurrentHealAbsorption = 0f;
		EmitSignalHealAbsorptionChanged(CharacterName, 0f, MaxHealth);
	}

	// ── rune hooks ────────────────────────────────────────────────────────────

	/// <summary>
	/// Convenience property: returns <c>true</c> when Rune 4 (Rune of Purity)
	/// is active for the current run.  Boss spells check this to decide whether
	/// to enable their "purest form" extra mechanics.
	/// </summary>
	protected bool RuneOfPurityActive =>
		RunState.Instance?.IsRuneActive(RuneIndex.Purity) == true;

	/// <summary>
	/// Override in boss subclasses to scale attack/ability interval fields
	/// by the Rune of Time haste multiplier.
	/// Called by <see cref="ApplyRuneModifiers"/> at the end of each boss's
	/// own <c>_Ready()</c>, AFTER interval fields have been initialised.
	/// </summary>
	protected virtual void OnApplyHasteRune()
	{
	}

	/// <summary>
	/// Call at the end of a boss character's <c>_Ready()</c> (after all timer
	/// fields are initialised) to apply any active rune modifiers that require
	/// per-boss knowledge (currently only Rune 3 — Time).
	/// </summary>
	protected void ApplyRuneModifiers()
	{
		if (IsFriendly) return;
		if (RunState.Instance?.IsRuneActive(RuneIndex.Time) == true)
			OnApplyHasteRune();
	}

	/// <summary>
	/// Apply an effect to this character. If an effect with the same
	/// <see cref="CharacterEffect.EffectId"/> is already active it is
	/// replaced (refreshed), not stacked.
	/// </summary>
	/// <summary>
	/// When set, <see cref="ApplyEffect"/> appends this string to every incoming
	/// effect's <see cref="CharacterEffect.EffectId"/> before storing it.
	/// Used by <see cref="Effects.MirrorImageEffect"/> so the clone's copy of a
	/// spell's effect never clobbers the player's original on the same target.
	/// Always reset to <c>null</c> in a <c>finally</c> block after use.
	/// </summary>
	internal static string? MirrorEffectIdSuffix { get; set; } = null;

	public void ApplyEffect(CharacterEffect effect)
	{
		// If a mirror-echo is being applied, stamp the effect ID so it lives
		// alongside the player's original rather than replacing it.
		if (MirrorEffectIdSuffix != null && !effect.EffectId.EndsWith(MirrorEffectIdSuffix))
			effect.EffectId += MirrorEffectIdSuffix;

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

		// Trigger the dispel tutorial the first time a harmful, dispellable debuff
		// lands on a friendly character (e.g. Corrosive Ooze from the Demon Slime).
		if (effect.IsHarmful && effect.IsDispellable && IsFriendly)
			CombatTutorialEvents.FireHarmfulEffectApplied(CharacterName);
	}

	/// <summary>
	/// Remove all active effects that are marked as harmful (i.e. debuffs).
	/// Called by the Dispel spell.
	/// Returns <c>true</c> if at least one effect was removed, <c>false</c> if
	/// the target had nothing dispellable — used to gate the Dispel cooldown.
	/// Virtual so that special targets (e.g. <see cref="CountessClone"/>) can
	/// intercept the call and trigger mechanic-specific behaviour instead.
	/// </summary>
	public virtual bool RemoveHarmfulEffects()
	{
		var toRemove = new List<string>();
		foreach (var (id, effect) in _effects)
			if (effect.IsHarmful && effect.IsDispellable)
				toRemove.Add(id);
		foreach (var id in toRemove)
			RemoveEffect(id);
		return toRemove.Count > 0;
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

	/// <summary>
	/// Remove an active effect by id without invoking its
	/// <see cref="CharacterEffect.OnExpired"/> callback.
	/// Use when you intend to immediately replace the effect with a new one
	/// and don't want the expiry logic to fire (e.g. Wrath of Nature swapping
	/// a plain HoT for a wrapped version).
	/// </summary>
	public void RemoveEffectSilent(string effectId)
	{
		if (_effects.Remove(effectId))
			EmitSignalEffectRemoved(CharacterName, effectId);
	}

	public enum EffectFilter
	{
		All,
		HarmfulOnly,
		FriendlyOnly
	}

	public void RefreshAllPlayerEffects(EffectFilter filter = EffectFilter.All, SpellSchool? school = null)
	{
		if (_effects.Count == 0) return;

		var effectsForSchool = school.HasValue
			? _effects.Values.Where(effect => effect.School == school.Value)
			: _effects.Values;

		var nonUltPlayerEffects = effectsForSchool.Where(effect =>
			effect.SourceCharacterName == GameConstants.HealerName && !effect.IsUltimateEffect);

		foreach (var effect in nonUltPlayerEffects)
		{
			switch (filter)
			{
				case EffectFilter.All:
				case EffectFilter.FriendlyOnly when !effect.IsHarmful:
				case EffectFilter.HarmfulOnly when effect.IsHarmful:
					effect.Refresh();
					break;
			}
		}
	}

	public void ExtendAllPlayerEffects(float duration, EffectFilter filter = EffectFilter.All, SpellSchool? school = null)
	{
		if (_effects.Count == 0) return;

		var effectsForSchool = school.HasValue
			? _effects.Values.Where(effect => effect.School == school.Value)
			: _effects.Values;

		var nonUltPlayerEffects = effectsForSchool.Where(effect =>
			effect.SourceCharacterName == GameConstants.HealerName && !effect.IsUltimateEffect);

		foreach (var effect in nonUltPlayerEffects)
		{
			switch (filter)
			{
				case EffectFilter.All:
				case EffectFilter.FriendlyOnly when !effect.IsHarmful:
				case EffectFilter.HarmfulOnly when effect.IsHarmful:
					effect.ExtendBy(duration);
					break;
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
			// Templar damage reduction
			DamageTakenMultiplier = Name == GameConstants.TemplarName ? 0.8f : 1.0f
		};

		foreach (var talent in Talents)
		foreach (var mod in talent.CharacterModifiers)
			mod.Modify(stats);

		// Apply modifiers from equipped items (same pipeline as talents).
		foreach (var item in EquippedItems)
		foreach (var mod in item.CharacterModifiers)
			mod.Modify(stats);

		// Also apply any active effect that acts as a character modifier
		// (e.g. AccelerationEffect contributing to CastSpeedMultiplier).
		// Mirrors how GetSpellModifiers handles ISpellModifier effects.
		foreach (var eff in _effects.Values)
			if (eff is ICharacterModifier effMod)
				effMod.Modify(stats);

		// Apply conditional modifiers that need the character reference to
		// inspect runtime state (e.g. HolyProtectionEffect checking active effects).
		foreach (var eff in _effects.Values)
			if (eff is IConditionalCharacterModifier condMod)
				condMod.Modify(stats, this);

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

		// Yield spell modifiers from equipped items (Legendary effects).
		foreach (var item in EquippedItems)
		foreach (var mod in item.SpellModifiers)
			yield return mod;

		foreach (var effect in _effects.Values)
			if (effect is ISpellModifier mod)
				yield return mod;
	}

	/// <summary>
	/// Returns <paramref name="baseInterval"/> scaled by this character's current
	/// haste, using the same formula applied to cast times and the global cooldown:
	/// <c>baseInterval * (1 − IncreasedHaste)</c>, floored at 0.2 s so no attack
	/// can fire faster than 5 times per second regardless of haste stacking.
	/// <para>
	/// Call this every time an attack timer resets so that haste gained or lost
	/// mid-fight (e.g. from <see cref="Effects.HasteEffect"/>) takes effect on
	/// the very next swing rather than requiring a full interval to elapse.
	/// </para>
	/// </summary>
	public float GetHasteAdjustedAttackInterval(float baseInterval)
	{
		var stats = GetCharacterStats();
		return Mathf.Max(baseInterval * (1f - stats.IncreasedHaste), 0.2f);
	}

	// ── protected helpers ────────────────────────────────────────────────────

	/// <summary>
	/// Public wrapper so external systems (SpellPipeline, effects, spells) can
	/// fire the <see cref="FloatingCombatText"/> signal, which is protected by
	/// the Godot source generator.
	/// </summary>
	public void RaiseFloatingCombatText(float amount, bool isHealing, int school, bool isCrit)
	{
		if (IsBeingRemoved || IsQueuedForDeletion()) return;
		EmitSignalFloatingCombatText(amount, isHealing, school, isCrit);
	}

	protected void RaiseBossMeleeTargetChanged(Character? target)
	{
		if (IsBeingRemoved || IsQueuedForDeletion()) return;
		EmitSignalBossMeleeTargetChanged(target?.CharacterName ?? string.Empty);
	}

	/// <summary>Subtract mana, clamped at 0.</summary>
	public void SpendMana(float amount)
	{
		var effectiveMax = GetCharacterStats().MaxMana;
		CurrentMana = Mathf.Max(0f, CurrentMana - amount);
		EmitSignalManaChanged(CharacterName, CurrentMana, effectiveMax);
	}

	public void SpendLife(float amount)
	{
		CurrentHealth = Mathf.Max(1f, CurrentHealth - amount);
		EmitSignalHealthChanged(CharacterName, CurrentHealth, MaxHealth);
	}

	/// <summary>Restore mana, clamped at the effective MaxMana (including item/spell bonuses).</summary>
	public void RestoreMana(float amount)
	{
		var effectiveMax = GetCharacterStats().MaxMana;
		CurrentMana = Mathf.Min(CurrentMana + amount, effectiveMax);
		EmitSignalManaChanged(CharacterName, CurrentMana, effectiveMax);
	}

	/// <summary>
	/// Re-initializes <see cref="CurrentMana"/> to the current effective maximum,
	/// accounting for all item and talent bonuses. Call after equipping items or
	/// applying any permanent stat changes that affect MaxMana.
	/// </summary>
	protected void ReinitializeMana()
	{
		var effectiveMax = GetCharacterStats().MaxMana;
		CurrentMana = effectiveMax;
		EmitSignalManaChanged(CharacterName, CurrentMana, effectiveMax);
	}

	// ── private helpers ──────────────────────────────────────────────────────
	void TickEffects(float delta)
	{
		if (_effects.Count == 0) return;

		List<string> expired = null;

		foreach (var (id, effect) in _effects.ToArray())
		{
			// effect may already have been removed by another effect
			if (!_effects.ContainsKey(id))
				continue;

			effect.Update(this, delta);

			if (effect.IsExpired)
				(expired ??= new List<string>()).Add(id);
		}

		if (expired == null) return;

		foreach (var id in expired)
		{
			if (!_effects.TryGetValue(id, out var effect))
				continue;

			effect.OnExpired(this);
			_effects.Remove(id);
			EmitSignalEffectRemoved(CharacterName, id);
		}
	}

	public CharacterEffect GetEffectById(string effectId)
	{
		_effects.TryGetValue(effectId, out var effect);

		return effect;
	}

	/// <summary>
	/// Returns all currently active effects on this character.
	/// Used by talents that need to enumerate or consume active effects
	/// (e.g. <see cref="Talents.Void.VoidResonanceTalent"/> consuming DoTs).
	/// </summary>
	public IEnumerable<CharacterEffect> GetAllEffects()
	{
		return _effects.Values;
	}

	void OnDeath()
	{
		// Give the Stone of Rebirth a chance to intercept death for party members.
		if (MerchantStore.TryRevive(this))
			return;

		IsBeingRemoved = true;
		ClearHealAbsorption();
		foreach (var effect in _effects.Values)
			effect.OnExpired(this);
		_effects.Clear();
		ApplyDeathVisuals();
		EmitSignalDied(this);
	}

	/// <summary>
	/// Override in subclasses to apply death visuals (stop animation,
	/// greyscale, lie-down rotation) when the character reaches 0 health.
	/// Called immediately before the <see cref="Died"/> signal is emitted.
	/// </summary>
	protected virtual void ApplyDeathVisuals()
	{
	}

	public override void _ExitTree()
	{
		IsBeingRemoved = true;
		GlobalAutoLoad.UnregisterSignalEmitter(this);
		base._ExitTree();
	}

	/// <summary>
	/// Returns all alive party members in a randomised order.
	/// Useful for abilities that need to visit every member 
	/// </summary>
	public List<Character> CollectAlivePartyMembers()
	{
		var members = new List<Character>();
		foreach (var node in GetTree().GetNodesInGroup(GameConstants.PartyGroupName))
			if (node is Character c && c.IsAlive)
				members.Add(c);

		// Fisher-Yates shuffle
		for (var i = members.Count - 1; i > 0; i--)
		{
			var j = (int)(GD.Randi() % (uint)(i + 1));
			(members[i], members[j]) = (members[j], members[i]);
		}

		return members;
	}

	public List<Character> CollectAliveEnemies()
	{
		var members = new List<Character>();
		foreach (var node in GetTree().GetNodesInGroup(GameConstants.BossGroupName))
			if (node is Character c && c.IsAlive)
				members.Add(c);

		// Fisher-Yates shuffle
		for (var i = members.Count - 1; i > 0; i--)
		{
			var j = (int)(GD.Randi() % (uint)(i + 1));
			(members[i], members[j]) = (members[j], members[i]);
		}

		return members;
	}
}