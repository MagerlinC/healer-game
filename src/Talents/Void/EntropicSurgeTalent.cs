using Godot;
using healerfantasy.SpellSystem;

namespace healerfantasy.Talents.Void;

/// <summary>
/// Void spells have a 15% chance to trigger an Entropic Surge, doubling their
/// final value on that cast. Adds an exciting high-variance element to Void
/// builds without any guaranteed throughput increase.
/// </summary>
public class EntropicSurgeTalent : ISpellModifier
{
    const float SurgeChance = 0.15f;
    const float SurgeMultiplier = 2.0f;

    public ModifierPriority Priority => ModifierPriority.BASE;

    public void OnBeforeCast(SpellContext ctx) { }

    public void OnCalculate(SpellContext ctx)
    {
        if (!ctx.Tags.HasFlag(SpellTags.Void)) return;
        if (GD.Randf() < SurgeChance)
            ctx.FinalValue *= SurgeMultiplier;
    }

    public void OnAfterCast(SpellContext ctx) { }
}
