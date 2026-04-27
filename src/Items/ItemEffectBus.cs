using System;
using System.Collections.Generic;
using Godot;

namespace healerfantasy.Items;

/// <summary>
/// Static event bus for item-effect UI notifications.
///
/// Item modifiers are plain C# objects (not Godot Nodes) so they cannot emit
/// Godot signals directly.  Instead they call <see cref="Activate"/> /
/// <see cref="Deactivate"/> here, and the UI layer subscribes to the
/// corresponding C# events.
///
/// A snapshot dictionary (<see cref="_activeEffects"/>) caches the current set
/// of active effects so that newly constructed <see cref="ItemEffectBar"/>
/// nodes (e.g. at the start of a new boss fight) can call
/// <see cref="ReplayCurrentState"/> in their <c>_Ready</c> and immediately
/// reflect any effect that was already pending from a previous fight.
///
/// Call <see cref="Reset"/> alongside <see cref="GlobalAutoLoad.Reset"/>
/// whenever a run ends so stale state does not bleed into the next run.
/// </summary>
public static class ItemEffectBus
{
    // ── events ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Fired when an item effect becomes active.
    /// Parameters: effectId, icon, displayName, description.
    /// </summary>
    public static event Action<string, Texture2D?, string, string>? ItemEffectActivated;

    /// <summary>
    /// Fired when an item effect is consumed or otherwise removed.
    /// Parameter: effectId.
    /// </summary>
    public static event Action<string>? ItemEffectDeactivated;

    // ── snapshot ──────────────────────────────────────────────────────────────

    // Keeps the current set of active effects so new subscribers can replay
    // state they missed while no bar was listening.
    static readonly Dictionary<string, (Texture2D? Icon, string DisplayName, string Description)>
        _activeEffects = new();

    // ── public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Mark an item effect as active and notify all current subscribers.
    /// </summary>
    public static void Activate(string effectId, Texture2D? icon, string displayName, string description)
    {
        _activeEffects[effectId] = (icon, displayName, description);
        ItemEffectActivated?.Invoke(effectId, icon, displayName, description);
    }

    /// <summary>
    /// Mark an item effect as inactive and notify all current subscribers.
    /// </summary>
    public static void Deactivate(string effectId)
    {
        _activeEffects.Remove(effectId);
        ItemEffectDeactivated?.Invoke(effectId);
    }

    /// <summary>
    /// Replay every currently-active effect to <paramref name="handler"/>.
    /// Call this from <c>ItemEffectBar._Ready</c> to pick up effects that
    /// were activated before the bar was added to the scene tree.
    /// </summary>
    public static void ReplayCurrentState(Action<string, Texture2D?, string, string> handler)
    {
        foreach (var (id, (icon, displayName, description)) in _activeEffects)
            handler(id, icon, displayName, description);
    }

    /// <summary>
    /// Clear all active-effect state and remove all event subscribers.
    /// Must be called alongside <see cref="GlobalAutoLoad.Reset"/> on run end.
    /// </summary>
    public static void Reset()
    {
        _activeEffects.Clear();
        ItemEffectActivated = null;
        ItemEffectDeactivated = null;
    }
}
