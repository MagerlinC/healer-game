using System;
using System.Collections.Generic;
using Godot;
using healerfantasy;
using healerfantasy.Effects;

/// <summary>
/// Manages the Sanguine Siphon channel for the Blood Prince.
///
/// Created and added to the World scene (boss's parent node) by
/// <see cref="healerfantasy.SpellResources.BossBloodPrinceSanguineSiphonSpell.Apply"/>.
/// The node owns:
///   • A countdown timer for the full channel duration.
///   • One <see cref="Line2D"/> per linked target, drawn from the boss to that
///     target using the blood-link texture (updated every frame so both parties
///     can move freely during the channel).
///   • The <see cref="SanguineBloodLinkEffect"/> debuff on each target (deals
///     damage per tick and heals the boss).
///   • The <see cref="SanguineDrainDebuff"/> on the boss (tracks the health
///     target that cancels the channel early if the party bursts the boss).
///
/// The channel ends when:
///   a) The full <see cref="_channelDuration"/> elapses naturally.
///   b) <see cref="SanguineDrainDebuff"/> detects the boss has been damaged to
///      the target health and fires <see cref="SanguineDrainDebuff.OnHealthTargetReached"/>.
///   c) The boss dies.
///
/// <see cref="OnChannelFinished"/> is invoked in all three cases so the boss can
/// update its UI and internal state.
/// </summary>
public partial class SanguineSiphonChannelNode : Node2D
{
	const string BloodLinkTexturePath = "res://assets/enemies/the-blood-prince/blood-link.png";

	// ── config ────────────────────────────────────────────────────────────────
	readonly Character _boss;
	readonly List<Character> _targets;
	readonly float _channelDuration;
	readonly float _lifeLeechPerTick;

	// ── runtime ───────────────────────────────────────────────────────────────
	float _remaining;
	bool _cancelled;
	SanguineDrainDebuff _drainDebuff;
	readonly List<Line2D> _links = new();

	// ── callback ──────────────────────────────────────────────────────────────
	/// <summary>
	/// Invoked when the channel finishes (naturally or cancelled early).
	/// Wired by the spell's Apply to call TheBloodPrince.OnSanguineSiphonChannelEnded.
	/// </summary>
	public Action OnChannelFinished { get; set; }

	// ── ctor ──────────────────────────────────────────────────────────────────
	public SanguineSiphonChannelNode(
		Character boss,
		List<Character> targets,
		float channelDuration,
		float lifeLeechPerTick)
	{
		_boss = boss;
		_targets = targets;
		_channelDuration = channelDuration;
		_lifeLeechPerTick = lifeLeechPerTick;
		_remaining = channelDuration;
	}

	// ── lifecycle ─────────────────────────────────────────────────────────────
	public override void _Ready()
	{
		var linkTexture = GD.Load<Texture2D>(BloodLinkTexturePath);

		// Render the links above all characters so they're never obscured.
		ZIndex = 10;

		// Create a blood-link Line2D for every target.
		foreach (var target in _targets)
		{
			var line = new Line2D();
			line.Width = 60f;
			line.DefaultColor = new Color(0.75f, 0.05f, 0.05f, 0.85f);

			if (linkTexture != null)
			{
				line.Texture = linkTexture;
				line.TextureMode = Line2D.LineTextureMode.Tile;
				line.TextureRepeat = TextureRepeatEnum.Enabled;
			}

			// Line2D points are in the parent node's LOCAL space.
			// ToLocal() converts global world positions into that space correctly
			// regardless of any transforms on this node or its ancestors.
			line.AddPoint(ToLocal(_boss.GlobalPosition));
			line.AddPoint(ToLocal(target.GlobalPosition));
			AddChild(line);
			_links.Add(line);
		}

		// Apply the drain debuff to the boss — records its current health and
		// sets the 10% break threshold.  Must happen BEFORE we read HealthTarget
		// back in the spell's Apply (which queries the debuff after AddChild).
		_drainDebuff = new SanguineDrainDebuff(_channelDuration)
		{
			AbilityName = "Sanguine Siphon",
			Icon = GD.Load<Texture2D>(AssetConstants.SpellIconAssets + "enemy/the-blood-prince/sanguine-siphon.png"),
			Description = "Sanguine Siphon is active. Deal enough damage to break the channel.",
			SourceCharacterName = _boss.CharacterName,
			OnHealthTargetReached = () => EndChannel(true)
		};
		_boss.ApplyEffect(_drainDebuff);

		// Apply the blood-link leech debuff to each target.
		foreach (var target in _targets)
		{
			if (!IsInstanceValid(target) || !target.IsAlive) continue;

			target.ApplyEffect(new SanguineBloodLinkEffect(_channelDuration)
			{
				LifeLeechPerTick = _lifeLeechPerTick,
				Boss = _boss,
				AbilityName = "Sanguine Siphon",
				Description = $"Linked by blood magic — {_lifeLeechPerTick:F0} health drained per second.",
				SourceCharacterName = _boss.CharacterName,
				Icon = GD.Load<Texture2D>(AssetConstants.SpellIconAssets + "enemy/the-blood-prince/sanguine-siphon.png")
			});
		}

		GD.Print($"[SanguineSiphon] Channel started. Targets: {_targets.Count}, " +
		         $"duration: {_channelDuration:F1}s, leech/tick: {_lifeLeechPerTick:F0}.");
	}

