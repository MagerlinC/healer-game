using Godot;
using healerfantasy;
using healerfantasy.Effects;
using healerfantasy.SpellSystem;

namespace healerfantasy.SpellResources;

/// <summary>
/// Siphons umbral energy through an enemy, dealing immediate damage and
/// converting a portion of it into healing for the caster.
/// Combines offensive and defensive value in a single instant cast.
/// </summary>
[GlobalClass]
public partial class SiphonTheDarkSpell : SpellResource
{
	[Export] public float DamagePerTick = 10f;
	[Export] public float Duration = 10f;
	[Export] public float TickRate = 1.0f;

	/// <summary>Fraction of damage dealt that is returned as healing to the caster.</summary>
	[Export] public float HealFraction = 0.25f;

	public SiphonTheDarkSpell()
	{
		Name = "Siphon the Dark";
		Description =
			$"Siphons the darkness from the target, dealing {DamagePerTick} umbral damage every {TickRate}s over {Duration}s and healing yourself for {(int)(HealFraction * 100)}% of damage dealt.";
		ManaCost = 10f;
		CastTime = 0.0f;
		Cooldown = 4f;
		School = SpellSchool.Umbra;
		Tags = SpellTags.Damage | SpellTags.Healing | SpellTags.Duration;
		TargetingType = TargetingType.Enemy;
		Icon = GD.Load<Texture2D>(AssetConstants.SpellIconAssets + "void/void-drain.png");
	}

	public override float GetBaseValue()
	{
		return DamagePerTick;
	}

	public override void Apply(SpellContext ctx)
	{
		ctx.Target.ApplyEffect(new DarkDrainEffect(DamagePerTick, Duration + ctx.EffectDurationBonus, HealFraction)
		{
			AbilityName = Name,
			Description = Description,
			SourceCharacterName = ctx.Caster?.CharacterName,
			Caster = ctx.Caster,
			School = SpellSchool.Umbra,
			IsDispellable = false,
			Icon = Icon
		});
	}
}