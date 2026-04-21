using Godot;
using healerfantasy;
using healerfantasy.SpellSystem;

namespace healerfantasy.SpellResources;

/// <summary>
/// Hurls a bolt of venom at an enemy, applying a nature-based poison that
/// deals damage over time. Nature's offensive counterpart to Renewing Bloom.
/// </summary>
[GlobalClass]
public partial class PoisonBoltSpell : SpellResource
{
    [Export] public float DamagePerTick = 8f;
    [Export] public float EffectDuration = 8f;
    [Export] public float TickInterval = 1f;

    public PoisonBoltSpell()
    {
        Name = "Poison Bolt";
        Description =
            $"Infects the target with virulent poison, dealing {DamagePerTick} nature damage every {TickInterval}s for {EffectDuration}s.";
        ManaCost = 7f;
        CastTime = 0.0f;
        School = SpellSchool.Nature;
        Tags = SpellTags.Damage | SpellTags.Nature | SpellTags.Duration;
        TargetType = TargetType.Enemy;
        Icon = GD.Load<Texture2D>("res://assets/spell-icons/nature/poison-bolt.png");
    }

    public override float GetBaseValue() => DamagePerTick;

    public override void Apply(SpellContext ctx)
    {
        ctx.Target?.ApplyEffect(new Effects.DamageOverTimeEffect(ctx.FinalValue, EffectDuration, TickInterval)
        {
            Icon = Icon,
            SourceCharacterName = ctx.Caster.CharacterName,
            AbilityName = Name
        });
    }
}
