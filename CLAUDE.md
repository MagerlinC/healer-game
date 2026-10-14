# Project Description: Healer Fantasy

Healer fantasy is a game heavily inspired by the party-healing style gameplay of World of Warcraft dungeons and raids, but set in a simple 2D world and played solo.

The player takes on the role of the Lightkeeper, a healer and spellcaster which they will choose spells and talents for in order to keep their party alive whilst killing bosses. In the game's "Last Light" theme the Lightkeeper is the only light against the darkness: healing is "restoring light", the party's health is their light, and spells cost kindling (mana).

The party consists of the player and their 3 NPC companions:

- The Dawnwarden (defensive focus, heavy frontliner with some self-healing)
- The Duskblade (a sneaky rogue dealing damage up close)
- The Starcaller (a spellcaster dealing damage from a distance)

## Gameplay

When entering the game, the player starts in the overworld. Here they can select their talents and a starting spell loadout, before beginning their run.

A run consists of a series of boss fights. During a boss fight, the Lightkeeper will have to use their selected spells (affected by their talents) to:

- Keep the party alive by casting helpful spells, such as restoring their light or shielding them
- These can either affect the entire group, or be cast individually on party members by casting them with the mouse over that party member's frame in the UI (WoW-style)
- Help the party deal damage to the boss

The player may focus more on either of these aspects via their spell and talent selection.

A gameplay example screenshot of the party in combat can be seen here:
![gameplay image](gameplay-example-screenshot.png)

## Code Structure

This codebase contains files for a Godot game written in C#.

- `assets/`: Contains all the assets for the game, such as sprites, sounds, and music.
- `src/`: Contains the code for the game, organized into subfolders for different systems (e.g., `SpellResources/`, `UI/`, `Effects/`).
