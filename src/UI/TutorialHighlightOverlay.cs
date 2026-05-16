#nullable enable
using System;
using Godot;

namespace healerfantasy.UI;

/// <summary>
/// Reusable in-combat tutorial overlay.
///
/// When shown, the game is paused and the screen is darkened by a full-screen
/// shader that keeps up to two "spotlight" rectangles fully lit.  Each lit area
/// receives a gold border, and a text panel is placed to the <em>right</em> of
/// the primary spotlight (falling back to the left when space is tight).
/// A directional arrow inside the panel points toward the primary spotlight.
///
/// The overlay is dismissed only by pressing the designated input action
/// (e.g. "deflect", "dispel"). When no action key applies (movement mechanics
/// such as Detonation Zone), a "Got it!" button is shown instead.
///
/// Usage
/// ─────
///   TutorialHighlightOverlay.Show(
///       GetTree(),
///       "Deflect!",
///       "Press [F] just before the attack lands …",
///       deflectPanel.GetGlobalRect(),
///       "deflect",
///       onDismissed:        () => PlayerProgressStore.MarkDeflectTutorialSeen(),
///       secondarySpotlight: partyFrameRect            // optional second lit rect
///   );
///
/// Pass <c>Rect2.Zero</c> / <c>default</c> for spotlights that are not needed.
/// Pass <c>null</c> for <paramref name="dismissActionName"/> to show a "Got it!" button.
/// </summary>
public partial class TutorialHighlightOverlay : CanvasLayer
{
	// ── shader ────────────────────────────────────────────────────────────────
	// canvas_item shader on a full-screen ColorRect.
	// UV goes 0→1 across the viewport, matching Control.GetGlobalRect() pixel coords.
	// spot1 / spot2: (x, y, width, height) in viewport pixels. width ≤ 0 → off.
	const string ShaderSrc = """
	                         shader_type canvas_item;
	                         uniform vec4 spot1    = vec4(0.0, 0.0, 0.0, 0.0);
	                         uniform vec4 spot2    = vec4(0.0, 0.0, 0.0, 0.0);
	                         uniform float dark_alpha = 0.82;
	                         uniform vec2  vp_size = vec2(1280.0, 720.0);
	                         void fragment() {
	                             vec2 px = UV * vp_size;
	                             bool lit = false;
	                             if (spot1.z > 0.0)
	                                 lit = lit || (px.x >= spot1.x && px.x <= spot1.x + spot1.z &&
	                                               px.y >= spot1.y && px.y <= spot1.y + spot1.w);
	                             if (spot2.z > 0.0)
	                                 lit = lit || (px.x >= spot2.x && px.x <= spot2.x + spot2.z &&
	                                               px.y >= spot2.y && px.y <= spot2.y + spot2.w);
	                             COLOR = vec4(0.0, 0.0, 0.0, lit ? 0.0 : dark_alpha);
	                         }
	                         """;

	// ── visual constants ──────────────────────────────────────────────────────
	static readonly Color SpotlightBorderCol = new(0.95f, 0.80f, 0.15f, 1.00f); // gold
	static readonly Color PanelBg = new(0.07f, 0.06f, 0.06f, 0.97f);
	static readonly Color PanelBorderCol = new(0.65f, 0.52f, 0.28f, 1.00f);
	static readonly Color TitleColor = new(0.95f, 0.84f, 0.50f, 1.00f);
	static readonly Color BodyColor = new(0.82f, 0.78f, 0.72f, 1.00f);
	static readonly Color ArrowColor = new(0.95f, 0.84f, 0.50f, 1.00f);

	const float SpotlightPad = 14f; // extra space around highlighted control
	const float TextBoxWidth = 360f;
	const float TextGap = 20f; // horizontal gap between spotlight edge and text panel

	// ── global guard ──────────────────────────────────────────────────────────
	static bool _isShowing;

	// ── instance config ───────────────────────────────────────────────────────
	readonly string _title;
	readonly string _body;
	readonly Rect2 _primary; // primary spotlight (screen-space pixels)
	readonly Rect2 _secondary; // optional second spotlight (may be empty)
	readonly string? _dismissAction; // InputMap action; null → "Got it!" button
	readonly Action? _onDismissed;

