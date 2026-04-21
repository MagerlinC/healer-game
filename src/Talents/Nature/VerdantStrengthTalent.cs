using healerfantasy.SpellSystem;

namespace healerfantasy.Talents.Nature;

/// <summary>
/// Amplifies all Nature-tagged healing spells by 20%.
/// The Nature school's healing equivalent of Void Specialist — a simple but
/// reliable throughput increase for HoT-focused builds.
/// </summary>
public class VerdantStrengthTalent : ISpellModifier
{
    const float Bonus = 1.20f;

    public ModifierPriority Priority => ModifierPriority.BASE;

    public void OnBeforeCast(SpellContext ctx) { }

    public void OnCalculate(SpellContext ctx)
    {
        if (!ctx.Tags.HasFlag(SpellTags.Nature | SpellTags.Healing)) return;
        ctx.FinalValue *= Bonus;
    }

    public void OnAfterCast(SpellContext ctx) { }
}
