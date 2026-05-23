using Godot;

/// <summary>
/// Compact cast bar shown inside a Blood Rune's health-bar frame while it channels
/// Blood Burst. Uses a deep-crimson colour scheme that distinguishes rune casts
/// from main-boss casts (<see cref="BossCastBar"/>).
///
/// Unlike <see cref="BossCastBar"/>, this bar is driven externally — the
/// <see cref="BloodRune"/> enemy calls <see cref="CastBarBase.StartCast"/> and
/// <see cref="CastBarBase.StopCast"/> directly on the instance returned by
/// <see cref="healerfantasy.UI.GameUI.AddMiniEnemyHealthBar"/>.
/// No signal subscriptions are needed.
/// </summary>
public partial class BloodRuneCastBar : CastBarBase
{
	// ── colour palette ────────────────────────────────────────────────────────
	protected override Color BgColor      => new(0.12f, 0.03f, 0.07f, 0.97f);
	protected override Color BorderColor  => new(0.80f, 0.10f, 0.25f, 0.90f);
	protected override Color NameColor    => new(1.00f, 0.72f, 0.72f);
	protected override Color TimeColor    => new(1.00f, 0.30f, 0.40f);
	protected override Color BarFillColor => new(0.75f, 0.08f, 0.20f);
	protected override Color BarBgColor   => new(0.20f, 0.05f, 0.10f);

	static readonly Color BorderPeak = new(1.00f, 0.40f, 0.55f, 1.00f);
	static readonly Color FillPeak   = new(1.00f, 0.20f, 0.35f);

	// ── visual update hook ────────────────────────────────────────────────────

	/// <summary>
	/// Pulses the border and brightens the fill as the cast approaches completion,
	/// building tension without dominating the screen like the full boss cast bar.
	/// </summary>
	protected override void OnCastVisualUpdate(float progress)
	{
		var pulseHz = Mathf.Lerp(1.5f, 5.0f, progress);
		var timeSec = Time.GetTicksMsec() / 1000f;
		var pulse   = Mathf.Sin(timeSec * pulseHz * Mathf.Tau) * 0.5f + 0.5f;

		PanelStyle.BorderColor = BorderColor.Lerp(BorderPeak, pulse * Mathf.Lerp(0.3f, 1.0f, progress));
		var borderPx = Mathf.RoundToInt(Mathf.Lerp(1f, 3f, progress));
		PanelStyle.SetBorderWidthAll(borderPx);

		BarFillStyle.BgColor = BarFillColor.Lerp(FillPeak, progress * 0.7f + pulse * 0.3f * progress);
	}
}
