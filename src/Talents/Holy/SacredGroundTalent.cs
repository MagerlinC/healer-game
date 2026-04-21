using healerfantasy.SpellSystem;

namespace healerfantasy.Talents.Holy;

/// <summary>
/// Each time a healing spell lands, restore a small amount of mana for every
/// target that was healed. Rewards keeping the whole party healthy and pairs
/// especially well with group healing spells like Wave of Incandescence.
/// </summary>
public class SacredGroundTalent : ISpellModifier
{
    const float ManaPerTarget = 2f;

    public ModifierPriority Priority => ModifierPriority.BASE;

    public void OnBeforeCast(SpellContext ctx) { }

    public void OnCalculate(SpellContext ctx) { }

    public void OnAfterCast(SpellContext ctx)
    {
        if (!ctx.Tags.HasFlag(SpellTags.Healing)) return;
        ctx.Caster.RestoreMana(ManaPerTarget * ctx.Targets.Count);
    }
}
