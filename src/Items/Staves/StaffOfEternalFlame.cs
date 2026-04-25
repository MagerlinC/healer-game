using Godot;
using healerfantasy.SpellSystem;
using healerfantasy.Talents;

namespace healerfantasy.Items.Staves;

/// <summary>
/// Staff of the Eternal Flame — Legendary staff that can drop from any boss.
/// A relic of the Healer's ancient order, still warm with divine fire that
/// never dims. Its flame rewards mastery: every critical heal channels a
/// spark of mana back to the caster.
///
/// Stat bonus:    +20% healing multiplier.
/// Legendary effect: Critical healing strikes restore 5 mana to the caster.
/// </summary>
public class StaffOfEternalFlame : EquippableItem
{
    public override string ItemId => "staff_of_eternal_flame";

    public StaffOfEternalFlame()
    {
        Name        = "Staff of the Eternal Flame";
        Description = "+20% healing multiplier.\nCritical healing strikes restore 5 mana.";
        Rarity      = ItemRarity.Legendary;
        Slot        = EquipSlot.Staff;
        Icon        = GD.Load<Texture2D>(AssetConstants.StaveIconPath(5));
        CharacterModifiers.Add(new HealingModifier());
        SpellModifiers.Add(new ManaOnCritHealModifier());
    }

    // ── modifiers ─────────────────────────────────────────────────────────────

    class HealingModifier : ICharacterModifier
    {
        public void Modify(CharacterStats stats) => stats.HealingMultiplier *= 1.20f;
    }

    /// <summary>
    /// After each spell cast, checks whether it was a critical healing strike.
    /// If so, restores 5 mana to the caster — the "Eternal Flame" legendary effect.
    /// </summary>
    class ManaOnCritHealModifier : ISpellModifier
    {
        public ModifierPriority Priority => ModifierPriority.ADDITIVE;

        public void OnBeforeCast(SpellContext context) { }
        public void OnCalculate(SpellContext context) { }

        public void OnAfterCast(SpellContext context)
        {
            var isCritHeal = context.Tags.HasFlag(SpellTags.Critical)
                          && context.Tags.HasFlag(SpellTags.Healing);
            if (isCritHeal)
                context.Caster.RestoreMana(5f);
        }
    }
}
