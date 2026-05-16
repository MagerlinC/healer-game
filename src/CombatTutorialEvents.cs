using System;

namespace healerfantasy;

/// <summary>
/// Thin event bus for triggering in-combat tutorial overlays.
///
/// Events are fired from deep within game systems (e.g. <see cref="Character.ApplyEffect"/>
/// and <see cref="SpellResources.BossDetonationZoneSpell.Apply"/>) where wiring to the UI
/// directly would create unwanted coupling. <see cref="CombatTutorialManager"/> subscribes
/// here and decides whether to show the overlay based on <see cref="PlayerProgressStore"/>.
/// </summary>
public static class CombatTutorialEvents
{
	/// <summary>
	/// Fired from <see cref="Character.ApplyEffect"/> the first time a harmful,
	/// dispellable effect is applied to a friendly character during a session.
	/// The string parameter is the <c>CharacterName</c> of the affected character.
	/// Subscribers should check <see cref="PlayerProgressStore.HasSeenDispelTutorial"/>
	/// before showing any UI.
	/// </summary>
	public static event Action<string>? HarmfulEffectApplied;

	/// <summary>
	/// Fired from <see cref="SpellResources.BossDetonationZoneSpell.Apply"/> whenever
	/// a Detonation Zone is placed. Subscribers should check
	/// <see cref="PlayerProgressStore.HasSeenDetonationTutorial"/> before showing any UI.
	/// </summary>
	public static event Action? DetonationZoneCast;

	/// <summary>Called internally by <see cref="Character.ApplyEffect"/>.</summary>
	internal static void FireHarmfulEffectApplied(string characterName) => HarmfulEffectApplied?.Invoke(characterName);

	/// <summary>Called internally by <see cref="SpellResources.BossDetonationZoneSpell.Apply"/>.</summary>
	internal static void FireDetonationZoneCast() => DetonationZoneCast?.Invoke();
}
