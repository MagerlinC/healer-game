using Godot;
using healerfantasy.SpellResources;

namespace healerfantasy.UI;

/// <summary>
/// Subscribes to every character's <c>FloatingCombatText</c> signal and spawns
/// <see cref="FloatingCombatText"/> labels above the corresponding character model.
///
/// Lives on a <see cref="CanvasLayer"/> (layer 8) so the numbers sit above the
/// world sprites but below the main UI (layer 10).
///
/// Usage — call <see cref="Register"/> for each character inside World._Ready():
/// <code>
///   var fct = new FloatingCombatTextManager();
///   AddChild(fct);
///   fct.Register(player);
///   fct.Register(templar);
///   // …
/// </code>
/// </summary>
public partial class FloatingCombatTextManager : CanvasLayer
{
    public FloatingCombatTextManager()
    {
        Layer = 8; // above world sprites, below main UI (layer 10)
    }

    /// <summary>
    /// Subscribe to <paramref name="character"/>'s <c>FloatingCombatText</c>
    /// signal so numbers appear above that character's model.
    /// </summary>
    public void Register(Character character)
    {
        character.FloatingCombatText += (amount, isHealing, school, isCrit) =>
            Spawn(character, amount, isHealing, (SpellSchool)school, isCrit);
    }

    // ── private ──────────────────────────────────────────────────────────────

    void Spawn(Character source, float amount, bool isHealing, SpellSchool school, bool isCrit)
    {
        // Guard: skip sub-pixel amounts (defensive; passive life-drain never
        // emits the signal, but DoT partial ticks could theoretically be tiny).
        if (amount < 0.5f) return;

        var label = FloatingCombatText.Create(amount, isHealing, school, isCrit);

        // Convert the character's world-space position to screen (canvas) space,
        // accounting for camera position and zoom.
        var canvasTransform = source.GetViewport().GetCanvasTransform();
        var screenPos = canvasTransform * source.GlobalPosition;

        // Offset upward to clear the top of the sprite (~32 px world = variable
        // screen px depending on zoom; a fixed 24 screen-px offset looks good).
        screenPos.Y -= 24f;
        // Centre-align: shift left by half an approximate label width.
        screenPos.X -= 12f;

        label.Position = screenPos;
        AddChild(label);
    }
}
