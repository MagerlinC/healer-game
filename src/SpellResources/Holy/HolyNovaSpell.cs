using System.Collections.Generic;
using Godot;
using healerfantasy.SpellSystem;

namespace healerfantasy.SpellResources;

/// <summary>
/// Releases a nova of divine energy that simultaneously heals all party members
/// and scorches the enemy with holy light.
///
/// The healing is processed through the full modifier pipeline (FinalValue).
/// The damage dealt to the boss is a fixed value and is applied directly,
/// bypassing spell modifiers (it cannot crit and is not affected by DamageMultiplier).
/// This makes Holy Nova primarily a support tool with a handy damage contribution.
/// </summary>
[GlobalClass]
public partial class HolyNovaSpell : SpellResource
{
    [Export] public float HealAmount = 14f;
    [Export] public float DamageAmount = 18f;

    public HolyNovaSpell()
    {
        Name = "Holy Nova";
        Description =
            $"Release a nova of holy light, healing all party members for {HealAmount} HP and dealing {DamageAmount} damage to the enemy.";
        ManaCost = 16f;
        CastTime = 1.5f;
        Cooldown = 10f;
        School = SpellSchool.Holy;
        Tags = SpellTags.Healing | SpellTags.Light | SpellTags.GroupSpell;
        RequiredSchoolPoints = 2;
        EffectType = EffectType.Helpful;
        Icon = GD.Load<Texture2D>(AssetConstants.SpellIconAssets + "holy/radiant-pillar.png");
    }

    public override float GetBaseValue() => HealAmount;

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
        // Heal all party members for the pipeline-modified value.
        foreach (var target in ctx.Targets)
            target.Heal(ctx.FinalValue);

        // Deal fixed holy damage to all active bosses.
        var isCrit = ctx.Tags.HasFlag(SpellTags.Critical);
        foreach (var node in ctx.Caster.GetTree().GetNodesInGroup(GameConstants.BossGroupName))
        {
            if (node is not Character { IsAlive: true } boss) continue;
            boss.TakeDamage(DamageAmount);
            boss.RaiseFloatingCombatText(DamageAmount, false, (int)School, isCrit);
        }
    }
}
