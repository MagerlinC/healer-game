using healerfantasy.SpellSystem;

namespace healerfantasy.SpellResources;

/// <summary>
/// The Wizard's basic damage spell — slower but hits harder than the melee attacks.
/// Deals instant magic damage to the boss.
/// </summary>
[Godot.GlobalClass]
public partial class WizardArcaneBlastSpell : SpellResource
{
    public float DamageAmount = 28f;

    public WizardArcaneBlastSpell()
    {
        Name        = "Arcane Blast";
        Description = "A focused bolt of arcane energy hurled at the enemy.";
        Tags        = SpellTags.Damage;
        ManaCost    = 0f;
        CastTime    = 0f;
        School      = SpellSchool.Void;
        EffectType  = EffectType.Harmful;
    }

    public override float GetBaseValue() => DamageAmount;

    public override void Apply(SpellContext ctx)
    {
        foreach (var target in ctx.Targets)
            target.TakeDamage(ctx.FinalValue);
    }
}
