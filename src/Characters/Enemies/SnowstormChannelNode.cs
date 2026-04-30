using System;
using Godot;
using healerfantasy;
using healerfantasy.CombatLog;
using healerfantasy.SpellResources;

/// <summary>
/// Manages the Snowstorm channel for the Queen of the Frozen Wastes.
///
/// Created and added to the World scene (Queen's parent node) by
/// <see cref="BossQueenSnowstormSpell.Apply"/>. The node:
///
///   • Ticks once per second for <see cref="_channelDuration"/> seconds.
///   • Each tick deals <see cref="_damagePerTick"/> to every living party member.
///   • Ends early if the Queen dies.
///   • Invokes <see cref="OnChannelFinished"/> when the channel ends (naturally
///     or because the boss died), so the Queen can clear the BossCastBar.
/// </summary>
public partial class SnowstormChannelNode : Node2D
{
	// ── config ────────────────────────────────────────────────────────────────
	readonly Character _boss;
	readonly float _channelDuration;
	readonly float _damagePerTick;

	// ── runtime ───────────────────────────────────────────────────────────────
	float _remaining;
	float _tickTimer;
	bool _ended;

	// ── callback ──────────────────────────────────────────────────────────────
	/// <summary>Invoked when the channel finishes (naturally or boss death).</summary>
	public Action OnChannelFinished { get; set; }

	// ── ctor ──────────────────────────────────────────────────────────────────
	public SnowstormChannelNode(Character boss, float channelDuration, float damagePerTick)
	{
		_boss = boss;
		_channelDuration = channelDuration;
		_damagePerTick = damagePerTick;
		_remaining = channelDuration;
		_tickTimer = 1f; // first tick fires after 1 second
	}

	// ── lifecycle ─────────────────────────────────────────────────────────────
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
				AbilityName = "Snowstorm",
				Amount = _damagePerTick,
				Type = CombatEventType.Damage,
				IsCrit = false,
				Description = "Battered by the Queen's raging blizzard."
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

		OnChannelFinished?.Invoke();
		GD.Print("[Snowstorm] Channel ended.");
		QueueFree();
	}
}