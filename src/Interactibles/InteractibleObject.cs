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
/// Usage:
/// <code>
/// var tome = AddInteractible(new InteractibleObject(
///     AssetConstants.SpellTomeInteractiblePath,
///     new Vector2(996f, FloorHeight - 12f), new Vector2(0.080f, 0.080f), 28f,
///     AssetConstants.SpellbookSfxPath));
/// tome.Interacted += () => OpenPanel(_spellPanel!);
/// WireHints(tome, "Spellbook  •  Click to open");
/// </code>
/// </summary>
public partial class InteractibleObject : Area2D
{
    /// <summary>Raised when the player left-clicks this interactible.</summary>
    public event Action? Interacted;

    readonly AudioStreamPlayer? _sfxPlayer;

    /// <param name="texturePath">res:// path to the sprite texture.</param>
    /// <param name="position">World-space position of the Area2D.</param>
    /// <param name="spriteScale">Scale applied to the inner Sprite2D.</param>
    /// <param name="collisionRadius">Radius of the CircleShape2D collider.</param>
    /// <param name="sfxPath">
    ///     Optional res:// path to a one-shot SFX clip played on click.
    ///     When null, no audio player is created.
    /// </param>
    public InteractibleObject(
        string texturePath,
        Vector2 position,
        Vector2 spriteScale,
        float collisionRadius,
        string? sfxPath = null)
    {
        Position = position;
        InputPickable = true;
        Monitoring = false;
        Monitorable = false;

        var sprite = new Sprite2D
        {
            Texture = GD.Load<Texture2D>(texturePath),
            Scale = spriteScale
        };
        AddChild(sprite);

        var collision = new CollisionShape2D
        {
            Shape = new CircleShape2D { Radius = collisionRadius }
        };
        AddChild(collision);

        if (sfxPath != null)
        {
            _sfxPlayer = new AudioStreamPlayer
            {
                Stream = GD.Load<AudioStream>(sfxPath),
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
}
