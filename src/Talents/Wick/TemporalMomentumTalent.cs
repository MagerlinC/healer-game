using Godot;
using healerfantasy.Effects;
using healerfantasy.SpellResources;
using healerfantasy.SpellSystem;

namespace healerfantasy.Talents.Wick;

public class TemporalMomentumTalent : ISpellModifier
{
	public Texture2D EffectIcon { get; set; }

	public ModifierPriority Priority => ModifierPriority.BASE;


	public void OnBeforeCast(SpellContext ctx)
	{
	}

	public void OnCalculate(SpellContext ctx)
	{
	}

	public void OnAfterCast(SpellContext ctx)
	{
		if (ctx.Spell.School == SpellSchool.Wick)
		{
			ctx.Caster.ApplyEffect(new TemporalMomentumEffect(10f)
			{
				Icon = EffectIcon
			});
		}
	}
}