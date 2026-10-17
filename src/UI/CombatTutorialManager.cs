#nullable enable
using Godot;
using healerfantasy.SpellSystem;

namespace healerfantasy.UI;

/// <summary>
/// Manages the three in-combat tutorial overlays:
///
///  • Deflect  — shown when the parry window opens (0.5 s before a parryable
///    cast lands), so the overlay appears exactly at the moment the player
///    can act, not at the start of the long wind-up.
///  • Dispel   — first time a harmful, dispellable effect is applied to the party.
///  • Detonation Zone — first time a Detonation Zone is placed.
///
/// Each overlay is shown at most once per save file (persisted in
/// <see cref="PlayerProgressStore"/>), and at most once per session (local bool
/// guard to avoid re-showing after the flag is set).
///
/// Add to the World scene via <see cref="Init"/> after the GameUI is ready.
/// </summary>
public partial class CombatTutorialManager : Node
{
	GameUI? _ui;
	Player? _player;

	// Per-session guards.
	bool _targetingShown;
	bool _deflectShown;
	bool _dispelShown;
	bool _detonationShown;

	// Deflect tutorial timing — we wait until the parry window actually opens
	// (ParryWindowDuration seconds before cast end) rather than showing at the
	// very start of the wind-up.
	bool _deflectPending; // a windup is in progress and the tutorial hasn't shown yet
	float _windupDuration; // total windup duration (seconds)
	float _windupElapsed; // time elapsed since WindupStarted fired

	// ── public API ────────────────────────────────────────────────────────────

	/// <summary>
	/// Provide the GameUI reference used to locate spell-slot screen positions,
	/// and the player used to cast Deflect when the Deflect tutorial is dismissed.
	/// Call this from World._Ready() before adding the node to the tree.
	/// </summary>
	public void Init(GameUI ui, Player player)
	{
		_ui = ui;
		_player = player;
	}

	// ── lifecycle ─────────────────────────────────────────────────────────────

	public override void _Ready()
	{
		ParryWindowManager.WindupStarted += OnWindupStarted;
		ParryWindowManager.WindupEnded += OnWindupEnded;
		CombatTutorialEvents.HarmfulEffectApplied += OnHarmfulEffectApplied;
		CombatTutorialEvents.DetonationZoneCast += OnDetonationZoneCast;

		// Show the targeting tutorial at the very start of the player's first combat.
		// Deferred so the GameUI layout is fully resolved before we query any rects.
		if (!PlayerProgressStore.HasSeenTargetingTutorial)
			Callable.From(ShowTargetingTutorial).CallDeferred();
	}

	public override void _ExitTree()
	{
		ParryWindowManager.WindupStarted -= OnWindupStarted;
		ParryWindowManager.WindupEnded -= OnWindupEnded;
		CombatTutorialEvents.HarmfulEffectApplied -= OnHarmfulEffectApplied;
		CombatTutorialEvents.DetonationZoneCast -= OnDetonationZoneCast;

		_deflectPending = false;
	}

	public override void _Process(double delta)
	{
		// Tick the deflect tutorial delay. Once the parry window opens we show
		// the overlay so the player can immediately act on what they're reading.
		if (!_deflectPending) return;

		_windupElapsed += (float)delta;

		var showAt = _windupDuration - ParryWindowManager.ParryWindowDuration;
		if (_windupElapsed >= showAt)
		{
			_deflectPending = false;
			ShowDeflectTutorial();
		}
	}

	// ── event handlers ────────────────────────────────────────────────────────

	void OnWindupStarted(string spellName, Texture2D icon, float duration)
	{
		if (_deflectShown || PlayerProgressStore.HasSeenDeflectTutorial) return;

		// Don't show the overlay yet — start the countdown to the parry window.
		_deflectPending = true;
		_windupDuration = duration;
		_windupElapsed = 0f;
	}

	/// <summary>
	/// The windup resolved (deflected, hit, or cancelled) before the tutorial timer
	/// fired. Cancel the pending show so a stale overlay doesn't appear mid-fight.
	/// </summary>
	void OnWindupEnded()
	{
		_deflectPending = false;
	}

