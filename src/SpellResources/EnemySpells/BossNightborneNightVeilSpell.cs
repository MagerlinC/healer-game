using Godot;
using healerfantasy.Effects;
using healerfantasy.SpellSystem;

namespace healerfantasy.SpellResources;

/// <summary>
/// The Nightborne's debuff — Night Veil.
/// Shrouds the target in choking darkness, dealing 20 damage per second
/// for 10 seconds. Dispellable.
/// </summary>
[GlobalClass]
public partial class BossNightborneNightVeilSpell : SpellResource
{
	const float DamagePerSecond = 20f;
	const float Duration = 10f;

	public BossNightborneNightVeilSpell()
	{
		Name = "Night Veil";
		Description =
			$"Wraps the target in a suffocating veil of shadow, dealing {DamagePerSecond} damage per second for {Duration} seconds. Dispellable.";
		Tags = SpellTags.Damage | SpellTags.Void | SpellTags.Duration;
		ManaCost = 0f;
		CastTime = 0f;
		Icon = GD.Load<Texture2D>(AssetConstants.SpellIconAssets + "enemy/the-nightborne/night-veil.png");
		EffectType = EffectType.Harmful;
	}

	public override void Apply(SpellContext ctx)
	{
		foreach (var target in ctx.Targets)
		{
			target.ApplyEffect(new DamageOverTimeEffect(DamagePerSecond, Duration, 1f, true)
			{
				EffectId = "NightVeil",
				AbilityName = Name,
				Description = Description,
				SourceCharacterName = ctx.Caster?.CharacterName,
				School = SpellSchool.Void,
				IsHarmful = true
			});
		}
	}
}