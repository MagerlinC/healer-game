using System.Collections.Generic;
using System.Linq;
using Godot;
using healerfantasy;
using healerfantasy.UI;

/// <summary>
/// Rune of Nature — manages the periodic spawning of <see cref="VinesEnemy"/>
/// during boss fights.
///
/// Added to the World scene when Rune 2 is active.  Listens for boss death to
/// stop spawning, and immediately despawns any live vines when all bosses die
/// (so vines don't linger after the fight ends).
/// </summary>
public partial class VinesManager : Node
{
    // ── state ─────────────────────────────────────────────────────────────────

    float _spawnTimer;
    int _spawnCounter;
    bool _active = true;

    /// <summary>Party member nodes injected by World._Ready().</summary>
    readonly List<Character> _party = new();

    /// <summary>Reference to GameUI so we can add/remove vine health bars.</summary>
    GameUI? _gameUI;

    // ── init ──────────────────────────────────────────────────────────────────

    public void Init(IEnumerable<Character> partyMembers, GameUI ui)
    {
        _party.AddRange(partyMembers);
        _gameUI = ui;
        // Start with a short delay so vines don't appear instantly.
        _spawnTimer = GameConstants.RuneNatureVinesInterval;
    }

    // ── lifecycle ─────────────────────────────────────────────────────────────

    public override void _Ready()
    {
        // Stop all vines activity when every boss dies.
        GlobalAutoLoad.SubscribeToSignal(
            nameof(Character.Died),
            Callable.From((Character c) =>
            {
                if (c.IsFriendly) return;
                // Check if all bosses are now dead.
                var anyBossAlive = GetTree()
                    .GetNodesInGroup(GameConstants.BossGroupName)
                    .OfType<Character>()
                    .Any(b => b != c && b.IsAlive && !b.IsInGroup(GameConstants.VinesGroupName));
                if (anyBossAlive) return;

                _active = false;
                // Instantly remove any live vines so they don't persist into victory.
                foreach (var node in GetTree().GetNodesInGroup(GameConstants.VinesGroupName).ToList())
                    if (node is VinesEnemy v)
                        v.QueueFree();
            }));
    }

    public override void _Process(double delta)
    {
        if (!_active) return;

        _spawnTimer -= (float)delta;
        if (_spawnTimer > 0f) return;

        _spawnTimer = GameConstants.RuneNatureVinesInterval;
        SpawnVines();
    }

    // ── private ───────────────────────────────────────────────────────────────

    void SpawnVines()
    {
        // Pick a random alive party member.
        var alive = _party.Where(p => p.IsAlive).ToList();
        if (alive.Count == 0) return;

        var rng = new RandomNumberGenerator();
        rng.Randomize();
        var target = alive[rng.RandiRange(0, alive.Count - 1)];

        _spawnCounter++;
        var vines = new VinesEnemy(target, $"Vines_{_spawnCounter}");

        // Add vines as a sibling of this manager in the World scene.
        GetParent().AddChild(vines);

        // Register the vines health bar in the GameUI.
        _gameUI?.AddVinesHealthBar(vines);

        // Remove the vines health bar when the vines die.
        vines.Connect(
            nameof(Character.Died),
            Callable.From((Character c) =>
            {
                if (c is VinesEnemy v)
                    _gameUI?.RemoveVinesHealthBar(v.CharacterName);
            }));
    }
}
