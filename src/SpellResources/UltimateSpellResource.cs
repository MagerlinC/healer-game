using healerfantasy.SpellSystem;

namespace healerfantasy.SpellResources.Void;

/// <summary>
/// Base class for all Ultimate spells.
/// Each ultimate has a casting requirement that must be met before it can be used.
/// Subclasses implement the requirement by overriding <see cref="Requirement"/>,
/// <see cref="OnRegularSpellCast"/>, and/or <see cref="OnProcessTick"/> to
/// accumulate <see cref="Progress"/> toward the threshold.
/// </summary>
public partial class UltimateSpellResource : SpellResource
{
	public string ActivationDescription { get; set; } = "";

	/// <summary>
	/// Accumulated progress toward the casting requirement.
	/// Compared against <see cref="Requirement"/> by <see cref="IsRequirementMet"/>.
	/// </summary>
	public float Progress { get; protected set; } = 0f;

	/// <summary>
	/// The threshold <see cref="Progress"/> must reach before this ultimate can be cast.
	/// Override in each subclass to match the spell's requirement.
	/// </summary>
	public virtual float Requirement => 1f;

	/// <summary>True when the requirement is fully met and the spell is castable.</summary>
	public bool IsRequirementMet => Progress >= Requirement;

	/// <summary>
	/// The EffectId of the buff this ultimate applies to the caster while it is running.
	/// Used by <see cref="UI.UltimateSlot"/> to determine the "active" visual state.
	/// Return an empty string for ultimates that don't apply a persistent buff.
	/// </summary>
	public virtual string ActiveEffectId => string.Empty;

	/// <summary>
	/// Called by <see cref="Characters.Player"/> after every successful non-ultimate
	/// spell cast. Override to advance <see cref="Progress"/> based on cast data.
	/// </summary>
	public virtual void OnRegularSpellCast(SpellContext ctx)
	{
	}

	/// <summary>
	/// Called by <see cref="Characters.Player"/> every frame while the player is alive.
	/// Override for time-based requirements (e.g. seconds of HoT coverage).
	/// </summary>
	public virtual void OnProcessTick(Character caster, float delta)
	{
	}

	/// <summary>
	/// Resets <see cref="Progress"/> to zero. Called by Player after the ultimate is cast
	/// so the player must rebuild the requirement for the next use.
	/// </summary>
	public void ResetProgress()
	{
		Progress = 0f;
	}

	/// <summary>
	/// Returns true if the requirement has been met.
	/// Subclasses may override to add additional conditions, but should call base
	/// or check <see cref="IsRequirementMet"/> themselves.
	/// The context parameter is available for overrides that need cast-time info,
	/// but is not used by the default implementation.
	/// </summary>
	public virtual bool CanCast(SpellContext ctx)
	{
		return IsRequirementMet;
	}
}