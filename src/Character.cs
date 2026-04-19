using Godot;
using healerfantasy;
using SpellResource = healerfantasy.SpellResources.SpellResource;

/// <summary>
/// Abstract base for every character in the game (player and NPCs alike).
/// Owns the health model, passive health drain, and exposes signals so the
/// UI and other systems can react without being coupled to the character.
/// </summary>
public abstract partial class Character : CharacterBody2D
{
	// ── signals ──────────────────────────────────────────────────────────────
	/// <summary>Emitted whenever health changes. Carries current and max values.</summary>
	[Signal]
	public delegate void HealthChangedEventHandler(float current, float max);

	[Signal]
	public delegate void ManaChangedEventHandler(float current, float max);

	/// <summary>Emitted once when the character reaches 0 HP.</summary>
	[Signal]
	public delegate void DiedEventHandler();

	// ── exports ──────────────────────────────────────────────────────────────
	[Export] public string CharacterName = "Character";
	[Export] public float MaxHealth = 100.0f;
	[Export] public float MaxMana = 100.0f;

	/// <summary>Fraction of MaxHealth lost per second (default 10 %).</summary>
	[Export] public float DrainPerSecond = 0.10f;

	[Export] public float ManaRegenPerSecond = 0.5f;

	// ── state ────────────────────────────────────────────────────────────────
	public float CurrentHealth { get; private set; }
	public float CurrentMana { get; private set; }
	public bool IsAlive => CurrentHealth > 0f;

	// ── lifecycle ────────────────────────────────────────────────────────────
	public override void _Ready()
	{
		CurrentHealth = MaxHealth;
		CurrentMana = MaxMana;
		// Init bars
		GlobalAutoLoad.RegisterSignalEmitter(this, nameof(ManaChanged));
		GlobalAutoLoad.RegisterSignalEmitter(this, nameof(HealthChanged));
		EmitSignalHealthChanged(CurrentHealth, MaxHealth);
		EmitSignalManaChanged(CurrentMana, MaxMana);
		AddToGroup("party"); // makes all characters findable for spell targeting
	}

	public override void _Process(double delta)
	{
		if (IsAlive)
			TakeDamage(MaxHealth * DrainPerSecond * (float)delta);

		CurrentMana = Mathf.Min(CurrentMana + ManaRegenPerSecond * (float)delta, MaxMana);
		EmitSignalManaChanged(CurrentMana, MaxMana);
	}

	// ── public API ───────────────────────────────────────────────────────────
	/// <summary>Apply damage, clamped at 0. Triggers death on first zero.</summary>
	public void TakeDamage(float amount)
	{
		if (!IsAlive) return;

		CurrentHealth = Mathf.Max(0f, CurrentHealth - amount);
		EmitSignalHealthChanged(CurrentHealth, MaxHealth);

		if (CurrentHealth == 0f)
			OnDeath();
	}

	/// <summary>
	///  Cast a spell if enough mana. Spells are responsible for their own effects and targets.	
	/// </summary>
	/// <param name="spell"></param>
	public void FireSpell(SpellResource spell)
	{
		if (!IsAlive) return;
		spell.Act(this, this);

	}

	/// <summary> Spend Mana 
	public void SpendMana(float amount)
	{
		if (!IsAlive) return;

		CurrentMana = Mathf.Max(0f, CurrentMana - amount);
		EmitSignal(SignalName.ManaChanged, CurrentMana, MaxMana);
	}

	/// <summary>Restore health, clamped to MaxHealth.</summary>
	public virtual void Heal(float amount)
	{
		if (!IsAlive) return;

		CurrentHealth = Mathf.Min(MaxHealth, CurrentHealth + amount);
		EmitSignal(SignalName.HealthChanged, CurrentHealth, MaxHealth);
	}

	// ── protected hooks ──────────────────────────────────────────────────────
	protected virtual void OnDeath()
	{
		EmitSignal(SignalName.Died);
	}
}