using Godot;
using healerfantasy;

/// <summary>
/// The four party health frames rendered at the bottom of the screen.
///
/// Responsibilities
/// ────────────────
/// • Builds and owns all frame UI nodes (health bars, shield bars, effect rows).
/// • Subscribes to HealthChanged / ShieldChanged / EffectApplied / EffectRemoved
///   via GlobalAutoLoad so GameUI doesn't have to know about the signal plumbing.
/// • Exposes <see cref="BindCharacter"/> so World can register Character nodes for
///   hover-target resolution.
/// • Exposes <see cref="GetHoveredCharacter"/> for Player spell targeting.
///
/// Layout
/// ──────
/// PartyFrames is a FullRect pass-through Control — it fills the CanvasLayer
/// anchor entirely but never intercepts mouse events itself.  The inner
/// HBoxContainer is centred with a downward offset, matching the previous layout.
/// </summary>
public partial class PartyFrames : Control
{
	// ── per-member config ─────────────────────────────────────────────────────
	static readonly (string Name, Color BarColor, float MaxHp)[] MemberDefs =
	{
		("Templar", new Color(0.88f, 0.30f, 0.50f), 150f), // rose-red
		("Healer", new Color(0.35f, 0.78f, 0.22f), 80f), // poison-green
		("Assassin", new Color(0.85f, 0.78f, 0.15f), 100f), // golden-yellow
		("Wizard", new Color(0.20f, 0.50f, 0.95f), 70f) // sapphire-blue
	};

	static readonly Color BorderDefault = new(0.32f, 0.26f, 0.26f);
	static readonly Color BorderHovered = new(0.90f, 0.80f, 0.20f);

	// ── node references ───────────────────────────────────────────────────────
	readonly ProgressBar[] _bars = new ProgressBar[4];
	readonly ProgressBar[] _shieldBars = new ProgressBar[4];
	readonly PanelContainer[] _panels = new PanelContainer[4];
	readonly StyleBoxFlat[] _panelStyles = new StyleBoxFlat[4];
	readonly HBoxContainer[] _effectBars = new HBoxContainer[4];
	readonly Character[] _characters = new Character[4];

	// ── lifecycle ─────────────────────────────────────────────────────────────
	public override void _Ready()
	{
		// Fill the parent anchor entirely, but never swallow mouse events.
		MouseFilter = MouseFilterEnum.Pass;

		var hbox = new HBoxContainer();
		hbox.MouseFilter = MouseFilterEnum.Pass;
		hbox.AddThemeConstantOverride("separation", 6);
		hbox.SetAnchorsPreset(LayoutPreset.Center);
		hbox.Position += new Vector2(0, 250); // push down from centre
		hbox.GrowHorizontal = GrowDirection.Both;
		hbox.GrowVertical = GrowDirection.Both;
		AddChild(hbox);

		for (var i = 0; i < MemberDefs.Length; i++)
		{
			var (name, barColor, maxHp) = MemberDefs[i];
			var wrapper = BuildFrame(
				name, barColor, maxHp,
				out _bars[i], out _shieldBars[i],
				out _panelStyles[i], out _panels[i], out _effectBars[i]);

			// Capture loop-local copies to avoid closure-capture bug.
			var style = _panelStyles[i];
			var panel = _panels[i];
			panel.MouseEntered += () => style.BorderColor = BorderHovered;
			panel.MouseExited += () => style.BorderColor = BorderDefault;

			hbox.AddChild(wrapper);
		}

		// ── party signal subscriptions ────────────────────────────────────────
		GlobalAutoLoad.SubscribeToPartySignal(
			"HealthChanged",
			slot => Callable.From((float current, float max) => SetHealth(slot, current, max)));

		GlobalAutoLoad.SubscribeToPartySignal(
			"ShieldChanged",
			slot => Callable.From((float shield, float maxHp) => SetShield(slot, shield, maxHp)));

		GlobalAutoLoad.SubscribeToPartySignal(
			"EffectApplied",
			slot => Callable.From((CharacterEffect effect) => ShowEffectIndicator(slot, effect)));

		GlobalAutoLoad.SubscribeToPartySignal(
			"EffectRemoved",
			slot => Callable.From((string id) => HideEffectIndicator(slot, id)));
	}

	// ── public API ────────────────────────────────────────────────────────────

	/// <summary>
	/// Register a Character so that hovering its frame resolves to the right
	/// game object during spell targeting.
	/// </summary>
	public void BindCharacter(int slot, Character character)
	{
		if (slot < 0 || slot >= _characters.Length) return;
		_characters[slot] = character;
	}

	/// <summary>
	/// Returns the Character whose frame the mouse cursor is currently over,
	/// or null if the cursor is not hovering any frame.
	/// </summary>
	public Character GetHoveredCharacter()
	{
		var mousePos = GetViewport().GetMousePosition();
		for (var i = 0; i < _panels.Length; i++)
		{
			if (_panels[i] != null && _panels[i].GetGlobalRect().HasPoint(mousePos))
				return _characters[i];
		}

		return null;
	}

	// ── private signal handlers ───────────────────────────────────────────────

