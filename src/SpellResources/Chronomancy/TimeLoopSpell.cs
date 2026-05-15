using Godot;
using healerfantasy;
using healerfantasy.SpellSystem;

namespace healerfantasy.SpellResources.Chronomancy;

/// <summary>
/// Places a temporal loop on a single ally. After the duration elapses, the
/// loop resolves and instantly heals them for a burst of health — effectively
/// a time-delayed safety net.
///
/// Useful for pre-empting incoming damage: cast it on a tank before a big hit
/// and the heal fires automatically at the right moment.
/// </summary>
[GlobalClass]
public partial class TimeLoopSpell : SpellResource
{
	[Export] public float DamageFraction = 0.50f;
	[Export] public float Delay = 4f;

	public TimeLoopSpell()
	{
		Name = "Time Loop";
		Description =
			$"Traps an enemy in a temporal loop, tracking all damage dealt to them. After {Delay}s, the loop resolves, exploding for {100 * DamageFraction:F0}% of the total damage dealt within that time.";
		ManaCost = 8f;
		CastTime = 0.0f;
		Cooldown = 8f;
		School = SpellSchool.Chronomancy;
		Tags = SpellTags.Damage | SpellTags.Duration;
		RequiredSchoolPoints = 1;
		TargetingType = TargetingType.Enemy;
		Icon = GD.Load<Texture2D>(AssetConstants.SpellIconAssets + "chronomancy/time-loop.png");
	}

	public override float GetBaseValue()
	{
		return DamageFraction;
	}

	public override void Apply(SpellContext ctx)
	{
		ctx.Target?.ApplyEffect(new Effects.TimeLoopEffect(Delay, ctx.FinalValue)
		{
			Icon = Icon,
			SourceCharacterName = ctx.Caster.CharacterName,
			AbilityName = Name,
			Description = Description
		});
	}
}