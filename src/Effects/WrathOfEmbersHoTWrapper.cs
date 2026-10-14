using healerfantasy.SpellResources;

namespace healerfantasy.Effects;

/// <summary>
/// Applied by the Wrath of Embers staff in place of any heal-over-time effect
/// cast by the player. Heals identically to the original HoT, but when it
/// expires it converts into a damage-over-time on every living enemy, dealing
/// the same damage per tick as the original HoT healed per tick.
/// </summary>
public partial class WrathOfEmbersHoTWrapper : HealOverTimeEffect
{
	public WrathOfEmbersHoTWrapper(HealOverTimeEffect source)
		: base(source.HealPerTick, source.Remaining, source.TickInterval)
	{
		EffectId = source.EffectId;
		School = source.School;
		Icon = source.Icon;
		SourceCharacterName = source.SourceCharacterName;
		AbilityName = source.AbilityName;
		Description = source.Description;
		HasteMultiplier = source.HasteMultiplier;
		IsUltimateEffect = source.IsUltimateEffect;
	}

	/// <summary>
	/// When this HoT expires naturally, apply a matching DoT to every living
	/// enemy. The DoT lasts as long as the original HoT and deals the same
	/// amount per tick. Does nothing if the host party member died — we only
	/// want the conversion to fire on natural expiry, not on death cleanup.
	/// </summary>
	public override void OnExpired(Character target)
	{
		if (!target.IsAlive) return;

		foreach (var enemy in target.CollectAliveEnemies())
		{
			enemy.ApplyEffect(new DamageOverTimeEffect(
				HealPerTick,
				Duration,
				TickInterval,
				false)
			{
				EffectId = $"WrathOfEmbers: {EffectId}",
				School = SpellSchool.Ember,
				Icon = Icon,
				SourceCharacterName = SourceCharacterName,
				AbilityName = "Wrath of Embers",
				Description = "Wrath of Embers: converted from an expiring heal-over-time effect."
			});
		}
	}
}