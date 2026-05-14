using Godot;
using healerfantasy;

namespace healerfantasy.Effects;

/// <summary>
/// A ghostly visual duplicate of the player, spawned while Mirror Image is active.
/// Floats beside the caster, mirrors their idle animation, and plays a one-shot
/// cast animation each time the mirror echo fires.
///
/// Added as a child of the player Character node so it inherits world position.
/// Freed automatically when Mirror Image expires via <see cref="MirrorImageEffect.OnExpired"/>.
/// </summary>
public partial class MirrorImageClone : Node2D
{
	/// <summary>Horizontal offset from the parent character, in local space.</summary>
	const float XOffset = -30f;

	AnimatedSprite2D _sprite = null!;

	public override void _Ready()
	{
		_sprite = new AnimatedSprite2D();
		_sprite.Scale = new Vector2(GameConstants.HealerSpriteScale, GameConstants.HealerSpriteScale);

		// Face the opposite direction to the player for a true mirror-image look.
		// The healer sprite is drawn facing left, so FlipH=true makes it face right.
		_sprite.FlipH = true;

		// Build SpriteFrames from the same healer assets the Player uses.
		var frames = new SpriteFrames();

		// SpriteFrames ships with one "default" animation — rename isn't exposed in
		// the C# API so we add ours separately. The unused "default" is harmless.
		frames.AddAnimation("idle");
		frames.SetAnimationSpeed("idle", 8.0);
		frames.SetAnimationLoop("idle", true);
		foreach (var i in new[] { 1, 2, 3 })
			frames.AddFrame("idle", GD.Load<Texture2D>($"res://assets/characters/healer/idle{i}.png"));

		frames.AddAnimation("cast");
		frames.SetAnimationSpeed("cast", 4.0);
		frames.SetAnimationLoop("cast", false); // one-shot; returns to idle on finish
		foreach (var i in new[] { 1, 2, 3, 4 })
			frames.AddFrame("cast", GD.Load<Texture2D>($"res://assets/characters/healer/cast{i}.png"));

		_sprite.SpriteFrames = frames;
		_sprite.Play("idle");
		_sprite.AnimationFinished += OnAnimationFinished;

		// ── Ghostly chronomancy shader ────────────────────────────────────────────
		// Blue-cyan tint with ~60% opacity so the clone reads as ethereal.
		// A sine-wave pulse on alpha gives it a faint breathing / shimmering effect.
		var shader = new Shader();
		shader.Code = """
		              shader_type canvas_item;
		              uniform float pulse_speed : hint_range(0.5, 4.0) = 1.8;
		              uniform float pulse_strength : hint_range(0.0, 0.3) = 0.12;
		              uniform float base_alpha : hint_range(0.0, 1.0) = 0.58;
		              uniform vec3 tint_color : source_color = vec3(0.35, 0.72, 1.0);
		              uniform float tint_strength : hint_range(0.0, 1.0) = 0.52;
		              void fragment() {
		              	vec4 col = texture(TEXTURE, UV);
		              	col.rgb = mix(col.rgb, tint_color, tint_strength * col.a);
		              	float pulse = sin(TIME * pulse_speed) * pulse_strength;
		              	col.a *= clamp(base_alpha + pulse, 0.0, 1.0);
		              	COLOR = col;
		              }
		              """;

		var mat = new ShaderMaterial();
		mat.Shader = shader;
		_sprite.Material = mat;

		AddChild(_sprite);

		// Offset so the clone hovers to the side of the caster, not on top of them.
		Position = new Vector2(XOffset, 0f);

		ZIndex = -1; // render behind the player
	}

	/// <summary>
	/// Play a one-shot cast animation. When the animation finishes, the clone
	/// automatically returns to its idle loop (see <see cref="OnAnimationFinished"/>).
	/// </summary>
	public void PlayCast()
	{
		_sprite?.Play("cast");
	}

	void OnAnimationFinished()
	{
		// Cast is a one-shot; return to the idle loop when it completes.
		if (_sprite?.Animation == "cast")
			_sprite.Play("idle");
	}
}
