using System.Collections.Generic;
using Godot;
using healerfantasy.SpellResources;
using healerfantasy.SpellResources.Void;
using healerfantasy.SpellSystem;
using healerfantasy.Talents;

namespace healerfantasy.Effects;

/// <summary>
/// Applied by Mirror Image (Chronomancy ultimate).
///
/// While active, every spell the caster fires is re-cast by the ghost clone at
/// 50% effectiveness (FinalValue × 0.5). The clone calls <see cref="SpellResource.Apply"/>
/// directly with a modified <see cref="SpellContext"/>, so the echoed spell
/// behaves identically to the original — HoTs apply real HoT effects, group
/// spells hit all targets, etc.
///
/// To prevent the clone's effects from replacing the player's copy of the same
/// effect on the same target, <see cref="Character.MirrorEffectIdSuffix"/> is
/// set to "_Mirror" around the Apply call. Every <see cref="Character.ApplyEffect"/>
/// call automatically appends this suffix while it is set.
///
/// A <see cref="MirrorImageClone"/> ghost node is spawned on the caster when
/// this effect is applied and freed when it expires.
/// </summary>
public partial class MirrorImageEffect : CharacterEffect, ISpellModifier
{
	const float MirrorFraction = 0.5f;
	const string MirrorSuffix = "_Mirror";

	/// <summary>The visual ghost clone added to the scene while this effect is active.</summary>
	MirrorImageClone? _clone;

	public MirrorImageEffect(float duration) : base(duration)
	{
		EffectId = "MirrorImageEffect";
	}

	// ── CharacterEffect lifecycle ─────────────────────────────────────────────

	public override void OnApplied(Character target)
	{
		_clone = new MirrorImageClone();
		target.AddChild(_clone);
	}

	public override void OnExpired(Character target)
	{
		_clone?.QueueFree();
		_clone = null;
	}

	// ── ISpellModifier ────────────────────────────────────────────────────────

	public ModifierPriority Priority => ModifierPriority.BASE;

	public void OnBeforeCast(SpellContext ctx)
	{
	}
	public void OnCalculate(SpellContext ctx)
	{
	}

	/// <summary>
	/// After every cast: have the ghost clone re-cast the same spell at 50% power.
	///
	/// Target resolution:
	/// <list type="bullet">
	///   <item>Group spells — all targets via <see cref="SpellResource.ResolveTargets"/>.</item>
	///   <item>Single-target healing — one random alive party member.</item>
	///   <item>Single-target damage — one random alive enemy.</item>
	/// </list>
	///
	/// <see cref="Character.MirrorEffectIdSuffix"/> is set around the Apply call
	/// so any effects the spell applies get a "_Mirror" suffix on their EffectId,
	/// preventing them from clobbering the player's version on the same target.
	/// </summary>
	public void OnAfterCast(SpellContext ctx)
	{
		// Don't echo ultimates or off-school utility spells.
		if (ctx.Spell is UltimateSpellResource) return;
		if (ctx.Spell.School == SpellSchool.Generic) return;

		// Echo value is 50% of the fully-resolved pipeline value.
		var echoFinalValue = ctx.FinalValue * MirrorFraction;
		if (echoFinalValue <= 0f) return;

		// ── Resolve clone targets ─────────────────────────────────────────────
		List<Character> echoTargets;

		if (ctx.Tags.HasFlag(SpellTags.GroupSpell))
		{
			// Group spell — clone replicates the same target set using the
			// spell's own resolver (party-wide heals, AoE damage, etc.).
			echoTargets = ctx.Spell.ResolveTargets(ctx.Caster, ctx.Target);
		}
		else if (ctx.Tags.HasFlag(SpellTags.Damage))
		{
			var enemies = ctx.Caster.CollectAliveEnemies();
			if (enemies.Count == 0) return;
			echoTargets = new List<Character> { enemies[(int)(GD.Randi() % (uint)enemies.Count)] };
		}
		else if (ctx.Tags.HasFlag(SpellTags.Healing))
		{
			var allies = ctx.Caster.CollectAlivePartyMembers();
			if (allies.Count == 0) return;
			echoTargets = new List<Character> { allies[(int)(GD.Randi() % (uint)allies.Count)] };
		}
		else
		{
			// Spell has neither Damage nor Healing tag — nothing meaningful to echo.
			return;
		}

		// ── Build the echo SpellContext ───────────────────────────────────────
		var echoCtx = new SpellContext
		{
			Caster = ctx.Caster,
			Spell = ctx.Spell,
			Tags = ctx.Tags,
			BaseValue = ctx.BaseValue * MirrorFraction,
			FinalValue = echoFinalValue,
			Targets = echoTargets,
			CasterStats = ctx.CasterStats,
			Timestamp = Time.GetTicksMsec() / 1000.0,
			// Don't forward EffectDurationBonus — talent bonuses belong to the player's cast only.
			EffectDurationBonus = 0f
		};

		// ── Apply via the spell's own Apply(), with mirror-suffixed effect IDs ─
		_clone?.PlayCast();

		Character.MirrorEffectIdSuffix = MirrorSuffix;
		try
		{
			ctx.Spell.Apply(echoCtx);
		}
		finally
		{
			// Always clear, even if Apply throws.
			Character.MirrorEffectIdSuffix = null;
		}

		// ── Floating combat text for instant (non-Duration) spells ────────────
		// HoT / DoT effects emit their own FCT on each tick, so skip those.
		// Instant spells call target.Heal / TakeDamage inside Apply but don't
		// emit FCT themselves (the pipeline normally does that at step 10b).
		if (!ctx.Tags.HasFlag(SpellTags.Duration))
		{
			var isDamage = ctx.Tags.HasFlag(SpellTags.Damage);
			var isHealing = ctx.Tags.HasFlag(SpellTags.Healing) && !isDamage;
			var school = (int)ctx.Spell.School;

			foreach (var t in echoTargets)
			{
				if (!IsInstanceValid(t) || t.IsBeingRemoved) continue;
				if (isDamage) t.RaiseFloatingCombatText(echoFinalValue, false, school, false);
				if (isHealing) t.RaiseFloatingCombatText(echoFinalValue, true, school, false);
			}
		}
	}
}