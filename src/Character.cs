using System.Collections.Generic;
using Godot;
using healerfantasy;
using SpellResource = healerfantasy.SpellResources.SpellResource;

/// <summary>
/// Abstract base for every character in the game (player and NPCs alike).
/// Owns health, mana, passive drain, and a collection of active
/// <see cref="CharacterEffect"/>s that are ticked every frame.
/// </summary>
public abstract partial class Character : CharacterBody2D
{
	// ── signals ──────────────────────────────────────────────────────────────
	[Signal] public delegate void HealthChangedEventHandler(float current, float max);
	[Signal] public delegate void ManaChangedEventHandler(float current, float max);
	[Signal] public delegate void DiedEventHandler();

	// ── exports ──────────────────────────────────────────────────────────────
	[Export] public string CharacterName = "Character";
	[Export] public float MaxHealth = 100.0f;
	[Export] public float MaxMana = 100.0f;

	/// <summary>Fraction of MaxHealth lost per second.</summary>
	[Export] public float DrainPerSecond = 0.10f;

	[Export] public float ManaRegenPerSecond = 0.5f;

	// ── state ────────────────────────────────────────────────────────────────
	public float CurrentHealth { get; private set; }
	public float CurrentMana  { get; private set; }
	public bool  IsAlive => CurrentHealth > 0f;

	// Keyed by CharacterEffect.EffectId for O(1) lookup and deduplication.
	readonly Dictionary<string, CharacterEffect> _effects = new();

	// ── lifecycle ────────────────────────────────────────────────────────────
	public override void _Ready()
	{
		CurrentHealth = MaxHealth;
		CurrentMana   = MaxMana;
		GlobalAutoLoad.RegisterSignalEmitter(this, nameof(ManaChanged));
		GlobalAutoLoad.RegisterSignalEmitter(this, nameof(HealthChanged));
		EmitSignalHealthChanged(CurrentHealth, MaxHealth);
		EmitSignalManaChanged(CurrentMana, MaxMana);
		AddToGroup("party");
	}

	public override void _Process(double delta)
	{
		if (IsAlive)
			TakeDamage(MaxHealth * DrainPerSecond * (float)delta);

		RestoreMana(ManaRegenPerSecond * (float)delta);
		TickEffects((float)delta);
	}

	// ── public API ───────────────────────────────────────────────────────────
	/// <summary>Apply damage, clamped at 0. Triggers death on first zero crossing.</summary>
	public void TakeDamage(float amount)
	{
		if (!IsAlive) return;

		CurrentHealth = Mathf.Max(0f, CurrentHealth - amount);
		EmitSignalHealthChanged(CurrentHealth, MaxHealth);

		if (CurrentHealth == 0f)
			OnDeath();
	}

	/// <summary>Restore health, clamped at MaxHealth.</summary>
	public void Heal(float amount)
	{
		if (!IsAlive) return;
		CurrentHealth = Mathf.Min(CurrentHealth + amount, MaxHealth);
		EmitSignalHealthChanged(CurrentHealth, MaxHealth);
	}

	/// <summary>
	/// Apply an effect to this character. If an effect with the same
	/// <see cref="CharacterEffect.EffectId"/> is already active it is
	/// replaced (refreshed), not stacked.
	/// </summary>
	public void ApplyEffect(CharacterEffect effect)
	{
		if (_effects.TryGetValue(effect.EffectId, out var existing))
			existing.OnExpired(this);

		_effects[effect.EffectId] = effect;
		effect.OnApplied(this);
	}

	/// <summary>Remove an active effect by id, if present.</summary>
	public void RemoveEffect(string effectId)
	{
		if (_effects.TryGetValue(effectId, out var effect))
		{
			effect.OnExpired(this);
			_effects.Remove(effectId);
		}
	}

	// ── protected helpers ────────────────────────────────────────────────────
	/// <summary>
	/// Fire a spell at the given target, deducting its mana cost at this point.
	/// </summary>
	protected void FireSpell(SpellResource spell, Character target)
	{
		SpendMana(spell.ManaCost);
		spell.Act(this, target);
	}

	/// <summary>Convenience overload — casts the spell on self.</summary>
	protected void FireSpell(SpellResource spell) => FireSpell(spell, this);

	/// <summary>Subtract mana, clamped at 0.</summary>
	protected void SpendMana(float amount)
	{
		CurrentMana = Mathf.Max(0f, CurrentMana - amount);
		EmitSignalManaChanged(CurrentMana, MaxMana);
	}

	/// <summary>Restore mana, clamped at MaxMana.</summary>
	protected void RestoreMana(float amount)
	{
		CurrentMana = Mathf.Min(CurrentMana + amount, MaxMana);
		EmitSignalManaChanged(CurrentMana, MaxMana);
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
		}
	}

	// ── protected virtuals ───────────────────────────────────────────────────
	protected virtual void OnDeath()
	{
		EmitSignal(SignalName.Died);
	}
}