	void SetHealth(int slot, float current, float max)
	{
		if (slot < 0 || slot >= _bars.Length) return;
		_bars[slot].MaxValue = max;
		_bars[slot].Value = current;
	}

	void SetShield(int slot, float shield, float maxHp)
	{
		if (slot < 0 || slot >= _shieldBars.Length) return;
		_shieldBars[slot].MaxValue = maxHp;
		_shieldBars[slot].Value = shield;
	}

	void ShowEffectIndicator(int slot, CharacterEffect effect)
	{
		if (slot < 0 || slot >= _effectBars.Length) return;
		HideEffectIndicator(slot, effect.EffectId); // remove stale indicator if refreshed
		_effectBars[slot].AddChild(new EffectIndicator(effect));
	}

	void HideEffectIndicator(int slot, string effectId)
	{
		if (slot < 0 || slot >= _effectBars.Length) return;
		foreach (var child in _effectBars[slot].GetChildren())
		{
			if (child is EffectIndicator ind && ind.CharacterEffect.EffectId == effectId)
			{
				ind.QueueFree();
				return;
			}
		}
	}

	// ── frame builder ─────────────────────────────────────────────────────────

	/// <summary>
	/// Builds one party frame: an effects row stacked above a health panel.
	/// Returns the outermost wrapper node to be added to the HBoxContainer.
	/// </summary>
	static Control BuildFrame(
		string name, Color barColor, float maxHp,
		out ProgressBar bar,
		out ProgressBar shieldBar,
		out StyleBoxFlat panelStyle,
		out PanelContainer panel,
		out HBoxContainer effectsBar)
	{
		// VBox: effects row (top) + health panel (bottom)
		var wrapper = new VBoxContainer();
		wrapper.MouseFilter = MouseFilterEnum.Pass;
		wrapper.AddThemeConstantOverride("separation", 2);

		// Effects row — fixed height so the frame doesn't jump when effects appear
		effectsBar = new HBoxContainer();
		effectsBar.AddThemeConstantOverride("separation", 3);
		effectsBar.CustomMinimumSize = new Vector2(0, 24);
		effectsBar.MouseFilter = MouseFilterEnum.Ignore;
		wrapper.AddChild(effectsBar);

		// Dark outer panel — MouseFilter.Stop is intentional: it lets
		// MouseEntered / MouseExited fire for the hover border highlight.
		panel = new PanelContainer();
		panel.CustomMinimumSize = new Vector2(138, 54);

		panelStyle = new StyleBoxFlat();
		panelStyle.BgColor = new Color(0.11f, 0.09f, 0.09f, 0.95f);
		panelStyle.SetCornerRadiusAll(6);
		panelStyle.SetBorderWidthAll(2);
		panelStyle.BorderColor = BorderDefault;
		panelStyle.ContentMarginLeft = 8f;
		panelStyle.ContentMarginRight = 8f;
		panelStyle.ContentMarginTop = 5f;
		panelStyle.ContentMarginBottom = 5f;
		panel.AddThemeStyleboxOverride("panel", panelStyle);

		// Health bar
		var healthBar = new ProgressBar();
		healthBar.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		healthBar.SizeFlagsVertical = SizeFlags.ExpandFill;
		healthBar.AddThemeConstantOverride("separation", 4);
		healthBar.ShowPercentage = false;
		healthBar.MaxValue = maxHp;
		healthBar.Value = maxHp;
		healthBar.MouseFilter = MouseFilterEnum.Ignore;
		healthBar.AddThemeStyleboxOverride("background", new StyleBoxFlat { BgColor = new Color(0.16f, 0.13f, 0.13f) });
		healthBar.AddThemeStyleboxOverride("fill", new StyleBoxFlat { BgColor = barColor });
		panel.AddChild(healthBar);
		bar = healthBar;

		// Name label
		var label = new Label();
		label.Text = name;
		label.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		label.SizeFlagsVertical = SizeFlags.ExpandFill;
		label.HorizontalAlignment = HorizontalAlignment.Center;
		label.VerticalAlignment = VerticalAlignment.Center;
		label.AddThemeFontSizeOverride("font_size", 14);
		label.AddThemeColorOverride("font_color", new Color(0.90f, 0.87f, 0.83f));
		label.MouseFilter = MouseFilterEnum.Ignore;
		panel.AddChild(label);

		// Shield overlay — transparent background, semi-transparent blue fill
		var shieldProgress = new ProgressBar();
		shieldProgress.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		shieldProgress.SizeFlagsVertical = SizeFlags.ExpandFill;
		shieldProgress.ShowPercentage = false;
		shieldProgress.MaxValue = maxHp;
		shieldProgress.Value = 0f;
		shieldProgress.MouseFilter = MouseFilterEnum.Ignore;
		shieldProgress.AddThemeStyleboxOverride("background", new StyleBoxFlat { BgColor = new Color(0f, 0f, 0f, 0f) });
		shieldProgress.AddThemeStyleboxOverride("fill", new StyleBoxFlat { BgColor = new Color(0.45f, 0.70f, 1.00f, 0.50f) });
		panel.AddChild(shieldProgress);
		shieldBar = shieldProgress;

		wrapper.AddChild(panel);
		return wrapper;
	}
}