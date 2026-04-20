using System;
using Godot;
using healerfantasy.SpellSystem;

namespace healerfantasy.Talents;

public enum ModifierPriority
{
	BASE = 0,
	ADDITIVE = 1,
	MULTIPLICATIVE = 2
}

/// <summary>
/// Describes a talent entry as it appears in the talent selector UI.
///
/// <see cref="Name"/>, <see cref="Description"/>, and <see cref="IconPath"/> are
/// the single source of truth — they are written once here and automatically
/// forwarded to the <see cref="Talent"/> built by <see cref="CreateTalent"/>.
///
/// <see cref="Configure"/> only needs to attach the right modifiers; it receives
/// the talent being built and the already-loaded icon texture so modifiers that
/// apply effects (e.g. <see cref="ShieldingReinvigorationTalent"/>) can store
/// it without re-loading from disk.
/// </summary>
public class TalentDefinition
{
	/// <summary>Display name shown in the talent panel.</summary>
	public string Name { get; init; }

	/// <summary>Short description shown below the icon in the talent slot.</summary>
	public string Description { get; init; }

	/// <summary>res:// path to the talent's icon texture.</summary>
	public string IconPath { get; init; }

	/// <summary>
	/// Attaches modifiers to the talent being built.
	/// Receives the blank <see cref="Talent"/> and the loaded icon texture.
	/// <para>
	/// Do NOT set <c>t.Name</c> or <c>t.Description</c> here — those are
	/// forwarded automatically from this definition by <see cref="CreateTalent"/>.
	/// </para>
	/// </summary>
	public Action<Talent, Texture2D> Configure { get; init; }

	/// <summary>
	/// Builds a fully-configured <see cref="Talent"/> ready to be added to a
	/// character. Name, Description, and icon loading are handled here so
	/// <see cref="Configure"/> stays focused on modifiers only.
	/// </summary>
	public Talent CreateTalent()
	{
		var icon = GD.Load<Texture2D>(IconPath);
		var talent = new Talent { Name = Name, Description = Description };
		Configure?.Invoke(talent, icon);
		return talent;
	}
}