using Godot;
using healerfantasy.CombatLog;
using healerfantasy.Effects;
using healerfantasy.SpellSystem;

namespace healerfantasy.SpellResources;

/// <summary>
/// Demon Slime's Corrosive Ooze attack.
/// Coats a random party member in acidic slime, dealing 12 damage per second
/// for 10 seconds.  Can be removed by Dispel.
/// </summary>
[GlobalClass]
public partial class BossCorrosiveOozeSpell : SpellResource
{
	public BossCorrosiveOozeSpell()
	{
		Name = "Corrosive Ooze";
		Description = "Coats the target in acidic slime, burning for 12 damage per second until cleansed.";
		Tags = SpellTags.Damage | SpellTags.Nature | SpellTags.Duration;
		ManaCost = 0f;
		CastTime = 0f;
		Icon = GD.Load<Texture2D>(AssetConstants.SpellIconAssets + "enemy/demon-slime/corrosive-ooze.png");
		EffectType = EffectType.Harmful;
	}

	public override void Apply(SpellContext ctx)
	{
		foreach (var target in ctx.Targets)
		{
			target.ApplyEffect(new CorrosiveOozeEffect
			{
				AbilityName = Name,
				Description = Description,
				SourceCharacterName = ctx.Caster?.CharacterName,
				School = SpellSchool.Nature,
				Icon = Icon
			});
		}
	}
}