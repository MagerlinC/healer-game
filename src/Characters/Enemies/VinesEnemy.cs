using System.Collections.Generic;
using Godot;
using healerfantasy.LastLight;
using healerfantasy;
using healerfantasy.Effects;
using healerfantasy.SpellResources;

/// <summary>
/// Rune of Nature — a vine creature that spawns every
/// <see cref="GameConstants.RuneNatureVinesInterval"/> seconds during boss fights.
///
/// On spawn the vines latch onto a random alive party member and deal
/// <see cref="GameConstants.RuneNatureVinesDamagePerSecond"/> damage per second
/// to that target until the vines are killed.
///
/// The vines have their own health bar displayed below the boss bar (managed by
/// <see cref="healerfantasy.UI.GameUI"/>).  Killing them removes them from the
/// scene and clears the health bar.
/// </summary>
public partial class VinesEnemy : EnemyCharacter
{
	/// <summary>
	/// Animation clips (frames / fps from the original sprites). These are gameplay:
	/// an attack's spell is cast when its clip finishes, so a clip's length is its wind-up.
	/// </summary>
	static readonly Dictionary<string, CharacterVisual.Clip> Clips = new()
	{
		["idle"] = new(3 / 3f, Loop: true)
	};

	// ── public state ──────────────────────────────────────────────────────────

	/// <summary>The party-member character this vine is currently latched onto.</summary>
	public Character? AttachedTarget { get; private set; }

	/// <summary>Display name shown on the vines health bar.</summary>
	public string DisplayName { get; private set; } = "Growing Vines";

	// ── internal ──────────────────────────────────────────────────────────────

	float _damageTimer;
	const float DamageInterval = 1.0f;

	/// <summary>Cached effect ID so we can remove it in <see cref="_ExitTree"/>.</summary>
	string _graspedEffectId = "";

	CharacterVisual _visual = null!;

	// ── ctor ──────────────────────────────────────────────────────────────────

	public VinesEnemy(Character target, string instanceName)
	{
		MaxHealth = (RunState.Instance.CurrentDungeonIndex + 1) * GameConstants.RuneNatureVinesMaxHealthPerDungeonTier;
		IsFriendly = false;
		CharacterName = instanceName;
		DisplayName = $"Vine ({target.CharacterName})";
		AttachedTarget = target;
	}

	// ── lifecycle ─────────────────────────────────────────────────────────────

	public override void _Ready()
	{
		base._Ready();
		AddToGroup(GameConstants.VinesGroupName);

		// ── visual ─────────────────────────────────────────────────────────
		_visual = CharacterVisual.Attach(this, Clips);

		// Position near the attached target if possible, else use zero.
		if (AttachedTarget != null)
			GlobalPosition = AttachedTarget.GlobalPosition + new Vector2(0f, -30f);

		// Apply the grasped effect to the targeted party member so their portrait
		// shows an effect indicator for the duration of this vine's life.
		if (AttachedTarget != null)
		{
			var effect = new GraspedByVineEffect(CharacterName);
			_graspedEffectId = effect.EffectId;
			AttachedTarget.ApplyEffect(effect);
		}
	}

	public override void _ExitTree()
	{
		// Remove the grasped effect whether the vine died normally (0.8s timer →
		// QueueFree) or was force-freed by VinesManager when the boss died.
		if (_graspedEffectId != "" && AttachedTarget != null
		                           && IsInstanceValid(AttachedTarget))
			AttachedTarget.RemoveEffect(_graspedEffectId);

		base._ExitTree(); // Unregisters this node from GlobalAutoLoad.SignalMap.
	}

	public override void _Process(double delta)
	{
		base._Process(delta);
		if (!IsAlive || AttachedTarget == null) return;

		// Follow target loosely.
		GlobalPosition = AttachedTarget.GlobalPosition + new Vector2(0f, -30f);

		// Periodic damage tick.
		_damageTimer -= (float)delta;
		if (_damageTimer <= 0f)
		{
			_damageTimer = DamageInterval;
			if (AttachedTarget.IsAlive)
				AttachedTarget.TakeDamage(GameConstants.RuneNatureVinesDamagePerSecond);
		}
	}

	// ── death ─────────────────────────────────────────────────────────────────

	protected override void ApplyDeathVisuals()
	{
		if (_visual != null) _visual.Modulate = new Color(0.5f, 0.5f, 0.5f);
		// Remove from scene shortly after death so the health bar can clean up.
		var timer = new Timer { WaitTime = 0.8f, OneShot = true, Autostart = true };
		AddChild(timer);
		timer.Timeout += () => QueueFree();
	}
}