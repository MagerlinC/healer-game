using System.Collections.Generic;
using Godot;
using healerfantasy.CombatLog;
using healerfantasy.SpellResources;

namespace healerfantasy.SpellSystem;

/// <summary>
/// Executes the full modifier pipeline for a spell cast.
///
/// Pipeline order
/// ──────────────
///  1. Build <see cref="SpellContext"/>
///  2. Compute caster's <see cref="CharacterStats"/> (base + ICharacterModifiers)
///  3. Resolve targets via <see cref="SpellResource.ResolveTargets"/>
///  4. Run <see cref="ISpellModifier.OnBeforeCast"/> (sorted by Priority)
///  5. Set FinalValue = BaseValue, apply stat multipliers (Damage/HealingMultiplier)
///  6. Run <see cref="ISpellModifier.OnCalculate"/>
///  7. Crit roll — if it hits, add <see cref="SpellTags.Critical"/> and scale FinalValue
///  8. Run <see cref="ISpellModifier.OnAfterCast"/>
///  9. Execute the spell via <see cref="SpellResource.Apply"/>
/// 10. Record in caster's <see cref="SpellHistory"/>
/// </summary>
public static class SpellPipeline
{
    /// <summary>
    /// Cast a spell from <paramref name="caster"/> at an explicit target.
    /// The spell may resolve additional targets internally (e.g. group heals).
    /// </summary>
    public static void Cast(SpellResource spell, Character caster, Character explicitTarget)
    {
        // ── 1. Build context ────────────────────────────────────────────────
        var ctx = new SpellContext
        {
            Caster    = caster,
            Spell     = spell,
            Tags      = spell.Tags,
            BaseValue = spell.GetBaseValue(),
            Timestamp = Time.GetTicksMsec() / 1000.0,
        };

        // ── 2. Compute character stats ──────────────────────────────────────
        ctx.CasterStats = caster.GetCharacterStats();

        // ── 3. Resolve targets ──────────────────────────────────────────────
        ctx.Targets = spell.ResolveTargets(caster, explicitTarget);

        // ── 4. Collect + sort modifiers ─────────────────────────────────────
        var modifiers = new List<ISpellModifier>(caster.GetSpellModifiers());
        modifiers.Sort((a, b) => a.Priority.CompareTo(b.Priority));

        // ── 5. OnBeforeCast ─────────────────────────────────────────────────
        foreach (var mod in modifiers)
            mod.OnBeforeCast(ctx);

        // ── 6. Apply stat multipliers, then OnCalculate ─────────────────────
        ctx.FinalValue = ctx.BaseValue;

        if (ctx.Tags.HasFlag(SpellTags.Damage))
            ctx.FinalValue *= ctx.CasterStats.DamageMultiplier;
        if (ctx.Tags.HasFlag(SpellTags.Healing))
            ctx.FinalValue *= ctx.CasterStats.HealingMultiplier;

        foreach (var mod in modifiers)
            mod.OnCalculate(ctx);

        // ── 7. Crit roll ────────────────────────────────────────────────────
        if (ctx.CasterStats.CritChance > 0f && GD.Randf() < ctx.CasterStats.CritChance)
        {
            ctx.Tags      |= SpellTags.Critical;
            ctx.FinalValue *= ctx.CasterStats.CritMultiplier;
        }

        // ── 8. OnAfterCast ──────────────────────────────────────────────────
        foreach (var mod in modifiers)
            mod.OnAfterCast(ctx);

        // ── 9. Execute spell ────────────────────────────────────────────────
        spell.Apply(ctx);

        // ── 10. Log to CombatLog (direct hits only; HoT ticks log themselves) ─
        bool isDirect = !ctx.Tags.HasFlag(SpellTags.HealOverTime);
        foreach (var target in ctx.Targets)
        {
            if (ctx.Tags.HasFlag(SpellTags.Healing) && isDirect)
            {
                CombatLog.CombatLog.Record(new CombatEventRecord
                {
                    Timestamp   = ctx.Timestamp,
                    SourceName  = caster.CharacterName,
                    TargetName  = target.CharacterName,
                    AbilityName = spell.Name,
                    Amount      = ctx.FinalValue,
                    Type        = CombatEventType.Healing,
                    IsCrit      = ctx.Tags.HasFlag(SpellTags.Critical),
                });
            }
            else if (ctx.Tags.HasFlag(SpellTags.Damage))
            {
                CombatLog.CombatLog.Record(new CombatEventRecord
                {
                    Timestamp   = ctx.Timestamp,
                    SourceName  = caster.CharacterName,
                    TargetName  = target.CharacterName,
                    AbilityName = spell.Name,
                    Amount      = ctx.FinalValue,
                    Type        = CombatEventType.Damage,
                    IsCrit      = ctx.Tags.HasFlag(SpellTags.Critical),
                });
            }
        }

        // ── 11. Record in history ───────────────────────────────────────────
        caster.SpellHistory.Record(spell.Name, ctx.Timestamp);
    }
}
