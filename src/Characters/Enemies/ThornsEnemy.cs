using System.Collections.Generic;
using Godot;
using healerfantasy.LastLight;
using healerfantasy;
using healerfantasy.Effects;
using healerfantasy.SpellResources;

/// <summary>
/// Rune of Nightfall — a thorn creature that spawns every
/// <see cref="GameConstants.RuneNightfallThornsInterval"/> seconds during boss fights.
///
/// On spawn the thorns latch onto a random alive party member and deal
/// <see cref="GameConstants.RuneNightfallThornsDamagePerSecond"/> damage per second
/// to that target until the thorns are killed.
///
/// The thorns have their own health bar displayed below the boss bar (managed by
/// <see cref="healerfantasy.UI.GameUI"/>).  Killing them removes them from the
/// scene and clears the health bar.
/// </summary>
public partial class ThornsEnemy : EnemyCharacter
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

	/// <summary>The party-member character this thorn is currently latched onto.</summary>
	public Character? AttachedTarget { get; private set; }

	/// <summary>Display name shown on the thorns health bar.</summary>
	public string DisplayName { get; private set; } = "Grasping Thorns";

	// ── internal ──────────────────────────────────────────────────────────────

	float _damageTimer;
	const float DamageInterval = 1.0f;

	/// <summary>Cached effect ID so we can remove it in <see cref="_ExitTree"/>.</summary>
	string _graspedEffectId = "";

	CharacterVisual _visual = null!;

	// ── ctor ──────────────────────────────────────────────────────────────────

	public ThornsEnemy(Character target, string instanceName)
	{
		// By tier rather than run progress, so dev test fights get the same thorns as a real run.
		MaxHealth = RunState.Instance.CurrentDungeon.Tier * GameConstants.RuneNightfallThornsMaxHealthPerDungeonTier;
		IsFriendly = false;
		CharacterName = instanceName;
		DisplayName = $"Thorns ({target.CharacterName})";
		AttachedTarget = target;
	}

	// ── lifecycle ─────────────────────────────────────────────────────────────

	public override void _Ready()
	{
		base._Ready();
		AddToGroup(GameConstants.ThornsGroupName);

		// ── visual ─────────────────────────────────────────────────────────
		_visual = CharacterVisual.Attach(this, Clips);

		// Position near the attached target if possible, else use zero.
		if (AttachedTarget != null)
			GlobalPosition = AttachedTarget.GlobalPosition + new Vector2(0f, -30f);

		// Apply the grasped effect to the targeted party member so their portrait
		// shows an effect indicator for the duration of this thorn's life.
		if (AttachedTarget != null)
		{
			var effect = new GraspedByThornsEffect(CharacterName);
			_graspedEffectId = effect.EffectId;
			AttachedTarget.ApplyEffect(effect);
		}
	}

	public override void _ExitTree()
	{
		// Remove the grasped effect whether the thorn died normally (0.8s timer →
		// QueueFree) or was force-freed by ThornsManager when the boss died.
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
				AttachedTarget.TakeDamage(GameConstants.RuneNightfallThornsDamagePerSecond);
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