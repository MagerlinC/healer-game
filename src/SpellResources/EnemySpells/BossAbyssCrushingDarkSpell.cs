using System.Collections.Generic;
using Godot;
using healerfantasy.Effects;
using healerfantasy.LastLight.Hud;
using healerfantasy.SpellSystem;

namespace healerfantasy.SpellResources;

/// <summary>
/// The Infinite Abyss's melee — Crushing Dark. A great tendril slams down on the tank
/// and the dark stays pressing on them: each hit adds a stack of
/// <see cref="CrushingDarkEffect"/> (more damage taken per stack, dispellable).
/// </summary>
[GlobalClass]
public partial class BossAbyssCrushingDarkSpell : SpellResource
{
	public float DamageAmount = 38f;
	public float DebuffDuration = 15f;

	public BossAbyssCrushingDarkSpell()
	{
		Name = "Crushing Dark";
		Description = "A great tendril slams down on the frontline, and the dark keeps pressing: " +
		              "each hit makes the target take more damage. Dispel it before it stacks too high.";
		Tags = SpellTags.Damage | SpellTags.Attack;
		School = SpellSchool.Umbra;
		ManaCost = 0f;
		CastTime = 0f;
		TargetingType = TargetingType.Enemy;
		Icon = LastLightIcons.ForAbyss("crushing-dark");
	}

	public override float GetBaseValue() => DamageAmount;

	public override void Apply(SpellContext ctx)
	{
		foreach (var target in ctx.Targets)
		{
			target.TakeDamage(ctx.FinalValue);
			target.ApplyEffect(new CrushingDarkEffect(DebuffDuration)
			{
				SourceCharacterName = GameConstants.AbyssBossName
			});
		}
	}
}
