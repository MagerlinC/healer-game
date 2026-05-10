using Godot;
using healerfantasy.SpellResources.Void;
using healerfantasy.SpellSystem;

namespace healerfantasy.SpellResources.Sanguimancy;

public partial class VampiricEmbrace : UltimateSpellResource
{
	float tickingDamagePerSecond = 5f;
	float leechRate = 0.2f;
	float damageMultiplierOnExpire = 5f;

	public VampiricEmbrace()
	{
		Name = "Vampiric Embrace";
		Description =
			$"Embrace the sanguine power of your party, making all allies lose {tickingDamagePerSecond} HP/s but leech {100 * leechRate:F0}% of damage dealt by you as healing to all party members. When this spell ends, all enemies are damaged for {100 * damageMultiplierOnExpire:F0}% of total life leeched.";
		ManaCost = 0f;
		CastTime = 0.0f;
		Cooldown = 20f;
		School = SpellSchool.Sanguimancy;
		RequiredSchoolPoints = 3;
		EffectType = EffectType.Helpful;
		Icon = GD.Load<Texture2D>(AssetConstants.SpellIconAssets + "sanguimancy/vampiric-embrace.png");
	}

	// TODO: Implement
	public override void Apply(SpellContext ctx)
	{
	}

	// TODO: Allow casting after spending a total of 50 health on sanguimancy spells 
	public override bool CanCast(SpellContext ctx)
	{
	}
}