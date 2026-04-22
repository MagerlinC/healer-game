using healerfantasy.SpellSystem;

namespace healerfantasy.SpellResources;

/// <summary>
/// The Templar's basic melee attack.
/// Deals instant physical damage to the boss.
/// </summary>
[Godot.GlobalClass]
public partial class TemplarShieldBashSpell : SpellResource
{
    public float DamageAmount = 20f;

    public TemplarShieldBashSpell()
    {
        Name        = "Shield Bash";
        Description = "The Templar crashes their shield into the enemy with full force.";
        Tags        = SpellTags.Damage;
        ManaCost    = 0f;
        CastTime    = 0f;
        School      = SpellSchool.Generic;
        EffectType  = EffectType.Harmful;
    }

    public override float GetBaseValue() => DamageAmount;

    public override void Apply(SpellContext ctx)
    {
        foreach (var target in ctx.Targets)
            target.TakeDamage(ctx.FinalValue);
    }
}
