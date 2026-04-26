using Godot;
using healerfantasy.Effects;
using healerfantasy.SpellResources;
using healerfantasy.SpellSystem;

namespace healerfantasy.Talents.Void;

/// <summary>
/// Void damage spells occasionally leave necrotic residue in their wake —
/// a short but vicious damage-over-time effect that piles on top of whatever
/// else is already ticking on the target.
///
/// The mini-DoT uses a unique EffectId ("NecroticTouch") so it never
/// collides with Decay or other existing DoTs.
/// </summary>
public class NecroticTouchTalent : ISpellModifier
{
	const float ProcChance = 0.25f;
	const float DamagePerTick = 5f;
	const float DoTDuration = 3f;
	const float TickInterval = 1f;

	public ModifierPriority Priority => ModifierPriority.BASE;

	public void OnBeforeCast(SpellContext ctx)
	{
	}

	public void OnCalculate(SpellContext ctx)
	{
	}

	public void OnAfterCast(SpellContext ctx)
	{
		if (!ctx.Tags.HasFlag(SpellTags.Void)) return;
		if (!ctx.Tags.HasFlag(SpellTags.Damage)) return;
		if (GD.Randf() >= ProcChance) return;

		var dot = new DamageOverTimeEffect(DamagePerTick, DoTDuration, TickInterval)
		{
			EffectId = "NecroticTouch",
			School = SpellSchool.Void,
			Icon = GD.Load<Texture2D>(AssetConstants.TalentIconAssets + "void/necrotic-touch"),
			SourceCharacterName = ctx.Caster.CharacterName,
			AbilityName = "Necrotic Touch"
		};

		ctx.Target?.ApplyEffect(dot);
	}
}