using Godot;
using System;
using System.Collections.Generic;

public partial class Hugger : CharacterBody2D
{
	[Export] public NodePath PlayerTarget;
	[Export] public NodePath TileMapPath;
	[Export] public float SeekSpeed = 150.0f;
	[Export] public float AlertSpeed = 700.0f;
	[Export] public float AttackSpeed = 400.0f;

	
	[Export] public int MinSearchRadius = 2;
	[Export] public int MaxSearchRadius = 4;
	[Export] public int AlertSearchRadius = 3;
	[Export] public int AttackDetectionRadius = 2;

	
	[Export] public float SeekNewDestinationTime = 3.0f;
	[Export] public float SeekPauseTime = 1.5f;

	
	private Vector2I _tileSize = new Vector2I(64, 64);

	
	private enum HuggerState { Seek, Alert, Attack, ForceMove }
	private enum SeekSubState { Moving, Pausing }

	
	private HuggerState _currentState = HuggerState.ForceMove; 
	private SeekSubState _seekSubState = SeekSubState.Moving;

	
	private Vector2 _originalPosition;
	private Vector2 _targetPosition;
	private Vector2 _alertSpot;
	private readonly List<Vector2> _currentPath = new();
	private int _currentPathIndex = 0;

	
	private double _stateTimer = 0.0;
	private double _seekTimer = 0.0;
	private double _pauseTimer = 0.0;

	
	private Vector2I _lastTilePosition;
	private readonly Random _random = new();
	private TileMapLayer _tileMap;
	private Node2D _player;

	
	private readonly List<Hugger> _nearbyHuggers = new();
	private readonly List<Vector2I> _searchedTiles = new();
	private bool _isSearchLeader = false;

	
	private Vector2 _debugMoveDirection = Vector2.Right;
	private double _debugDirectionChangeTimer = 0.0;
	[Export] public string DebugState = "Seek"; 

	
	private bool _isLatching = false;
	private bool _isLatched = false;
	private double _latchTimer = 0.0;
	private double _badStateTimer = 0.0;
	private double _detachTime = 0.0;
	private double _badThreshold = 0.0;
	private Node2D _attachPoint;

	
	
	

	public override void _Ready()
	{
		_originalPosition = GlobalPosition;
		_targetPosition = _originalPosition;

		
		if (!TileMapPath.IsEmpty)
			_tileMap = GetNode<TileMapLayer>(TileMapPath);
		else
			_tileMap = GetTree().GetCurrentScene().GetNodeOrNull<TileMapLayer>("Feature") ??
						 GetTree().GetCurrentScene().GetNodeOrNull<TileMapLayer>("BG");

		if (_tileMap == null)
		{
			GD.PrintErr("Hugger: TileMap not found – path‑finding disabled. Listing scene nodes:");
			ListAvailableNodes(GetTree().GetCurrentScene(), 0);
		}
		else if (_tileMap.TileSet != null)
		{
			_tileSize = _tileMap.TileSet.TileSize;
			GD.Print($"Hugger using tile size: {_tileSize}");
		}

		
		_player = !PlayerTarget.IsEmpty ? GetNode<Node2D>(PlayerTarget) :
				  GetTree().GetCurrentScene().GetNodeOrNull<Node2D>("kiddo");
		if (_player == null)
			GD.PrintErr("Hugger: Player not found for targeting.");

		SetDebugState(DebugState);
		AddToGroup("huggers");
	}

	
	
	

	private void ListAvailableNodes(Node root, int depth)
	{
		if (depth > 5) return;
		string indent = new(' ', depth * 2);
		GD.Print($"{indent}{root.Name} ({root.GetType()})");
		foreach (Node child in root.GetChildren())
			ListAvailableNodes(child, depth + 1);
	}

