using Godot;
using SpellResource = healerfantasy.SpellResources.SpellResource;

/// <summary>
/// Shared cast-bar widget: icon + spell-name overlay on a fill bar + countdown.
/// Subclasses wire their own signal subscriptions in <c>_Ready</c> — this base
/// only handles layout, ticking, and the public <see cref="StartCast"/> /
/// <see cref="StopCast"/> API.
/// </summary>
public abstract partial class CastBarBase : PanelContainer
{
	// ── colours — overridable by subclasses ──────────────────────────────────
	protected virtual Color BgColor      => new(0.10f, 0.08f, 0.07f, 0.95f);
	protected virtual Color BorderColor  => new(0.55f, 0.44f, 0.22f, 0.90f);
	protected virtual Color NameColor    => new(0.92f, 0.88f, 0.82f);
	protected virtual Color TimeColor    => new(0.95f, 0.84f, 0.50f);
	protected virtual Color BarFillColor => new(0.80f, 0.62f, 0.12f); // warm amber
	protected virtual Color BarBgColor   => new(0.14f, 0.11f, 0.09f);

	// ── child node refs ───────────────────────────────────────────────────────
	TextureRect _iconRect;
	Label       _nameLabel;
	Label       _timeLabel;
	ProgressBar _bar;

	// ── exposed style boxes — subclasses may animate these ────────────────────
	protected StyleBoxFlat PanelStyle;
	protected StyleBoxFlat BarFillStyle;

	// ── cast state ────────────────────────────────────────────────────────────
	float _duration;
	float _remaining;
	bool  _isCasting;
	/// <summary>
	/// When true the bar drains from 1 → 0 (channel / reverse mode) instead of
	/// filling from 0 → 1 (normal cast). Set by <see cref="StartChannel"/>.
	/// </summary>
	bool _isChannel;

	// ── subclass hooks ────────────────────────────────────────────────────────

	/// <summary>
	/// Called every frame while a cast is in progress.
	/// <paramref name="progress"/> runs 0 → 1 over the full cast duration.
	/// Override in subclasses to animate colours, border width, etc.
	/// </summary>
	protected virtual void OnCastVisualUpdate(float progress) { }

	// ── lifecycle ─────────────────────────────────────────────────────────────
	public override void _Ready()
	{
		BuildLayout();
		Visible = false;
	}

	public override void _Process(double delta)
	{
		if (!_isCasting) return;

		_remaining -= (float)delta;

		// In channel mode the bar drains 1 → 0; in cast mode it fills 0 → 1.
		var progress = Mathf.Clamp(1f - _remaining / _duration, 0f, 1f);
		_bar.Value      = _isChannel ? 1f - progress : progress;
		_timeLabel.Text = $"{Mathf.Max(0f, _remaining):F1}s";

		OnCastVisualUpdate(progress);

		if (_remaining <= 0f)
			StopCast();
	}

	// ── public API ────────────────────────────────────────────────────────────

	/// <summary>
	/// Begin displaying the cast bar.
	/// Pass the spell whose icon and name should be shown, and the true timer
	/// length (after any cast-speed modifiers have been applied).
	/// Does nothing if <paramref name="adjustedDuration"/> is zero or negative.
	/// </summary>
	public void StartCast(SpellResource spell, float adjustedDuration)
	{
		if (adjustedDuration <= 0f) return;

		_duration  = adjustedDuration;
		_remaining = adjustedDuration;
		_isCasting = true;

		_iconRect.Texture = spell?.Icon;
		_nameLabel.Text   = spell?.Name ?? string.Empty;
		_timeLabel.Text   = $"{_remaining:F1}s";
		_bar.Value        = 0f;

		Visible = true;
	}

	/// <summary>
	/// Begin displaying the cast bar using a plain string instead of a
	/// <see cref="SpellResource"/> — useful for boss casts that have no icon.
	/// </summary>
	public void StartCast(string spellName, Texture2D icon, float duration)
	{
		if (duration <= 0f) return;

		_duration  = duration;
		_remaining = duration;
		_isCasting = true;

		_iconRect.Texture = icon;
		_nameLabel.Text   = spellName;
		_timeLabel.Text   = $"{_remaining:F1}s";
		_bar.Value        = 0f;

		Visible = true;
	}

