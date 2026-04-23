using Godot;
using healerfantasy.Effects;
using healerfantasy.SpellSystem;

namespace healerfantasy.Talents.Holy;

/// <summary>
/// Healing spells infuse targets with divine energy, granting them a short
/// buff that increases the damage they deal.
///
/// Flavour: your heals don't just restore health — they fire up your allies,
/// making every heal a mini-buff to their offensive output.
/// Synergises especially well with group healing spells that hit multiple targets.
/// </summary>
public class RadiantInfusionTalent : ISpellModifier
{
    const float DamageBonus = 0.15f;
    const float BuffDuration = 6f;

    /// <summary>Icon passed from <see cref="TalentRegistry"/> so the buff indicator
    /// shows the talent's own icon rather than the triggering spell's icon.</summary>
    public Texture2D EffectIcon { get; set; }

    public ModifierPriority Priority => ModifierPriority.BASE;

    public void OnBeforeCast(SpellContext ctx) { }

    public void OnCalculate(SpellContext ctx) { }

    public void OnAfterCast(SpellContext ctx)
    {
        if (!ctx.Tags.HasFlag(SpellTags.Healing)) return;

        foreach (var target in ctx.Targets)
        {
            if (!target.IsAlive) continue;
            target.ApplyEffect(new RadiantInfusionEffect(BuffDuration, DamageBonus)
            {
                Icon = EffectIcon
            });
        }
    }
}
