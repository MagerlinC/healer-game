#nullable enable
using Godot;

namespace healerfantasy.UI;

/// <summary>
/// Shared tutorial content used by both <see cref="TutorialPopup"/> (first-run
/// welcome overlay) and <see cref="NewsBoardPane"/> (the always-visible board entry).
///
/// <see cref="Build"/> returns a ready-to-use <see cref="ScrollContainer"/> populated
/// with all tutorial sections.  The caller is responsible for setting any additional
/// size flags it needs before adding the control to its parent.
/// </summary>
public static class TutorialContent
{
	static readonly Color TitleColor = new(0.95f, 0.84f, 0.50f);
	static readonly Color BodyColor = new(0.82f, 0.78f, 0.72f);

	/// <summary>
	/// Builds and returns a <see cref="ScrollContainer"/> containing all tutorial
	/// sections.  Both <c>SizeFlagsHorizontal</c> and <c>SizeFlagsVertical</c> are
	/// set to <c>ExpandFill</c> so the control fills whichever parent container it
	/// is placed in.
	/// </summary>
	public static ScrollContainer Build()
	{
		var scroll = new ScrollContainer();
		scroll.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		scroll.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
		scroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;

		var vbox = new VBoxContainer();
		vbox.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		vbox.AddThemeConstantOverride("separation", 20);
		scroll.AddChild(vbox);

		vbox.AddChild(MakeSection(
			"⚔  Gameplay",
			"As the Healer, your role is to keep your party alive through boss fights. " +
			"Cast healing and shielding spells on your allies — hover over a party member's frame " +
			"and cast to target them individually, or use group spells to help everyone at once.\n\n" +
			"You can also contribute to the fight with offensive spells, dealing damage to the boss " +
			"alongside your party."));

		vbox.AddChild(MakeSection(
			"📖  Spells",
			"Pick your spells from the Spellbook before each run. You can equip multiple spells " +
			"and mix different schools of magic to build your ideal loadout.\n\n" +
			"➜  Click the Spellbook tome to configure your spells!"));

		vbox.AddChild(MakeSection(
			"✦  Talents",
			"Talents are passive upgrades that enhance your spells and playstyle. " +
			"Talents are offered as rewards for killing bosses, and selecting talents within a school " +
			"unlocks both new spells and further talents within that school.\n" +
			"Use the Talent Board to pick a school affinity to influence talents offered during a run.\n\n" +
			"➜  Click the Talent Board to change your school affinity or review earned talents!"));

		vbox.AddChild(MakeSection(
			"🎒  Items",
			"Powerful items drop from bosses during dungeon runs and can greatly boost your " +
			"effectiveness. Between dungeons, visit the Armory at your rest camp to browse and equip them."));

		vbox.AddChild(MakeSection(
			"✦  Dispel",
			"Some boss abilities apply harmful debuffs to you or your party. " +
			"Use Dispel to cleanse all harmful effects from the character under your cursor. " +
			"Some debuffs can be deadly if left unchecked, whilst others might require more strategic timing."));

		vbox.AddChild(MakeSection(
			"🛡  Deflect",
			"Bosses sometimes telegraph powerful abilities with a visible wind-up. " +
			"Activate Deflect just before it goes off to deflect the ability and reduce its damage to zero. " +
			"Timing is everything — too early or too late and it won't work!"));

		return scroll;
	}

	/// <summary>
	/// Builds a two-line section control: a coloured heading label above a
	/// word-wrapped body label.  Used by <see cref="Build"/> and can be reused
	/// by callers that need the same visual style for additional sections.
	/// </summary>
	public static Control MakeSection(string heading, string body)
	{
		var vbox = new VBoxContainer();
		vbox.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		vbox.AddThemeConstantOverride("separation", 6);

		var headingLabel = new Label();
		headingLabel.Text = heading;
		headingLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		headingLabel.AddThemeFontSizeOverride("font_size", 15);
		headingLabel.AddThemeColorOverride("font_color", TitleColor);
		vbox.AddChild(headingLabel);

		var bodyLabel = new Label();
		bodyLabel.Text = body;
		bodyLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		bodyLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		bodyLabel.AddThemeFontSizeOverride("font_size", 13);
		bodyLabel.AddThemeColorOverride("font_color", BodyColor);
		vbox.AddChild(bodyLabel);

		return vbox;
	}
}