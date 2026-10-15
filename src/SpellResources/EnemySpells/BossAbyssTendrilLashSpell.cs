using System.Collections.Generic;
using Godot;
using healerfantasy.Effects;
using healerfantasy.LastLight.Hud;
using healerfantasy.SpellSystem;

namespace healerfantasy.SpellResources;

/// <summary>An Abyssal Tendril's attack — Lash, at a random party member.</summary>
[GlobalClass]
public partial class BossAbyssTendrilLashSpell : SpellResource
{
	public float DamageAmount = 18f;

	public BossAbyssTendrilLashSpell()
	{
		Name = "Lash";
		Description = "An Abyssal Tendril lashes out at a party member.";
		Tags = SpellTags.Damage | SpellTags.Attack;
		School = SpellSchool.Umbra;
		ManaCost = 0f;
		CastTime = 0f;
		TargetingType = TargetingType.Enemy;
		Icon = LastLightIcons.ForAbyss("lash");
	}

	public override float GetBaseValue() => DamageAmount;

	public override void Apply(SpellContext ctx)
	{
		foreach (var target in ctx.Targets)
			target.TakeDamage(ctx.FinalValue);
	}
}
