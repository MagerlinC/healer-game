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
    AudioStreamPlayer? _sfxPlayer;

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

        // Sprite — only created when a texture path has been set.
        if (!string.IsNullOrEmpty(TexturePath))
        {
            _sprite = new Sprite2D
            {
                Texture = GD.Load<Texture2D>(TexturePath),
                Scale = SpriteScale
            };
            AddChild(_sprite);
        }

        var collision = new CollisionShape2D
        {
            Shape = new CircleShape2D { Radius = CollisionRadius }
        };
        AddChild(collision);

        // SFX player — only created when a path has been supplied.
        if (!string.IsNullOrEmpty(SfxPath))
        {
            _sfxPlayer = new AudioStreamPlayer
            {
                Stream = GD.Load<AudioStream>(SfxPath),
                VolumeDb = -6f
            };
            AddChild(_sfxPlayer);
        }

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

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Swaps the displayed texture at runtime (e.g. after an affinity change).
    /// Safe to call before <see cref="_Ready"/> has run — the texture will be
    /// applied once the sprite node is created.
    /// </summary>
    public void SetTexture(string texturePath)
    {
        TexturePath = texturePath;
        if (_sprite != null)
            _sprite.Texture = GD.Load<Texture2D>(texturePath);
    }
}
