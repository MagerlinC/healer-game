using System.Collections.Generic;
using Godot;
using healerfantasy.SpellSystem;

namespace healerfantasy.SpellResources;

/// <summary>
/// Crystal Knight's Structural Crush — a telegraphed AoE attack that hits the
/// entire party for 35 damage.
///
/// The parry check is resolved upstream by <see cref="CrystalKnight"/> via
/// <see cref="ParryWindowManager.ConsumeResult"/>: if the player deflected in
/// time, the boss simply skips casting this spell entirely. This spell is only
/// invoked when the hit actually lands.
///
/// <see cref="Parryable"/> is set to true so that UI and boss logic can identify
/// this spell as parryable.
/// </summary>
[GlobalClass]
public partial class BossStructuralCrushSpell : SpellResource
{
	public float DamageAmount = 35f;

	public BossStructuralCrushSpell()
	{
		Name = "Structural Crush";
		Description = "A devastating crystalline shockwave that crushes the entire party — unless deflected in time.";
		Tags = SpellTags.Damage;
		ManaCost = 0f;
		CastTime = 0f;
		Icon = GD.Load<Texture2D>("res://assets/spell-icons/enemy/crystal-knight/structural-crush.png");
		Parryable = true;
		EffectType = EffectType.Harmful;
	}

	public override float GetBaseValue()
	{
		return DamageAmount;
	}

	/// <summary>Targets every alive party member.</summary>
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
		foreach (var target in ctx.Targets)
			target.TakeDamage(ctx.FinalValue);
	}
}