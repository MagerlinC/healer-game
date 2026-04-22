using healerfantasy.SpellSystem;

namespace healerfantasy.SpellResources;

/// <summary>
/// Bringer of Death's ranged spell.
/// Tears at the soul of a random party member, dealing void damage.
/// </summary>
[Godot.GlobalClass]
public partial class BossSoulRendSpell : SpellResource
{
    public float DamageAmount = 20f;

    public BossSoulRendSpell()
    {
        Name        = "Soul Rend";
        Description = "The Bringer tears at the target's very soul, dealing void damage.";
        Tags        = SpellTags.Damage | SpellTags.Void;
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