	private void SetDebugState(string stateName)
	{
		switch (stateName.ToLower())
		{
			case "forcemove":
				_currentState = HuggerState.ForceMove;
				_currentPath.Clear();
				_currentPath.AddRange(new[]
				{
					GlobalPosition + new Vector2(100, 0),
					GlobalPosition + new Vector2(100, 100),
					GlobalPosition + new Vector2(0, 100),
					GlobalPosition
				});
				_currentPathIndex = 0;
				break;

			case "seek":
				_currentState = HuggerState.Seek;
				PickRandomDestination();
				break;

			case "alert":
				_currentState = HuggerState.Alert;
				SwitchToAlertState(GlobalPosition + new Vector2(200, 200));
				break;

			case "attack":
				if (_player == null)
				{
					GD.PrintErr("Cannot enter Attack – player missing");
					_currentState = HuggerState.ForceMove;
				}
				else
				{
					_currentState = HuggerState.Attack;
					SwitchToAttackState();
				}
				break;

			default:
				GD.PrintErr($"Unknown debug state: {stateName}");
				_currentState = HuggerState.ForceMove;
				break;
		}
	}

	
	
	

	public override void _PhysicsProcess(double delta)
	{
		_stateTimer += delta;
		_seekTimer += delta;
		_debugDirectionChangeTimer += delta;

		if (_currentState == HuggerState.ForceMove)
		{
			HandleForceMoveState(delta);
			MoveAndSlide();
			return;
		}

		if (_tileMap == null || _player == null)
		{
			GD.PrintErr("Hugger: Missing TileMap or player reference.");
			return;
		}

		switch (_currentState)
		{
			case HuggerState.Seek:
				HandleSeekState(delta);
				break;
			case HuggerState.Alert:
				HandleAlertState(delta);
				break;
			case HuggerState.Attack:
				HandleAttackState(delta);
				break;
		}

		Vector2I playerTile = GetTilePosition(_player.GlobalPosition);
		Vector2I myTile = GetTilePosition(GlobalPosition);
		_lastTilePosition = myTile;

		
		if (!(_player is Kid kid && kid.MudSinkingProgress >= 1.0f))
		{
			if (IsPlayerInDetectionRadius(myTile, playerTile, AttackDetectionRadius))
				SwitchToAttackState();
		}

		MoveAndSlide();
	}

	
	
	

	private void HandleForceMoveState(double _)
	{
		if (_debugDirectionChangeTimer > 3.0)
		{
			_debugDirectionChangeTimer = 0;
			
			_debugMoveDirection = new Vector2(-_debugMoveDirection.Y, _debugMoveDirection.X);
			GD.Print($"Hugger: ForceMove direction = {_debugMoveDirection}");
		}
		Velocity = _debugMoveDirection * SeekSpeed;
	}

	private void HandleSeekState(double delta)
	{
		if (_seekTimer >= SeekNewDestinationTime)
		{
			PickRandomDestination();
			_seekTimer = 0.0;
		}

		switch (_seekSubState)
		{
			case SeekSubState.Moving:
				MoveAlongPath(SeekSpeed);
				break;
			case SeekSubState.Pausing:
				_pauseTimer += delta;
				if (_pauseTimer >= SeekPauseTime)
				{
					_pauseTimer = 0.0;
					_seekSubState = SeekSubState.Moving;
				}
				break;
		}
	}

	private void HandleAlertState(double delta)
	{
		if (_currentPathIndex < _currentPath.Count)
		{
			MoveAlongPath(AlertSpeed);
		}
		else if (GlobalPosition.DistanceTo(_alertSpot) <= 20)
		{
			if (_stateTimer < 15.0)
				SearchAroundAlertSpot();
			else
				ReturnToNearestAvailableTileAndSeek();
		}
	}

