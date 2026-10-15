using System.Collections.Generic;
using System.Linq;
using Godot;
using healerfantasy;
using healerfantasy.UI;

/// <summary>
/// Rune of Nightfall — manages the periodic spawning of <see cref="ThornsEnemy"/>
/// during boss fights.
///
/// Added to the World scene when Rune 2 is active.  Listens for boss death to
/// stop spawning, and immediately despawns any live thorns when all bosses die
/// (so thorns don't linger after the fight ends).
/// </summary>
public partial class ThornsManager : Node
{
    // ── state ─────────────────────────────────────────────────────────────────

    float _spawnTimer;
    int _spawnCounter;
    bool _active = true;

    /// <summary>Party member nodes injected by World._Ready().</summary>
    readonly List<Character> _party = new();

    /// <summary>Reference to GameUI so we can add/remove thorn health bars.</summary>
    GameUI? _gameUI;

    // ── init ──────────────────────────────────────────────────────────────────

    public void Init(IEnumerable<Character> partyMembers, GameUI ui)
    {
        _party.AddRange(partyMembers);
        _gameUI = ui;
        // Start with a short delay so thorns don't appear instantly.
        _spawnTimer = GameConstants.RuneNightfallThornsInterval;
    }

    // ── lifecycle ─────────────────────────────────────────────────────────────

    public override void _Ready()
    {
        // Stop all thorns activity when every boss dies.
        GlobalAutoLoad.SubscribeToSignal(
            nameof(Character.Died),
            Callable.From((Character c) =>
            {
                if (c.IsFriendly) return;
                // Check if all bosses are now dead.
                var anyBossAlive = GetTree()
                    .GetNodesInGroup(GameConstants.BossGroupName)
                    .OfType<Character>()
                    .Any(b => b != c && b.IsAlive && !b.IsInGroup(GameConstants.ThornsGroupName));
                if (anyBossAlive) return;

                _active = false;
                // Instantly remove any live thorns so they don't persist into victory.
                foreach (var node in GetTree().GetNodesInGroup(GameConstants.ThornsGroupName).ToList())
                    if (node is ThornsEnemy v)
                        v.QueueFree();
            }));
    }

    public override void _Process(double delta)
    {
        if (!_active) return;

        _spawnTimer -= (float)delta;
        if (_spawnTimer > 0f) return;

        _spawnTimer = GameConstants.RuneNightfallThornsInterval;
        SpawnThorns();
    }

    // ── private ───────────────────────────────────────────────────────────────

    void SpawnThorns()
    {
        // Pick a random alive party member.
        var alive = _party.Where(p => p.IsAlive).ToList();
        if (alive.Count == 0) return;

        var rng = new RandomNumberGenerator();
        rng.Randomize();
        var target = alive[rng.RandiRange(0, alive.Count - 1)];

        _spawnCounter++;
        var thorns = new ThornsEnemy(target, $"Thorns_{_spawnCounter}");

        // Add thorns as a sibling of this manager in the World scene.
        GetParent().AddChild(thorns);

        // Register the thorns health bar in the GameUI.
        _gameUI?.AddThornsHealthBar(thorns);

        // Remove the thorns health bar when the thorns die.
        thorns.Connect(
            nameof(Character.Died),
            Callable.From((Character c) =>
            {
                if (c is ThornsEnemy v)
                    _gameUI?.RemoveThornsHealthBar(v.CharacterName);
            }));
    }
}
