using Godot;

/// <summary>
/// Short-lived visual spawned when <see cref="BossQueenBurstOfWinterSpell"/> resolves.
///
/// The burst.png asset is loaded as a centred <see cref="Sprite2D"/>. The node
/// starts at a very small scale, then tweens outward while fading to transparent
/// over <see cref="ExpandDuration"/> seconds before freeing itself.
/// No game-logic lives here — damage and knockback are handled by the spell class.
/// </summary>
public partial class BurstOfWinterNovaEffect : Node2D
{
	const string BurstTexturePath = "res://assets/enemies/queen-of-the-frozen-wastes/burst.png";

	/// <summary>Starting scale of the ring (tiny, centred on the boss).</summary>
	const float StartScale = 0.05f;

	/// <summary>Final scale reached at the end of the expand. Tune if the ring looks too large/small.</summary>
	const float EndScale = 0.65f;

	/// <summary>Seconds the ring takes to expand and fade out.</summary>
	const float ExpandDuration = 0.4f;

	public override void _Ready()
	{
		// Render above characters so the burst is clearly visible.
		ZIndex = 20;

		var texture = GD.Load<Texture2D>(BurstTexturePath);
		if (texture == null)
		{
			GD.PrintErr("[BurstOfWinterNovaEffect] Could not load burst.png — effect will be invisible.");
			QueueFree();
			return;
		}

		var sprite = new Sprite2D
		{
			Texture = texture,
			Scale   = new Vector2(StartScale, StartScale),
			Modulate = Colors.White
		};
		AddChild(sprite);

		// Expand + fade in parallel, then free the node.
		var tween = CreateTween();
		tween.SetParallel(true);

		tween.TweenProperty(sprite, "scale",
				new Vector2(EndScale, EndScale), ExpandDuration)
			.SetEase(Tween.EaseType.Out)
			.SetTrans(Tween.TransitionType.Quad);

		tween.TweenProperty(sprite, "modulate",
				new Color(1f, 1f, 1f, 0f), ExpandDuration)
			.SetEase(Tween.EaseType.In)
			.SetTrans(Tween.TransitionType.Quad);

		// Free after the tween finishes (parallel tweens all share the same duration).
		GetTree().CreateTimer(ExpandDuration).Timeout += QueueFree;
	}
}