	/// <summary>
	/// Begin displaying the bar in <em>channel</em> (reverse) mode.
	/// The bar starts full and drains to empty over <paramref name="duration"/> seconds,
	/// mirroring the WoW-style channel bar that counts down to zero.
	/// </summary>
	public void StartChannel(string spellName, Texture2D icon, float duration)
	{
		if (duration <= 0f) return;

		_isChannel = true;
		_duration  = duration;
		_remaining = duration;
		_isCasting = true;

		_iconRect.Texture = icon;
		_nameLabel.Text   = spellName;
		_timeLabel.Text   = $"{_remaining:F1}s";
		_bar.Value        = 1f; // starts full, drains to 0

		Visible = true;
	}

	public void StopCast()
	{
		_isCasting = false;
		_isChannel = false;
		Visible    = false;
	}

	// ── layout ────────────────────────────────────────────────────────────────
	void BuildLayout()
	{
		PanelStyle = new StyleBoxFlat();
		PanelStyle.BgColor = BgColor;
		PanelStyle.SetCornerRadiusAll(5);
		PanelStyle.SetBorderWidthAll(1);
		PanelStyle.BorderColor        = BorderColor;
		PanelStyle.ContentMarginLeft  = 8f;
		PanelStyle.ContentMarginRight = 8f;
		PanelStyle.ContentMarginTop   = 6f;
		PanelStyle.ContentMarginBottom = 6f;
		AddThemeStyleboxOverride("panel", PanelStyle);

		MouseFilter = MouseFilterEnum.Ignore;

		var row = new HBoxContainer();
		row.AddThemeConstantOverride("separation", 8);
		AddChild(row);

		// Icon
		_iconRect                    = new TextureRect();
		_iconRect.CustomMinimumSize  = new Vector2(24f, 24f);
		_iconRect.ExpandMode         = TextureRect.ExpandModeEnum.IgnoreSize;
		_iconRect.StretchMode        = TextureRect.StretchModeEnum.KeepAspectCentered;
		_iconRect.MouseFilter        = MouseFilterEnum.Ignore;
		row.AddChild(_iconRect);

		// Overlay container (bar + text)
		var overlay                  = new Control();
		overlay.SizeFlagsHorizontal  = SizeFlags.ExpandFill;
		overlay.CustomMinimumSize    = new Vector2(0f, 24f);
		row.AddChild(overlay);

		// Progress bar
		_bar = new ProgressBar();
		_bar.AnchorLeft = 0; _bar.AnchorRight  = 1;
		_bar.AnchorTop  = 0; _bar.AnchorBottom = 1;
		_bar.OffsetLeft = _bar.OffsetRight = _bar.OffsetTop = _bar.OffsetBottom = 0;
		_bar.ShowPercentage = false;
		_bar.MinValue       = 0f;
		_bar.MaxValue       = 1f;
		_bar.Value          = 0f;
		_bar.MouseFilter    = MouseFilterEnum.Ignore;

		var barBg = new StyleBoxFlat();
		barBg.BgColor = BarBgColor;
		barBg.SetCornerRadiusAll(3);
		_bar.AddThemeStyleboxOverride("background", barBg);

		BarFillStyle = new StyleBoxFlat();
		BarFillStyle.BgColor = BarFillColor;
		BarFillStyle.SetCornerRadiusAll(3);
		_bar.AddThemeStyleboxOverride("fill", BarFillStyle);

		overlay.AddChild(_bar);

		// Spell-name label
		_nameLabel             = new Label();
		_nameLabel.AnchorLeft  = 0; _nameLabel.AnchorRight  = 1;
		_nameLabel.AnchorTop   = 0; _nameLabel.AnchorBottom = 1;
		_nameLabel.OffsetLeft  = 8; _nameLabel.OffsetRight  = -40;
		_nameLabel.VerticalAlignment = VerticalAlignment.Center;
		_nameLabel.AddThemeFontSizeOverride("font_size", 14);
		_nameLabel.AddThemeColorOverride("font_color", NameColor);
		_nameLabel.MouseFilter = MouseFilterEnum.Ignore;
		overlay.AddChild(_nameLabel);

		// Countdown label
		_timeLabel                      = new Label();
		_timeLabel.AnchorLeft           = 1; _timeLabel.AnchorRight  = 1;
		_timeLabel.AnchorTop            = 0; _timeLabel.AnchorBottom = 1;
		_timeLabel.OffsetLeft           = -40; _timeLabel.OffsetRight = -8;
		_timeLabel.HorizontalAlignment  = HorizontalAlignment.Right;
		_timeLabel.VerticalAlignment    = VerticalAlignment.Center;
		_timeLabel.AddThemeFontSizeOverride("font_size", 13);
		_timeLabel.AddThemeColorOverride("font_color", TimeColor);
		_timeLabel.MouseFilter          = MouseFilterEnum.Ignore;
		overlay.AddChild(_timeLabel);
	}
}
