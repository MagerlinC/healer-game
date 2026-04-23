using Godot;
using healerfantasy;

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
	protected override Color BgColor      => new(0.14f, 0.05f, 0.05f, 0.97f);
	protected override Color BorderColor  => new(0.85f, 0.20f, 0.15f, 0.90f);
	protected override Color NameColor    => new(1.00f, 0.75f, 0.65f);
	protected override Color TimeColor    => new(1.00f, 0.35f, 0.25f);
	protected override Color BarFillColor => new(0.90f, 0.22f, 0.10f);
	protected override Color BarBgColor   => new(0.22f, 0.07f, 0.07f);

	// ── animation targets ─────────────────────────────────────────────────────
	// The border pulses between the base red and a searing orange-yellow.
	static readonly Color BorderPeak = new(1.00f, 0.65f, 0.10f, 1.00f);
	// The bar fill brightens towards a vivid orange as the cast completes.
	static readonly Color FillPeak   = new(1.00f, 0.50f, 0.05f);

	// ── lifecycle ─────────────────────────────────────────────────────────────
	public override void _Ready()
	{
		base._Ready();

		GlobalAutoLoad.SubscribeToSignal(
			nameof(CrystalKnight.CastWindupStarted),
			Callable.From((string spellName, Texture2D icon, float duration) =>
				StartCast(spellName, icon, duration)));

		GlobalAutoLoad.SubscribeToSignal(
			nameof(CrystalKnight.CastWindupEnded),
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
		float pulseHz  = Mathf.Lerp(2.0f, 6.0f, progress);
		float timeSec  = Time.GetTicksMsec() / 1000f;
		float pulse    = (Mathf.Sin(timeSec * pulseHz * Mathf.Tau) * 0.5f + 0.5f); // 0 → 1

		// Border: colour pulses between base red and searing orange-yellow;
		//         width thickens from 1 px to 4 px as the cast completes.
		PanelStyle.BorderColor = BorderColor.Lerp(BorderPeak, pulse * Mathf.Lerp(0.4f, 1.0f, progress));
		int borderPx = Mathf.RoundToInt(Mathf.Lerp(1f, 4f, progress));
		PanelStyle.SetBorderWidthAll(borderPx);

		// Bar fill: brightens from base red towards vivid orange.
		BarFillStyle.BgColor = BarFillColor.Lerp(FillPeak, progress * 0.75f + pulse * 0.25f * progress);
	}
}
