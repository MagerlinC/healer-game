using healerfantasy.SpellSystem;

namespace healerfantasy.SpellResources;

/// <summary>
/// Bringer of Death's melee auto-attack.
/// A necrotic cleave that deals instant physical damage to the tank.
/// </summary>
[Godot.GlobalClass]
public partial class BossDeathboltSpell : SpellResource
{
    public float DamageAmount = 25f;

    public BossDeathboltSpell()
    {
        Name        = "Deathbolt";
        Description = "A vicious strike imbued with necrotic energy, aimed at the party's frontline.";
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
