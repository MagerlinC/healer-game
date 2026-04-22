using healerfantasy.SpellSystem;

namespace healerfantasy.SpellResources;

/// <summary>
/// Demon Slime's ranged attack.
/// Spits a glob of burning acid at a random party member.
/// </summary>
[Godot.GlobalClass]
public partial class BossAcidSpitSpell : SpellResource
{
    public float DamageAmount = 25f;

    public BossAcidSpitSpell()
    {
        Name        = "Acid Spit";
        Description = "A glob of corrosive acid hurled at a random party member.";
        Tags        = SpellTags.Damage | SpellTags.Nature;
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