	private void HandleAttackState(double delta)
	{
		var kid = _player as Kid;
		bool goodState = kid != null && !kid.IsBeingHugged && Mathf.IsZeroApprox(kid.MudSinkingProgress);

		if (!_isLatching && !_isLatched)
		{
			if (!goodState)
			{
				_badStateTimer += delta;
				if (_badStateTimer > _badThreshold)
				{
					ReturnToSeekState();
					return;
				}
			}
			else
			{
				_isLatching = true;
			}
		}

		
		if (_isLatching)
		{
			if (_attachPoint != null)
			{
				Vector2 target = _attachPoint.GlobalPosition;
				Vector2 dir = (target - GlobalPosition).Normalized();
				Velocity = dir * AttackSpeed * 1.5f;

				if (GlobalPosition.DistanceTo(target) < 10)
				{
					_isLatching = false;
					_isLatched = true;
					_latchTimer = 0.0;
					kid?.StartHugging();
				}
			}
			return;
		}

		
		if (_isLatched)
		{
			if (_attachPoint != null)
			{
				GlobalPosition = _attachPoint.GlobalPosition;
				Velocity = Vector2.Zero;
			}
			_latchTimer += delta;
			if (_latchTimer > _detachTime || (kid != null && kid.MudSinkingProgress >= 1.0f))
			{
				kid?.StopHugging();
				ReturnToSeekState();
			}
			return;
		}

		
		Vector2 toPlayer = _player.GlobalPosition - GlobalPosition;
		float dist = toPlayer.Length();
		if (dist > 150)
			Velocity = toPlayer.Normalized() * AttackSpeed;
		else if (dist < 100)
			Velocity = -toPlayer.Normalized() * (AttackSpeed * 0.7f);
		else
			Velocity = new Vector2(-toPlayer.Y, toPlayer.X).Normalized() * AttackSpeed;
	}

	
	
	

	private void SwitchToAlertState(Vector2 alertLocation)
	{
		_currentState = HuggerState.Alert;
		_alertSpot = alertLocation;
		_stateTimer = 0.0;

		_currentPath.Clear();
		_currentPath.AddRange(FindPath(GlobalPosition, _alertSpot));
		_currentPathIndex = 0;
		_searchedTiles.Clear();

		FindNearbyHuggers();
		_isSearchLeader = _nearbyHuggers.Count == 0 ||
						 (_nearbyHuggers.Count > 0 && GetInstanceId() < _nearbyHuggers[0].GetInstanceId());
	}

	private void SwitchToAttackState()
	{
		_currentState = HuggerState.Attack;
		_stateTimer = 0.0;
		_currentPath.Clear();

		_isLatching = false;
		_isLatched = false;
		_latchTimer = 0.0;
		_badStateTimer = 0.0;
		_detachTime = 7.0 + _random.NextDouble() * 3.0; 
		_badThreshold = 3.0 + _random.NextDouble() * 2.0; 

		_attachPoint = null;
		if (_player is Node2D pd)
		{
			Node2D attachPts = pd.GetNodeOrNull<Node2D>("HuggerAttachPoints");
			if (attachPts != null && attachPts.GetChildCount() > 0)
			{
				_attachPoint = attachPts.GetChild<Node2D>(_random.Next(attachPts.GetChildCount()));
			}
		}
	}

	private void ReturnToSeekState()
	{
		_currentState = HuggerState.Seek;
		_stateTimer = 0.0;
		_seekTimer = 0.0;
		ReturnToNearestAvailableTileAndSeek();
	}

	private void ReturnToNearestAvailableTileAndSeek()
	{
		Vector2I currentTile = GetTilePosition(GlobalPosition);
		Vector2I nearest = currentTile; 

		if (nearest == currentTile)
		{
			_seekSubState = SeekSubState.Moving;
			_seekTimer = SeekNewDestinationTime; 
			return;
		}

		_currentPath.Clear();
		_currentPath.AddRange(FindPath(GlobalPosition, _tileMap.MapToLocal(nearest)));
		_currentPathIndex = 0;
		_seekSubState = SeekSubState.Moving;
		_seekTimer = SeekNewDestinationTime;
	}

	
	
	

	private Vector2I GetTilePosition(Vector2 worldPos) =>
		_tileMap == null ? Vector2I.Zero : _tileMap.LocalToMap(_tileMap.ToLocal(worldPos));

