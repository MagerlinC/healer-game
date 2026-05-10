using Godot;
using healerfantasy.SpellResources.Void;
using healerfantasy.SpellSystem;

namespace healerfantasy.SpellResources.Chronomancy;

public class MirrorImage : UltimateSpellResource
{

	static float damageIncrease = 0.20f;
	static float duration = 10f;

	public MirrorImage()
	{
		Name = "Mirror Image";
		Description =
			$"Reach into a parallel timeline and pull a mirror image of yourself into this one. Each time you cast a spell while Mirror Image is active, your mirror image mimics that spell, dealing 50% of the damage or healing to a random target within range. Lasts {duration}s";
		ManaCost = 0f;
		CastTime = 0.0f;
		Cooldown = 20f;
		School = SpellSchool.Chronomancy;
		RequiredSchoolPoints = 3;
		EffectType = EffectType.Helpful;
		Icon = GD.Load<Texture2D>(AssetConstants.SpellIconAssets + "chronomancy/mirror-image.png");
	}

	// TODO: implement. Consider that two instances of the same spell might cause ID clashes on effects, mirror clone should maybe prefix IDs?
	public override void Apply(SpellContext ctx)
	{
	}

	// TODO: Allow casting after having a total of 12 seconds of chronomancy buffs active across all targets
	public override bool CanCast(SpellContext ctx)
	{
	}
}