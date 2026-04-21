using Godot;
using healerfantasy;

/// <summary>
/// Boss cast bar. Shown below the boss health bar while the Crystal Knight is
/// winding up a telegraphed attack (currently: Structural Crush).
///
/// Uses a red/orange danger colour scheme so it reads as threatening, distinct
/// from the player's warm-amber cast bar. A "DEFLECT!" prompt is overlaid on
/// the right side to remind the player what to do.
///
/// Extends <see cref="CastBarBase"/> and wires the
/// <see cref="CrystalKnight.CastWindupStarted"/> and
/// <see cref="CrystalKnight.CastWindupEnded"/> signals.
/// </summary>
public partial class BossCastBar : CastBarBase
{
	// ── danger colour palette ─────────────────────────────────────────────────
	protected override Color BgColor      => new(0.14f, 0.05f, 0.05f, 0.97f);
	protected override Color BorderColor  => new(0.85f, 0.20f, 0.15f, 0.90f);
	protected override Color NameColor    => new(1.00f, 0.75f, 0.65f);
	protected override Color TimeColor    => new(1.00f, 0.35f, 0.25f);
	protected override Color BarFillColor => new(0.90f, 0.22f, 0.10f); // danger red
	protected override Color BarBgColor   => new(0.22f, 0.07f, 0.07f);

	// ── lifecycle ─────────────────────────────────────────────────────────────
	public override void _Ready()
	{
		base._Ready();

		// Append a "DEFLECT!" hint label on top of the base layout.
		BuildDeflectLabel();

		GlobalAutoLoad.SubscribeToSignal(
			nameof(CrystalKnight.CastWindupStarted),
			Callable.From((string spellName, Texture2D icon, float duration) =>
				StartCast(spellName, icon, duration)));

		GlobalAutoLoad.SubscribeToSignal(
			nameof(CrystalKnight.CastWindupEnded),
			Callable.From(StopCast));
	}

	// ── private ───────────────────────────────────────────────────────────────

	/// <summary>
	/// Overlays a pulsing "DEFLECT!" label on the far-right of the bar so the
	/// player immediately knows what action is required.
	/// </summary>
	void BuildDeflectLabel()
	{
		// We add it directly to self; it floats over the base HBoxContainer.
		var label = new Label();
		label.Text = "DEFLECT!";
		label.AddThemeFontSizeOverride("font_size", 13);
		label.AddThemeColorOverride("font_color",        new Color(1.00f, 0.90f, 0.20f));
		label.AddThemeColorOverride("font_shadow_color", new Color(0.00f, 0.00f, 0.00f, 1.00f));
		label.AddThemeConstantOverride("shadow_offset_x", 1);
		label.AddThemeConstantOverride("shadow_offset_y", 1);
		label.AnchorLeft   = 1f;
		label.AnchorRight  = 1f;
		label.AnchorTop    = 0f;
		label.AnchorBottom = 1f;
		label.GrowHorizontal   = GrowDirection.Begin;
		label.OffsetLeft       = -80f;
		label.OffsetRight      = -8f;
		label.HorizontalAlignment = HorizontalAlignment.Right;
		label.VerticalAlignment   = VerticalAlignment.Center;
		label.MouseFilter = MouseFilterEnum.Ignore;
		AddChild(label);
	}
}
