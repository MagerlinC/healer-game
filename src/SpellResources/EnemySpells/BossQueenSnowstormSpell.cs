using Godot;
using healerfantasy.SpellSystem;

namespace healerfantasy.SpellResources;

/// <summary>
/// Queen of the Frozen Wastes — Snowstorm.
///
/// Cast phase (1 second, shown on BossCastBar via CastWindupStarted)
/// ─────────────────────────────────────────────────────────────────
/// The Queen raises her hands and calls down a blizzard.
///
/// Channel phase (ChannelDuration = 8 seconds)
/// ─────────────────────────────────────────────
/// A <see cref="SnowstormChannelNode"/> is spawned on the Queen's parent.
/// Every second of the channel every living party member takes
/// <see cref="DamagePerTick"/> damage. The channel bar in the UI drains
/// from full to empty over the duration.
///
/// The spell resolves via <see cref="Apply"/> which is called after the
/// 1-second cast animation finishes in <see cref="QueenOfTheFrozenWastes"/>.
/// </summary>
[GlobalClass]
public partial class BossQueenSnowstormSpell : SpellResource
{
	/// <summary>Damage dealt to each party member per channel tick (once per second).</summary>
	public float DamagePerTick = 20f;

	/// <summary>How long the channel persists after the cast completes.</summary>
	public float ChannelDuration = 8f;

	/// <summary>Reference to the Queen — must be set before SpellPipeline.Cast is called.</summary>
	public Character Boss { get; set; }

	public BossQueenSnowstormSpell()
	{
		Name = "Snowstorm";
		Description = $"The Queen summons a raging blizzard, dealing {20:F0} frost damage " +
		              $"to the entire party every second for {8:F0} seconds.";
		Tags = SpellTags.Damage | SpellTags.Duration;
		Icon = GD.Load<Texture2D>(AssetConstants.SpellIconAssets + "enemy/queen-of-the-frozen-wastes/snowstorm.png");
		ManaCost = 0f;
		CastTime = 1.0f;
		EffectType = EffectType.Harmful;
	}

	public override void Apply(SpellContext ctx)
	{
		if (Boss == null || !IsInstanceValid(Boss) || !Boss.IsAlive)
		{
			GD.PrintErr("[Snowstorm] Apply called with null/dead Boss — aborting.");
			return;
		}

		var parent = Boss.GetParent();
		if (parent == null)
		{
			GD.PrintErr("[Snowstorm] Boss has no parent — cannot attach channel node.");
			return;
		}

		var channel = new SnowstormChannelNode(Boss, ChannelDuration, DamagePerTick)
		{
			OnChannelFinished = () => (Boss as QueenOfTheFrozenWastes)?.OnSnowstormChannelEnded()
		};

		parent.AddChild(channel);

		// Notify the queen so she emits SnowstormChannelStarted → BossCastBar shows channel bar.
		(Boss as QueenOfTheFrozenWastes)?.OnSnowstormChannelStarted(this);

		GD.Print($"[Snowstorm] Channel started — {ChannelDuration:F0}s, {DamagePerTick:F0} dmg/tick.");
	}
}