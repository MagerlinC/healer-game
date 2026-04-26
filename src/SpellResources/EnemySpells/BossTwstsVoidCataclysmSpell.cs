using System.Collections.Generic;
using Godot;
using healerfantasy.SpellResources.Generic;
using healerfantasy.SpellSystem;

namespace healerfantasy.SpellResources;

/// <summary>
/// That Which Swallowed the Stars' signature ability — Void Cataclysm.
///
/// Fires a single detonation hit. This spell is cast THREE times in sequence
/// (with 1.5-second parry windows between each), managed by
/// <see cref="ThatWhichSwallowedTheStars"/>.  Each individual hit is deflectable
/// independently — the existing DeflectOverlay opens for each of the three waves.
/// </summary>
[GlobalClass]
public partial class BossTwstsVoidCataclysmSpell : SpellResource
{
	public float DamageAmount = 65f;

	public BossTwstsVoidCataclysmSpell()
	{
		Name = "Void Cataclysm";
		Description =
			"A cataclysmic eruption of void energy — three detonations strike the entire party in rapid succession. Each can be deflected independently.";
		Tags = SpellTags.Damage | SpellTags.Void;
		ManaCost = 0f;
		CastTime = 0f;
		Parryable = true;
		Icon = GD.Load<Texture2D>(AssetConstants.SpellIconAssets + "enemy/that-which-swallowed-the-stars/void-cataclysm.png");
		EffectType = EffectType.Harmful;
	}

	public override float GetBaseValue()
	{
		return DamageAmount;
	}

	/// <summary>Targets the entire living party.</summary>
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