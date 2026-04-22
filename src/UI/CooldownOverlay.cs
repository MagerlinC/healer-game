using System.Collections.Generic;
using Godot;

/// <summary>
/// A clock-sweep overlay that sits on top of a spell action bar slot.
///
/// Renders a dark pie inscribed within the slot that starts covering the full
/// circle immediately after a spell is cast and reveals clockwise from 12 o'clock
/// as the cooldown expires (the same visual convention as World of Warcraft).
/// A centered label shows the integer seconds remaining.
///
/// Usage:
///   overlay.Start(spell.Cooldown);   // call once when the spell fires
///   overlay.Tick((float)delta);      // call every frame in ActionBar._Process
/// The overlay draws nothing and hides its label when inactive.
/// </summary>
public partial class CooldownOverlay : Control
{
    float _remaining;
    float _duration;
    Label _label;

    const int Segments = 48;
    static readonly Color OverlayColour = new(0f, 0f, 0f, 0.68f);

    /// <summary>True while the cooldown is counting down.</summary>
    public bool IsActive => _remaining > 0f && _duration > 0f;

    /// <summary>Seconds remaining on the current cooldown. Zero when inactive.</summary>
    public float Remaining => _remaining;

    public override void _Ready()
    {
        _label = new Label();
        _label.HorizontalAlignment = HorizontalAlignment.Center;
        _label.VerticalAlignment = VerticalAlignment.Center;
        _label.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _label.MouseFilter = MouseFilterEnum.Ignore;
        _label.AddThemeFontSizeOverride("font_size", 14);
        _label.AddThemeColorOverride("font_color", Colors.White);
        _label.AddThemeColorOverride("font_shadow_color", new Color(0f, 0f, 0f, 0.9f));
        _label.AddThemeConstantOverride("shadow_offset_x", 1);
        _label.AddThemeConstantOverride("shadow_offset_y", 1);
        _label.Visible = false;
        AddChild(_label);
    }

    /// <summary>
    /// Begin (or restart) the sweep animation for a cooldown of
    /// <paramref name="duration"/> seconds.
    /// </summary>
    public void Start(float duration)
    {
        _duration = duration;
        _remaining = duration;
        UpdateLabel();
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
        UpdateLabel();
        QueueRedraw();
    }

    /// <summary>Immediately clear the overlay (e.g. on scene reset).</summary>
    public void Clear()
    {
        _remaining = 0f;
        _duration = 0f;
        if (_label != null) _label.Visible = false;
        QueueRedraw();
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    void UpdateLabel()
    {
        if (_label == null) return;
        if (_remaining > 0f)
        {
            // Ceiling so the display reads "1" right up until the last moment.
            _label.Text = Mathf.CeilToInt(_remaining).ToString();
            _label.Visible = true;
        }
        else
        {
            _label.Visible = false;
        }
    }

    public override void _Draw()
    {
        if (!IsActive) return;

        var fraction = _remaining / _duration; // 1.0 = just cast, 0.0 = ready
        if (fraction <= 0f) return;

        var center = Size / 2f;

        // Half-diagonal radius: the pie extends past the corners so the entire
        // square slot is covered. Godot clips drawing to the Control bounds,
        // so the result looks like a rectangular swipe, not a circle.
        var radius = Mathf.Sqrt(Size.X * Size.X + Size.Y * Size.Y) / 2f;

        // WoW-style reveal: the bright area grows clockwise from 12 o'clock.
        // The dark pie therefore starts where the elapsed portion ends and
        // sweeps clockwise back to 12 o'clock, covering the remaining fraction.
        var elapsed = 1f - fraction;
        var startAngle = -Mathf.Pi / 2f + elapsed * Mathf.Tau;
        var sweepAngle = fraction * Mathf.Tau;

        var points = new List<Vector2> { center };
        for (var i = 0; i <= Segments; i++)
        {
            var angle = startAngle + (float)i / Segments * sweepAngle;
            points.Add(center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius);
        }

        DrawColoredPolygon(points.ToArray(), OverlayColour);
    }
}
