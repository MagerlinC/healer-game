using System.Collections.Generic;
using Godot;
using healerfantasy.SpellResources.Generic;
using healerfantasy.SpellSystem;

namespace healerfantasy.SpellResources;

/// <summary>
/// Flying Demon's telegraphed AoE — Infernal Eruption.
/// After a 3-second wind-up the demon exhales a torrent of fel flames,
/// scorching the entire party for heavy damage — unless deflected in time.
///
/// The parry check is resolved by <see cref="FlyingDemon"/> before this
/// spell is ever cast; if deflected, the cast is skipped entirely.
/// </summary>
[GlobalClass]
public partial class BossInfernalEruptionSpell : SpellResource
{
	public float DamageAmount = 60f;

	public BossInfernalEruptionSpell()
	{
		Name = "Infernal Eruption";
		Description = "The Flying Demon exhales a torrent of fel flames that scorches the entire party — unless deflected.";
		Tags = SpellTags.Damage | SpellTags.Nature;
		ManaCost = 0f;
		CastTime = 0f;
		Parryable = true;
		EffectType = EffectType.Harmful;
		Icon = GD.Load<Texture2D>(AssetConstants.SpellIconAssets + "enemy/flying-demon/infernal-eruption.png");
	}

	public override float GetBaseValue()
	{
		return DamageAmount;
	}

	public override List<Character> ResolveTargets(Character caster, Character explicitTarget)
	{
		var targets = new List<Character>();
		foreach (var node in caster.GetTree().GetNodesInGroup("party"))
			if (node is Character c && c.IsAlive)
				targets.Add(c);
		return targets;
	}

	public override void Apply(SpellContext ctx)
	{
		DeflectSpell.PlayDeflectFailedSound(ctx.Caster);
		foreach (var target in ctx.Targets)
			target.TakeDamage(ctx.FinalValue);
	}
}