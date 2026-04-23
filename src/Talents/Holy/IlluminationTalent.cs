using healerfantasy.SpellSystem;

namespace healerfantasy.Talents.Holy;

/// <summary>
/// Critical healing strikes are blessed with divine efficiency, refunding a
/// large portion of the spell's mana cost when they proc.
///
/// Synergises strongly with <see cref="CriticalAmplifierTalent"/> and other
/// crit-boosting talents — the more often you crit, the less your heals cost.
/// Rewards a high-crit Holy healing build with near-free casting on lucky procs.
/// </summary>
public class IlluminationTalent : ISpellModifier
{
    /// <summary>Fraction of mana cost refunded on a healing crit.</summary>
    const float RefundFraction = 0.50f;

    public ModifierPriority Priority => ModifierPriority.BASE;

    public void OnBeforeCast(SpellContext ctx) { }

    public void OnCalculate(SpellContext ctx) { }

    public void OnAfterCast(SpellContext ctx)
    {
        if (!ctx.Tags.HasFlag(SpellTags.Healing)) return;
        if (!ctx.Tags.HasFlag(SpellTags.Critical)) return;

        var refund = ctx.Spell.ManaCost * RefundFraction;
        ctx.Caster.RestoreMana(refund);
    }
}
