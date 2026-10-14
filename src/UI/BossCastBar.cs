using Godot;
using healerfantasy;
using healerfantasy.LastLight;
using healerfantasy.SpellResources;

/// <summary>
/// Boss cast bar. Shown below the boss health bar while the Crystal Knight is
/// winding up a telegraphed attack (Structural Crush — a deflectable cast).
///
/// Uses a pulsing red/orange danger scheme that intensifies as the cast nears
/// completion. The border thickens and flashes, and the fill colour brightens,
/// giving a visceral sense of mounting threat without a static text label.
///
/// The <see cref="DeflectOverlay"/> node in the World scene handles the
/// full-screen darkening effect that accompanies this bar.
///
/// Extends <see cref="CastBarBase"/> and wires the
/// <see cref="CrystalKnight.CastWindupStarted"/> and
/// <see cref="CrystalKnight.CastWindupEnded"/> signals.
/// </summary>
public partial class BossCastBar : CastBarBase
{
	// ── base danger colour palette ────────────────────────────────────────────
	protected override Color BgColor => new(0.06f, 0.02f, 0.018f, 0.96f);
	protected override Color BorderColor => new(0.55f, 0.12f, 0.08f, 0.95f);
	protected override Color NameColor => new(0.92f, 0.78f, 0.7f);
	protected override Color TimeColor => new(0.9f, 0.4f, 0.2f);
	protected override Color BarFillColor => new(0.55f, 0.1f, 0.06f);
	protected override Color BarBgColor => new(0.08f, 0.02f, 0.02f);

	// ── animation targets ─────────────────────────────────────────────────────
	// The border pulses between the base red and a searing orange-yellow.
	static readonly Color BorderPeak = new(0.95f, 0.42f, 0.16f, 1.00f);

	// The bar fill brightens towards a vivid orange as the cast completes.
	static readonly Color FillPeak = new(0.9f, 0.3f, 0.1f);

	// ── lifecycle ─────────────────────────────────────────────────────────────
	public override void _Ready()
	{
		base._Ready();

		// ── pre-channel cast bars (all bosses that emit CastWindupStarted) ─────
		// GlobalAutoLoad wires this to every registered emitter of the same name,
		// so CrystalKnight, TheBloodPrince, etc. all feed into the same bar.
		GlobalAutoLoad.SubscribeToSignal(
			nameof(CrystalKnight.CastWindupStarted),
			Callable.From((string spellName, Texture2D icon, float duration) =>
				StartCast(spellName, icon, duration)));

		GlobalAutoLoad.SubscribeToSignal(
			nameof(CrystalKnight.CastWindupEnded),
			Callable.From(StopCast));

		// ── Blood Prince: Sanguine Siphon channel bar (reverse / draining mode) ─
		GlobalAutoLoad.SubscribeToSignal(
			nameof(TheBloodPrince.SanguineChannelStarted),
			Callable.From((BossBloodPrinceSanguineSiphonSpell spell) =>
				StartChannel(spell.Name, spell.Icon, spell.ChannelDuration)));

		GlobalAutoLoad.SubscribeToSignal(
			nameof(TheBloodPrince.SanguineChannelEnded),
			Callable.From(StopCast));

		// ── Queen of the Frozen Wastes: Snowstorm channel bar ────────────────
		GlobalAutoLoad.SubscribeToSignal(
			nameof(QueenOfTheFrozenWastes.SnowstormChannelStarted),
			Callable.From((BossQueenSnowstormSpell spell) =>
				StartChannel(spell.Name, spell.Icon, spell.ChannelDuration)));

		GlobalAutoLoad.SubscribeToSignal(
			nameof(QueenOfTheFrozenWastes.SnowstormChannelEnded),
			Callable.From(StopCast));
	}

	// ── visual update hook ────────────────────────────────────────────────────

	/// <summary>
	/// Called every frame while the cast bar is active.
	/// Pulses the border colour and widens it as <paramref name="progress"/>
	/// approaches 1, giving a mounting sense of urgency.
	/// </summary>
	protected override void OnCastVisualUpdate(float progress)
	{
		// Fast pulse frequency that accelerates with progress: starts at ~2 Hz,
		// reaches ~6 Hz as the cast completes, so urgency is palpable.
		var pulseHz = Mathf.Lerp(2.0f, 6.0f, progress);
		var timeSec = Time.GetTicksMsec() / 1000f;
		var pulse = Mathf.Sin(timeSec * pulseHz * Mathf.Tau) * 0.5f + 0.5f; // 0 → 1

		// Border: colour pulses between base red and searing orange-yellow;
		//         width thickens from 1 px to 4 px as the cast completes.
		PanelStyle.BorderColor = BorderColor.Lerp(BorderPeak, pulse * Mathf.Lerp(0.4f, 1.0f, progress));
		var borderPx = Mathf.RoundToInt(Mathf.Lerp(1f, 4f, progress));
		PanelStyle.SetBorderWidthAll(borderPx);

		// Bar fill: brightens from base red towards vivid orange.
		BarFillStyle.BgColor = BarFillColor.Lerp(FillPeak, progress * 0.75f + pulse * 0.25f * progress);
	}
}