	bool _dismissed;

	// Deferred panel positioning (applied in _Process once layout is complete).
	bool _positionPending;
	PanelContainer? _panelRef;
	Vector2 _vpSize;
	Rect2 _paddedPrimary;
	bool _textOnRight; // true = text is to the right of the spotlight

	// ── constructor ───────────────────────────────────────────────────────────
	TutorialHighlightOverlay(
		string title, string body,
		Rect2 primary, Rect2 secondary,
		string? dismissAction, Action? onDismissed)
	{
		_title = title;
		_body = body;
		_primary = primary;
		_secondary = secondary;
		_dismissAction = dismissAction;
		_onDismissed = onDismissed;

		Layer = 20;
		ProcessMode = ProcessModeEnum.Always;
	}

	// ── factory ───────────────────────────────────────────────────────────────

	/// <summary>
	/// Pause the game and display the tutorial overlay.
	/// Returns <c>null</c> if another overlay is already visible.
	/// </summary>
	/// <param name="tree">Active <see cref="SceneTree"/>.</param>
	/// <param name="title">Short heading.</param>
	/// <param name="body">Explanatory body text.</param>
	/// <param name="spotlight">
	/// Primary spotlight rect in viewport pixel coordinates
	/// (use <c>Control.GetGlobalRect()</c>). Pass <c>Rect2.Zero</c> for no spotlight.
	/// </param>
	/// <param name="dismissActionName">
	/// InputMap action the player must press to dismiss (e.g. <c>"deflect"</c>).
	/// Pass <c>null</c> to show a "Got it!" button instead.
	/// </param>
	/// <param name="onDismissed">Callback when dismissed.</param>
	/// <param name="secondarySpotlight">
	/// Optional second spotlight rect (e.g. the afflicted party frame for the
	/// Dispel tutorial). Pass <c>default</c> / <c>Rect2.Zero</c> to omit.
	/// </param>
	public static TutorialHighlightOverlay? Show(
		SceneTree tree,
		string title, string body,
		Rect2 spotlight,
		string? dismissActionName,
		Action? onDismissed = null,
		Rect2 secondarySpotlight = default)
	{
		if (_isShowing) return null;
		_isShowing = true;

		tree.Paused = true;

		var overlay = new TutorialHighlightOverlay(
			title, body, spotlight, secondarySpotlight, dismissActionName, onDismissed);
		tree.Root.AddChild(overlay);
		return overlay;
	}

	// ── lifecycle ─────────────────────────────────────────────────────────────

	public override void _Ready()
	{
		BuildUI();
	}

	public override void _ExitTree()
	{
		if (!_dismissed)
		{
			_dismissed = true;
			_isShowing = false;
			GetTree().Paused = false;
		}
	}

	public override void _Process(double delta)
	{
		if (_dismissed) return;

		if (_positionPending)
			TryApplyPanelPosition();

		if (_dismissAction != null && Input.IsActionJustPressed(_dismissAction))
			Dismiss();
	}

	// ── dismiss ───────────────────────────────────────────────────────────────

	void Dismiss()
	{
		if (_dismissed) return;
		_dismissed = true;
		_isShowing = false;
		GetTree().Paused = false;
		_onDismissed?.Invoke();
		QueueFree();
	}

	// ── UI construction ───────────────────────────────────────────────────────

