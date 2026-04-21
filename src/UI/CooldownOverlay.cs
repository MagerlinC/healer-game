using System.Collections.Generic;
using Godot;

/// <summary>
/// A clock-sweep overlay that sits on top of a spell action bar slot.
///
/// Renders a dark pie that starts covering the full slot immediately after a
/// spell is cast and sweeps away clockwise as the cooldown expires — the same
/// visual convention used by World of Warcraft and many other ARPGs.
///
/// Usage:
///   overlay.Start(spell.Cooldown);   // call once when the spell fires
///   overlay.Tick((float)delta);      // call every frame in ActionBar._Process
/// The overlay draws nothing (and is effectively invisible) when inactive.
///
/// The pie radius is set to the slot's half-diagonal so the sweep covers the
/// full rectangular area including corners, not just an inscribed circle.
/// </summary>
public partial class CooldownOverlay : Control
{
    float _remaining;
    float _duration;

    const int Segments = 48; // smoothness of the arc edge
    static readonly Color OverlayColour = new(0f, 0f, 0f, 0.68f);

    /// <summary>True while the cooldown is counting down.</summary>
    public bool IsActive => _remaining > 0f && _duration > 0f;

    /// <summary>
    /// Begin (or restart) the sweep animation for a cooldown of
    /// <paramref name="duration"/> seconds.
    /// </summary>
    public void Start(float duration)
    {
        _duration = duration;
        _remaining = duration;
        QueueRedraw();
    }

    /// <summary>
    /// Advance the countdown by <paramref name="delta"/> seconds.
    /// Automatically stops drawing when the cooldown reaches zero.
    /// Call this every frame from ActionBar._Process.
    /// </summary>
    public void Tick(float delta)
    {
        if (!IsActive) return;
        _remaining = Mathf.Max(_remaining - delta, 0f);
        QueueRedraw();
    }

    /// <summary>Immediately clear the overlay (e.g. on scene reset).</summary>
    public void Clear()
    {
        _remaining = 0f;
        _duration = 0f;
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (!IsActive) return;

        var fraction = _remaining / _duration; // 1.0 = full cooldown, 0.0 = ready
        if (fraction <= 0f) return;

        var center = Size / 2f;

        // Half-diagonal: the pie covers all four corners of the rectangular slot.
        var radius = Mathf.Sqrt(Size.X * Size.X + Size.Y * Size.Y) / 2f;

        // Sweep clockwise from 12 o'clock (-PI/2) by fraction * 360 degrees.
        var sweepAngle = fraction * Mathf.Tau;
        var startAngle = -Mathf.Pi / 2f;

        var points = new List<Vector2> { center };
        for (var i = 0; i <= Segments; i++)
        {
            var angle = startAngle + (float)i / Segments * sweepAngle;
            points.Add(center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius);
        }

        DrawColoredPolygon(points.ToArray(), OverlayColour);
    }
}
