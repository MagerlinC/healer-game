using Godot;
using healerfantasy.SpellSystem;

namespace healerfantasy.SpellResources;

/// <summary>
/// The Tallow Queen — Molten Drop.
///
/// 1-second cast (shown on BossCastBar). On resolution a
/// <see cref="MoltenDropProjectile"/> is spawned at the Queen's position
/// and floats slowly toward the healer.
///
/// When the drop reaches the Lightkeeper it bursts, leaving a permanent
/// <see cref="MoltenWaxPool"/> — a pool of hot wax that deals damage
/// over time to anyone standing inside it.
///
/// The mechanic rewards running the drop to the edge of the arena so the
/// resulting pool doesn't claim valuable central space.
/// </summary>
[GlobalClass]
public partial class BossQueenMoltenDropSpell : SpellResource
{
	/// <summary>Damage per second dealt by the explosion zone left behind.</summary>
	public float ZoneDamagePerTick = 15f;

	/// <summary>Pixels per second at which the drop floats toward the healer.</summary>
	public float MoltenDropSpeed = 85f;

	/// <summary>Reference to the Queen — must be set before SpellPipeline.Cast is called.</summary>
	public Character Boss { get; set; }

	public BossQueenMoltenDropSpell()
	{
		Name = "Molten Drop";
		Description = "The Queen lets fall a bead of molten tallow that drifts toward the Lightkeeper. " +
		              "On impact it bursts, leaving a pool of scalding wax that burns away light " +
		              "to anyone standing within it.";
		Icon = GD.Load<Texture2D>(AssetConstants.SpellIconAssets + "enemy/the-tallow-queen/molten-drop.png");
		Tags = SpellTags.Damage;
		ManaCost = 0f;
		CastTime = 1.5f;
		TargetingType = TargetingType.Enemy;
	}

	public override void Apply(SpellContext ctx)
	{
		if (Boss == null || !IsInstanceValid(Boss) || !Boss.IsAlive)
		{
			GD.PrintErr("[MoltenDrop] Apply called with null/dead Boss — aborting.");
			return;
		}

		var parent = Boss.GetParent();
		if (parent == null)
		{
			GD.PrintErr("[MoltenDrop] Boss has no parent — cannot attach molten drop.");
			return;
		}

		var drop = new MoltenDropProjectile(MoltenDropSpeed, ZoneDamagePerTick);
		drop.GlobalPosition = Boss.GlobalPosition;
		parent.AddChild(drop);

		GD.Print($"[MoltenDrop] MoltenDrop spawned at {Boss.GlobalPosition}.");
	}
}