using Godot;
using healerfantasy.Effects;
using healerfantasy.SpellSystem;

namespace healerfantasy.SpellResources;

/// <summary>
/// Mecha Golem's debuff — Magnetic Pulse.
/// Fires a magnetic field disruption at a random party member,
/// warping their armour and causing them to take 20% more damage
/// from all sources for 10 seconds. Dispellable.
/// </summary>
[GlobalClass]
public partial class BossCrushedSpell : SpellResource
{
	public const float DebuffDuration = 10f;
	public const float DamageAmplification = 0.20f;

	public BossCrushedSpell()
	{
		Name = "Crushed";
		Description = "Crushes the target's armor, causing them to take 20% more damage for 10 seconds.";
		Tags = SpellTags.Damage | SpellTags.Duration;
		ManaCost = 0f;
		CastTime = 0f;
		EffectType = EffectType.Harmful;
		Icon = GD.Load<Texture2D>(AssetConstants.SpellIconAssets + "enemy/mecha-golem/crushed.png");
	}

	public override void Apply(SpellContext ctx)
	{
		foreach (var target in ctx.Targets)
		{
			target.ApplyEffect(new VulnerableEffect(DebuffDuration, DamageAmplification)
			{
				EffectId = "CrushEffect",
				AbilityName = Name,
				Description = Description,
				SourceCharacterName = ctx.Caster?.CharacterName,
				Icon = Icon,
				IsDispellable = true
			});
		}
	}
}