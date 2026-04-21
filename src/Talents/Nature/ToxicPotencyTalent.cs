using healerfantasy.SpellSystem;

namespace healerfantasy.Talents.Nature;

/// <summary>
/// Amplifies all Nature-tagged damage spells by 20%.
/// Pairs well with Poison Bolt for a more offensively-oriented Nature build.
/// </summary>
public class ToxicPotencyTalent : ISpellModifier
{
    const float Bonus = 1.20f;

    public ModifierPriority Priority => ModifierPriority.BASE;

    public void OnBeforeCast(SpellContext ctx) { }

    public void OnCalculate(SpellContext ctx)
    {
        if (!ctx.Tags.HasFlag(SpellTags.Nature | SpellTags.Damage)) return;
        ctx.FinalValue *= Bonus;
    }

    public void OnAfterCast(SpellContext ctx) { }
}
