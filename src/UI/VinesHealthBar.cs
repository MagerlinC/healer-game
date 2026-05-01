#nullable enable
using Godot;
using healerfantasy;

namespace healerfantasy.UI;

/// <summary>
/// A compact health bar for a <see cref="VinesEnemy"/>, displayed below the
/// boss bar during a Rune-of-Nature fight.
///
/// Subscribes to <see cref="Character.HealthChanged"/> filtered by
/// <see cref="TrackedName"/> and hides itself when health reaches zero.
/// </summary>
public partial class VinesHealthBar : Control
{
    // ── public ────────────────────────────────────────────────────────────────

    /// <summary>The CharacterName this bar is tracking.</summary>
    public string TrackedName { get; }

    // ── node refs ─────────────────────────────────────────────────────────────

    ProgressBar _bar = null!;
    Label _label = null!;

    // ── ctor ──────────────────────────────────────────────────────────────────

    public VinesHealthBar(string characterName, string displayName, float currentHealth, float maxHealth)
    {
        TrackedName = characterName;
        CustomMinimumSize = new Vector2(0f, 18f);
        SizeFlagsHorizontal = SizeFlags.ExpandFill;

        // Green progress bar.
        _bar = new ProgressBar
        {
            MaxValue = maxHealth,
            Value = currentHealth,
            ShowPercentage = false,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        _bar.AddThemeStyleboxOverride("background",
            new StyleBoxFlat { BgColor = new Color(0.10f, 0.08f, 0.06f, 0.90f) });
        _bar.AddThemeStyleboxOverride("fill",
            new StyleBoxFlat { BgColor = new Color(0.20f, 0.55f, 0.15f) });

        // Label drawn over the bar.
        _label = new Label
        {
            Text = $"{displayName}  {currentHealth:F0}/{maxHealth:F0}",
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = MouseFilterEnum.Ignore
        };
        _label.AnchorRight = 1f; _label.AnchorBottom = 1f;
        _label.AddThemeColorOverride("font_color", new Color(0.90f, 0.87f, 0.83f));
        _label.AddThemeFontSizeOverride("font_size", 11);
        _label.AddThemeColorOverride("font_outline_color", Colors.Black);
        _label.AddThemeConstantOverride("outline_size", 2);

        AddChild(_bar);
        AddChild(_label);
    }

    // ── lifecycle ─────────────────────────────────────────────────────────────

    public override void _Ready()
    {
        GlobalAutoLoad.SubscribeToSignal(
            nameof(Character.HealthChanged),
            Callable.From((string name, float cur, float max) =>
            {
                if (name != TrackedName) return;
                _bar.MaxValue = max;
                _bar.Value = cur;
                _label.Text = $"Growing Vines  {cur:F0}/{max:F0}";
                if (cur <= 0f) QueueFree();
            }));
    }
}
