using System.Collections.Generic;
using System.Linq;
using Godot;
using healerfantasy.CombatLog;

/// <summary>
/// A compact panel showing healing-per-second or damage-per-second for each
/// tracked character, with a relative bar fill and a per-ability tooltip on hover.
///
/// Layout (bottom-up):
///   Title label
///   One row per tracked character, sorted highest→lowest rate:
///     [██████░░] Healer   42/s
///
/// Hover a row to see a per-ability breakdown tooltip.
/// </summary>
public partial class CombatMeter : PanelContainer
{
	public enum MeterType
	{
		Healing,
		Damage
	}

	// ── colours ───────────────────────────────────────────────────────────────
	static readonly Color PanelBg = new(0.10f, 0.08f, 0.07f, 0.92f);
	static readonly Color PanelBorder = new(0.40f, 0.33f, 0.22f, 0.90f);
	static readonly Color TitleColor = new(0.80f, 0.72f, 0.50f);
	static readonly Color LabelColor = new(0.90f, 0.87f, 0.83f);
	static readonly Color RowBg = new(0.14f, 0.11f, 0.10f, 1f);
	static readonly Color RowHoverBg = new(0.22f, 0.18f, 0.14f, 1f);
	static readonly Color HealFill = new(0.25f, 0.68f, 0.30f, 0.75f);
	static readonly Color DamageFill = new(0.80f, 0.22f, 0.18f, 0.75f);
	static readonly Color TooltipBg = new(0.10f, 0.08f, 0.07f, 0.96f);
	static readonly Color TooltipBorder = new(0.55f, 0.44f, 0.22f, 0.90f);

	// ── config ────────────────────────────────────────────────────────────────
	const double RefreshInterval = 0.5;

	// ── state ─────────────────────────────────────────────────────────────────
	readonly MeterType _type;
	readonly List<string> _trackedNames = new();

	// Per-row cached node refs so we can update in-place and MoveChild to re-sort.
	readonly Dictionary<string, RowRefs> _rowCache = new();

	VBoxContainer _rowContainer;
	double _refreshTimer;

	// Tooltip
	CanvasLayer _tooltipLayer;
	PanelContainer _tooltipPanel;
	Label _tooltipLabel;
	string _hoveredName; // null when no row is hovered

	// ── types ─────────────────────────────────────────────────────────────────
	record RowRefs(Control Outer, ProgressBar Bar, Label NameLbl, Label ValueLbl, StyleBoxFlat BgStyle);

	// ── constructor ───────────────────────────────────────────────────────────
	public CombatMeter(MeterType type)
	{
		_type = type;
	}

	// ── public API ────────────────────────────────────────────────────────────
	/// <summary>
	/// Add a character name to track. Safe to call before or after <c>_Ready</c>:
	/// if the meter is already in the scene tree the row is created immediately,
	/// otherwise it is deferred to <see cref="_Ready"/> via <see cref="BuildRows"/>.
	/// </summary>
	public void RegisterCharacter(string name)
	{
		if (_trackedNames.Contains(name)) return;
		_trackedNames.Add(name);

		// If _Ready has already run, create the row right now.
		if (_rowContainer != null)
		{
			var refs = CreateRow(name, _type == MeterType.Healing ? HealFill : DamageFill);
			_rowCache[name] = refs;
			_rowContainer.AddChild(refs.Outer);
		}
	}

	// ── lifecycle ─────────────────────────────────────────────────────────────
	public override void _Ready()
	{
		BuildPanel();
		BuildRows();
		BuildTooltip();
	}

	public override void _Process(double delta)
	{
		_refreshTimer += delta;
		if (_refreshTimer >= RefreshInterval)
		{
			_refreshTimer = 0;
			Refresh();
		}

		if (_hoveredName != null)
			PositionTooltip();
	}