	void ShowTargetingTutorial()
	{
		if (_targetingShown || PlayerProgressStore.HasSeenTargetingTutorial) return;
		_targetingShown = true;

		TutorialHighlightOverlay.Show(
			GetTree(),
			"Combat Tutorial - Targeting",
			"Cast a spell while hovering your mouse over a character to target them directly.\n\n" +
			"With no target hovered, harmful spells target the boss and helpful spells target yourself.\n\n" +
			"Click a party frame to lock in a default target - spells will target them if no other target is being hovered.",
			new Rect2(),
			null,
			PlayerProgressStore.MarkTargetingTutorialSeen
		);
	}

	void ShowDeflectTutorial()
	{
		// Double-check the guard in case WindupEnded and the timer fired on the
		// same frame (order of _Process vs event is non-deterministic).
		if (_deflectShown || PlayerProgressStore.HasSeenDeflectTutorial) return;
		_deflectShown = true;

		var spotlight = GetSpotlightRect(_ui?.GenericBar?.DeflectPanel);
		var key = GetKeybindLabel("deflect");

		TutorialHighlightOverlay.Show(
			GetTree(),
			"Deflect!",
			$"The enemy is casting a deadly ability, which can be deflected with the right timing!\n\n" +
			$"Press [{key}] when the ability is about to go off to deflect the incoming ability, " +
			$"preventing the damage it would deal.",
			spotlight,
			"deflect",
			() =>
			{
				PlayerProgressStore.MarkDeflectTutorialSeen();
				// The dismissing key press happened while paused — honour it as a deflect.
				_player?.CastDeflect();
			}
		);
	}

	void OnHarmfulEffectApplied(string characterName)
	{
		if (_dispelShown || PlayerProgressStore.HasSeenDispelTutorial) return;
		_dispelShown = true;

		var dispelSpotlight = GetSpotlightRect(_ui?.GenericBar?.DispelPanel);
		var frameSpotlight = _ui?.GetPartyFrameRect(characterName) ?? new Rect2();
		var key = GetKeybindLabel("dispel");

		// The text panel anchors beside the primary spotlight, so make the afflicted
		// party frame primary — anchoring to the Dispel icon covered the frame.
		var hasFrame = frameSpotlight.Size.X > 1f && frameSpotlight.Size.Y > 1f;
		var primary = hasFrame ? frameSpotlight : dispelSpotlight;
		var secondary = hasFrame ? dispelSpotlight : new Rect2();

		TutorialHighlightOverlay.Show(
			GetTree(),
			"Use Dispel!",
			$"{characterName} has been afflicted with a harmful debuff!\n\n" +
			$"Hover over their party frame (highlighted) and press [{key}] " +
			$"to cleanse all harmful effects from them.\n\n" +
			$"Some debuffs can be deadly if left unchecked!",
			primary,
			"dispel",
			() =>
			{
				PlayerProgressStore.MarkDispelTutorialSeen();
				// The dismissing key press happened while paused — honour it as a dispel.
				_player?.CastDispelOnFirstAfflicted();
			},
			secondary
		);
	}

	void OnDetonationZoneCast()
	{
		if (_detonationShown || PlayerProgressStore.HasSeenDetonationTutorial) return;
		_detonationShown = true;

		TutorialHighlightOverlay.Show(
			GetTree(),
			"Move Out!",
			"The boss has marked your position with a Detonation Zone!\n\n" +
			"Move away from the dangerous area before it detonates — " +
			"anyone still standing inside will take heavy damage.\n\n" +
			"Watch your feet and keep moving!",
			new Rect2(),
			null,
			PlayerProgressStore.MarkDetonationTutorialSeen
		);
	}

	// ── helpers ───────────────────────────────────────────────────────────────

	static Rect2 GetSpotlightRect(Control? panel)
	{
		return panel != null ? panel.GetGlobalRect() : new Rect2();
	}

	static string GetKeybindLabel(string actionName)
	{
		var events = InputMap.ActionGetEvents(actionName);
		if (events.Count > 0 && events[0] is InputEventKey key)
			return OS.GetKeycodeString(key.PhysicalKeycode);
		return actionName;
	}
}