using healerfantasy.SpellResources;

namespace healerfantasy.Effects;

public partial class SwarmOfTheForestDamageEffect : DamageOverTimeEffect
{

	Character _caster;
	float _duration;
	float _valuePerTick;
	public SwarmOfTheForestDamageEffect(float duration, float damagePerTick)
		: base(damagePerTick, duration, 1f, false)
	{
		EffectId = "SwarmOfTheForestDamageEffect";
		School = SpellSchool.Nature;
		_duration = duration;
		_valuePerTick = damagePerTick;
	}

	public override void OnExpired(Character target)
	{
		foreach (var character in target.CollectAlivePartyMembers())
		{
			character.ApplyEffect(new SwarmOfTheForestHealingEffect(_duration, _valuePerTick)
			{
				Icon = Icon
			});
		}
	}
}

public partial class SwarmOfTheForestHealingEffect : HealOverTimeEffect
{

	public SwarmOfTheForestHealingEffect(float duration, float healingPerTick)
		: base(healingPerTick, duration)
	{
		EffectId = "SwarmOfTheForestHealingEffect";
		School = SpellSchool.Nature;
		Description = $"Healing for {healingPerTick} per second for {duration}s.";
	}
}