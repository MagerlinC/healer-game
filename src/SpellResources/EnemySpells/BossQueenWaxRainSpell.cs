using Godot;
using healerfantasy.Runes;
using healerfantasy.SpellSystem;

namespace healerfantasy.SpellResources;

/// <summary>
/// The Tallow Queen — Wax Rain.
///
/// Cast phase (1 second, shown on BossCastBar via CastWindupStarted)
/// ─────────────────────────────────────────────────────────────────
/// The Queen raises her hands and the spire weeps hot wax.
///
/// Channel phase (ChannelDuration = 8 seconds)
/// ─────────────────────────────────────────────
/// A <see cref="WaxRainChannelNode"/> is spawned on the Queen's parent.
/// Every second of the channel every living party member takes
/// <see cref="DamagePerTick"/> damage. The channel bar in the UI drains
/// from full to empty over the duration.
///
/// The spell resolves via <see cref="Apply"/> which is called after the
/// 1-second cast animation finishes in <see cref="TheTallowQueen"/>.
/// </summary>
[GlobalClass]
public partial class BossQueenWaxRainSpell : SpellResource
{
	/// <summary>Damage dealt to each party member per channel tick (once per second).</summary>
	public float DamagePerTick = 20f;

	/// <summary>How long the channel persists after the cast completes.</summary>
	public float ChannelDuration = 6f;

	/// <summary>Reference to the Queen — must be set before SpellPipeline.Cast is called.</summary>
	public Character Boss { get; set; }

	public BossQueenWaxRainSpell()
	{
		Name = "Wax Rain";
		Description = $"Molten wax rains from the spire's ceiling, dealing {20:F0} damage " +
		              $"to the entire party every second for {8:F0} seconds.";
		Tags = SpellTags.Damage | SpellTags.Duration;
		Icon = GD.Load<Texture2D>(AssetConstants.SpellIconAssets + "enemy/the-tallow-queen/wax-rain.png");
		ManaCost = 0f;
		CastTime = 1.0f;
		TargetingType = TargetingType.Enemy;
	}

	public override void Apply(SpellContext ctx)
	{
		if (Boss == null || !IsInstanceValid(Boss) || !Boss.IsAlive)
		{
			GD.PrintErr("[WaxRain] Apply called with null/dead Boss — aborting.");
			return;
		}

		var parent = Boss.GetParent();
		if (parent == null)
		{
			GD.PrintErr("[WaxRain] Boss has no parent — cannot attach channel node.");
			return;
		}

		var starlessRuneActive = RunState.Instance.IsRuneActive(RuneIndex.StarlessNight);
		var channel = new WaxRainChannelNode(Boss, starlessRuneActive ? ChannelDuration + 2f : ChannelDuration, DamagePerTick)
		{
			OnChannelFinished = () => (Boss as TheTallowQueen)?.OnWaxRainChannelEnded()
		};

		parent.AddChild(channel);

		// Notify the queen so she emits WaxRainChannelStarted → BossCastBar shows channel bar.
		(Boss as TheTallowQueen)?.OnWaxRainChannelStarted(this);

		GD.Print($"[WaxRain] Channel started — {ChannelDuration:F0}s, {DamagePerTick:F0} dmg/tick.");
	}
}