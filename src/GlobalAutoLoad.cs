using System;
using System.Collections.Generic;
using Godot;

namespace healerfantasy;

public partial class GlobalAutoLoad : Node
{
	public static GlobalAutoLoad Instance { get; private set; }

	// ── generic signal registry ───────────────────────────────────────────────
	// Maps each emitter node to the set of signal names it has registered.
	static readonly Dictionary<Node, HashSet<string>> SignalMap = new();

	// ── party-slot signal registry ────────────────────────────────────────────
	// Maps each party-member node to its UI slot index (0 = Templar … 3 = Wizard).
	static readonly Dictionary<Node, int> PartySlotMap = new();

	// Buffers slot-aware subscriptions so GameUI can subscribe in its own
	// _Ready before World has finished registering party members.
	// Maps signal name → list of slot-factory functions.
	static readonly Dictionary<string, List<Func<int, Callable>>> PartySubscriptions = new();

	public override void _Ready()
	{
		Instance = this;
	}

	// ── generic pub/sub ───────────────────────────────────────────────────────

	/// <summary>
	/// Register <paramref name="instance"/> as an emitter of <paramref name="signalName"/>.
	/// Call this from the emitting node's <c>_Ready</c>.
	/// </summary>
	public static void RegisterSignalEmitter(Node instance, string signalName)
	{
		if (!SignalMap.ContainsKey(instance))
			SignalMap[instance] = new HashSet<string>();

		SignalMap[instance].Add(signalName);
	}

	public static void UnregisterSignalEmitter(Node instance)
	{
		SignalMap.Remove(instance);
	}

	/// <summary>
	/// Connect <paramref name="callback"/> to every currently-registered emitter
	/// of <paramref name="signalName"/>. No-op if no emitter is registered yet.
	/// </summary>
	public static void SubscribeToSignal(string signalName, Callable callback)
	{
		foreach (var kvp in SignalMap)
		{
			if (kvp.Value.Contains(signalName))
			{
				if (!kvp.Key.IsConnected(signalName, callback))
					kvp.Key.Connect(signalName, callback);
			}
		}
	}

	/// <summary>
	/// Subscribe to a per-slot signal on every party member.
	/// <para>
	/// <paramref name="callableFactory"/> is called once per party member with
	/// that member's slot index; it should return a <see cref="Callable"/> that
	/// captures the slot and handles the signal.
	/// </para>
	/// <para>
	/// Safe to call before party members are registered — connections are deferred
	/// and made automatically when <see cref="RegisterPartyMember"/> is called.
	/// </para>
	/// </summary>
	/// <summary>
	/// Clears all signal and party-slot registrations.
	/// Must be called before reloading the scene so that stale node references
	/// held in the static dictionaries don't survive into the next run and cause
	/// connection errors or phantom signal deliveries.
	/// </summary>
	public static void Reset()
	{
		SignalMap.Clear();
		PartySlotMap.Clear();
		PartySubscriptions.Clear();
	}
}