using System;
using Godot;
using healerfantasy;
using healerfantasy.CombatLog;
using healerfantasy.SpellResources;

/// <summary>
/// Manages the Wax Rain channel for the Tallow Queen.
///
/// Created and added to the World scene (Queen's parent node) by
/// <see cref="BossQueenWaxRainSpell.Apply"/>. The node:
///
///   • Ticks once per second for <see cref="_channelDuration"/> seconds.
///   • Each tick deals <see cref="_damagePerTick"/> to every living party member.
///   • Ends early if the Queen dies.
///   • Invokes <see cref="OnChannelFinished"/> when the channel ends (naturally
///     or because the boss died), so the Queen can clear the BossCastBar.
/// </summary>
public partial class WaxRainChannelNode : Node2D
{
	// ── config ────────────────────────────────────────────────────────────────
	readonly Character _boss;
	readonly float _channelDuration;
	readonly float _damagePerTick;

	// ── runtime ───────────────────────────────────────────────────────────────
	float _remaining;
	float _tickTimer;
	bool _ended;
	CpuParticles2D _rain;

	// ── callback ──────────────────────────────────────────────────────────────
	/// <summary>Invoked when the channel finishes (naturally or boss death).</summary>
	public Action OnChannelFinished { get; set; }

	// ── ctor ──────────────────────────────────────────────────────────────────
	public WaxRainChannelNode(Character boss, float channelDuration, float damagePerTick)
	{
		_boss = boss;
		_channelDuration = channelDuration;
		_damagePerTick = damagePerTick;
		_remaining = channelDuration;
		_tickTimer = 1f; // first tick fires after 1 second
	}

	// ── lifecycle ─────────────────────────────────────────────────────────────
	public override void _Ready()
	{
		SpawnRain();
	}

	public override void _Process(double delta)
	{
		if (_ended) return;

		// End early if the boss dies.
		if (!IsInstanceValid(_boss) || !_boss.IsAlive)
		{
			EndChannel();
			return;
		}

		_remaining -= (float)delta;
		_tickTimer -= (float)delta;

		// Deal a tick of damage to all party members every second.
		if (_tickTimer <= 0f)
		{
			_tickTimer = 1f;
			DamageParty();
		}

		if (_remaining <= 0f)
			EndChannel();
	}

	// ── private ───────────────────────────────────────────────────────────────

	/// <summary>
	/// Spawns molten droplets falling from just above the top of the camera view
	/// across its full width. Droplets are thin streaks aligned to their velocity,
	/// and their randomised lifetime makes them "land" at varying depths.
	/// </summary>
	void SpawnRain()
	{
		var center = Vector2.Zero;
		var half = new Vector2(320f, 180f);
		if (GetViewport().GetCamera2D() is { } cam)
		{
			center = cam.GetScreenCenterPosition();
			half = GetViewport().GetVisibleRect().Size / (2f * cam.Zoom);
		}

		var streak = new Gradient();
		streak.SetColor(0, new Color(1f, 0.85f, 0.55f, 0f));
		streak.SetColor(1, new Color(1f, 0.9f, 0.65f, 1f));

		_rain = new CpuParticles2D
		{
			Amount = 140,
			Lifetime = 1.1f,
			LifetimeRandomness = 0.35f,
			Texture = new GradientTexture2D
			{
				Gradient = streak, Width = 2, Height = 10,
				FillFrom = new Vector2(0f, 0f), FillTo = new Vector2(0f, 1f)
			},
			Position = new Vector2(center.X, center.Y - half.Y - 12f),
			EmissionShape = CpuParticles2D.EmissionShapeEnum.Rectangle,
			EmissionRectExtents = new Vector2(half.X + 24f, 4f),
			Direction = new Vector2(0.08f, 1f),
			Spread = 4f,
			Gravity = new Vector2(0f, 300f),
			InitialVelocityMin = 220f,
			InitialVelocityMax = 300f,
			ScaleAmountMin = 0.7f,
			ScaleAmountMax = 1.3f,
			ZIndex = 20,
			Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add }
		};
		_rain.SetParticleFlag(CpuParticles2D.ParticleFlags.AlignYToVelocity, true);

		var fade = new Gradient();
		fade.SetColor(0, new Color(1f, 0.8f, 0.45f, 0.9f));
		fade.SetColor(1, new Color(1f, 0.5f, 0.15f, 0.6f));
		_rain.ColorRamp = fade;

		AddChild(_rain);
	}

	/// <summary>
	/// Stops emitting and hands the rain to the parent so droplets already in
	/// the air finish falling after this node frees itself.
	/// </summary>
	void StopRain()
	{
		if (_rain == null || !IsInstanceValid(_rain) || GetParent() is not { } parent) return;
		_rain.Emitting = false;
		_rain.Reparent(parent);
		var rain = _rain;
		GetTree().CreateTimer(rain.Lifetime).Timeout += () =>
		{
			if (IsInstanceValid(rain)) rain.QueueFree();
		};
		_rain = null;
	}

	void DamageParty()
	{
		foreach (var node in GetTree().GetNodesInGroup("party"))
		{
			if (node is not Character target || !target.IsAlive) continue;

			target.TakeDamage(_damagePerTick);
			target.RaiseFloatingCombatText(_damagePerTick, false, (int)SpellSchool.Generic, false);

			CombatLog.Record(new CombatEventRecord
			{
				Timestamp = Time.GetTicksMsec() / 1000.0,
				SourceName = _boss.CharacterName,
				TargetName = target.CharacterName,
				AbilityName = "Wax Rain",
				Amount = _damagePerTick,
				Type = CombatEventType.Damage,
				IsCrit = false,
				Description = "Burned by the wax raining from the spire."
			});
		}
	}

	/// <summary>
	/// Interrupts the channel immediately, firing <see cref="OnChannelFinished"/>
	/// so the caller (the Queen) can clean up the cast bar and reset its state.
	/// Safe to call more than once — guarded by <see cref="_ended"/>.
	/// </summary>
	public void Cancel() => EndChannel();

	void EndChannel()
	{
		if (_ended) return;
		_ended = true;

		StopRain();
		OnChannelFinished?.Invoke();
		GD.Print("[WaxRain] Channel ended.");
		QueueFree();
	}
}