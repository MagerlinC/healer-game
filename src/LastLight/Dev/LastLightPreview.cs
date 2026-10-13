using System.Collections.Generic;
using Godot;

namespace healerfantasy.LastLight.Dev;

/// <summary>
/// Dev-only: launches an Ancient Keep fight, chips the party's health so the
/// rot is visible, saves a few screenshots and quits.
///
///   godot --path . res://src/LastLight/Dev/LastLightPreview.tscn -- --boss=1 --out=C:/tmp/shot
/// </summary>
public partial class LastLightPreview : Node
{
	/// <summary>Optional region to also save at 2× (--crop=x,y,w,h), for close-ups.</summary>
	static Rect2I? Crop;

	/// <summary>
	/// UI states to trigger at t=2s (--do=a,b): settings, panel0..panelN (overlay panels),
	/// tooltip, pause, killparty, killboss.
	/// </summary>
	static string[] Actions = System.Array.Empty<string>();

	public override void _Ready()
	{
		var boss = 0;
		var scene = "fight";
		var dungeon = 0;
		var outPrefix = OS.GetUserDataDir() + "/last-light";
		foreach (var arg in OS.GetCmdlineUserArgs())
		{
			if (arg.StartsWith("--boss=")) boss = int.Parse(arg.Substring(7));
			if (arg.StartsWith("--out=")) outPrefix = arg.Substring(6);
			if (arg.StartsWith("--scene=")) scene = arg.Substring(8);
			if (arg.StartsWith("--dungeon=")) dungeon = int.Parse(arg.Substring(10));
			if (arg.StartsWith("--do=")) Actions = arg.Substring(5).Split(',');
			if (arg.StartsWith("--crop="))
			{
				var v = arg.Substring(7).Split(',');
				Crop = new Rect2I(int.Parse(v[0]), int.Parse(v[1]), int.Parse(v[2]), int.Parse(v[3]));
			}
		}

		var path = scene switch
		{
			"menu" => "res://levels/MainMenu.tscn",
			"overworld" => "res://levels/Overworld.tscn",
			"camp" => "res://levels/Camp.tscn",
			"map" => "res://levels/MapScreen.tscn",
			_ => "res://levels/World.tscn"
		};
		if (scene == "fight") RunState.Instance.SetupDevTestFight(DungeonDefinition.All[dungeon], boss);
		else RunState.Instance.Reset();
		// Camp and map: pretend the first dungeon is cleared so progress is visible.
		if (scene is "camp" or "map") RunState.Instance.CompleteDungeon();

		GetTree().Root.CallDeferred(Node.MethodName.AddChild,
			new Shooter { OutPrefix = outPrefix, ProcessMode = ProcessModeEnum.Always });
		GetTree().CallDeferred(SceneTree.MethodName.ChangeSceneToFile, path);
	}

	partial class Shooter : Node
	{
		public string OutPrefix = "";
		float _t;
		bool _damaged;
		readonly List<float> _shots = new() { 2.6f, 3.3f, 4.1f, 5.0f };
		int _index;
		bool _acted;

		void RunActions()
		{
			var scene = GetTree().CurrentScene;
			foreach (var action in Actions)
			{
				if (action == "settings" && GetTree().Root.FindChild("SettingsPanel", true, false) is Control settings)
					settings.Visible = true;
				else if (action.StartsWith("panel") && scene != null)
				{
					var layers = new List<CanvasLayer>();
					foreach (var child in scene.GetChildren())
						if (child is CanvasLayer { Layer: 10 } layer) layers.Add(layer);
					var i = int.Parse(action.Substring(5));
					if (i < layers.Count) layers[i].Visible = true;
				}
				else if (action == "tooltip")
					GameTooltip.Show("Renewing Light", "Heals the target for 40 over 12 seconds. Costs 12 mana.");
				else if (action == "pause")
					Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = Key.Escape, Keycode = Key.Escape, Pressed = true });
				else if (action is "killparty" or "killboss")
				{
					var group = action == "killparty" ? GameConstants.PartyGroupName : GameConstants.BossGroupName;
					foreach (var node in GetTree().GetNodesInGroup(group))
						if (node is Character c) c.TakeDamage(999999f);
				}
			}
		}

		public override void _Process(double delta)
		{
			_t += (float)delta;
			if (!_acted && _t > 2.0f)
			{
				_acted = true;
				RunActions();
			}

			// Overworld: walk the healer left partway through, so the light is seen following.
			if (_t > 3.0f && _t < 4.2f && GetTree().CurrentScene is { } current)
				foreach (var child in current.GetChildren())
					if (child is OverworldPlayer walker)
						walker.Position += new Vector2(-150f * (float)delta, -45f * (float)delta);

			if (!_damaged && _t > 1.2f)
			{
				_damaged = true;
				foreach (var node in GetTree().GetNodesInGroup(GameConstants.PartyGroupName))
				{
					if (node is not Character c) continue;
					var fraction = c.CharacterName switch
					{
						"Templar" => 0.35f,
						"Assassin" => 0.7f,
						"Wizard" => 0.15f,
						_ => 0f
					};
					if (fraction > 0f) c.TakeDamage(c.MaxHealth * fraction);
				}
			}

			if (_index < _shots.Count && _t > _shots[_index])
			{
				// Make the healer cast for the later shots so that pose is covered too.
				if (_index == 2 && GetTree().CurrentScene?.GetNodeOrNull<AnimatedSprite2D>("Healer/AnimatedSprite2D") is { } healer)
					healer.Play("cast");

				var img = GetViewport().GetTexture().GetImage();
				img.SavePng($"{OutPrefix}-{_index}.png");
				if (Crop is { } crop)
				{
					var region = img.GetRegion(crop);
					region.Resize(crop.Size.X * 2, crop.Size.Y * 2, Image.Interpolation.Nearest);
					region.SavePng($"{OutPrefix}-{_index}-crop.png");
				}
				_index++;
				if (_index == _shots.Count) GetTree().Quit();
			}
		}
	}
}
