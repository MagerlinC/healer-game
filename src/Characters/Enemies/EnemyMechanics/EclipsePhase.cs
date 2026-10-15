using System;
using Godot;
using healerfantasy;
using healerfantasy.CombatLog;
using healerfantasy.LastLight;
using healerfantasy.SpellResources;

/// <summary>
/// The Infinite Abyss's Eclipse: the dark swallows the arena and the Lightkeeper's
/// light shrinks to a small pocket around her. Any ally left outside it takes heavy
/// damage. Flame casts brighten the light (see <see cref="LightMechanics"/>) and push
/// the pocket back out; Gloom shrinks it further.
///
/// Fades in, holds for <see cref="Duration"/>, fades out, then raises
/// <see cref="Finished"/> and frees itself. Ends early if the boss dies.
/// </summary>
public partial class EclipsePhase : Node
{
	public float Duration { get; set; } = 7f;
	public float DamagePerTick { get; set; } = 12f;
	public Character Boss { get; set; } = null!;
	public event Action Finished;

	const float Fade = 0.8f;
	const float TickInterval = 0.5f;
	const float OpenRadius = 0.7f;   // pocket radius before the eclipse closes in (screen heights)
	const float EclipseRadius = 0.2f;  // pocket radius at full eclipse, before brightening
	const float Squash = 1.5f;       // matches creeping_dark.gdshader

	CreepingDarkness _dark = null!;
	float _t, _tick;
	bool _done;

	public override void _Ready()
	{
		// Above the arena, below the HUD (GameUI is on layer 10).
		var layer = new CanvasLayer { Layer = 1 };
		AddChild(layer);
		_dark = new CreepingDarkness { FollowHealer = true, MaxDark = 0.86f, Radius = OpenRadius };
		_dark.Modulate = new Color(1f, 1f, 1f, 0f);
		layer.AddChild(_dark);
	}

	public override void _Process(double delta)
	{
		if (_done) return;
		var dt = (float)delta;
		_t += dt;

		var bossGone = !IsInstanceValid(Boss) || !Boss.IsAlive;
		if (bossGone && _t < Duration - Fade) _t = Duration - Fade; // wind down at once

		var amount = Mathf.Min(Mathf.Clamp(_t / Fade, 0f, 1f), Mathf.Clamp((Duration - _t) / Fade, 0f, 1f));
		_dark.Modulate = new Color(1f, 1f, 1f, amount);
		_dark.Radius = Mathf.Lerp(OpenRadius, EclipseRadius * Brightness(), amount);

		if (amount >= 0.99f && !bossGone)
		{
			_tick -= dt;
			if (_tick <= 0f)
			{
				_tick = TickInterval;
				BurnAlliesOutsideTheLight();
			}
		}

		if (_t >= Duration)
		{
			_done = true;
			Finished?.Invoke();
			QueueFree();
		}
	}

	/// <summary>How far the Lightkeeper's light currently reaches, relative to normal.</summary>
	float Brightness()
	{
		foreach (var node in GetTree().GetNodesInGroup(GameConstants.PartyGroupName))
			if (node is Character c && c.CharacterName == GameConstants.LightkeeperName)
				return Mathf.Pow(Mathf.Clamp(LightMechanics.LightMultiplier(c), 0.5f, 1.5f), 1.6f);
		return 1f;
	}

	void BurnAlliesOutsideTheLight()
	{
		var size = GetViewport().GetVisibleRect().Size;
		var aspect = size.Y > 0f ? size.X / size.Y : 1.7778f;
		// Same centre and reach as the pocket CreepingDarkness draws.
		var centre = LastLightStyle.LightFeetUv.Lerp(LastLightStyle.LightUv, 0.35f);
		var reach = _dark.Radius * Mathf.Lerp(0.9f, 1f, Mathf.Clamp(LastLightStyle.LightStrength, 0f, 1.2f)) * 0.9f;

		foreach (var node in GetTree().GetNodesInGroup(GameConstants.PartyGroupName))
		{
			if (node is not Character c || !c.IsAlive || c.CharacterName == GameConstants.LightkeeperName) continue;

			var feet = c.GetGlobalTransformWithCanvas() * new Vector2(0f, 20f);
			var q = feet / size - centre;
			q.X *= aspect;
			q.Y *= Squash;
			if (q.Length() <= reach) continue;

			c.TakeDamage(DamagePerTick);
			c.RaiseFloatingCombatText(DamagePerTick, false, (int)SpellSchool.Umbra, false);
			CombatLog.Record(new CombatEventRecord
			{
				Timestamp = Time.GetTicksMsec() / 1000.0,
				SourceName = GameConstants.AbyssBossName,
				TargetName = c.CharacterName,
				AbilityName = "Eclipse",
				Amount = DamagePerTick,
				Type = CombatEventType.Damage,
				IsCrit = false,
				Description = "Left outside the light during the Eclipse."
			});
		}
	}
}
