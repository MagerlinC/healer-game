using Godot;
using healerfantasy.Effects;
using healerfantasy.SpellSystem;

namespace healerfantasy.SpellResources;

/// <summary>
/// Flying Demon's damage-over-time — Fel Burn.
/// Coats a party member in corrosive demonic fire that burns for
/// 15 damage per second over 8 seconds. Dispellable.
/// </summary>
[GlobalClass]
public partial class BossFelBurnSpell : SpellResource
{
	const float DamagePerSecond = 15f;
	const float Duration = 8f;

	public BossFelBurnSpell()
	{
		Name = "Fel Burn";
		Description = "Coats the target in corrosive demonic fire, burning for 15 damage per second until cleansed.";
		Tags = SpellTags.Damage | SpellTags.Nature | SpellTags.Duration;
		ManaCost = 0f;
		CastTime = 0f;
		EffectType = EffectType.Harmful;
		Icon = GD.Load<Texture2D>(AssetConstants.SpellIconAssets + "enemy/flying-demon/fel-burn.png");
	}

	public override void Apply(SpellContext ctx)
	{
		foreach (var target in ctx.Targets)
		{
			target.ApplyEffect(new DamageOverTimeEffect(DamagePerSecond, Duration)
			{
				EffectId = "FelBurn",
				AbilityName = Name,
				Description = Description,
				SourceCharacterName = ctx.Caster?.CharacterName,
				School = SpellSchool.Nature,
				Icon = Icon,
				IsDispellable = true
			});
		}
	}
}