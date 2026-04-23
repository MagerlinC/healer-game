using System.Collections.Generic;
using Godot;
using healerfantasy.SpellSystem;

namespace healerfantasy.SpellResources;

/// <summary>
/// Forges a desperate pact with the void — sacrificing a portion of the caster's
/// own health to instantly restore a significant amount of mana.
///
/// Always self-targeted: the caster is both the one paying the cost and receiving
/// the benefit. Useful in critical moments when mana runs dry and a major heal
/// is needed immediately.
///
/// Note: the HP cost goes through <see cref="Character.TakeDamage"/> and is
/// subject to active shields and damage-reduction buffs.
/// </summary>
[GlobalClass]
public partial class DarkPactSpell : SpellResource
{
    [Export] public float HpCost = 20f;
    [Export] public float ManaGained = 30f;

    public DarkPactSpell()
    {
        Name = "Dark Pact";
        Description =
            $"Sacrifice {HpCost} HP to restore {ManaGained} mana instantly.";
        ManaCost = 0f;
        CastTime = 0.0f;
        Cooldown = 20f;
        School = SpellSchool.Void;
        Tags = SpellTags.Void;
        RequiredSchoolPoints = 1;
        EffectType = EffectType.Helpful;
        Icon = GD.Load<Texture2D>(AssetConstants.SpellIconAssets + "void/shield-of-atrophy.png");
    }

    /// <summary>
    /// Override to always target the caster, regardless of what party member
    /// the player clicked on when activating the spell.
    /// </summary>
    public override List<Character> ResolveTargets(Character caster, Character explicitTarget)
    {
        return new List<Character> { caster };
    }

    public override void Apply(SpellContext ctx)
    {
        // Pay the HP cost (goes through shields/damage-reduction as normal).
        ctx.Caster.TakeDamage(HpCost);

        // Receive the mana gain.
        ctx.Caster.RestoreMana(ManaGained);
    }
}
