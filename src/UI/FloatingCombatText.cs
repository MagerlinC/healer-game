using Godot;
using healerfantasy.SpellResources;

namespace healerfantasy.UI;

/// <summary>
/// A single floating combat text number that appears above a character model,
/// drifts upward, and fades out over ~1.5 seconds.
///
/// Spawn instances via <see cref="FloatingCombatTextManager"/>; do not add to
/// the scene tree manually.
/// </summary>
public partial class FloatingCombatText : Label
{
	/// <summary>
	/// Create a configured but not-yet-added label. Set <c>Position</c> before
	/// calling <c>AddChild</c> so <c>_Ready</c> sees the correct origin.
	/// </summary>
	public static FloatingCombatText Create(float amount, bool isHealing, SpellSchool school, bool isCrit)
	{
		var label = new FloatingCombatText();

		var rounded = Mathf.RoundToInt(amount);
		label.Text = (isHealing ? $"+{rounded}" : $"{rounded}") + (isCrit ? "!" : "");
		label.AddThemeFontSizeOverride("font_size", isCrit ? 32 : 16);
		label.AddThemeColorOverride("font_color", SchoolColor(school));
		label.HorizontalAlignment = HorizontalAlignment.Center;
		label.VerticalAlignment = VerticalAlignment.Center;
		label.AutowrapMode = TextServer.AutowrapMode.Off;
		label.MouseFilter = MouseFilterEnum.Ignore;

		return label;
	}

	public override void _Ready()
	{
		// Small random horizontal drift so stacked numbers don't perfectly overlap.
		var offsetX = GD.Randf() * 20f - 10f;

		var tween = CreateTween();
		tween.SetParallel(true);

		// Float upward.
		tween.TweenProperty(this, "position:y", Position.Y - 30f, 1.5f)
			.SetEase(Tween.EaseType.Out)
			.SetTrans(Tween.TransitionType.Quad);

		// Slight horizontal drift.
		tween.TweenProperty(this, "position:x", Position.X + offsetX, 1.5f)
			.SetEase(Tween.EaseType.Out);

		// Fade out — hold full opacity for 0.6 s, then fade over the remaining 0.9 s.
		tween.TweenProperty(this, "modulate:a", 0f, 0.9f)
			.SetDelay(0.6f);

		// Self-destruct once the animation completes.
		// Use tween.Finished rather than GetTree().CreateTimer so the callback is
		// automatically cancelled if the node is freed mid-animation (e.g. on a
		// scene change). SceneTreeTimers are not bound to any node and would fire
		// on a disposed FCT node 1–1.5 s into the next scene, crashing the game.
		tween.Finished += QueueFree;
	}

	// ── colour palette ───────────────────────────────────────────────────────
	static Color SchoolColor(SpellSchool school)
	{
		return school switch
		{
			SpellSchool.Holy => new Color(1.00f, 0.90f, 0.30f), // gold
			SpellSchool.Nature => new Color(0.40f, 0.90f, 0.10f), // bright green
			SpellSchool.Void => new Color(0.72f, 0.40f, 1.00f), // purple
			SpellSchool.Chronomancy => new Color(0.20f, 0.85f, 1.00f), // cyan
			_ => new Color(1.00f, 1.00f, 1.00f) // white
		};
	}
}