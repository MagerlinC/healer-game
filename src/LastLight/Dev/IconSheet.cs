using System.Collections.Generic;
using System.Linq;
using Godot;
using healerfantasy.LastLight.Hud;
using healerfantasy.SpellResources;
using healerfantasy.Talents;

namespace healerfantasy.LastLight.Dev;

/// <summary>
/// Dev-only: every spell and talent icon on one sheet, grouped by school, for review.
/// Opened by the preview harness with --scene=icons.
/// </summary>
public partial class IconSheet : CanvasLayer
{
	public override void _Ready()
	{
		var bg = new ColorRect { Color = new Color(0.03f, 0.025f, 0.02f) };
		bg.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		AddChild(bg);

		var rows = new VBoxContainer { Position = new Vector2(24, 16) };
		rows.AddThemeConstantOverride("separation", 6);
		AddChild(rows);

		var spells = SpellRegistry.AllSpells.Concat(SpellRegistry.UltimateSpells).Concat(SpellRegistry.GenericSpells)
			.GroupBy(s => s.GetType()).Select(g => g.First()).ToList();

		foreach (var school in new[]
		         {
			         SpellSchool.Flame, SpellSchool.Ember, SpellSchool.Umbra, SpellSchool.Wick,
			         SpellSchool.Pyre, SpellSchool.Generic
		         })
		{
			var row = new HBoxContainer();
			row.AddThemeConstantOverride("separation", 8);
			rows.AddChild(row);

			var title = new Label { Text = SchoolInfo.Name(school), CustomMinimumSize = new Vector2(90, 0) };
			LastLightHud.Style(title, 16, SchoolInfo.Color(school));
			row.AddChild(title);

			foreach (var spell in spells.Where(s => s.School == school))
				row.AddChild(Cell(spell.Icon, spell.Name));

			row.AddChild(new Control { CustomMinimumSize = new Vector2(20, 0) });
			foreach (var talent in TalentRegistry.AllTalents.Where(t => t.School == school))
				row.AddChild(Cell(talent.LoadIcon(), talent.Name));
		}

		var runes = new HBoxContainer();
		runes.AddThemeConstantOverride("separation", 8);
		rows.AddChild(runes);
		var runeTitle = new Label { Text = "Runes", CustomMinimumSize = new Vector2(90, 0) };
		LastLightHud.Style(runeTitle, 16, new Color(0.86f, 0.8f, 0.66f));
		runes.AddChild(runeTitle);
		foreach (var rune in new[] { Runes.RuneIndex.Twilight, Runes.RuneIndex.Nightfall, Runes.RuneIndex.Midnight, Runes.RuneIndex.StarlessNight, Runes.RuneIndex.Abyss })
			runes.AddChild(Cell(LastLightIcons.ForRune(rune), Runes.RuneInfo.Name(rune)));
	}

	static Control Cell(Texture2D icon, string name)
	{
		var box = new VBoxContainer { CustomMinimumSize = new Vector2(84, 0) };
		box.AddChild(new TextureRect
		{
			Texture = icon,
			CustomMinimumSize = new Vector2(64, 64),
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered
		});
		var label = new Label
		{
			Text = name, HorizontalAlignment = HorizontalAlignment.Center,
			AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(84, 0)
		};
		label.AddThemeFontSizeOverride("font_size", 10);
		label.AddThemeColorOverride("font_color", new Color(0.8f, 0.76f, 0.7f));
		box.AddChild(label);
		return box;
	}
}
