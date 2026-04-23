using healerfantasy.Effects;
using healerfantasy.SpellSystem;


namespace healerfantasy.Talents.Nature;

/// <summary>
/// Nature's magic flows more efficiently when it finds fertile ground.
/// Casting a heal-over-time spell on a target that already has a HoT active
/// restores mana — the roots of one bloom feeding into the next.
///
/// Encourages proactive HoT management: keep Renewing Bloom rolling on
/// multiple party members and Wild Growth becomes almost free when targets
/// are already blooming.
/// </summary>
public class DeepRootsTalent : ISpellModifier
{
    const float ManaRestore = 4f;

    public ModifierPriority Priority => ModifierPriority.BASE;

    public void OnBeforeCast(SpellContext ctx) { }

    public void OnCalculate(SpellContext ctx) { }

    public void OnAfterCast(SpellContext ctx)
    {
        // Only trigger for Nature HoT spells.
        if (!ctx.Tags.HasFlag(SpellTags.Healing)) return;
        if (!ctx.Tags.HasFlag(SpellTags.Duration)) return;
        if (!ctx.Tags.HasFlag(SpellTags.Nature)) return;

        // Check each target: if they already had a HoT active when this spell
        // was cast (OnAfterCast runs before Apply, so existing effects are intact).
        bool anyHadHot = false;
        foreach (var target in ctx.Targets)
        {
            if (target.GetEffectById(nameof(HealOverTimeEffect)) != null)
            {
                anyHadHot = true;
                break;
            }
        }

        if (anyHadHot)
            ctx.Caster.RestoreMana(ManaRestore);
    }
}
