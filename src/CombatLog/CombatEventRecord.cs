using System.Collections.Generic;

namespace healerfantasy.CombatLog;

public enum CombatEventType
{
	Healing,
	Damage
}

public enum SourceType
{
	Enemy,
	PlayerParty
}

/// <summary>
/// An immutable snapshot of a single healing or damage event emitted by the
/// combat system. Collected into <see cref="CombatLog"/> for meter display
/// and per-ability breakdowns.
/// </summary>
public readonly struct CombatEventRecord
{
	/// <summary>Godot engine time in seconds when the event occurred.</summary>
	public double Timestamp { get; init; }

	/// <summary>CharacterName of the character that caused the event.</summary>
	public string SourceName { get; init; }

	/// <summary>CharacterName of the character that received the healing/damage.</summary>
	public string TargetName { get; init; }

	/// <summary>Name of the spell or effect that produced the event.</summary>
	public string AbilityName { get; init; }

	/// <summary>Effective amount healed or damage dealt.</summary>
	public float Amount { get; init; }

	public CombatEventType Type { get; init; }
	public bool IsCrit { get; init; }

	/// <summary>
	/// Flavour description of the spell or effect that produced this event.
	/// Null on records created before this field was introduced (older saves).
	/// Used by the history/death-screen UI to show a hover tooltip.
	/// </summary>
	public string? Description { get; init; }
}