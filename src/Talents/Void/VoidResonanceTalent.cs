using healerfantasy.Effects;
using healerfantasy.SpellSystem;

namespace healerfantasy.Talents.Void;

/// <summary>
/// Direct void damage spells resonate with active damage-over-time effects,
/// dealing more damage when the target is already afflicted by a DoT.
///
/// Creates a powerful DoT → burst combo: apply Decay first, then follow
/// up with Shadow Bolt to benefit from the resonance bonus.
/// Only applies to instant-damage (non-Duration) void spells.
/// </summary>
public class VoidResonanceTalent : ISpellModifier
{
    const float BonusMultiplier = 1.15f;

    public ModifierPriority Priority => ModifierPriority.BASE;

    public void OnBeforeCast(SpellContext ctx) { }

    public void OnCalculate(SpellContext ctx)
    {
        // Only instant-damage Void spells (no Duration tag — excludes Decay itself).
        if (!ctx.Tags.HasFlag(SpellTags.Void)) return;
        if (!ctx.Tags.HasFlag(SpellTags.Damage)) return;
        if (ctx.Tags.HasFlag(SpellTags.Duration)) return;

        // Check if the target has any active DoT (any DamageOverTimeEffect or NecroticTouch).
        var target = ctx.Target;
        if (target == null) return;

        var hasDot = target.GetEffectById(nameof(DamageOverTimeEffect)) != null
                  || target.GetEffectById("NecroticTouch") != null;

        if (hasDot)
            ctx.FinalValue *= BonusMultiplier;
    }

    public void OnAfterCast(SpellContext ctx) { }
}
