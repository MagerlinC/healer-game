using System;
using System.Collections.Generic;
using Godot;

namespace healerfantasy;

public partial class GlobalAutoLoad : Node
{
	public static GlobalAutoLoad Instance { get; private set; }

	// ── generic signal registry ───────────────────────────────────────────────
	// Maps each emitter node to the set of signal names it has registered.
	private static readonly Dictionary<Node, HashSet<string>> SignalMap = new();

	// ── party-slot signal registry ────────────────────────────────────────────
	// Maps each party-member node to its UI slot index (0 = Templar … 3 = Wizard).
	private static readonly Dictionary<Node, int> PartySlotMap = new();

	// Buffers slot-aware subscriptions so GameUI can subscribe in its own
	// _Ready before World has finished registering party members.
	// Maps signal name → list of slot-factory functions.
	private static readonly Dictionary<string, List<Func<int, Callable>>> PartySubscriptions = new();

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

	// ── party-slot pub/sub ────────────────────────────────────────────────────

	/// <summary>
	/// Register <paramref name="node"/> as the party member in <paramref name="slot"/>.
	/// Immediately connects any subscriptions that were registered before this call.
	/// </summary>
	public static void RegisterPartyMember(Node node, int slot)
	{
		PartySlotMap[node] = slot;

		// Connect buffered subscriptions for every signal this node emits
		if (!SignalMap.TryGetValue(node, out var signals)) return;

		foreach (var (signalName, factories) in PartySubscriptions)
		{
			if (!signals.Contains(signalName)) continue;
			foreach (var factory in factories)
				node.Connect(signalName, factory(slot));
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

	public static void SubscribeToPartySignal(string signalName, Func<int, Callable> callableFactory)
	{
		// Buffer so future RegisterPartyMember calls connect automatically
		if (!PartySubscriptions.TryGetValue(signalName, out var list))
			PartySubscriptions[signalName] = list = new List<Func<int, Callable>>();
		list.Add(callableFactory);

		// Connect immediately to any already-registered party members
		foreach (var (node, slot) in PartySlotMap)
		{
			if (SignalMap.TryGetValue(node, out var signals) && signals.Contains(signalName))
				node.Connect(signalName, callableFactory(slot));
		}
	}
}
