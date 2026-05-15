using System.Linq;
using Godot;
using healerfantasy.Effects;
using healerfantasy.SpellSystem;

namespace healerfantasy.SpellResources.Void;

public partial class ConsumingVoid : SpellResource
{

	public static readonly string SpellName = "Consuming Void";
	[Export] public float AddedDamagePerConsumedHoT = 15f;
	[Export] public float BaseDamage = 10f;

	public ConsumingVoid()
	{
		Name = SpellName;
		Description =
			$"Consume all healing-over-time effects on your party, dealing {BaseDamage:F0} void damage to your target, plus and additional {AddedDamagePerConsumedHoT:F0} per effect consumed.";
		ManaCost = 10f;
		CastTime = 1.0f;
		School = SpellSchool.Void;
		Tags = SpellTags.Damage;
		RequiredSchoolPoints = 2;
		EffectType = EffectType.Harmful;
		Icon = GD.Load<Texture2D>(AssetConstants.SpellIconAssets + "void/consuming-void.png");
	}

	public override float GetBaseValue()
	{
		return BaseDamage;
	}

	public override void Apply(SpellContext ctx)
	{
		var party = ctx.Caster.CollectAlivePartyMembers();
		var totalPartyHots = 0;
		foreach (var character in party)
		{
			var hotEffects = character.GetAllEffects().Where(e => e is HealOverTimeEffect);
			foreach (var hotEffect in hotEffects)
			{
				character.RemoveEffect(hotEffect.EffectId);
				totalPartyHots++;
			}
		}

		var totalDamage = ctx.FinalValue + totalPartyHots * AddedDamagePerConsumedHoT;
		ctx.Target?.TakeDamage(totalDamage);
	}
}