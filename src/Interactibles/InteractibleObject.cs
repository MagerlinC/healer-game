#nullable enable
using System;
using Godot;

namespace healerfantasy;

/// <summary>
/// A self-contained interactible Area2D node.
///
/// Bundles the sprite, collision shape, optional sound effect, and click
/// detection that previously had to be wired up individually by each scene
/// controller.  Left-clicking the object plays the SFX (if configured) and
/// raises the <see cref="Interacted"/> C# event.
///
/// Hover behaviour (automatic):
///   • Cursor changes to a pointing hand while the mouse is over the collider.
///   • A warm-gold glow outline fades in beneath the sprite and fades out
///     when the mouse leaves, giving the player a clear affordance that the
///     object is clickable.
///
/// Supports two usage modes:
///
/// <b>Code-created</b> (existing behaviour — no scene changes required):
/// <code>
/// var tome = AddInteractible(new InteractibleObject(
///     AssetConstants.SpellbookPath,
///     new Vector2(996f, FloorHeight - 12f), new Vector2(0.080f, 0.080f), 28f,
///     AssetConstants.SpellbookSfxPath));
/// tome.Interacted += () => OpenPanel(_spellPanel!);
/// </code>
///
/// <b>Editor-placed</b> (allows visual positioning against the background):
///   1. Add an <c>InteractibleObject</c> node to the .tscn scene in the Godot editor.
///   2. Set <see cref="SpriteScale"/>, <see cref="CollisionRadius"/>, and
///      (optionally) <see cref="SfxPath"/> in the Inspector.
///   3. Leave <see cref="TexturePath"/> empty if the texture is set at runtime
///      (e.g. via <see cref="SetTexture"/>).
///   4. Position and scale the node with the editor transform handles.
///   5. In code, <c>GetNode&lt;InteractibleObject&gt;("NodeName")</c> and wire
///      <see cref="Interacted"/>.
/// </summary>
[GlobalClass]
public partial class InteractibleObject : Area2D
{
    /// <summary>Raised when the player left-clicks this interactible.</summary>
    public event Action? Interacted;

    // ── Exported properties (Inspector-editable for editor-placed instances) ──

    /// <summary>
    /// res:// path to the sprite texture.
    /// May be left empty when the texture is applied at runtime via <see cref="SetTexture"/>.
    /// </summary>
    [Export] public string TexturePath { get; set; } = "";

    /// <summary>Scale of the inner Sprite2D relative to this node.</summary>
    [Export] public Vector2 SpriteScale { get; set; } = Vector2.One;

    /// <summary>Radius of the CircleShape2D used for click detection.</summary>
    [Export] public float CollisionRadius { get; set; } = 28f;

    /// <summary>
    /// Optional res:// path to a one-shot SFX clip played on click.
    /// Leave empty for no audio.
    /// </summary>
    [Export] public string SfxPath { get; set; } = "";

    // ── Runtime nodes — created in _Ready ─────────────────────────────────────

    Sprite2D? _sprite;

    /// <summary>
    /// Second sprite rendered behind <see cref="_sprite"/> that carries the
    /// outline-glow shader.  Starts fully transparent; tweened to opaque on
    /// hover and back to transparent on exit.
    /// </summary>
    Sprite2D? _glowSprite;

    AudioStreamPlayer? _sfxPlayer;
    Tween? _glowTween;

    /// <summary>
    /// Shared across all instances so the shader is compiled only once.
    /// </summary>
    static Shader? _glowShader;

    // ── Constructors ──────────────────────────────────────────────────────────

    /// <summary>
    /// Parameterless constructor for nodes placed in the Godot editor.
    /// Configure the node via the exported Inspector properties and the
    /// editor transform handles.
    /// </summary>
    public InteractibleObject() { }

