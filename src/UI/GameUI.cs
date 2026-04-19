using Godot;
using healerfantasy;
using SpellResource = healerfantasy.SpellResources.SpellResource;

/// <summary>
/// Party health-frame bar at the bottom of the screen.
/// Each frame has a dark background; the coloured fill IS the health bar —
/// as health drops the fill shrinks left, revealing the dark empty portion.
///
/// Member order: 0 = Templar, 1 = Healer, 2 = Assassin, 3 = Wizard
/// </summary>
public partial class GameUI : CanvasLayer
{
	CastBar _castBar;
	ProgressBar _manaBar;

	// ── per-member config: name + the colour the bar fills with ─────────────
	static readonly (string Name, Color BarColor, float MaxHp)[] MemberDefs =
	{
		("Templar", new Color(0.88f, 0.30f, 0.50f), 150f), // rose-red
		("Healer", new Color(0.35f, 0.78f, 0.22f), 80f), // poison-green
		("Assassin", new Color(0.85f, 0.78f, 0.15f), 100f), // golden-yellow
		("Wizard", new Color(0.20f, 0.50f, 0.95f), 70f) // sapphire-blue
	};

	readonly ProgressBar[] _bars = new ProgressBar[4];

	// ── lifecycle ────────────────────────────────────────────────────────────
	public override void _Ready()
	{
		Layer = 10;

		// Full-screen transparent control used only for edge-anchoring
		var anchor = new Control();
		anchor.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		anchor.MouseFilter = Control.MouseFilterEnum.Ignore;
		AddChild(anchor);

		_castBar = new CastBar();
		_castBar.Size = new Vector2(200, 14);
		// Center 
		_castBar.AnchorLeft = 0.5f;
		_castBar.AnchorTop = 0.6f;

		// Offset to actually center the bar itself
		_castBar.OffsetLeft = -100; // half width
		_castBar.OffsetRight = 100;
		_castBar.OffsetTop = 20; // move down from center
		_castBar.OffsetBottom = 40;
		anchor.AddChild(_castBar);

		_manaBar = new ManaBar();
		_manaBar.Size = new Vector2(200, 14);

		_manaBar.AnchorLeft = 0.5f;
		_manaBar.AnchorRight = 0.5f;
		_manaBar.AnchorTop = 0.6f;

		// Same centering, just lower
		_manaBar.OffsetLeft = -100;
		_manaBar.OffsetRight = 100;
		_manaBar.OffsetTop = 50;
		_manaBar.OffsetBottom = 70;

		var fillStyle = new StyleBoxFlat();
		_manaBar.AddThemeColorOverride("fill_color", Colors.Blue);
		fillStyle.BgColor = Colors.Blue;

		_manaBar.AddThemeStyleboxOverride("fill", fillStyle);
		anchor.AddChild(_manaBar);

		// Horizontal row pinned to the bottom-left corner
		var hbox = new HBoxContainer();
		hbox.AddThemeConstantOverride("separation", 6);
		hbox.AnchorLeft = 0f;
		hbox.AnchorTop = 1f;
		hbox.AnchorRight = 0f;
		hbox.AnchorBottom = 1f;
		hbox.GrowHorizontal = Control.GrowDirection.End;
		hbox.GrowVertical = Control.GrowDirection.Begin;
		hbox.OffsetLeft = 10f;
		hbox.OffsetTop = -74f;
		hbox.OffsetBottom = -10f;
		anchor.AddChild(hbox);

		for (var i = 0; i < MemberDefs.Length; i++)
		{
			var (name, barColor, maxHp) = MemberDefs[i];
			hbox.AddChild(BuildFrame(name, barColor, maxHp, out _bars[i]));
		}
	}

	// ── public API ───────────────────────────────────────────────────────────
	/// <summary>Update a member's displayed health. Index matches MemberDefs order.</summary>
	public void SetHealth(int index, float current, float max)
	{
		if (index < 0 || index >= _bars.Length) return;
		_bars[index].MaxValue = max;
		_bars[index].Value = current;
	}

	// ── frame builder ────────────────────────────────────────────────────────
	static PanelContainer BuildFrame(
		string name, Color barColor, float maxHp, out ProgressBar bar)
	{
		// Dark outer frame
		var panel = new PanelContainer();
		panel.CustomMinimumSize = new Vector2(138, 54);

		var panelStyle = new StyleBoxFlat();
		panelStyle.BgColor = new Color(0.11f, 0.09f, 0.09f, 0.95f);
		panelStyle.SetCornerRadiusAll(6);
		panelStyle.SetBorderWidthAll(2);
		panelStyle.BorderColor = new Color(0.32f, 0.26f, 0.26f);
		panelStyle.ContentMarginLeft = 8f;
		panelStyle.ContentMarginRight = 8f;
		panelStyle.ContentMarginTop = 5f;
		panelStyle.ContentMarginBottom = 5f;
		panel.AddThemeStyleboxOverride("panel", panelStyle);

		var progressBar = new ProgressBar();
		progressBar.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		progressBar.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
		progressBar.AddThemeConstantOverride("separation", 4);
		panel.AddChild(progressBar);

		// Name label — light text on dark background
		var label = new Label();
		label.Text = name;
		label.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		label.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
		label.HorizontalAlignment = HorizontalAlignment.Center;
		label.VerticalAlignment = VerticalAlignment.Center;
		label.AddThemeFontSizeOverride("font_size", 14);
		label.AddThemeColorOverride("font_color", new Color(0.90f, 0.87f, 0.83f));
		panel.AddChild(label);

		// Health bar — coloured fill, dark empty portion
		progressBar.MaxValue = maxHp;
		progressBar.Value = maxHp;
		progressBar.ShowPercentage = false;
		progressBar.CustomMinimumSize = new Vector2(0, 14);

		// The fill IS the character's colour
		var fillStyle = new StyleBoxFlat();
		fillStyle.BgColor = barColor;
		fillStyle.SetCornerRadiusAll(3);
		progressBar.AddThemeStyleboxOverride("fill", fillStyle);

		// The background shows through as "missing" health
		var bgStyle = new StyleBoxFlat();
		bgStyle.BgColor = new Color(0.08f, 0.07f, 0.07f);
		bgStyle.SetCornerRadiusAll(3);
		progressBar.AddThemeStyleboxOverride("background", bgStyle);

		bar = progressBar;
		return panel;
	}
}