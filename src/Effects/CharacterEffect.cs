using Godot;
using healerfantasy.CombatLog;
using healerfantasy.Effects;
using healerfantasy.SpellResources;

namespace healerfantasy;

/// <summary>
/// Base class for any ongoing effect applied to a Character —
/// heal-over-time, damage-over-time, shields, slows, etc.
///
/// Subclasses override <see cref="OnTick"/> for per-interval logic and
/// optionally <see cref="OnApplied"/> / <see cref="OnExpired"/> for
/// setup and cleanup.
///
/// Effects are identified by <see cref="EffectId"/>, which defaults to the
/// concrete class name. Applying an effect whose Id is already active on a
/// Character replaces (refreshes) the old one rather than stacking it.
/// </summary>
public abstract partial class CharacterEffect : RefCounted
{
	/// <summary>
	/// Unique identifier used for deduplication.
	/// Defaults to the concrete class name so each subclass only ever has
	/// one active instance per character. Override to allow multiple distinct
	/// effects of the same type (e.g. different DoT sources).
	/// </summary>
	/// <summary>
	/// Unique identifier used for deduplication.
	/// Defaults to the concrete class name so each subclass only ever has
	/// one active instance per character. Override in the subclass constructor
	/// (or set externally before calling <see cref="Character.ApplyEffect"/>)
	/// to give the effect a spell-specific ID so multiple distinct DoT sources
	/// don't collide and overwrite each other.
	/// </summary>
	public string EffectId { get; set; }

	/// <summary>Total duration of the effect in seconds.</summary>
	public float Duration { get; protected set; }

	/// <summary>Seconds remaining. Counts down to zero.</summary>
	public float Remaining { get; private set; }

	public bool IsExpired => Remaining <= 0f;

	public int MaximumStacks = 0;
	public int CurrentStacks { get; set; } = 1;

	public bool IsDispellable = false;

	/// <summary>
	/// Optional icon displayed on the affected character's UI frame.
	/// Set this from the spell's <c>Act</c> method when constructing the effect.
	/// </summary>
	public Texture2D Icon { get; set; }

	/// <summary>
	/// CharacterName of the character that applied this effect.
	/// When set, ticking effects (e.g. HealOverTime) will log events to
	/// <see cref="healerfantasy.CombatLog.CombatLog"/> for meter display.
	/// </summary>
	public string SourceCharacterName { get; set; }

	/// <summary>Display name of the spell or talent that applied this effect.</summary>
	public string AbilityName { get; set; }

	/// <summary>Flavour/tooltip description of the spell or talent that applied this effect.</summary>
	public string Description { get; set; }

	/// <summary>
	/// Spell school of this effect, used to colour floating combat text on each tick.
	/// Set this from the spell's <c>Apply</c> method when constructing the effect.
	/// </summary>
	public SpellSchool School { get; set; } = SpellSchool.Generic;

	/// <summary>
	/// When true, this effect can be removed by Dispel.
	/// Set to true for debuffs (damage-over-time, curses, etc.).
	/// </summary>
	public bool IsHarmful { get; set; } = false;

	// How often OnTick fires. 0 means no discrete ticks (continuous only).
	readonly float _tickInterval;
	float _tickTimer;

	/// <param name="duration">Total effect duration in seconds.</param>
	/// <param name="tickInterval">
	/// How often <see cref="OnTick"/> is called, in seconds.
	/// Pass 0 to skip ticking (use <see cref="OnApplied"/> for instant effects).
	/// The first tick fires on the very first <see cref="Update"/> call.
	/// </param>
	protected CharacterEffect(float duration, float tickInterval = 0f)
	{
		EffectId = GetType().Name;
		Duration = duration;
		Remaining = duration;
		_tickInterval = tickInterval;
		_tickTimer = 0f; // fire first tick immediately
	}

	// ── hooks for subclasses ─────────────────────────────────────────────────
	/// <summary>Called once when the effect is first applied to a character.</summary>
	public virtual void OnApplied(Character target)
	{
	}

	/// <summary>
	/// Called when a new effect with the same <see cref="EffectId"/> is applied
	/// while this instance is already active on <paramref name="target"/>.
	/// The default behaviour refreshes the duration. Override to implement stacking
	/// (e.g. incrementing <see cref="CurrentStacks"/>).
	/// <para>
	/// The incoming <paramref name="newEffect"/> is discarded after this call —
	/// the dictionary keeps <em>this</em> instance, so all mutations must be
	/// made on <c>this</c>.
	/// </para>
	/// </summary>
	public virtual void OnReapplied(Character target, CharacterEffect newEffect)
	{
		Refresh();
	}

	/// <summary>Called on each tick while the effect is active.</summary>
	protected virtual void OnTick(Character target)
	{
	}

	/// <summary>Called once when the effect expires naturally or is removed.</summary>
	public virtual void OnExpired(Character target)
	{
	}

	public void Refresh()
	{
		Remaining = Duration;
	}

	/// <summary>
	/// Override to trigger early expiry based on runtime state.
	/// Checked every frame in <see cref="Update"/> before tick processing.
	/// Example: <see cref="Effects.ShieldEffect"/> returns <c>true</c> when the
	/// character's shield has been fully consumed by incoming damage.
	/// </summary>
	protected virtual bool ShouldExpireEarly(Character target)
	{
		return false;
	}

	// ── internal update loop — driven by Character._Process ──────────────────
	public void Update(Character target, float delta)
	{
		Remaining -= delta;

		// Allow subclasses to cut the duration short (e.g. shield fully consumed).
		if (!IsExpired && ShouldExpireEarly(target))
			Remaining = 0f;

		if (_tickInterval <= 0f) return;

		_tickTimer -= delta;
		while (_tickTimer <= 0f)
		{
			OnTick(target);
			_tickTimer += _tickInterval;

			// Stop ticking if the effect expired during this batch
			if (IsExpired) break;
		}
	}
}