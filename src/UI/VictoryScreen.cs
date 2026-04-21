using Godot;
using healerfantasy;

/// <summary>
/// Full-screen victory overlay shown when the boss dies.
///
/// Sits on CanvasLayer 20 (same layer as DeathScreen) and is hidden by default.
/// Subscribes to <see cref="Character.Died"/> from GlobalAutoLoad; when the
/// dying character is not friendly (i.e. the boss) <see cref="ShowVictoryScreen"/>
/// is called.
///
/// Add this node as a child of the World scene root (same as DeathScreen).
///
/// ProcessMode is Always so the buttons keep receiving input while the tree
/// is paused.
/// </summary>
public partial class VictoryScreen : CanvasLayer
{
    public override void _Ready()
    {
        Layer       = 20;
        Visible     = false;
        ProcessMode = ProcessModeEnum.Always;

        GlobalAutoLoad.SubscribeToSignal(
            nameof(Character.Died),
            Callable.From((Character character) =>
            {
                if (!character.IsFriendly)
                    ShowVictoryScreen();
            }));

        // ── Dark overlay ──────────────────────────────────────────────────────
        var overlay = new ColorRect();
        overlay.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        overlay.Color       = new Color(0f, 0f, 0f, 0.80f);
        overlay.MouseFilter = Control.MouseFilterEnum.Stop;
        AddChild(overlay);

        // ── Centred content column ────────────────────────────────────────────
        var vbox = new VBoxContainer();
        vbox.SetAnchorsPreset(Control.LayoutPreset.Center);
        vbox.GrowHorizontal = Control.GrowDirection.Both;
        vbox.GrowVertical   = Control.GrowDirection.Both;
        vbox.AddThemeConstantOverride("separation", 28);
        overlay.AddChild(vbox);

        // Title
        var title = new Label();
        title.Text                = "VICTORY!";
        title.HorizontalAlignment = HorizontalAlignment.Center;
        title.AddThemeFontSizeOverride("font_size", 48);
        title.AddThemeColorOverride("font_color", new Color(0.95f, 0.84f, 0.50f));
        vbox.AddChild(title);

        var sub = new Label();
        sub.Text                = "The boss has been defeated.";
        sub.HorizontalAlignment = HorizontalAlignment.Center;
        sub.AddThemeFontSizeOverride("font_size", 18);
        sub.AddThemeColorOverride("font_color", new Color(0.72f, 0.68f, 0.62f));
        vbox.AddChild(sub);

        // Button row
        var btnRow = new HBoxContainer();
        btnRow.Alignment = BoxContainer.AlignmentMode.Center;
        btnRow.AddThemeConstantOverride("separation", 20);
        vbox.AddChild(btnRow);

        btnRow.AddChild(MakeButton("Play Again",   new Color(0.18f, 0.14f, 0.10f), new Color(0.65f, 0.52f, 0.28f), OnPlayAgainPressed));
        btnRow.AddChild(MakeButton("Main Menu",    new Color(0.14f, 0.11f, 0.09f), new Color(0.45f, 0.38f, 0.22f), OnMainMenuPressed));
    }

    // ── public API ────────────────────────────────────────────────────────────

    public void ShowVictoryScreen()
    {
        if (Visible) return;
        Visible = true;
        GetTree().Paused = true;
    }

    // ── button callbacks ──────────────────────────────────────────────────────

    void OnPlayAgainPressed()
    {
        GetTree().Paused = false;
        GlobalAutoLoad.Reset();
        // Keep RunState intact — player goes back to Overworld with their choices
        GetTree().ChangeSceneToFile("res://levels/Overworld.tscn");
    }

    void OnMainMenuPressed()
    {
        GetTree().Paused = false;
        GlobalAutoLoad.Reset();
        RunState.Instance?.Reset();
        GetTree().ChangeSceneToFile("res://levels/MainMenu.tscn");
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    static Button MakeButton(string text, Color bgColor, Color borderColor, System.Action onPressed)
    {
        var btn = new Button();
        btn.Text                    = text;
        btn.CustomMinimumSize       = new Vector2(180f, 52f);
        btn.SizeFlagsHorizontal     = Control.SizeFlags.ShrinkCenter;
        btn.MouseDefaultCursorShape = Control.CursorShape.PointingHand;
        btn.AddThemeFontSizeOverride("font_size", 18);
        btn.AddThemeColorOverride("font_color",       new Color(0.90f, 0.87f, 0.83f));
        btn.AddThemeColorOverride("font_hover_color", new Color(0.95f, 0.84f, 0.50f));

        var normal = MakeStyle(bgColor, borderColor);
        var hover  = MakeStyle(new Color(bgColor.R + 0.08f, bgColor.G + 0.06f, bgColor.B + 0.04f), borderColor * 1.3f);
        btn.AddThemeStyleboxOverride("normal",  normal);
        btn.AddThemeStyleboxOverride("hover",   hover);
        btn.AddThemeStyleboxOverride("pressed", normal);
        btn.AddThemeStyleboxOverride("focus",   normal);

        btn.Pressed += onPressed;
        return btn;
    }

    static StyleBoxFlat MakeStyle(Color bg, Color border)
    {
        var s = new StyleBoxFlat();
        s.BgColor = bg;
        s.SetCornerRadiusAll(6);
        s.SetBorderWidthAll(2);
        s.BorderColor          = border;
        s.ContentMarginLeft    = s.ContentMarginRight  = 16f;
        s.ContentMarginTop     = s.ContentMarginBottom = 10f;
        return s;
    }
}