	// ── layout builders ───────────────────────────────────────────────────────
	void BuildPanel()
	{
		CustomMinimumSize = new Vector2(185f, 0f);

		var style = new StyleBoxFlat();
		style.BgColor = PanelBg;
		style.SetCornerRadiusAll(5);
		style.SetBorderWidthAll(1);
		style.BorderColor = PanelBorder;
		style.ContentMarginLeft = 6f;
		style.ContentMarginRight = 6f;
		style.ContentMarginTop = 5f;
		style.ContentMarginBottom = 5f;
		AddThemeStyleboxOverride("panel", style);

		MouseFilter = MouseFilterEnum.Stop;

		var vbox = new VBoxContainer();
		vbox.AddThemeConstantOverride("separation", 3);
		AddChild(vbox);

		// Title
		var title = new Label();
		title.Text = _type == MeterType.Healing ? "HEALING" : "DAMAGE";
		title.HorizontalAlignment = HorizontalAlignment.Center;
		title.AddThemeFontSizeOverride("font_size", 11);
		title.AddThemeColorOverride("font_color", TitleColor);
		title.MouseFilter = MouseFilterEnum.Ignore;
		vbox.AddChild(title);

		// Separator
		var sep = new HSeparator();
		sep.MouseFilter = MouseFilterEnum.Ignore;
		var sepStyle = new StyleBoxFlat();
		sepStyle.BgColor = PanelBorder;
		sepStyle.ContentMarginTop = 1f;
		sep.AddThemeStyleboxOverride("separator", sepStyle);
		vbox.AddChild(sep);

		// Row container
		_rowContainer = new VBoxContainer();
		_rowContainer.AddThemeConstantOverride("separation", 2);
		_rowContainer.MouseFilter = MouseFilterEnum.Pass;
		vbox.AddChild(_rowContainer);
	}

	void BuildRows()
	{
		var fillColor = _type == MeterType.Healing ? HealFill : DamageFill;

		foreach (var name in _trackedNames)
		{
			var refs = CreateRow(name, fillColor);
			_rowCache[name] = refs;
			_rowContainer.AddChild(refs.Outer);
		}
	}

	RowRefs CreateRow(string characterName, Color fillColor)
	{
		// Outer: a fixed-height Control that stacks a ProgressBar + label overlay.
		var outer = new Control();
		outer.CustomMinimumSize = new Vector2(0f, 20f);
		outer.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		outer.MouseFilter = MouseFilterEnum.Stop;

		// Background ProgressBar (fills proportionally to rate)
		var bar = new ProgressBar();
		bar.SetAnchorsPreset(LayoutPreset.FullRect);
		bar.MinValue = 0f;
		bar.MaxValue = 1f;
		bar.Value = 0f;
		bar.ShowPercentage = false;
		bar.MouseFilter = MouseFilterEnum.Ignore;

		var bgStyle = new StyleBoxFlat();
		bgStyle.BgColor = RowBg;
		bar.AddThemeStyleboxOverride("background", bgStyle);

		var fillStyle = new StyleBoxFlat();
		fillStyle.BgColor = fillColor;
		bar.AddThemeStyleboxOverride("fill", fillStyle);

		outer.AddChild(bar);

		// Label overlay (HBoxContainer anchored to full rect)
		var hbox = new HBoxContainer();
		hbox.SetAnchorsPreset(LayoutPreset.FullRect);
		hbox.AddThemeConstantOverride("separation", 0);
		hbox.MouseFilter = MouseFilterEnum.Ignore;

		var nameLabel = new Label();
		nameLabel.Text = " " + characterName;
		nameLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		nameLabel.VerticalAlignment = VerticalAlignment.Center;
		nameLabel.AddThemeFontSizeOverride("font_size", 11);
		nameLabel.AddThemeColorOverride("font_color", LabelColor);
		nameLabel.MouseFilter = MouseFilterEnum.Ignore;
		hbox.AddChild(nameLabel);

		var valueLabel = new Label();
		valueLabel.Text = "0/s ";
		valueLabel.VerticalAlignment = VerticalAlignment.Center;
		valueLabel.HorizontalAlignment = HorizontalAlignment.Right;
		valueLabel.AddThemeFontSizeOverride("font_size", 11);
		valueLabel.AddThemeColorOverride("font_color", LabelColor);
		valueLabel.MouseFilter = MouseFilterEnum.Ignore;
		hbox.AddChild(valueLabel);

		outer.AddChild(hbox);

		// Hover highlight
		outer.MouseEntered += () => OnRowEntered(characterName, bgStyle);
		outer.MouseExited += () => OnRowExited(bgStyle);

		return new RowRefs(outer, bar, nameLabel, valueLabel, bgStyle);
	}

