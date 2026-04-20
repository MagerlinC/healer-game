using System;
using Godot;
using healerfantasy.Talents;

/// <summary>
/// One talent card in the talent selector grid.
///
/// Layout (bottom-to-top, z-order):
///   • Monk icon — fills the icon area
///   • Talent frame overlay — cut from the talent-frames spritesheet,
///     drawn on top so the transparent centre reveals the icon beneath
///   • Dim colour rect — fades the icon when the talent is unselected
///   • Name + description labels below the icon area
///
/// Clicking the card toggles its selected state and fires
/// <see cref="Toggled"/> so the parent panel can react.
/// </summary>
public partial class TalentSlot : PanelContainer
{
    // ── constants ────────────────────────────────────────────────────────────
    // talent-frames.png: 680×544 → 10 cols × 8 rows → 68×68 per frame.
    // We use the first frame (row 0, col 0) for all slots.
    const int   FrameW = 68, FrameH = 68;
    const float SlotW  = 130f, SlotH = 170f;
    const float IconAreaSize = 100f;

    static readonly Color BorderIdle     = new(0.28f, 0.22f, 0.16f);
    static readonly Color BorderHover    = new(0.70f, 0.58f, 0.30f);
    static readonly Color BorderSelected = new(0.98f, 0.82f, 0.15f); // bright gold

    // Unselected: icon desaturated + darkened; selected: full colour
    static readonly Color FrameTintIdle     = new(0.55f, 0.50f, 0.45f, 1f);
    static readonly Color FrameTintSelected = new(1.00f, 0.90f, 0.55f, 1f); // warm gold

    static readonly Color DimIdle     = new(0f, 0f, 0f, 0.52f);
    static readonly Color DimSelected = new(0f, 0f, 0f, 0f);

    // ── public surface ───────────────────────────────────────────────────────
    public TalentDefinition Definition { get; }
    public bool             IsSelected { get; private set; }

    /// <summary>Raised whenever the user clicks the slot and its state changes.</summary>
    public event Action<TalentSlot> Toggled;

    // ── private refs updated at runtime ──────────────────────────────────────
    StyleBoxFlat _outerStyle;
    TextureRect  _frameOverlay;
    ColorRect    _dimOverlay;

    // Shared greyscale shader — applied to the icon when the slot is idle.
    static ShaderMaterial _greyMat;
    static ShaderMaterial GreyMat => _greyMat ??= MakeGreyMaterial();

    // ── constructor ──────────────────────────────────────────────────────────
    public TalentSlot(TalentDefinition def, bool selected = false)
    {
        Definition = def;
        IsSelected = selected;
    }

    // ── lifecycle ────────────────────────────────────────────────────────────
    public override void _Ready()
    {
        CustomMinimumSize = new Vector2(SlotW, SlotH);
        MouseDefaultCursorShape = CursorShape.PointingHand;

        // ── outer card style ────────────────────────────────────────────────
        _outerStyle = new StyleBoxFlat();
        _outerStyle.BgColor = new Color(0.09f, 0.07f, 0.07f, 0.97f);
        _outerStyle.SetCornerRadiusAll(6);
        _outerStyle.SetBorderWidthAll(2);
        _outerStyle.BorderColor = BorderIdle;
        _outerStyle.ContentMarginLeft   = 8f;
        _outerStyle.ContentMarginRight  = 8f;
        _outerStyle.ContentMarginTop    = 8f;
        _outerStyle.ContentMarginBottom = 8f;
        AddThemeStyleboxOverride("panel", _outerStyle);

        // ── content layout ──────────────────────────────────────────────────
        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 6);
        vbox.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        AddChild(vbox);

        // ── icon stacking area ──────────────────────────────────────────────
        var iconArea = new Control();
        iconArea.CustomMinimumSize = new Vector2(IconAreaSize, IconAreaSize);
        iconArea.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        vbox.AddChild(iconArea);

        // Layer 1 — monk icon
        var iconTex = GD.Load<Texture2D>(Definition.IconPath);
        var iconRect = new TextureRect();
        iconRect.Texture     = iconTex;
        iconRect.ExpandMode  = TextureRect.ExpandModeEnum.IgnoreSize;
        iconRect.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
        iconRect.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        iconArea.AddChild(iconRect);