    /// <summary>
    /// Full constructor for creating the interactible entirely in code.
    /// All parameters map directly to the equivalent exported property.
    /// </summary>
    /// <param name="texturePath">res:// path to the sprite texture.</param>
    /// <param name="position">World-space position of the Area2D.</param>
    /// <param name="spriteScale">Scale applied to the inner Sprite2D.</param>
    /// <param name="collisionRadius">Radius of the CircleShape2D collider.</param>
    /// <param name="sfxPath">
    ///     Optional res:// path to a one-shot SFX clip played on click.
    ///     Pass <c>null</c> or omit for no audio.
    /// </param>
    public InteractibleObject(
        string texturePath,
        Vector2 position,
        Vector2 spriteScale,
        float collisionRadius,
        string? sfxPath = null)
    {
        TexturePath = texturePath;
        Position = position;
        SpriteScale = spriteScale;
        CollisionRadius = collisionRadius;
        if (sfxPath != null) SfxPath = sfxPath;
    }

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    public override void _Ready()
    {
        InputPickable = true;
        Monitoring = false;
        Monitorable = false;

        // ── Sprites — only created when a texture path has been set ──────────
        if (!string.IsNullOrEmpty(TexturePath))
        {
            var tex = GD.Load<Texture2D>(TexturePath);

            // Glow sprite is added FIRST so it renders behind the main sprite.
            _glowShader ??= BuildGlowShader();
            _glowSprite = new Sprite2D
            {
                Texture  = tex,
                Scale    = SpriteScale,
                Modulate = new Color(1f, 1f, 1f, 0f),   // fully transparent until hovered
                Material = new ShaderMaterial { Shader = _glowShader }
            };
            AddChild(_glowSprite);

            // Main sprite on top — reuses the already-loaded Texture2D reference.
            _sprite = new Sprite2D { Texture = tex, Scale = SpriteScale };
            AddChild(_sprite);
        }

        // ── Collision ─────────────────────────────────────────────────────────
        var collision = new CollisionShape2D
        {
            Shape = new CircleShape2D { Radius = CollisionRadius }
        };
        AddChild(collision);

        // ── SFX player ────────────────────────────────────────────────────────
        if (!string.IsNullOrEmpty(SfxPath))
        {
            _sfxPlayer = new AudioStreamPlayer
            {
                Stream   = GD.Load<AudioStream>(SfxPath),
                VolumeDb = -6f
            };
            AddChild(_sfxPlayer);
        }

        // ── Hover — pointer cursor + glow fade ────────────────────────────────
        MouseEntered += OnHoverEnter;
        MouseExited  += OnHoverExit;

        // ── Click ─────────────────────────────────────────────────────────────
        InputEvent += (_, ev, _) =>
        {
            if (ev is InputEventMouseButton mb &&
                mb.ButtonIndex == MouseButton.Left &&
                mb.Pressed)
            {
                _sfxPlayer?.Play();
                Interacted?.Invoke();
            }
        };
    }

    // ── Hover handlers ────────────────────────────────────────────────────────

    void OnHoverEnter()
    {
        Input.SetDefaultCursorShape(Input.CursorShape.PointingHand);

        if (_glowSprite == null) return;
        _glowTween?.Kill();
        _glowTween = CreateTween();
        _glowTween.TweenProperty(_glowSprite, "modulate:a", 1.0f, 0.18f);
    }

    void OnHoverExit()
    {
        Input.SetDefaultCursorShape(Input.CursorShape.Arrow);

        if (_glowSprite == null) return;
        _glowTween?.Kill();
        _glowTween = CreateTween();
        _glowTween.TweenProperty(_glowSprite, "modulate:a", 0.0f, 0.18f);
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Swaps the displayed texture at runtime (e.g. after an affinity change).
    /// Safe to call before <see cref="_Ready"/> has run — the texture will be
    /// applied once the sprite nodes are created.
    /// </summary>
    public void SetTexture(string texturePath)
    {
        TexturePath = texturePath;
        if (string.IsNullOrEmpty(texturePath)) return;

        var tex = GD.Load<Texture2D>(texturePath);
        if (_sprite     != null) _sprite.Texture     = tex;
        if (_glowSprite != null) _glowSprite.Texture = tex;
    }

    // ── Glow shader ───────────────────────────────────────────────────────────

    /// <summary>
    /// Builds the outline-glow shader shared by all <see cref="InteractibleObject"/>
    /// instances (compiled once, reused via <see cref="_glowShader"/>).
    ///
    /// Technique: sample the texture's alpha channel at 8 neighbouring texels.
    /// Wherever at least one neighbour is opaque, render a warm-gold pixel.
    /// Because this sprite sits BEHIND the main sprite, the glow is naturally
    /// invisible inside the opaque sprite body — it only shows as a halo around
    /// the edges, giving a clean outline-glow affordance.
    /// </summary>
    static Shader BuildGlowShader()
    {
        var s = new Shader();
        s.Code = @"
shader_type canvas_item;

// How many texels outward the glow extends.
uniform float glow_width = 4.0;

void fragment() {
    vec2 px = TEXTURE_PIXEL_SIZE * glow_width;

    float aL  = texture(TEXTURE, UV + vec2(-px.x,   0.0  )).a;
    float aR  = texture(TEXTURE, UV + vec2( px.x,   0.0  )).a;
    float aU  = texture(TEXTURE, UV + vec2(  0.0,  -px.y )).a;
    float aD  = texture(TEXTURE, UV + vec2(  0.0,   px.y )).a;
    float aUL = texture(TEXTURE, UV + vec2(-px.x,  -px.y )).a;
    float aUR = texture(TEXTURE, UV + vec2( px.x,  -px.y )).a;
    float aDL = texture(TEXTURE, UV + vec2(-px.x,   px.y )).a;
    float aDR = texture(TEXTURE, UV + vec2( px.x,   px.y )).a;

    float glow = max(max(max(aL, aR), max(aU, aD)),
                     max(max(aUL, aUR), max(aDL, aDR)));

    // Warm gold — matches the game's panel-border colour palette.
    COLOR = vec4(1.0, 0.84, 0.32, glow);
}
";
        return s;
    }
}
