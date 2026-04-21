using healerfantasy.SpellSystem;

namespace healerfantasy.Talents.Nature;

/// <summary>
/// After any healing spell lands, Nature's energy spreads — instantly healing
/// the most-injured party member not already targeted by the spell for a small
/// amount. Creates passive spread-healing and rewards keeping people topped off.
/// </summary>
public class FlourishingTalent : ISpellModifier
{
    const float BonusHeal = 6f;

    public ModifierPriority Priority => ModifierPriority.BASE;

    public void OnBeforeCast(SpellContext ctx) { }

    public void OnCalculate(SpellContext ctx) { }

    public void OnAfterCast(SpellContext ctx)
    {
        if (!ctx.Tags.HasFlag(SpellTags.Healing)) return;

        // Find the most-injured living party member not already targeted.
        Character lowestHpTarget = null;
        var lowestFraction = float.MaxValue;

        foreach (var node in ctx.Caster.GetTree().GetNodesInGroup("party"))
        {
            if (node is not Character { IsAlive: true } c) continue;
            if (ctx.Targets.Contains(c)) continue;

            var fraction = c.CurrentHealth / c.MaxHealth;
            if (fraction < lowestFraction)
            {
                lowestFraction = fraction;
                lowestHpTarget = c;
            }
        }

        lowestHpTarget?.Heal(BonusHeal);
    }
}
