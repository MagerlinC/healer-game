namespace healerfantasy.SpellSystem;

/// <summary>
/// An <see cref="ISpellModifier"/> that also applies to spell casts made by
/// NPC party members (Templar, Assassin, Wizard).
///
/// Normal <see cref="ISpellModifier"/>s on the player's equipped items only
/// run when the player casts. Implement this interface instead (or in addition)
/// to make a modifier party-wide: <see cref="SpellPipeline"/> will inject it
/// into the modifier list whenever a <see cref="PartyMember"/> casts, sourcing
/// the instances from <see cref="Items.ItemStore"/>.
///
/// Typical use: items with an aura-style buff that reads "party members deal
/// X% more damage / healing" (e.g. Chains of Command).
/// </summary>
public interface IPartySpellModifier : ISpellModifier
{
}
