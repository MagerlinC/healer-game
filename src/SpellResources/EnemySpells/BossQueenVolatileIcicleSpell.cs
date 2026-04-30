using Godot;
using healerfantasy.SpellSystem;

namespace healerfantasy.SpellResources;

/// <summary>
/// Queen of the Frozen Wastes — Volatile Icicle.
///
/// 1-second cast (shown on BossCastBar). On resolution a
/// <see cref="VolatileIcicleProjectile"/> is spawned at the Queen's position
/// and floats slowly toward the healer.
///
/// When the icicle reaches any party member it explodes, leaving a permanent
/// <see cref="IcicleExplosionZone"/> — a blue circular area that deals damage
/// over time to anyone standing inside it.
///
/// The mechanic rewards running the icicle to the edge of the arena so the
/// resulting zone doesn't claim valuable central space.
/// </summary>
[GlobalClass]
public partial class BossQueenVolatileIcicleSpell : SpellResource
{
	/// <summary>Damage per second dealt by the explosion zone left behind.</summary>
	public float ZoneDamagePerTick = 15f;

	/// <summary>Pixels per second at which the icicle floats toward the healer.</summary>
	public float IcicleSpeed = 85f;

	/// <summary>Reference to the Queen — must be set before SpellPipeline.Cast is called.</summary>
	public Character Boss { get; set; }

	public BossQueenVolatileIcicleSpell()
	{
		Name = "Volatile Icicle";
		Description = "The Queen conjures a shard of volatile ice that floats toward the healer. " +
		              "On impact it explodes, leaving a frozen zone that deals damage over time " +
		              "to anyone standing within it.";
		Icon = GD.Load<Texture2D>(AssetConstants.SpellIconAssets + "enemy/queen-of-the-frozen-wastes/volatile-icicle.png");
		Tags = SpellTags.Damage;
		ManaCost = 0f;
		CastTime = 1.5f;
		EffectType = EffectType.Harmful;
	}

	public override void Apply(SpellContext ctx)
	{
		if (Boss == null || !IsInstanceValid(Boss) || !Boss.IsAlive)
		{
			GD.PrintErr("[VolatileIcicle] Apply called with null/dead Boss — aborting.");
			return;
		}

		var parent = Boss.GetParent();
		if (parent == null)
		{
			GD.PrintErr("[VolatileIcicle] Boss has no parent — cannot attach icicle.");
			return;
		}

		var icicle = new VolatileIcicleProjectile(IcicleSpeed, ZoneDamagePerTick);
		icicle.GlobalPosition = Boss.GlobalPosition;
		parent.AddChild(icicle);

		GD.Print($"[VolatileIcicle] Icicle spawned at {Boss.GlobalPosition}.");
	}
}