	public override void _Process(double delta)
	{
		if (_cancelled) return;

		// End the channel if the boss died.
		if (!IsInstanceValid(_boss) || !_boss.IsAlive)
		{
			EndChannel(false);
			return;
		}

		_remaining -= (float)delta;

		// Update line endpoints every frame — characters can move.
		for (var i = 0; i < _targets.Count; i++)
		{
			if (i >= _links.Count) break;
			var target = _targets[i];
			var line = _links[i];

			if (!IsInstanceValid(target) || !target.IsAlive)
			{
				line.Visible = false;
				continue;
			}

			line.Visible = true;
			line.SetPointPosition(0, ToLocal(_boss.GlobalPosition));
			line.SetPointPosition(1, ToLocal(target.GlobalPosition));
		}

		if (_remaining <= 0f)
			EndChannel(false);
	}

	// ── public helpers ────────────────────────────────────────────────────────

	/// <summary>
	/// Returns the drain-target health as a fraction of the boss's MaxHealth
	/// (0 – 1). Safe to call immediately after AddChild (after _Ready runs).
	/// </summary>
	public float GetHealthTargetFraction()
	{
		if (_drainDebuff == null || _boss == null || _boss.MaxHealth <= 0f) return 0f;
		return Mathf.Clamp(_drainDebuff.HealthTarget / _boss.MaxHealth, 0f, 1f);
	}

	// ── private ───────────────────────────────────────────────────────────────

	/// <summary>
	/// Tears down the channel: removes visuals, removes debuffs, notifies the boss.
	/// Safe to call multiple times — subsequent calls are no-ops.
	/// </summary>
	/// <param name="triggeredByDrainDebuff">
	/// When <c>true</c> the drain debuff already expired itself; skip the manual
	/// RemoveEffect call to avoid a double-expiry / recursive loop.
	/// </param>
	void EndChannel(bool triggeredByDrainDebuff)
	{
		if (_cancelled) return;
		_cancelled = true;

		GD.Print($"[SanguineSiphon] Channel ended (triggered by drain debuff: {triggeredByDrainDebuff}).");

		// Remove blood-link debuffs from all targets.
		foreach (var target in _targets)
			if (IsInstanceValid(target))
				target.RemoveEffect("SanguineBloodLink");

		// Remove the drain debuff from the boss — but only when the debuff did NOT
		// trigger this call (if it did, it is already being removed by TickEffects).
		if (!triggeredByDrainDebuff && IsInstanceValid(_boss))
			_boss.RemoveEffect("SanguineDrain");

		// Free all Line2D visuals.
		foreach (var line in _links)
			if (IsInstanceValid(line))
				line.QueueFree();
		_links.Clear();

		// Notify the boss so it can update the cast bar and health marker.
		OnChannelFinished?.Invoke();

		QueueFree();
	}
}