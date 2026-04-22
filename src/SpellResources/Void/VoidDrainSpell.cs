using Godot;
using healerfantasy;
using healerfantasy.SpellSystem;

namespace healerfantasy.SpellResources;

/// <summary>
/// Siphons void energy through an enemy, dealing immediate damage and
/// converting a portion of it into healing for the caster.
/// Combines offensive and defensive value in a single instant cast.
/// </summary>
[GlobalClass]
public partial class VoidDrainSpell : SpellResource
{
	[Export] public float DamageAmount = 20f;

	/// <summary>Fraction of damage dealt that is returned as healing to the caster.</summary>
	[Export] public float HealFraction = 0.50f;

	public VoidDrainSpell()
	{
		Name = "Void Drain";
		Description =
			$"Drains void energy from the target, dealing {DamageAmount} void damage and healing yourself for {(int)(DamageAmount * 0.50f)} HP.";
		ManaCost = 10f;
		CastTime = 0.0f;
		Cooldown = 4f;
		School = SpellSchool.Void;
		Tags = SpellTags.Damage | SpellTags.Healing | SpellTags.Void;
		EffectType = EffectType.Harmful;
		Icon = GD.Load<Texture2D>(AssetConstants.SpellIconAssets + "void/void-drain.png");
	}

	public override float GetBaseValue()
	{
		return DamageAmount;
	}

	public override void Apply(SpellContext ctx)
	{
		ctx.Target?.TakeDamage(ctx.FinalValue);

		var healAmount = ctx.FinalValue * HealFraction;
		ctx.Caster.Heal(healAmount);

		// The pipeline only emits FCT for ctx.Targets (the enemy).
		// Emit the self-heal float here so the caster sees their own gain.
		var isCrit = ctx.Tags.HasFlag(SpellTags.Critical);
		ctx.Caster.RaiseFloatingCombatText(healAmount, true, (int)School, isCrit);
	}
}