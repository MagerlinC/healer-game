using Godot;
using healerfantasy.SpellSystem;

namespace healerfantasy.SpellResources;

/// <summary>
/// Blood Knight's Blood Drain — a telegraphed life-steal attack.
///
/// Deals damage to a party member and heals the Blood Knight for an equal amount.
/// This spell is only invoked after the parry window elapses without a Deflect —
/// the <see cref="BloodKnight"/> boss is responsible for the wind-up and parry check.
///
/// <see cref="Parryable"/> is set to true so UI and boss logic can identify
/// this spell as a deflectable cast.
/// </summary>
[GlobalClass]
public partial class BossBloodDrainSpell : SpellResource
{
	public float DamageAmount = 65f;

	/// <summary>Reference to the Blood Knight, set by the boss before casting.</summary>
	public Character Boss { get; set; }

	public BossBloodDrainSpell()
	{
		Name = "Blood Drain";
		Description =
			"The Blood Knight drains the life-force from a victim, healing himself for the damage dealt. Deflect to interrupt.";
		Tags = SpellTags.Damage | SpellTags.Duration;
		ManaCost = 0f;
		CastTime = 0f;
		Parryable = true;
		Icon = GD.Load<Texture2D>(AssetConstants.SpellIconAssets + "enemy/blood-knight/blood-drain.png");
		EffectType = EffectType.Harmful;
	}

	public override float GetBaseValue()
	{
		return DamageAmount;
	}

	public override void Apply(SpellContext ctx)
	{
		foreach (var target in ctx.Targets)
		{
			target.TakeDamage(ctx.FinalValue);

			// Siphon the same amount back as healing on the Blood Knight.
			if (Boss == null || !IsInstanceValid(Boss) || !Boss.IsAlive) continue;
			Boss.Heal(ctx.FinalValue);
			Boss.RaiseFloatingCombatText(ctx.FinalValue, true, (int)SpellSchool.Generic, false);
		}
	}
}