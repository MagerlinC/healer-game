using Godot;
using healerfantasy.Effects;
using healerfantasy.SpellResources;
using healerfantasy.SpellSystem;

namespace healerfantasy.Talents.Chronomancy;

public class FutureSightTalent : ISpellModifier
{

	public Texture2D EffectIcon { get; set; }
	readonly float procChance = 0.1f;
	public ModifierPriority Priority => ModifierPriority.BASE;

	public void OnBeforeCast(SpellContext ctx)
	{
	}

	public void OnCalculate(SpellContext ctx)
	{
	}

	public void OnAfterCast(SpellContext ctx)
	{
		var random = GD.Randf();
		if (ctx.Spell.School != SpellSchool.Chronomancy && random < procChance)
		{
			ctx.Caster.ApplyEffect(new FutureSightEffect(10f)
			{
				Icon = EffectIcon
			});
		}
	}
}