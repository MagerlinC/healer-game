using System.Collections.Generic;
using Godot;

namespace healerfantasy;

public partial class GlobalAutoLoad : Node
{
	public static GlobalAutoLoad Instance { get; private set; }

	private static readonly Dictionary<Node, HashSet<string>> SignalMap = new();

	public override void _Ready()
	{
		Instance = this;
	}

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
}