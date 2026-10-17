using System.Linq;
using Godot;
using healerfantasy.Effects;
using healerfantasy.SpellSystem;

namespace healerfantasy.SpellResources.Umbra;

public partial class ConsumingDarkSpell : SpellResource
{

	public static readonly string SpellName = "Consuming Dark";
	[Export] public float AddedDamagePerConsumedHoT = 25f;
	[Export] public float BaseDamage = 10f;

	public ConsumingDarkSpell()
	{
		Name = SpellName;
		Description =
			$"Consume all lingering light effects on your party, dealing {BaseDamage:F0} umbral damage to your target, plus an additional {AddedDamagePerConsumedHoT:F0} per effect consumed.";
		ManaCost = 10f;
		CastTime = 1.0f;
		School = SpellSchool.Umbra;
		Tags = SpellTags.Damage;
		RequiredSchoolPoints = 2;
		TargetingType = TargetingType.Enemy;
		Icon = GD.Load<Texture2D>(AssetConstants.SpellIconAssets + "void/consuming-void.png");
	}

	public override float GetBaseValue()
	{
		return BaseDamage;
	}

	/// <summary>
	/// Count all HoT effects currently on the party and fold their bonus into
	/// <see cref="SpellContext.BaseValue"/> so that the full damage (including the
	/// per-HoT bonus) flows through the modifier pipeline, crit roll, and central
	/// combat-log recording correctly.
	/// </summary>
	public override void OnAfterTargetsResolved(SpellContext ctx)
	{
		var party = ctx.Caster.CollectAlivePartyMembers();
		var hotCount = 0;
		foreach (var character in party)
			hotCount += character.GetAllEffects().Count(e => e is HealOverTimeEffect);

		ctx.BaseValue = BaseDamage + hotCount * AddedDamagePerConsumedHoT;
	}

	public override void Apply(SpellContext ctx)
	{
		// Remove all party HoTs (counted in OnAfterTargetsResolved).
		// ctx.FinalValue already includes BaseDamage + per-HoT bonus,
		// scaled by damage multipliers and crit — no manual arithmetic needed.
		var party = ctx.Caster.CollectAlivePartyMembers();
		foreach (var character in party)
		{
			foreach (var hotEffect in character.GetAllEffects().Where(e => e is HealOverTimeEffect).ToList())
				character.RemoveEffect(hotEffect.EffectId);
		}

		ctx.Target?.TakeDamage(ctx.FinalValue);
	}
}