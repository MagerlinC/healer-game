using healerfantasy.SpellResources;

namespace healerfantasy.Effects;

public partial class VoidDrainEffect : DamageOverTimeEffect
{
	/// <summary>Fraction of each tick's damage returned as healing to the caster.</summary>
	public float HealFraction { get; }

	/// <summary>
	/// The character who cast Void Drain. Set from <see cref="VoidDrainSpell.Apply"/>
	/// so that each tick can siphon healing back to them.
	/// </summary>
	public Character Caster { get; set; }

	public VoidDrainEffect(float damagePerTick, float duration, float healFraction) : base(damagePerTick, duration, 1f)
	{
		EffectId = "VoidDrain";
		School = SpellSchool.Void;
		HealFraction = healFraction;
	}

	protected override void OnTick(Character target)
	{
		base.OnTick(target);

		if (Caster == null) return;

		// Siphon a fraction of each tick's damage back to the caster.
		var healAmount = DamagePerTick * HealFraction;
		Caster.Heal(healAmount);

		// The base DoT pipeline emits FCT only on the enemy target.
		// Emit a separate heal float so the caster sees their own gain.
		Caster.RaiseFloatingCombatText(healAmount, true, (int)School, false);
	}
}