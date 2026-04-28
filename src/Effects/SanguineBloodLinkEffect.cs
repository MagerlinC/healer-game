using Godot;
using healerfantasy.CombatLog;
using healerfantasy.SpellResources;

namespace healerfantasy.Effects;

/// <summary>
/// Per-target debuff applied during the Blood Prince's Sanguine Siphon channel.
///
/// Ticks every second, dealing <see cref="LifeLeechPerTick"/> damage to the
/// linked target and restoring the same amount to the Blood Prince. The visual
/// blood-link line between boss and target is owned by the parent
/// <see cref="SanguineSiphonChannelNode"/>.
///
/// Not dispellable — the blood bond is too powerful to be cleansed.
/// Removed automatically when the channel ends (early or natural).
/// </summary>
public partial class SanguineBloodLinkEffect : CharacterEffect
{
    /// <summary>Health drained from this target per second.</summary>
    public float LifeLeechPerTick { get; init; } = 20f;

    /// <summary>The Blood Prince — receives the leeched health on each tick.</summary>
    public Character Boss { get; init; }

    public SanguineBloodLinkEffect(float duration) : base(duration, 1f)
    {
        EffectId = "SanguineBloodLink";
        School = SpellSchool.Void;
        IsDispellable = false;
        IsHarmful = true;
    }

    protected override void OnTick(Character target)
    {
        if (!target.IsAlive) return;

        target.TakeDamage(LifeLeechPerTick);
        target.RaiseFloatingCombatText(LifeLeechPerTick, false, (int)School, false);

        CombatLog.CombatLog.Record(new CombatEventRecord
        {
            Timestamp = Time.GetTicksMsec() / 1000.0,
            SourceName = SourceCharacterName ?? "The Blood Prince",
            TargetName = target.CharacterName,
            AbilityName = AbilityName ?? "Sanguine Siphon",
            Amount = LifeLeechPerTick,
            Description = Description,
            Type = CombatEventType.Damage,
            IsCrit = false
        });

        if (Boss == null || !GodotObject.IsInstanceValid(Boss) || !Boss.IsAlive) return;

        Boss.Heal(LifeLeechPerTick);
        Boss.RaiseFloatingCombatText(LifeLeechPerTick, true, (int)School, false);
    }
}
