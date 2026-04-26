using System;
using System.Collections.Generic;
using Godot;
using healerfantasy;
using healerfantasy.CombatLog;
using healerfantasy.SpellResources;

public partial class ThatWhichSwallowedTheStarsMemoryGame : Node2D
{
	[Signal]
	public delegate void CompletedEventHandler();

	public float DamageAmount { get; set; } = 120f;
	public string BossName { get; set; } = GameConstants.SanctumBoss3Name;

	readonly Texture2D[] _safeTextures =
	{
		GD.Load<Texture2D>("res://assets/enemies/that-which-swallowed-the-stars/memory-game/safe1.png"),
		GD.Load<Texture2D>("res://assets/enemies/that-which-swallowed-the-stars/memory-game/safe2.png"),
		GD.Load<Texture2D>("res://assets/enemies/that-which-swallowed-the-stars/memory-game/safe3.png")
	};

	readonly Texture2D _dangerTexture =
		GD.Load<Texture2D>("res://assets/enemies/that-which-swallowed-the-stars/memory-game/danger.png");

	readonly Texture2D _dangerTextureFull =
		GD.Load<Texture2D>("res://assets/enemies/that-which-swallowed-the-stars/memory-game/danger-full.png");

	readonly List<int> _safeQuadrants = new();
	readonly Sprite2D[] _tiles = new Sprite2D[4];
	readonly bool[] _hitThisReplayStep = new bool[4];
	readonly RandomNumberGenerator _rng = new();

	Rect2 _arenaRect;
	State _state;
	float _stateTimer;
	int _stepIndex;

	const float PreviewDuration = 1.0f;
	const float PreviewGapDuration = 1.00f;
	const float RecallDelayDuration = 2.0f;
	const float ReplayDuration = 1.0f;
	const float ReplayGapDuration = 1.00f;
	const float ArenaPadding = 64f;

	enum State
	{
		Preview,
		PreviewGap,
		RecallDelay,
		Replay,
		ReplayGap,
		Finished
	}

	public override void _Ready()
	{
		_rng.Randomize();
		_arenaRect = BuildArenaRect();
		BuildTiles();

		_safeQuadrants.Add(_rng.RandiRange(0, 3));
		for (var i = 1; i < 3; i++)
		{
			var previousQuadrant = _safeQuadrants[i - 1];
			var adjacentQuadrants = GetAdjacentQuadrants(previousQuadrant);
			_safeQuadrants.Add(adjacentQuadrants[_rng.RandiRange(0, adjacentQuadrants.Count - 1)]);
		}

		StartPreviewStep(0);
	}

	public override void _Process(double delta)
	{
		if (_state == State.Finished)
			return;

		_stateTimer -= (float)delta;

		if (_state == State.Replay)
			CheckReplayDamage();

		if (_stateTimer > 0f)
			return;

		switch (_state)
		{
			case State.Preview:
				HideAllTiles();
				if (_stepIndex >= _safeQuadrants.Count - 1)
				{
					_state = State.RecallDelay;
					_stateTimer = RecallDelayDuration;
				}
				else
				{
					_state = State.PreviewGap;
					_stateTimer = PreviewGapDuration;
				}

				break;

			case State.PreviewGap:
				StartPreviewStep(_stepIndex + 1);
				break;

			case State.RecallDelay:
				StartReplayStep(0);
				break;

			case State.Replay:
				HideAllTiles();
				if (_stepIndex >= _safeQuadrants.Count - 1)
				{
					Finish();
				}
				else
				{
					_state = State.ReplayGap;
					_stateTimer = ReplayGapDuration;
				}

				break;

			case State.ReplayGap:
				StartReplayStep(_stepIndex + 1);
				break;
		}
	}

	Rect2 BuildArenaRect()
	{
		var min = new Vector2(float.MaxValue, float.MaxValue);
		var max = new Vector2(float.MinValue, float.MinValue);

		void Include(Vector2 point)
		{
			min.X = Mathf.Min(min.X, point.X);
			min.Y = Mathf.Min(min.Y, point.Y);
			max.X = Mathf.Max(max.X, point.X);
			max.Y = Mathf.Max(max.Y, point.Y);
		}

		foreach (var node in GetTree().GetNodesInGroup("party"))
			if (node is Character character && character.IsAlive)
				Include(character.GlobalPosition);

		if (GetParent() is Node2D parent)
			Include(parent.GlobalPosition);

		if (min.X == float.MaxValue || max.X == float.MinValue)
			return new Rect2(GlobalPosition - new Vector2(100f, 100f), new Vector2(200f, 200f));

		var center = (min + max) / 2f;
		var span = max - min;
		var side = Mathf.Max(span.X, span.Y) + ArenaPadding * 2f;
		return new Rect2(center - new Vector2(side / 2f, side / 2f), new Vector2(side, side));
	}

