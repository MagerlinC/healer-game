using System.Collections.Generic;
using Godot;
using healerfantasy.SpellSystem;

namespace healerfantasy.SpellResources;

/// <summary>
/// The Blood Prince's signature spell — Sanguine Siphon.
///
/// Cast phase (CastTime = 1 s)
/// ───────────────────────────
/// The boss winds up with a cast animation. <see cref="TheBloodPrince"/> emits
/// CastWindupStarted so the UI can show a cast bar. When the animation finishes,
/// <see cref="Apply"/> is called.
///
/// Channel phase (ChannelDuration seconds)
/// ─────────────────────────────────────────
/// Apply spawns a <see cref="SanguineSiphonChannelNode"/> on the boss's parent,
/// which drives the entire channel lifecycle:
///
///   • Applies <see cref="healerfantasy.Effects.SanguineBloodLinkEffect"/> to every
///     target in <see cref="PendingTargets"/> — dealing LifeLeechedPerTick damage
///     per second and healing the boss for the same amount.
///   • Applies <see cref="healerfantasy.Effects.SanguineDrainDebuff"/> to the boss —
///     recording its current health and setting a break threshold at
///     (currentHealth − 10 % of MaxHealth). If the party bursts the boss to that
///     threshold the channel is cancelled early.
///   • Draws a blood-link Line2D between the boss and each target every frame.
///   • Emits <see cref="TheBloodPrince.SanguineChannelStarted"/> /
///     <see cref="TheBloodPrince.SanguineHealthTargetSet"/> on start, and their
///     "Ended" / "Cleared" counterparts when the channel ends so the UI can show
///     a reverse (draining) channel bar and a health-target marker.
///
/// <see cref="PendingTargets"/> must be populated by <see cref="TheBloodPrince"/>
/// before calling SpellPipeline.Cast, and is consumed by
/// <see cref="ResolveTargets"/>.
/// </summary>
[GlobalClass]
public partial class BossBloodPrinceSanguineSiphonSpell : SpellResource
{
	// ── tunables ──────────────────────────────────────────────────────────────

	/// <summary>Health drained from each linked target (and given to the boss) per second.</summary>
	public float LifeLeechedPerTick = 20f;

	/// <summary>How long the channel lasts before ending naturally.</summary>
	public float ChannelDuration = 8f;

	// ── wiring ────────────────────────────────────────────────────────────────

	/// <summary>Reference to the Blood Prince — must be set by the boss before casting.</summary>
	public Character Boss { get; set; }

	/// <summary>
	/// Targets pre-selected by <see cref="TheBloodPrince.CastSanguineSiphon"/>.
	/// <see cref="ResolveTargets"/> returns this list so the SpellPipeline passes
	/// all of them into <see cref="Apply"/> at once. Cleared after each cast.
	/// </summary>
	public List<Character> PendingTargets { get; set; }

	// ── ctor ──────────────────────────────────────────────────────────────────

	public BossBloodPrinceSanguineSiphonSpell()
	{
		Name = "Sanguine Siphon";
		Description = $"The Blood Prince links his blood to nearby enemies, " +
		              $"draining {LifeLeechedPerTick:F0} life per second from each. " +
		              $"Deal enough damage to break the channel.";
		Tags = SpellTags.Damage | SpellTags.Void | SpellTags.Duration;
		Icon = GD.Load<Texture2D>(AssetConstants.SpellIconAssets + "enemy/the-blood-prince/sanguine-siphon.png");
		ManaCost = 0f;
		CastTime = 1.0f;
		EffectType = EffectType.Harmful;
	}

	// ── pipeline overrides ────────────────────────────────────────────────────

	public override List<Character> ResolveTargets(Character caster, Character explicitTarget)
	{
		// Use the pre-selected target list when available; fall back to the
		// single explicit target so the spell is still usable in isolation.
		if (PendingTargets != null && PendingTargets.Count > 0)
			return PendingTargets;
		return new List<Character> { explicitTarget };
	}

	public override void Apply(SpellContext ctx)
	{
		if (Boss == null || !IsInstanceValid(Boss) || !Boss.IsAlive)
		{
			GD.PrintErr("[SanguineSiphon] Apply called with null/dead Boss reference — aborting.");
			return;
		}

		var targets = ctx.Targets;
		if (targets == null || targets.Count == 0)
		{
			GD.PrintErr("[SanguineSiphon] Apply called with no targets — aborting.");
			return;
		}

		// ── spawn the channel node on the World (boss's parent) ───────────────
		var channelNode = new SanguineSiphonChannelNode(
			Boss,
			targets,
			ChannelDuration,
			LifeLeechedPerTick);

		channelNode.OnChannelFinished = () =>
			(Boss as TheBloodPrince)?.OnSanguineSiphonChannelEnded();

		var parent = Boss.GetParent();
		if (parent == null)
		{
			GD.PrintErr("[SanguineSiphon] Boss has no parent — cannot attach channel node.");
			return;
		}

		parent.AddChild(channelNode);
		// _Ready has now run: drain debuff applied, HealthTarget computed.

		// ── notify boss UI ────────────────────────────────────────────────────
		var healthTargetFraction = channelNode.GetHealthTargetFraction();
		(Boss as TheBloodPrince)?.OnSanguineSiphonChannelStarted(this, healthTargetFraction);

		// ── consume pending targets ───────────────────────────────────────────
		PendingTargets = null;
	}
}