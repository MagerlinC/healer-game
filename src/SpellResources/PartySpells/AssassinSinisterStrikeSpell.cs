using healerfantasy.SpellSystem;

namespace healerfantasy.SpellResources;

/// <summary>
/// The Assassin's basic melee attack — fast and frequent.
/// Deals instant physical damage to the boss.
/// </summary>
[Godot.GlobalClass]
public partial class AssassinSinisterStrikeSpell : SpellResource
{
    public float DamageAmount = 12f;

    public AssassinSinisterStrikeSpell()
    {
        Name        = "Sinister Strike";
        Description = "A quick, precise strike from the Assassin's blade.";
        Tags        = SpellTags.Damage | SpellTags.Attack;
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