	private List<Vector2> FindPath(Vector2 from, Vector2 to)
	{
		Vector2I start = GetTilePosition(from);
		Vector2I end = GetTilePosition(to);

		
		if (!IsTileValidForMovement(start) || !IsTileValidForMovement(end))
			return new();
		if (start == end)
			return new() { to };

		
		var cameFrom = new Dictionary<Vector2I, Vector2I>();
		var gScore = new Dictionary<Vector2I, float> { [start] = 0f };
		var fScore = new Dictionary<Vector2I, float> { [start] = HeuristicCost(start, end) };
		var openSet = new List<Vector2I> { start };
		var closedSet = new HashSet<Vector2I>();

		while (openSet.Count > 0)
		{
			
			Vector2I current = openSet[0];
			foreach (var node in openSet)
				if (fScore.TryGetValue(node, out float fNode) && fNode < fScore[current])
					current = node;

			if (current == end)
				return ReconstructPath(cameFrom, current, from, to);

			openSet.Remove(current);
			closedSet.Add(current);

			foreach (Vector2I neighbor in GetNeighbors(current))
			{
				if (closedSet.Contains(neighbor)) continue;
				float tentativeG = gScore[current] + 1f;

				bool inOpen = openSet.Contains(neighbor);
				if (!inOpen || tentativeG < gScore.GetValueOrDefault(neighbor, float.PositiveInfinity))
				{
					cameFrom[neighbor] = current;
					gScore[neighbor] = tentativeG;
					fScore[neighbor] = tentativeG + HeuristicCost(neighbor, end);
					if (!inOpen) openSet.Add(neighbor);
				}
			}
		}

		return new(); 
	}

	private List<Vector2> ReconstructPath(Dictionary<Vector2I, Vector2I> cameFrom, Vector2I current, Vector2 from, Vector2 to)
	{
		var path = new List<Vector2> { to };
		while (cameFrom.TryGetValue(current, out var prev))
		{
			current = prev;
			path.Insert(0, _tileMap.MapToLocal(current));
		}
		return path;
	}

	private IEnumerable<Vector2I> GetNeighbors(Vector2I tile)
	{
		static IEnumerable<Vector2I> Dirs()
		{
			yield return new Vector2I(1, 0);
			yield return new Vector2I(-1, 0);
			yield return new Vector2I(0, 1);
			yield return new Vector2I(0, -1);
			
			yield return new Vector2I(1, 1);
			yield return new Vector2I(-1, 1);
			yield return new Vector2I(1, -1);
			yield return new Vector2I(-1, -1);
		}

		foreach (var d in Dirs())
		{
			Vector2I n = tile + d;
			if (IsTileValidForMovement(n))
				yield return n;
		}
	}

