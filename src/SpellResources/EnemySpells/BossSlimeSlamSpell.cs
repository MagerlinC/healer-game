using healerfantasy.SpellSystem;

namespace healerfantasy.SpellResources;

/// <summary>
/// Demon Slime's melee auto-attack.
/// A bone-crunching cleave against the party's tank.
/// </summary>
[Godot.GlobalClass]
public partial class BossSlimeSlamSpell : SpellResource
{
    public float DamageAmount = 22f;

    public BossSlimeSlamSpell()
    {
        Name        = "Slime Slam";
        Description = "The Demon Slime crashes its gelatinous mass into the party's frontline.";
        Tags        = SpellTags.Damage;
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