	void BuildTiles()
	{
		var quadrantSize = _arenaRect.Size / 2f;
		for (var i = 0; i < _tiles.Length; i++)
		{
			var tile = new Sprite2D
			{
				Centered = true,
				Visible = false,
				ZIndex = 50
			};

			var quadrantRect = GetQuadrantRect(i);
			tile.Position = quadrantRect.GetCenter() - GlobalPosition;
			tile.Scale = quadrantSize / _dangerTexture.GetSize();
			AddChild(tile);
			_tiles[i] = tile;
		}
	}

	void StartPreviewStep(int stepIndex)
	{
		_stepIndex = stepIndex;
		_state = State.Preview;
		_stateTimer = PreviewDuration;
		ShowPreviewPattern(_safeQuadrants[stepIndex], _safeTextures[stepIndex]);
	}

	void StartReplayStep(int stepIndex)
	{
		_stepIndex = stepIndex;
		_state = State.Replay;
		_stateTimer = ReplayDuration;
		Array.Fill(_hitThisReplayStep, false);
		ShowReplayPattern(_safeQuadrants[stepIndex]);
		CheckReplayDamage();
	}

	void ShowPreviewPattern(int safeQuadrant, Texture2D safeTexture)
	{
		for (var i = 0; i < _tiles.Length; i++)
		{
			_tiles[i].Texture = i == safeQuadrant ? safeTexture : _dangerTexture;
			_tiles[i].Visible = true;
		}
	}

	void ShowReplayPattern(int safeQuadrant)
	{
		for (var i = 0; i < _tiles.Length; i++)
		{
			_tiles[i].Visible = i != safeQuadrant;
			_tiles[i].Texture = _dangerTextureFull;
		}
	}

	void HideAllTiles()
	{
		foreach (var tile in _tiles)
			tile.Visible = false;
	}

	void CheckReplayDamage()
	{
		var player = FindPlayerCharacter();
		if (player == null || !player.IsAlive)
			return;

		for (var i = 0; i < _tiles.Length; i++)
		{
			if (!_tiles[i].Visible || _hitThisReplayStep[i])
				continue;

			if (!GetQuadrantRect(i).HasPoint(player.GlobalPosition))
				continue;

			_hitThisReplayStep[i] = true;
			player.TakeDamage(DamageAmount);
			player.RaiseFloatingCombatText(DamageAmount, false, (int)SpellSchool.Generic, false);
			CombatLog.Record(new CombatEventRecord
			{
				Timestamp = Time.GetTicksMsec() / 1000.0,
				SourceName = BossName,
				TargetName = player.CharacterName,
				AbilityName = "Stolen Memory",
				Amount = DamageAmount,
				Type = CombatEventType.Damage,
				IsCrit = false,
				Description =
					"A fragment of a stolen memory replays across the arena. Stand in the correct positions to avoid taking damage."
			});
		}
	}

	Character FindPlayerCharacter()
	{
		foreach (var node in GetTree().GetNodesInGroup("party"))
			if (node is Character character
			    && character.IsAlive
			    && character.CharacterName == GameConstants.HealerName)
				return character;

		return null;
	}

	Rect2 GetQuadrantRect(int quadrantIndex)
	{
		var half = _arenaRect.Size / 2f;
		var origin = _arenaRect.Position;
		return quadrantIndex switch
		{
			0 => new Rect2(origin, half),
			1 => new Rect2(origin + new Vector2(half.X, 0f), half),
			2 => new Rect2(origin + new Vector2(0f, half.Y), half),
			_ => new Rect2(origin + half, half)
		};
	}

	List<int> GetAdjacentQuadrants(int quadrantIndex)
	{
		return quadrantIndex switch
		{
			0 => new List<int> { 1, 2 },
			1 => new List<int> { 0, 3 },
			2 => new List<int> { 0, 3 },
			_ => new List<int> { 1, 2 }
		};
	}

	void Finish()
	{
		HideAllTiles();
		_state = State.Finished;
		EmitSignalCompleted();
		QueueFree();
	}
}