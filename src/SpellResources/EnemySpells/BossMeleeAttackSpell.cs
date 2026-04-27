using healerfantasy.SpellSystem;

namespace healerfantasy.SpellResources;

/// <summary>
/// Crystal Knight's melee auto-attack.
/// Deals instant physical damage to its explicit target (the tank).
/// Damage amount is configured via <see cref="DamageAmount"/>.
/// </summary>
[Godot.GlobalClass]
public partial class BossMeleeAttackSpell : SpellResource
{
    public float DamageAmount = 20f;

    public BossMeleeAttackSpell()
    {
        Name        = "Crystal Slash";
        Description = "A crushing blow from the Crystal Knight's crystalline gauntlet.";
        Tags        = SpellTags.Damage | SpellTags.Attack;
        ManaCost    = 0f;
        CastTime    = 0f;
    }

    public override float GetBaseValue() => DamageAmount;

    public override void Apply(SpellContext ctx)
    {
        foreach (var target in ctx.Targets)
            target.TakeDamage(ctx.FinalValue);
    }
}
