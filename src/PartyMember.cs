using Godot;

/// <summary>
/// A non-player party member (Templar, Assassin, Wizard).
/// Inherits health, signals, and future combat logic from Character.
/// NPC-specific behaviour (AI, animations, abilities) will be added here.
/// </summary>
public partial class PartyMember : Character
{
	// All health logic lives in Character.
	// This class is the hook for NPC-specific behaviour going forward.
}
