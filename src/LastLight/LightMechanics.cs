using Godot;
using healerfantasy.SpellResources;
using healerfantasy.SpellSystem;

namespace healerfantasy.LastLight;

/// <summary>
/// How each spell school plays with the healer's light. Called by
/// <see cref="SpellPipeline"/> after every successful cast.
///
///   Flame — brightens the light for a few seconds and burns away one Gloom.
///   Ember — healing leaves a smouldering pocket of light under the ally.
///   Umbra — adds a stack of Gloom to the party: the light dims and the
///          darkness creeps faster (+damage taken per stack). The trade-off.
///   Wick — the flame gutters, then flares.
///
/// Gloom is gameplay; the rest is visual feedback.
/// </summary>
public static class LightMechanics
{
	public const int MaxGloom = 5;
	public const float GloomDuration = 10f;
	public const float GloomDamagePerStack = 0.04f;
	const float GloomDimPerStack = 0.09f;

	const float BrightenDuration = 4f;
	const float BrightenAmount = 0.3f;
	const float GutterDuration = 0.9f;

	static double _brightenStart = -100, _gutterStart = -100;

	static double Now => Time.GetTicksMsec() / 1000.0;

	public static void OnCast(SpellContext ctx)
	{
		if (ctx.Caster is not Player) return;

		switch (ctx.Spell.School)
		{
			case SpellSchool.Flame:
				_brightenStart = Now;
				BurnAwayGloom(ctx.Caster);
				break;

			case SpellSchool.Ember:
				if (ctx.Tags.HasFlag(SpellTags.Healing) && ctx.Targets != null)
					foreach (var target in ctx.Targets)
						if (target is { IsFriendly: true } && GodotObject.IsInstanceValid(target))
							EmberPocket.SpawnUnder(target);
				break;

			case SpellSchool.Umbra:
				AddGloom(ctx.Caster);
				break;

			case SpellSchool.Wick:
				_gutterStart = Now;
				break;
		}
	}

	/// <summary>
	/// Multiplier on the healer's light radius: brighter after Flame, a gutter-and-flare
	/// after Wick, and dimmer for every stack of Gloom on the healer.
	/// </summary>
	public static float LightMultiplier(Character healer)
	{
		var m = 1f;

		var sinceBrighten = (float)(Now - _brightenStart);
		if (sinceBrighten < BrightenDuration)
			m += BrightenAmount * Mathf.Min(sinceBrighten / 0.25f, 1f) * (1f - Mathf.Pow(sinceBrighten / BrightenDuration, 3f));

		var sinceGutter = (float)(Now - _gutterStart);
		if (sinceGutter < GutterDuration)
		{
			var t = sinceGutter / GutterDuration;
			// dip hard, then flare past normal and settle
			m *= t < 0.35f
				? Mathf.Lerp(1f, 0.5f, t / 0.35f)
				: Mathf.Lerp(0.5f, 1f, (t - 0.35f) / 0.65f) + 0.35f * Mathf.Sin((t - 0.35f) / 0.65f * Mathf.Pi);
		}

		var gloom = GloomStacks(healer);
		m *= 1f - GloomDimPerStack * gloom;
		return m;
	}

	public static int GloomStacks(Character character) =>
		character?.GetEffectById(GloomEffect.Id) is { } e ? e.CurrentStacks : 0;

	// ── Gloom ────────────────────────────────────────────────────────────────

	static void AddGloom(Character caster)
	{
		foreach (var node in caster.GetTree().GetNodesInGroup(GameConstants.PartyGroupName))
			if (node is Character { IsAlive: true } member)
				member.ApplyEffect(new GloomEffect());
	}

	static void BurnAwayGloom(Character caster)
	{
		foreach (var node in caster.GetTree().GetNodesInGroup(GameConstants.PartyGroupName))
		{
			if (node is not Character member || member.GetEffectById(GloomEffect.Id) is not { } gloom) continue;
			var left = gloom.CurrentStacks - 1;
			var remaining = gloom.Remaining;
			member.RemoveEffect(GloomEffect.Id);
			if (left > 0 && member.IsAlive)
				member.ApplyEffect(new GloomEffect(left, remaining));
		}
	}
}

/// <summary>
/// Gloom — the dark let in by Umbra spells. Each stack makes the bearer take more
/// damage and dims the healer's light. Stacks up to <see cref="LightMechanics.MaxGloom"/>;
/// refreshed by further Umbra casts, burned away one at a time by Flame casts.
/// </summary>
public partial class GloomEffect : CharacterEffect, ICharacterModifier
{
	public const string Id = "Gloom";

	static Texture2D _icon;

	public GloomEffect(int stacks = 1, float duration = LightMechanics.GloomDuration) : base(duration)
	{
		EffectId = Id;
		AbilityName = "Gloom";
		School = SpellSchool.Umbra;
		IsHarmful = true;
		IsDispellable = false;
		MaximumStacks = LightMechanics.MaxGloom;
		CurrentStacks = stacks;
		Icon = _icon ??= Hud.LastLightIcons.Gloom();
		UpdateDescription();
	}

	public override void OnReapplied(Character target, CharacterEffect newEffect)
	{
		CurrentStacks = Mathf.Min(CurrentStacks + 1, MaximumStacks);
		Refresh();
		UpdateDescription();
	}

	public void Modify(CharacterStats stats)
	{
		stats.DamageTakenMultiplier += LightMechanics.GloomDamagePerStack * CurrentStacks;
	}

	void UpdateDescription()
	{
		Description = $"The dark creeps closer. Taking {Mathf.RoundToInt(LightMechanics.GloomDamagePerStack * CurrentStacks * 100)}% more damage. " +
		              "Umbra spells deepen it; Flame spells burn it away.";
	}
}