	void BuildUI()
	{
		// Root control — blocks all mouse events to the world behind.
		var root = new Control();
		root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		root.MouseFilter = Control.MouseFilterEnum.Stop;
		root.ProcessMode = ProcessModeEnum.Always;
		AddChild(root);

		var vpSize = GetViewport().GetVisibleRect().Size;
		var hasPrimary = _primary.Size.X > 1f && _primary.Size.Y > 1f;
		var hasSecondary = _secondary.Size.X > 1f && _secondary.Size.Y > 1f;

		// Pad the spotlight rects (breathing room around each element).
		var paddedPrimary = hasPrimary ? Pad(_primary) : new Rect2();
		var paddedSecondary = hasSecondary ? Pad(_secondary) : new Rect2();

		// ── Shader overlay ────────────────────────────────────────────────────
		AddShaderOverlay(root, vpSize, paddedPrimary, paddedSecondary);

		// ── Gold borders around each spotlight ────────────────────────────────
		if (hasPrimary) AddSpotlightBorder(root, paddedPrimary);
		if (hasSecondary) AddSpotlightBorder(root, paddedSecondary);

		// ── Text panel ────────────────────────────────────────────────────────
		// When there is a primary spotlight, try to place text to the RIGHT of it.
		// Fall back to left if the text would go off screen.
		var textOnRight = false;
		if (hasPrimary)
		{
			var rightX = paddedPrimary.End.X + TextGap;
			textOnRight = rightX + TextBoxWidth <= vpSize.X - 8f;
		}

		var panel = BuildTextPanel(root, hasPrimary, textOnRight);
		_positionPending = true;
		_panelRef = panel;
		_vpSize = vpSize;
		_paddedPrimary = paddedPrimary;
		_textOnRight = textOnRight;
	}

	void AddShaderOverlay(Control parent, Vector2 vpSize, Rect2 spot1, Rect2 spot2)
	{
		var shader = new Shader { Code = ShaderSrc };
		var mat = new ShaderMaterial { Shader = shader };

		mat.SetShaderParameter("vp_size", vpSize);
		mat.SetShaderParameter("spot1", ToVec4(spot1));
		mat.SetShaderParameter("spot2", ToVec4(spot2));

		var rect = new ColorRect();
		rect.Color = new Color(0f, 0f, 0f, 0f); // shader drives actual output
		rect.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		rect.MouseFilter = Control.MouseFilterEnum.Ignore;
		rect.Material = mat;
		parent.AddChild(rect);
	}

	static void AddSpotlightBorder(Control parent, Rect2 rect)
	{
		var style = new StyleBoxFlat();
		style.BgColor = new Color(0f, 0f, 0f, 0f); // transparent fill
		style.SetBorderWidthAll(3);
		style.BorderColor = SpotlightBorderCol;
		style.SetCornerRadiusAll(6);

		var panel = new PanelContainer();
		panel.Position = rect.Position;
		panel.Size = rect.Size;
		panel.AddThemeStyleboxOverride("panel", style);
		panel.MouseFilter = Control.MouseFilterEnum.Ignore;
		parent.AddChild(panel);
	}

