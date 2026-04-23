using Godot;
using healerfantasy.Effects;
using healerfantasy.SpellSystem;

namespace healerfantasy.SpellResources;

/// <summary>
/// A nourishing heal that draws on nature's existing growth energy.
/// Heals the target for <see cref="HealAmount"/> HP normally, but if the
/// target already has a heal-over-time effect active, the heal is amplified
/// by <see cref="HotBonusMultiplier"/> — nature's roots run deeper when
/// the ground is already fertile.
///
/// Pairs naturally with Renewing Bloom: apply the bloom first, then follow
/// up with Nourish to burst-heal a target for significantly more than the
/// base value alone.
/// </summary>
[GlobalClass]
public partial class NourishSpell : SpellResource
{
    [Export] public float HealAmount = 35f;

    /// <summary>
    /// Multiplicative bonus applied when a HoT is active on the target.
    /// 1.30 = 30% more healing.
    /// </summary>
    [Export] public float HotBonusMultiplier = 1.30f;

    public NourishSpell()
    {
        Name = "Nourish";
        Description =
            $"Heals the target for {HealAmount} HP. Heals for 30% more if the target has a heal-over-time effect active.";
        ManaCost = 10f;
        CastTime = 2.0f;
        Cooldown = 0f;
        School = SpellSchool.Nature;
        Tags = SpellTags.Healing | SpellTags.Nature;
        RequiredSchoolPoints = 1;
        EffectType = EffectType.Helpful;
        Icon = GD.Load<Texture2D>(AssetConstants.SpellIconAssets + "nature/swarm-of-locusts.png");
    }

    public override float GetBaseValue() => HealAmount;

    public override void Apply(SpellContext ctx)
    {
        if (ctx.Target == null) return;

        // Check for any active heal-over-time effect (HealOverTimeEffect default EffectId).
        var hasHot = ctx.Target.GetEffectById(nameof(HealOverTimeEffect)) != null;
        var finalHeal = hasHot ? ctx.FinalValue * HotBonusMultiplier : ctx.FinalValue;

        ctx.Target.Heal(finalHeal);

        // Emit extra FCT if the bonus triggered, since the pipeline will already
        // have emitted FCT for ctx.FinalValue — show the bonus portion separately.
        if (hasHot)
        {
            var bonusAmount = finalHeal - ctx.FinalValue;
            var isCrit = ctx.Tags.HasFlag(SpellTags.Critical);
            ctx.Target.RaiseFloatingCombatText(bonusAmount, true, (int)School, isCrit);
        }
    }
}
