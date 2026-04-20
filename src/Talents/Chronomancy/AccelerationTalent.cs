using Godot;
using healerfantasy.Effects;
using healerfantasy.SpellResources;
using healerfantasy.SpellSystem;

namespace healerfantasy.Talents.Chronomancy;

/// <summary>
/// After each spell cast, applies (or stacks) an <see cref="AccelerationEffect"/>
/// on the caster, granting +10% cast speed per stack for a short duration.
/// </summary>
public class AccelerationTalent : ISpellModifier
{
	public Texture2D EffectIcon { get; set; }
	const float BuffDuration = 5f;


	public ModifierPriority Priority { get; } = ModifierPriority.BASE;

	public void OnBeforeCast(SpellContext context)
	{
	}

	public void OnCalculate(SpellContext context)
	{
	}

	public void OnAfterCast(SpellContext context)
	{
		if (context.Spell.School == SpellSchool.Chronomancy)
			context.Caster.ApplyEffect(new AccelerationEffect(BuffDuration)
			{
				Icon = EffectIcon
			});
	}
}