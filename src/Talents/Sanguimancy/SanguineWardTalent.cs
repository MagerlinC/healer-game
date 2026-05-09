using Godot;
using healerfantasy.Effects;
using healerfantasy.SpellResources;
using healerfantasy.SpellSystem;

namespace healerfantasy.Talents.Sanguimancy;

/// <summary>
/// Every time the caster spends health on a Sanguimancy spell, all living
/// allies receive a shield equal to the health cost for 10 seconds.
///
/// The caster's self-sacrifice wraps the entire party in a ward of blood —
/// pain becomes armour.  The shield is applied during OnAfterCast (before
/// the spell's Apply), so when Exsanguinate fires, allies briefly hold a
/// shield before the drain hits, partially absorbing their own cost.
/// </summary>
public class SanguineWardTalent : ISpellModifier
{
    const float ShieldDuration = 10f;

    public Texture2D EffectIcon { get; set; }
    public ModifierPriority Priority => ModifierPriority.BASE;

    public void OnBeforeCast(SpellContext ctx) { }
    public void OnCalculate(SpellContext ctx) { }

    public void OnAfterCast(SpellContext ctx)
    {
        if (ctx.Spell.School != SpellSchool.Sanguimancy) return;
        if (ctx.Spell.HpCost <= 0f) return;

        foreach (var node in ctx.Caster.GetTree().GetNodesInGroup("party"))
        {
            if (node is not Character { IsAlive: true } ally) continue;

            ally.ApplyEffect(new ShieldEffect("SanguineWard", ctx.Spell.HpCost, ShieldDuration)
            {
                Icon = EffectIcon,
                School = SpellSchool.Sanguimancy,
                SourceCharacterName = ctx.Caster.CharacterName,
                AbilityName = "Sanguine Ward",
                Description = $"Shielded by the caster's blood sacrifice."
            });
        }
    }
}
