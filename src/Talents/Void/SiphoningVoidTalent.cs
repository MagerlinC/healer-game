using healerfantasy.SpellSystem;

namespace healerfantasy.Talents.Void;

/// <summary>
/// Each time a Void damage spell lands, the caster siphons 1 mana from the
/// void. Provides passive mana sustain for Void-heavy builds and rewards
/// keeping DoTs active on the enemy.
/// </summary>
public class SiphoningVoidTalent : ISpellModifier
{
    const float ManaPerCast = 1f;

    public ModifierPriority Priority => ModifierPriority.BASE;

    public void OnBeforeCast(SpellContext ctx) { }

    public void OnCalculate(SpellContext ctx) { }

    public void OnAfterCast(SpellContext ctx)
    {
        if (!ctx.Tags.HasFlag(SpellTags.Void | SpellTags.Damage)) return;
        ctx.Caster.RestoreMana(ManaPerCast);
    }
}
