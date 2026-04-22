#nullable enable
using System.Collections.Generic;
using Godot;
using healerfantasy.SpellSystem;

namespace healerfantasy.SpellResources;

public enum EffectType
{
	Helpful,
	Harmful
}

public enum SpellSchool
{
	Nature,
	Void,
	Holy,
	Chronomancy,
	Generic
}

[GlobalClass]
public partial class SpellResource : Resource
{
	[Export] public string Name;
	[Export] public string Description;
	[Export] public float ManaCost;
	[Export] public float CastTime;
	[Export] public float Cooldown;
	[Export] public bool Parryable = false;
	[Export] public EffectType EffectType = EffectType.Helpful;
	[Export] public SpellSchool School;

	[Export] public Texture2D Icon;

	/// <summary>
	/// Tags that describe this spell's school and behaviour.
	/// Used by the modifier pipeline to gate modifiers conditionally.
	/// Set these in each subclass constructor.
	/// </summary>
	public SpellTags Tags { get; protected set; } = SpellTags.None;

	/// <summary>
	/// Minimum number of talent points the player must have invested in this
	/// spell's <see cref="School"/> before the spell can be equipped.
	/// 0 means no requirement (default — spell is always available).
	/// Set this in subclass constructors for advanced spells.
	/// </summary>
	public int RequiredSchoolPoints { get; protected set; } = 0;
	// ── Pipeline integration ─────────────────────────────────────────────────

	/// <summary>
	/// The raw numeric value that seeds <see cref="SpellContext.BaseValue"/>.
	/// Override in subclasses to return the primary magnitude (heal amount,
	/// damage amount, HoT per-tick value, etc.).
	/// Returns 0 by default for spells with no meaningful numeric output.
	/// </summary>
	public virtual float GetBaseValue()
	{
		return 0f;
	}

	/// <summary>
	/// Resolves the full target list for this cast.
	/// Single-target spells return a one-element list (the default).
	/// Group spells override this to return all valid targets.
	/// </summary>
	public virtual List<Character> ResolveTargets(Character caster, Character explicitTarget)
	{
		return new List<Character> { explicitTarget };
	}

	/// <summary>
	/// Execute the spell using the fully-processed <see cref="SpellContext"/>.
	/// Override in subclasses to read <see cref="SpellContext.FinalValue"/>
	/// and <see cref="SpellContext.Targets"/> so that modifier pipeline output
	/// is honoured.
	/// </summary>
	public virtual void Apply(SpellContext ctx)
	{
		GD.Print("Base spell has no effect.");
	}
}