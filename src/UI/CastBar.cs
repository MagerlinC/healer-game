using Godot;
using healerfantasy;
using SpellResource = healerfantasy.SpellResources.SpellResource;

/// <summary>
/// Cast bar shown while the player is channelling a spell.
/// Displays the spell's icon, name, remaining time, and a fill bar.
/// </summary>
public partial class CastBar : PanelContainer
{
	// ── colours ──────────────────────────────────────────────────────────────
	static readonly Color BgColor = new(0.10f, 0.08f, 0.07f, 0.95f);
	static readonly Color BorderColor = new(0.55f, 0.44f, 0.22f, 0.90f);
	static readonly Color NameColor = new(0.92f, 0.88f, 0.82f);
	static readonly Color TimeColor = new(0.95f, 0.84f, 0.50f);
	static readonly Color BarFillColor = new(0.80f, 0.62f, 0.12f); // warm amber
	static readonly Color BarBgColor = new(0.14f, 0.11f, 0.09f);

	// ── child node refs ───────────────────────────────────────────────────────
	TextureRect _iconRect;
	Label _nameLabel;
	Label _timeLabel;
	ProgressBar _bar;

	// ── cast state ────────────────────────────────────────────────────────────
	float _duration;
	float _remaining;
	bool _isCasting;

	// ── lifecycle ─────────────────────────────────────────────────────────────
	public override void _Ready()
	{
		BuildLayout();
		Visible = false;

		GlobalAutoLoad.SubscribeToSignal(
			nameof(Player.CastStarted),
			Callable.From((SpellResource spell) => StartCast(spell)));

		GlobalAutoLoad.SubscribeToSignal(
			nameof(Player.CastCancelled),
			Callable.From(StopCast));
	}

	public override void _Process(double delta)
	{
		if (!_isCasting) return;

		_remaining -= (float)delta;

		_bar.Value = Mathf.Clamp(1f - _remaining / _duration, 0f, 1f);
		_timeLabel.Text = $"{Mathf.Max(0f, _remaining):F1}s";

		if (_remaining <= 0f)
			StopCast();
	}

	// ── public API ────────────────────────────────────────────────────────────
	public void StartCast(SpellResource spell)
	{
		if (spell.CastTime == 0f) return;

		_duration = spell.CastTime;
		_remaining = spell.CastTime;
		_isCasting = true;

		_iconRect.Texture = spell.Icon;
		_nameLabel.Text = spell.Name ?? string.Empty;
		_timeLabel.Text = $"{_remaining:F1}s";
		_bar.Value = 0f;

		Visible = true;
	}

	// ── private ───────────────────────────────────────────────────────────────
	void StopCast()
	{
		_isCasting = false;
		Visible = false;
	}

	void BuildLayout()
	{
		// ── outer panel ───────────────────────────────────────────────────────
		var panelStyle = new StyleBoxFlat();
		panelStyle.BgColor = BgColor;
		panelStyle.SetCornerRadiusAll(5);
		panelStyle.SetBorderWidthAll(1);
		panelStyle.BorderColor = BorderColor;
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

		// ── icon ──────────────────────────────────────────────────────────────
		_iconRect = new TextureRect();
		_iconRect.CustomMinimumSize = new Vector2(24f, 24f);
		_iconRect.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
		_iconRect.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
		_iconRect.MouseFilter = MouseFilterEnum.Ignore;
		row.AddChild(_iconRect);

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
		barBg.BgColor = BarBgColor;
		barBg.SetCornerRadiusAll(3);
		_bar.AddThemeStyleboxOverride("background", barBg);

		var barFill = new StyleBoxFlat();
		barFill.BgColor = BarFillColor;
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
		_nameLabel.AddThemeColorOverride("font_color", NameColor);
		_nameLabel.MouseFilter = MouseFilterEnum.Ignore;

		overlay.AddChild(_nameLabel);

		// ── time label (right-aligned) ────────────────────────────────────────
		_timeLabel = new Label();
		_timeLabel.AnchorLeft = 1;
		_timeLabel.AnchorRight = 1;
		_timeLabel.AnchorTop = 0;
		_timeLabel.AnchorBottom = 1;

		_timeLabel.OffsetLeft = -40;
		_timeLabel.OffsetRight = -8;

		_timeLabel.HorizontalAlignment = HorizontalAlignment.Right;
		_timeLabel.VerticalAlignment = VerticalAlignment.Center;

		_timeLabel.AddThemeFontSizeOverride("font_size", 13);
		_timeLabel.AddThemeColorOverride("font_color", TimeColor);
		_timeLabel.MouseFilter = MouseFilterEnum.Ignore;

		overlay.AddChild(_timeLabel);
	}
}