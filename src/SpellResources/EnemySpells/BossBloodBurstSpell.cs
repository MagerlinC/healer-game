using System.Collections.Generic;
using Godot;
using healerfantasy.SpellSystem;

namespace healerfantasy.SpellResources;

/// <summary>
/// Blood Rune's Blood Burst — a surge of corrupted blood energy that detonates
/// across the entire party. Cast by <see cref="BloodRune"/> adds during the
/// Blood Knight fight.
///
/// The spell resource is used by <see cref="BloodRune"/> to supply the icon and
/// name to its health-frame cast bar. Damage is applied directly via
/// <see cref="Character.TakeDamage"/> rather than through
/// <see cref="SpellPipeline"/>, matching the convention used by other add enemies
/// (e.g. <see cref="VinesEnemy"/>).
/// </summary>
[GlobalClass]
public partial class BossBloodBurstSpell : SpellResource
{
	public float DamageAmount = 60f;

	public BossBloodBurstSpell()
	{
		Name = "Blood Burst";
		Description =
			"The Blood Rune detonates in a surge of corrupted blood energy, damaging the entire party.";
		Tags = SpellTags.Damage;
		ManaCost = 0f;
		CastTime = 0f;
		TargetingType = TargetingType.Enemy;
		Icon = GD.Load<Texture2D>(AssetConstants.SpellIconAssets + "enemy/blood-knight/blood-burst.png");
	}

	public override float GetBaseValue() => DamageAmount;

	public override List<Character> ResolveTargets(Character caster, Character explicitTarget)
	{
		var targets = new List<Character>();
		foreach (var node in caster.GetTree().GetNodesInGroup(GameConstants.PartyGroupName))
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
