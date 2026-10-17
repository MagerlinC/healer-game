#nullable enable
using Godot;
using healerfantasy.LastLight;
using healerfantasy.LastLight.Hud;
using healerfantasy.SpellResources;
using healerfantasy.UI;

namespace healerfantasy.Merchant;

/// <summary>
/// Short, paused presentation for the Stone of Rebirth saving a party member.
///
/// Sequence
/// ────────
///   1. The tree is paused. The fallen character stays at 0 health, so its
///      <see cref="SilhouetteRig"/> (which processes while paused) topples.
///   2. After <see cref="FallHold"/> seconds the character is revived; the rig
///      rises back up and "Stone of Rebirth used" appears over their head.
///   3. After <see cref="RevivedHold"/> more seconds the tree is unpaused, the
///      heal number pops, and the label fades out before this node frees itself.
///
/// The character is never marked as removed and <c>Died</c> is never emitted.
/// <see cref="Character.TakeDamage"/> and <see cref="Character.Heal"/> both ignore a
/// 0-health character, so nothing can touch them while they are down.
/// </summary>
public partial class StoneOfRebirthSequence : CanvasLayer
{
	/// <summary>How long the character lies fallen before getting back up.</summary>
	const float FallHold = 0.7f;

	/// <summary>How long the game stays paused after the revive (covers the rise).</summary>
	const float RevivedHold = 1.4f;

	const float LabelFadeOut = 0.6f;

	static readonly Color Gold = new(1f, 0.84f, 0.45f);

	Character _character = null!;
	float _reviveAmount;
	bool _pausedByUs;
	Label? _label;

	/// <summary>
	/// Start the sequence for <paramref name="character"/>, who will come back
	/// with <paramref name="reviveAmount"/> health. Adds itself to the scene.
	/// </summary>
	public static void Play(Character character, float reviveAmount)
	{
		var seq = new StoneOfRebirthSequence { _character = character, _reviveAmount = reviveAmount };
		character.GetTree().CurrentScene.AddChild(seq);
	}

	public override void _Ready()
	{
		Layer = 9; // above floating combat text (8), below the main UI (10)
		ProcessMode = ProcessModeEnum.Always;

		var tree = GetTree();
		_pausedByUs = !tree.Paused;
		tree.Paused = true;

		var tween = CreateTween().SetPauseMode(Tween.TweenPauseMode.Process);
		tween.TweenInterval(FallHold);
		tween.TweenCallback(Callable.From(Revive));
		tween.TweenInterval(RevivedHold);
		tween.TweenCallback(Callable.From(Resume));
	}

	void Revive()
	{
		if (!IsInstanceValid(_character))
		{
			Resume();
			return;
		}

		// Revive() bypasses the IsAlive guard that Heal() has, which is required
		// here because CurrentHealth is 0.
		_character.Revive(_reviveAmount);
		ShowLabel();
	}

	void ShowLabel()
	{
		_label = new Label
		{
			Text = "Stone of Rebirth used",
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center,
			AutowrapMode = TextServer.AutowrapMode.Off,
			MouseFilter = Control.MouseFilterEnum.Ignore,
			Modulate = new Color(1f, 1f, 1f, 0f)
		};
		_label.AddThemeFontOverride("font", LastLightHud.Serif);
		_label.AddThemeFontSizeOverride("font_size", 20);
		_label.AddThemeColorOverride("font_color", Gold);
		_label.AddThemeColorOverride("font_outline_color", new Color(0f, 0f, 0f, 0.9f));
		_label.AddThemeConstantOverride("outline_size", 6);
		_label.AddThemeColorOverride("font_shadow_color", new Color(LastLightHud.Ember, 0.5f));
		_label.AddThemeConstantOverride("shadow_offset_x", 0);
		_label.AddThemeConstantOverride("shadow_offset_y", 0);
		_label.AddThemeConstantOverride("shadow_outline_size", 12);
		AddChild(_label);

		// Centre the label just above the standing figure's head.
		var head = HeadScreenPosition();
		var size = _label.GetCombinedMinimumSize();
		_label.Size = size;
		_label.Position = head - new Vector2(size.X * 0.5f, size.Y - 2f);

		var tween = _label.CreateTween().SetPauseMode(Tween.TweenPauseMode.Process).SetParallel();
		tween.TweenProperty(_label, "modulate:a", 1f, 0.25f);
		tween.TweenProperty(_label, "position:y", _label.Position.Y - 14f, 0.6f)
			.SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Quad);
	}

	/// <summary>Top-centre of the character's standing figure, in viewport pixels.</summary>
	Vector2 HeadScreenPosition()
	{
		if (_character.GetNodeOrNull<SilhouetteRig>("SilhouetteRig") is { } rig)
		{
			var rect = rig.GetScreenRect();
			return new Vector2(rect.GetCenter().X, rect.Position.Y - 6f);
		}

		return _character.GetViewport().GetCanvasTransform() * _character.GlobalPosition - new Vector2(0f, 48f);
	}

	void Resume()
	{
		// Leave the game paused if the player opened the pause menu meanwhile.
		if (_pausedByUs && !PauseMenu.IsOpen)
			GetTree().Paused = false;

		// The heal number floats on the (now running) combat-text layer.
		if (IsInstanceValid(_character) && _character.IsAlive)
			_character.RaiseFloatingCombatText(_reviveAmount, true, (int)SpellSchool.Flame, false);

		if (_label == null)
		{
			QueueFree();
			return;
		}

		var tween = CreateTween().SetPauseMode(Tween.TweenPauseMode.Process);
		tween.TweenProperty(_label, "modulate:a", 0f, LabelFadeOut).SetDelay(0.3f);
		tween.Finished += QueueFree;
	}
}