	void BuildTooltip()
	{
		_tooltipLayer = new CanvasLayer();
		_tooltipLayer.Layer = 50;
		AddChild(_tooltipLayer);

		var panelStyle = new StyleBoxFlat();
		panelStyle.BgColor = TooltipBg;
		panelStyle.SetCornerRadiusAll(4);
		panelStyle.SetBorderWidthAll(1);
		panelStyle.BorderColor = TooltipBorder;
		panelStyle.ContentMarginLeft = 8f;
		panelStyle.ContentMarginRight = 8f;
		panelStyle.ContentMarginTop = 5f;
		panelStyle.ContentMarginBottom = 5f;

		_tooltipPanel = new PanelContainer();
		_tooltipPanel.AddThemeStyleboxOverride("panel", panelStyle);
		_tooltipPanel.Visible = false;
		_tooltipPanel.MouseFilter = MouseFilterEnum.Ignore;
		_tooltipLayer.AddChild(_tooltipPanel);

		_tooltipLabel = new Label();
		_tooltipLabel.AddThemeFontSizeOverride("font_size", 11);
		_tooltipLabel.AddThemeColorOverride("font_color", LabelColor);
		_tooltipLabel.MouseFilter = MouseFilterEnum.Ignore;
		_tooltipPanel.AddChild(_tooltipLabel);
	}

	// ── refresh ───────────────────────────────────────────────────────────────
	void Refresh()
	{
		var now = Time.GetTicksMsec() / 1000.0;
		var type = _type == MeterType.Healing ? CombatEventType.Healing : CombatEventType.Damage;
		var rates = CombatLog.GetRatePerSource(type, now);

		// Sort tracked names by rate descending
		var sorted = _trackedNames
			.Select(n => (name: n, rate: rates.TryGetValue(n, out var r) ? r : 0f))
			.OrderByDescending(x => x.rate)
			.ToArray();

		var maxRate = sorted.Length > 0 && sorted[0].rate > 0f ? sorted[0].rate : 1f;

		for (var i = 0; i < sorted.Length; i++)
		{
			var (name, rate) = sorted[i];
			if (!_rowCache.TryGetValue(name, out var row)) continue;

			row.Bar.MaxValue = maxRate;
			row.Bar.Value = rate;
			row.ValueLbl.Text = rate >= 0.5f ? $"{rate:F0}/s " : "0/s ";

			_rowContainer.MoveChild(row.Outer, i);
		}
	}

	// ── hover / tooltip ───────────────────────────────────────────────────────
	void OnRowEntered(string name, StyleBoxFlat bgStyle)
	{
		bgStyle.BgColor = RowHoverBg;
		_hoveredName = name;
		UpdateTooltipContent(name);
		_tooltipPanel.Visible = true;
		PositionTooltip();
	}

	void OnRowExited(StyleBoxFlat bgStyle)
	{
		bgStyle.BgColor = RowBg;
		_hoveredName = null;
		_tooltipPanel.Visible = false;
	}

	void UpdateTooltipContent(string sourceName)
	{
		var now = Time.GetTicksMsec() / 1000.0;
		var type = _type == MeterType.Healing ? CombatEventType.Healing : CombatEventType.Damage;
		var breakdown = CombatLog.GetBreakdown(sourceName, type, now);

		if (breakdown.Count == 0)
		{
			_tooltipLabel.Text = $"{sourceName}\nNo data in last {CombatLog.DefaultWindow:F0}s";
			return;
		}

		// Sort abilities by total descending, compute grand total for %
		var entries = breakdown
			.OrderByDescending(kv => kv.Value)
			.ToArray();

		var grandTotal = entries.Sum(kv => kv.Value);
		var label = _type == MeterType.Healing ? "HPS" : "DPS";

		var lines = new System.Text.StringBuilder();
		lines.AppendLine($"{sourceName}  ({label}, last {CombatLog.DefaultWindow:F0}s)");
		lines.AppendLine("──────────────────────");

		foreach (var (ability, total) in entries)
		{
			var pct = grandTotal > 0f ? total / grandTotal * 100f : 0f;
			lines.AppendLine($"{ability,-16} {total,6:F0}  ({pct:F0}%)");
		}

		_tooltipLabel.Text = lines.ToString().TrimEnd();
	}

	void PositionTooltip()
	{
		var mouse = GetViewport().GetMousePosition();
		var tipSize = _tooltipPanel.Size;
		var viewport = GetViewport().GetVisibleRect().Size;

		var x = mouse.X + 14f;
		var y = mouse.Y - 10f;

		if (x + tipSize.X > viewport.X) x = mouse.X - tipSize.X - 10f;
		if (y + tipSize.Y > viewport.Y) y = viewport.Y - tipSize.Y - 4f;

		_tooltipPanel.Position = new Vector2(x, y);
	}
}