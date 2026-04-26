using System.Collections.Generic;
using Godot;
using healerfantasy.Effects;
using healerfantasy.SpellSystem;

namespace healerfantasy.SpellResources.Chronomancy;

/// <summary>
/// Weaves a temporal barrier around the entire party, surrounding each member
/// with a brief time-locked shield that absorbs incoming damage before it can
/// reach their health.
///
/// Unlike Divine Aegis (single-target Holy shield), Temporal Ward applies to
/// everyone at once — invaluable before a boss's wide-area attack that would
/// otherwise force frantic single-target healing.
/// </summary>
[GlobalClass]
public partial class TemporalWardSpell : SpellResource
{
    [Export] public float ShieldAmount = 20f;
    [Export] public float ShieldDuration = 5f;

    public TemporalWardSpell()
    {
        Name = "Temporal Ward";
        Description =
            $"Surround all party members in a time-locked barrier, absorbing up to {ShieldAmount} damage each for {ShieldDuration}s.";
        ManaCost = 18f;
        CastTime = 1.5f;
        Cooldown = 15f;
        School = SpellSchool.Chronomancy;
        Tags = SpellTags.Healing | SpellTags.Duration | SpellTags.GroupSpell;
        RequiredSchoolPoints = 2;
        EffectType = EffectType.Helpful;
        Icon = GD.Load<Texture2D>(AssetConstants.SpellIconAssets + "healer/healer3.png");
    }

    public override float GetBaseValue() => ShieldAmount;

    public override List<Character> ResolveTargets(Character caster, Character explicitTarget)
    {
        var targets = new List<Character>();
        foreach (var node in caster.GetTree().GetNodesInGroup("party"))
            if (node is Character { IsAlive: true } c)
                targets.Add(c);
        return targets;
    }

    public override void Apply(SpellContext ctx)
    {
        foreach (var target in ctx.Targets)
        {
            target.ApplyEffect(new ShieldEffect(ctx.FinalValue, ShieldDuration)
            {
                EffectId = "TemporalWard",
                Icon = ctx.Spell.Icon,
                School = School,
                SourceCharacterName = ctx.Caster.CharacterName,
                AbilityName = Name,
                Description = Description
            });
        }
    }
}
