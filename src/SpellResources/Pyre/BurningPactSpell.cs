using System.Collections.Generic;
using Godot;
using healerfantasy.SpellSystem;

namespace healerfantasy.SpellResources;

/// <summary>
/// Forges a desperate pact with the dark — sacrificing a portion of the caster's
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
public partial class BurningPactSpell : SpellResource
{
	[Export] public float HpCost = 20f;
	[Export] public float ManaGained = 30f;

	public BurningPactSpell()
	{
		Name = "Burning Pact";
		Description =
			$"Burn {HpCost} of your own light to restore {ManaGained} kindling instantly.";
		ManaCost = 0f;
		HealthCost = HpCost;
		CastTime = 0.0f;
		Cooldown = 14f;
		School = SpellSchool.Pyre;
		RequiredSchoolPoints = 1;
		TargetingType = TargetingType.Self;
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
		// Receive the mana gain.
		ctx.Caster.RestoreMana(ManaGained);
	}
}