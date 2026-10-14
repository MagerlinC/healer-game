using healerfantasy.SpellResources;

namespace healerfantasy.Effects;

public partial class MothswarmDamageEffect : DamageOverTimeEffect
{

	Character _caster;
	float _duration;
	float _valuePerTick;
	public MothswarmDamageEffect(float duration, float damagePerTick)
		: base(damagePerTick, duration, 1f, false)
	{
		EffectId = "MothswarmDamageEffect";
		School = SpellSchool.Ember;
		_duration = duration;
		_valuePerTick = damagePerTick;
	}

	public override void OnExpired(Character target)
	{
		foreach (var character in target.CollectAlivePartyMembers())
		{
			character.ApplyEffect(new MothswarmHealingEffect(_duration, _valuePerTick)
			{
				Icon = Icon
			});
		}
	}
}

public partial class MothswarmHealingEffect : HealOverTimeEffect
{

	public MothswarmHealingEffect(float duration, float healingPerTick)
		: base(healingPerTick, duration)
	{
		EffectId = "MothswarmHealingEffect";
		School = SpellSchool.Ember;
		Description = $"Healing for {healingPerTick} per second for {duration}s.";
	}
}