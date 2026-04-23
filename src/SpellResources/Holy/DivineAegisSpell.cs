using Godot;
using healerfantasy.Effects;
using healerfantasy.SpellSystem;

namespace healerfantasy.SpellResources;

/// <summary>
/// Wraps a target in a layer of holy light, creating a damage-absorbing shield
/// for a short duration. The shield soaks incoming hits before they can damage
/// health, then fades naturally on expiry.
///
/// An excellent pre-emptive cooldown to cast before a predictable boss attack.
/// </summary>
[GlobalClass]
public partial class DivineAegisSpell : SpellResource
{
    [Export] public float ShieldAmount = 20f;
    [Export] public float ShieldDuration = 10f;

    public DivineAegisSpell()
    {
        Name = "Divine Aegis";
        Description = $"Surround the target in holy light, absorbing up to {ShieldAmount} damage for {ShieldDuration}s.";
        ManaCost = 8f;
        CastTime = 0.0f;
        Cooldown = 8f;
        School = SpellSchool.Holy;
        Tags = SpellTags.Healing;
        EffectType = EffectType.Helpful;
        Icon = GD.Load<Texture2D>(AssetConstants.SpellIconAssets + "holy/aura-of-radiance.png");
    }

    public override float GetBaseValue() => ShieldAmount;

    public override void Apply(SpellContext ctx)
    {
        ctx.Target?.ApplyEffect(new ShieldEffect(ctx.FinalValue, ShieldDuration)
        {
            EffectId = "DivineAegis",
            Icon = ctx.Spell.Icon,
            School = School,
            SourceCharacterName = ctx.Caster.CharacterName,
            AbilityName = Name
        });
    }
}