        // Layer 2 — talent frame sprite (transparent centre, ornate border)
        var atlas = new AtlasTexture();
        atlas.Atlas  = GD.Load<Texture2D>("res://assets/frames/talent-frames.png");
        atlas.Region = new Rect2(0, 0, FrameW, FrameH);

        _frameOverlay = new TextureRect();
        _frameOverlay.Texture     = atlas;
        _frameOverlay.ExpandMode  = TextureRect.ExpandModeEnum.IgnoreSize;
        _frameOverlay.StretchMode = TextureRect.StretchModeEnum.Scale;
        _frameOverlay.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _frameOverlay.MouseFilter = MouseFilterEnum.Ignore;
        iconArea.AddChild(_frameOverlay);

        // Layer 3 — dim overlay (darkens icon when unselected)
        _dimOverlay = new ColorRect();
        _dimOverlay.Color       = DimIdle;
        _dimOverlay.MouseFilter = MouseFilterEnum.Ignore;
        _dimOverlay.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        iconArea.AddChild(_dimOverlay);

        // ── name label ──────────────────────────────────────────────────────
        var nameLabel = new Label();
        nameLabel.Text                = Definition.Name;
        nameLabel.HorizontalAlignment = HorizontalAlignment.Center;
        nameLabel.AutowrapMode        = TextServer.AutowrapMode.WordSmart;
        nameLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        nameLabel.AddThemeFontSizeOverride("font_size", 12);
        nameLabel.AddThemeColorOverride("font_color", new Color(0.92f, 0.88f, 0.82f));
        vbox.AddChild(nameLabel);

        // ── description label ────────────────────────────────────────────────
        var descLabel = new Label();
        descLabel.Text                = Definition.Description;
        descLabel.HorizontalAlignment = HorizontalAlignment.Center;
        descLabel.AutowrapMode        = TextServer.AutowrapMode.WordSmart;
        descLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        descLabel.AddThemeFontSizeOverride("font_size", 10);
        descLabel.AddThemeColorOverride("font_color", new Color(0.60f, 0.56f, 0.50f));
        vbox.AddChild(descLabel);

        // ── apply initial visual state ──────────────────────────────────────
        ApplyVisuals();

        // ── input events ────────────────────────────────────────────────────
        MouseEntered += () =>
        {
            _outerStyle.BorderColor = IsSelected ? BorderSelected : BorderHover;
        };
        MouseExited += () =>
        {
            _outerStyle.BorderColor = IsSelected ? BorderSelected : BorderIdle;
        };
        GuiInput += OnGuiInput;
    }

    // ── public API ───────────────────────────────────────────────────────────
    public void SetSelected(bool selected)
    {
        IsSelected = selected;
        ApplyVisuals();
    }

    // ── private ──────────────────────────────────────────────────────────────
    void OnGuiInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton mb
            && mb.ButtonIndex == MouseButton.Left
            && mb.Pressed)
        {
            SetSelected(!IsSelected);
            Toggled?.Invoke(this);
            AcceptEvent();
        }
    }

    void ApplyVisuals()
    {
        if (_outerStyle   == null) return; // called before _Ready — skip
        if (_frameOverlay == null) return;
        if (_dimOverlay   == null) return;

        _outerStyle.BorderColor  = IsSelected ? BorderSelected : BorderIdle;
        _frameOverlay.Modulate   = IsSelected ? FrameTintSelected : FrameTintIdle;
        _dimOverlay.Color        = IsSelected ? DimSelected : DimIdle;
    }

    static ShaderMaterial MakeGreyMaterial()
    {
        var shader = new Shader();
        shader.Code = """
            shader_type canvas_item;
            void fragment() {
                vec4 col  = texture(TEXTURE, UV);
                float grey = dot(col.rgb, vec3(0.299, 0.587, 0.114));
                COLOR = vec4(vec3(grey * 0.6), col.a);
            }
            """;
        var mat = new ShaderMaterial();
        mat.Shader = shader;
        return mat;
    }
}
