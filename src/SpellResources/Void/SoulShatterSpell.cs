using Godot;
using healerfantasy.Effects;
using healerfantasy.SpellSystem;

namespace healerfantasy.SpellResources;

/// <summary>
/// Fires a concentrated bolt of void energy that shatters the target's defences,
/// dealing immediate damage and leaving them Vulnerable — a debuff that causes
/// them to take 15% more damage from ALL sources for its duration.
///
/// The debuff amplifies every subsequent hit from the entire party, making this
/// a powerful opener when paired with companions' sustained attacks. Dispellable.
/// </summary>
[GlobalClass]
public partial class SoulShatterSpell : SpellResource
{
    [Export] public float DamageAmount = 30f;
    [Export] public float VulnerableDuration = 8f;
    [Export] public float VulnerableAmplification = 0.15f;

    public SoulShatterSpell()
    {
        Name = "Soul Shatter";
        Description =
            $"Deal {DamageAmount} void damage and apply Vulnerable for {VulnerableDuration}s, causing the target to take {(int)(VulnerableAmplification * 100)}% more damage from all sources.";
        ManaCost = 9f;
        CastTime = 1.5f;
        Cooldown = 6f;
        School = SpellSchool.Void;
        Tags = SpellTags.Damage | SpellTags.Void;
        RequiredSchoolPoints = 1;
        EffectType = EffectType.Harmful;
        Icon = GD.Load<Texture2D>(AssetConstants.SpellIconAssets + "void/void-burst.png");
    }

    public override float GetBaseValue() => DamageAmount;

    public override void Apply(SpellContext ctx)
    {
        ctx.Target?.TakeDamage(ctx.FinalValue);

        ctx.Target?.ApplyEffect(new VulnerableEffect(VulnerableDuration, VulnerableAmplification)
        {
            Icon = ctx.Spell.Icon,
            School = School,
            SourceCharacterName = ctx.Caster.CharacterName,
            AbilityName = Name
        });
    }
}