	PanelContainer BuildTextPanel(Control parent, bool hasSpotlight, bool textOnRight)
	{
		var panelStyle = new StyleBoxFlat();
		panelStyle.BgColor = PanelBg;
		panelStyle.SetCornerRadiusAll(10);
		panelStyle.SetBorderWidthAll(2);
		panelStyle.BorderColor = PanelBorderCol;
		panelStyle.ContentMarginLeft = panelStyle.ContentMarginRight = 22f;
		panelStyle.ContentMarginTop = panelStyle.ContentMarginBottom = 18f;

		var panel = new PanelContainer();
		panel.CustomMinimumSize = new Vector2(TextBoxWidth, 0f);
		panel.AddThemeStyleboxOverride("panel", panelStyle);
		panel.MouseFilter = Control.MouseFilterEnum.Pass;
		parent.AddChild(panel);

		var vbox = new VBoxContainer();
		vbox.AddThemeConstantOverride("separation", 10);
		panel.AddChild(vbox);

		// Title
		var titleLabel = new Label();
		titleLabel.Text = _title;
		titleLabel.HorizontalAlignment = HorizontalAlignment.Center;
		titleLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		titleLabel.AddThemeFontSizeOverride("font_size", 18);
		titleLabel.AddThemeColorOverride("font_color", TitleColor);
		vbox.AddChild(titleLabel);

		var sep = new HSeparator();
		sep.AddThemeColorOverride("color", new Color(0.50f, 0.40f, 0.22f, 0.55f));
		vbox.AddChild(sep);

		// Body
		var bodyLabel = new Label();
		bodyLabel.Text = _body;
		bodyLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		bodyLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		bodyLabel.AddThemeFontSizeOverride("font_size", 14);
		bodyLabel.AddThemeColorOverride("font_color", BodyColor);
		vbox.AddChild(bodyLabel);

		// Arrow pointing toward the primary spotlight.
		// Text to the right → arrow points left (←). Text to the left → right (→).
		if (hasSpotlight)
		{
			var arrowLabel = new Label();
			arrowLabel.Text = textOnRight ? "←" : "→";
			arrowLabel.HorizontalAlignment = textOnRight
				? HorizontalAlignment.Left
				: HorizontalAlignment.Right;
			arrowLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
			arrowLabel.AddThemeFontSizeOverride("font_size", 24);
			arrowLabel.AddThemeColorOverride("font_color", ArrowColor);
			vbox.AddChild(arrowLabel);
		}

		// "Got it!" button — only when there is no action key dismiss.
		if (_dismissAction == null)
		{
			var btn = new Button();
			btn.Text = "Got it!";
			btn.CustomMinimumSize = new Vector2(140f, 36f);
			btn.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
			btn.ProcessMode = ProcessModeEnum.Always;
			btn.Pressed += Dismiss;

			var normal = MakeButtonStyle(new Color(0.12f, 0.17f, 0.12f), new Color(0.30f, 0.65f, 0.28f));
			var hover = MakeButtonStyle(new Color(0.18f, 0.24f, 0.16f), new Color(0.40f, 0.80f, 0.35f));
			btn.AddThemeStyleboxOverride("normal", normal);
			btn.AddThemeStyleboxOverride("hover", hover);
			btn.AddThemeStyleboxOverride("pressed", normal);
			btn.AddThemeStyleboxOverride("focus", normal);
			btn.AddThemeFontSizeOverride("font_size", 15);
			btn.AddThemeColorOverride("font_color", new Color(0.90f, 0.87f, 0.83f));
			vbox.AddChild(btn);
		}

		return panel;
	}

	// ── deferred positioning ──────────────────────────────────────────────────

	void TryApplyPanelPosition()
	{
		if (_panelRef == null) return;
		var sz = _panelRef.Size;
		if (sz.X < 1f || sz.Y < 1f) return; // not laid out yet

		_positionPending = false;

		var hasPrimary = _paddedPrimary.Size.X > 1f;
		float panelX, panelY;

		if (!hasPrimary)
		{
			// No spotlight — centre the panel on screen.
			panelX = (_vpSize.X - sz.X) * 0.5f;
			panelY = (_vpSize.Y - sz.Y) * 0.5f;
		}
		else if (_textOnRight)
		{
			panelX = _paddedPrimary.End.X + TextGap;
			panelY = _paddedPrimary.GetCenter().Y - sz.Y * 0.5f;
		}
		else
		{
			// Fall back: text to the LEFT of the spotlight.
			panelX = _paddedPrimary.Position.X - sz.X - TextGap;
			panelY = _paddedPrimary.GetCenter().Y - sz.Y * 0.5f;
		}

		panelX = Mathf.Clamp(panelX, 8f, _vpSize.X - sz.X - 8f);
		panelY = Mathf.Clamp(panelY, 8f, _vpSize.Y - sz.Y - 8f);

		_panelRef.Position = new Vector2(panelX, panelY);
	}

	// ── helpers ───────────────────────────────────────────────────────────────

	static Rect2 Pad(Rect2 r)
	{
		return new Rect2(
			r.Position - new Vector2(SpotlightPad, SpotlightPad),
			r.Size + new Vector2(SpotlightPad * 2f, SpotlightPad * 2f));
	}

	static Vector4 ToVec4(Rect2 r)
	{
		return r.Size.X > 0f
			? new Vector4(r.Position.X, r.Position.Y, r.Size.X, r.Size.Y)
			: Vector4.Zero;
	}

	static StyleBoxFlat MakeButtonStyle(Color bg, Color border)
	{
		var s = new StyleBoxFlat();
		s.BgColor = bg;
		s.SetCornerRadiusAll(6);
		s.SetBorderWidthAll(2);
		s.BorderColor = border;
		s.ContentMarginLeft = s.ContentMarginRight = 18f;
		s.ContentMarginTop = s.ContentMarginBottom = 8f;
		return s;
	}
}