	private static float HeuristicCost(Vector2I a, Vector2I b) => Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y);

	private bool IsTileValidForMovement(Vector2I tile)
	{
		if (_tileMap == null) return true; 

		try
		{
			TileData data = _tileMap.GetCellTileData(tile);
			if (data == null) return true; 

			bool hasCollision = false;
			try
			{
				for (int i = 0; i < data.GetCollisionPolygonsCount(0); i++)
				{
					hasCollision = true; break;
				}
			}
			catch (Exception e)
			{
				GD.PrintErr($"Collision check error: {e.Message}");
			}

			return !hasCollision;
		}
		catch (Exception e)
		{
			GD.PrintErr($"IsTileValidForMovement error ({tile}): {e.Message}");
			return true;
		}
	}

	
	
	

	private void PickRandomDestination()
	{
		int radius = _random.Next(MinSearchRadius, MaxSearchRadius + 1);
		for (int attempt = 0; attempt < 10; attempt++)
		{
			float angle = (float)(_random.NextDouble() * Math.PI * 2);
			Vector2 dir = new((float)Math.Cos(angle), (float)Math.Sin(angle));
			Vector2 candidate = GlobalPosition + dir * (_tileSize.X * radius);
			Vector2I tile = GetTilePosition(candidate);
			if (!IsTileValidForMovement(tile)) continue;

			_targetPosition = _tileMap.MapToLocal(tile);
			_currentPath.Clear();
			_currentPath.AddRange(FindPath(GlobalPosition, _targetPosition));
			_currentPathIndex = 0;
			if (_currentPath.Count > 0) break;
		}
	}

	private void MoveAlongPath(float speed)
	{
		if (_currentPathIndex >= _currentPath.Count) { Velocity = Vector2.Zero; return; }
		Vector2 next = _currentPath[_currentPathIndex];
		Velocity = (next - GlobalPosition).Normalized() * speed;
		if (GlobalPosition.DistanceTo(next) < 10)
		{
			_currentPathIndex++;
			if (_currentPathIndex >= _currentPath.Count)
			{
				Velocity = Vector2.Zero;
				if (_currentState == HuggerState.Alert && GlobalPosition.DistanceTo(_alertSpot) < 20)
					_searchedTiles.Clear();
			}
		}
	}

	private bool IsPlayerInDetectionRadius(Vector2I me, Vector2I pl, int r) =>
		Math.Abs(pl.X - me.X) + Math.Abs(pl.Y - me.Y) <= r;

	private void SearchAroundAlertSpot()
	{
		Vector2I here = GetTilePosition(GlobalPosition);
		if (!_searchedTiles.Contains(here)) _searchedTiles.Add(here);

		var tiles = GetTilesInRadius(GetTilePosition(_alertSpot), AlertSearchRadius);
		tiles.RemoveAll(t => _searchedTiles.Contains(t));

		if (_nearbyHuggers.Count > 0 && _isSearchLeader)
			DivideTilesAmongHuggers(tiles);

		if (tiles.Count == 0 || _stateTimer > 15)
		{
			ReturnToNearestAvailableTileAndSeek();
			return;
		}

		Vector2I target = GetNearestTile(here, tiles);
		_currentPath.Clear();
		_currentPath.AddRange(FindPath(GlobalPosition, _tileMap.MapToLocal(target)));
		_currentPathIndex = 0;
	}

	private List<Vector2I> GetTilesInRadius(Vector2I center, int radius)
	{
		var list = new List<Vector2I>();
		for (int x = -radius; x <= radius; x++)
		for (int y = -radius; y <= radius; y++)
		{
			if (Math.Abs(x) + Math.Abs(y) > radius) continue;
			Vector2I tile = new(center.X + x, center.Y + y);
			if (IsTileValidForMovement(tile)) list.Add(tile);
		}
		return list;
	}

	private Vector2I GetNearestTile(Vector2I from, List<Vector2I> tiles)
	{
		Vector2I best = tiles[0];
		int bestDist = int.MaxValue;
		foreach (var t in tiles)
		{
			int d = Math.Abs(t.X - from.X) + Math.Abs(t.Y - from.Y);
			if (d < bestDist) { bestDist = d; best = t; }
		}
		return best;
	}

	private void FindNearbyHuggers()
	{
		_nearbyHuggers.Clear();
		foreach (Node n in GetTree().GetNodesInGroup("huggers"))
			if (n is Hugger h && h != this && GlobalPosition.DistanceTo(h.GlobalPosition) < 300)
				_nearbyHuggers.Add(h);
	}

	private void DivideTilesAmongHuggers(List<Vector2I> tiles)
	{
		var all = new List<Hugger>(_nearbyHuggers) { this };
		all.Sort((a, b) => a.GetInstanceId().CompareTo(b.GetInstanceId()));
		int idx = all.IndexOf(this);
		int sector = 360 / all.Count;
		int startA = idx * sector;
		int endA = (idx + 1) * sector;
		Vector2 alertPos = _alertSpot;
		tiles.RemoveAll(tile =>
		{
			Vector2 dir = _tileMap.MapToLocal(tile) - alertPos;
			float ang = Mathf.RadToDeg(Mathf.Atan2(dir.Y, dir.X));
			if (ang < 0) ang += 360;
			return ang < startA || ang >= endA;
		});
	}
	
	public void Alert(Vector2 alertPosition) => SwitchToAlertState(alertPosition);
}
