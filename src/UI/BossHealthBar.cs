using Godot;

namespace healerfantasy.UI;

public partial class BossHealthBar : PanelContainer
{

	Label _nameLabel;
	Label _currentHealthLabel;
	ProgressBar _bar;

	Color BarTextColor = new(0.95f, 0.90f, 0.85f);

	public override void _Ready()
	{
		BuildLayout();
		GlobalAutoLoad.SubscribeToSignal(nameof(Character.HealthChanged),
			Callable.From((string charName, float current, float max) => UpdateProgress(charName, current, max)));
		Visible = false;
	}

	void UpdateProgress(string charName, float current, float max)
	{
		if (charName == GameConstants.Boss1Name)
		{
			_bar.Value = Mathf.Clamp(current / max, 0f, 1f);
			_nameLabel.Text = charName;
			_currentHealthLabel.Text = $"{current:F0} / {max:F0}";
			Visible = true;
		}
	}

	void BuildLayout()
	{
		// ── outer panel ───────────────────────────────────────────────────────
		var panelStyle = new StyleBoxFlat();
		panelStyle.BgColor = new Color(0.15f, 0.05f, 0.05f, 0.8f);
		panelStyle.SetCornerRadiusAll(5);
		panelStyle.SetBorderWidthAll(1);
		panelStyle.BorderColor = new Color(0.15f, 0.05f, 0.05f, 0.8f);
		panelStyle.ContentMarginLeft = 8f;
		panelStyle.ContentMarginRight = 8f;
		panelStyle.ContentMarginTop = 6f;
		panelStyle.ContentMarginBottom = 6f;
		AddThemeStyleboxOverride("panel", panelStyle);

		MouseFilter = MouseFilterEnum.Ignore;

		// ── main row ──────────────────────────────────────────────────────────
		var row = new HBoxContainer();
		row.AddThemeConstantOverride("separation", 8);
		AddChild(row);

		// ── overlay container (bar + text) ────────────────────────────────────
		var overlay = new Control();
		overlay.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		overlay.CustomMinimumSize = new Vector2(0f, 24f);
		row.AddChild(overlay);

		// ── progress bar (fills entire overlay) ───────────────────────────────
		_bar = new ProgressBar();
		_bar.AnchorLeft = 0;
		_bar.AnchorRight = 1;
		_bar.AnchorTop = 0;
		_bar.AnchorBottom = 1;

		_bar.OffsetLeft = 0;
		_bar.OffsetRight = 0;
		_bar.OffsetTop = 0;
		_bar.OffsetBottom = 0;

		_bar.ShowPercentage = false;
		_bar.MinValue = 0f;
		_bar.MaxValue = 1f;
		_bar.Value = 0f;
		_bar.MouseFilter = MouseFilterEnum.Ignore;

		var barBg = new StyleBoxFlat();
		barBg.BgColor = new Color(0.25f, 0.08f, 0.08f, 0.9f);
		barBg.SetCornerRadiusAll(3);
		_bar.AddThemeStyleboxOverride("background", barBg);

		var barFill = new StyleBoxFlat();
		barFill.BgColor = new Color(0.85f, 0.15f, 0.15f, 0.9f);
		barFill.SetCornerRadiusAll(3);
		_bar.AddThemeStyleboxOverride("fill", barFill);

		overlay.AddChild(_bar);

		// ── name label (center-left) ──────────────────────────────────────────
		_nameLabel = new Label();
		_nameLabel.AnchorLeft = 0;
		_nameLabel.AnchorRight = 1;
		_nameLabel.AnchorTop = 0;
		_nameLabel.AnchorBottom = 1;

		_nameLabel.OffsetLeft = 8;
		_nameLabel.OffsetRight = -40; // leave space for time
		_nameLabel.VerticalAlignment = VerticalAlignment.Center;

		_nameLabel.AddThemeFontSizeOverride("font_size", 14);
		_nameLabel.AddThemeColorOverride("font_color", BarTextColor);
		_nameLabel.MouseFilter = MouseFilterEnum.Ignore;

		overlay.AddChild(_nameLabel);

		// ── time label (right-aligned) ────────────────────────────────────────
		_currentHealthLabel = new Label();
		_currentHealthLabel.AnchorLeft = 1;
		_currentHealthLabel.AnchorRight = 1;
		_currentHealthLabel.AnchorTop = 0;
		_currentHealthLabel.AnchorBottom = 1;

		_currentHealthLabel.OffsetLeft = -100;
		_currentHealthLabel.OffsetRight = -8;

		_currentHealthLabel.HorizontalAlignment = HorizontalAlignment.Right;
		_currentHealthLabel.VerticalAlignment = VerticalAlignment.Center;

		_currentHealthLabel.AddThemeFontSizeOverride("font_size", 13);
		_currentHealthLabel.AddThemeColorOverride("font_color", BarTextColor);
		_currentHealthLabel.MouseFilter = MouseFilterEnum.Ignore;

		overlay.AddChild(_currentHealthLabel);
	}
}