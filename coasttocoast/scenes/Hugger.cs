using Godot;
using System;
using System.Collections.Generic;

public partial class Hugger : CharacterBody2D
{
	[Export] public NodePath PlayerTarget;
	[Export] public NodePath TileMapPath;
	[Export] public float SeekSpeed = 150.0f;
	[Export] public float AlertSpeed = 300.0f;
	[Export] public float AttackSpeed = 200.0f;
	
	// Search radius parameters
	[Export] public int MinSearchRadius = 2;
	[Export] public int MaxSearchRadius = 4;
	[Export] public int AlertSearchRadius = 3;
	[Export] public int AttackDetectionRadius = 2;
	
	// Timer for random movement
	[Export] public float SeekNewDestinationTime = 3.0f;
	[Export] public float SeekPauseTime = 1.5f; // Pause duration between movements
    
    // Tile size constants based on marsh_tiles.tscn
    private Vector2I TILE_SIZE = new Vector2I(64, 64);

	// State machine variables
	private enum HuggerState
	{
		Seek,
		Alert,
		Attack,
        // Add debug state for testing
        ForceMove
	}
	
	// Additional seek state tracking
	private enum SeekSubState
	{
		Moving,
		Pausing
	}
	
	private HuggerState _currentState = HuggerState.ForceMove; // Start with force move for debugging
	private SeekSubState _seekSubState = SeekSubState.Moving; // Sub-state for seek behavior
	private Vector2 _originalPosition;
	private Vector2 _targetPosition;
	private Vector2 _alertSpot;
	private List<Vector2> _currentPath = new List<Vector2>();
	private int _currentPathIndex = 0;
	private double _stateTimer = 0.0;
	private double _seekTimer = 0.0;
	private double _pauseTimer = 0.0; // Timer for pausing between movements
	private Vector2I _lastTilePosition;
	private Random _random = new Random();
	private TileMapLayer _tileMap; // Changed from TileMap to TileMapLayer
	private Node2D _player;
	
	// Tracking of other huggers for cooperative search
	private List<Hugger> _nearbyHuggers = new List<Hugger>();
	private List<Vector2I> _searchedTiles = new List<Vector2I>();
	private bool _isSearchLeader = false;
    
    // Debug variables
    private Vector2 _debugMoveDirection = Vector2.Right;
    private double _debugDirectionChangeTimer = 0.0;

	// Add a function to switch between states (for testing)
	[Export] public string DebugState = "ForceMove"; // Can be "ForceMove", "Seek", "Alert", "Attack"
	
	public override void _Ready()
	{
		// Store original position for returning after alert
		_originalPosition = GlobalPosition;
		_targetPosition = _originalPosition;
		
		// Get the tilemap - try different approaches to finding it
		if (!TileMapPath.IsEmpty)
		{
			_tileMap = GetNode<TileMapLayer>(TileMapPath);
		}
		else
		{
			// Try to find the tilemap in the parent scene - try both names we've seen
			_tileMap = GetTree().GetCurrentScene().GetNodeOrNull<TileMapLayer>("Feature");
			if (_tileMap == null)
			{
				_tileMap = GetTree().GetCurrentScene().GetNodeOrNull<TileMapLayer>("BG");
			}
		}
		
		if (_tileMap == null)
		{
			GD.PrintErr("Hugger: Tilemap not found for pathfinding. Listing available nodes:");
			ListAvailableNodes(GetTree().GetCurrentScene(), 0);
		}
		else 
		{
			// Verify the tile size from the loaded tilemap
			if (_tileMap.TileSet != null)
			{
				// Use the actual tile size from the tilemap
				TILE_SIZE = _tileMap.TileSet.TileSize;
				GD.Print("Hugger using tile size: " + TILE_SIZE);
			}
		}
		
		// Get player reference
		if (!PlayerTarget.IsEmpty)
		{
			_player = GetNode<Node2D>(PlayerTarget);
		}
		else
		{
			// Try to find player in the scene
			_player = GetTree().GetCurrentScene().GetNodeOrNull<Node2D>("kiddo");
		}
		
		if (_player == null)
		{
			GD.PrintErr("Hugger: Player not found for targeting.");
		}

		// Debug print
		//GD.Print("Hugger: Initial position - " + GlobalPosition);
		
		// Set initial state from export variable
		SetDebugState(DebugState);
		
		// Add to huggers group for cooperative search
		AddToGroup("huggers");
	}
	
	// Helper method to list available nodes for debugging
	private void ListAvailableNodes(Node root, int depth)
	{
		if (depth > 5) return; // Avoid going too deep
		
		string indent = new string(' ', depth * 2);
		GD.Print(indent + root.Name + " (" + root.GetType() + ")");
		
		foreach (Node child in root.GetChildren())
		{
			ListAvailableNodes(child, depth + 1);
		}
	}
	
	// Method to set the state for debugging
	private void SetDebugState(string stateName)
	{
		switch (stateName.ToLower())
		{
			case "forcemove":
				_currentState = HuggerState.ForceMove;
				// Debug - test a hard-coded path to see if movement works
				_currentPath = new List<Vector2>
				{
					GlobalPosition + new Vector2(100, 0),
					GlobalPosition + new Vector2(100, 100),
					GlobalPosition + new Vector2(0, 100),
					GlobalPosition
				};
				_currentPathIndex = 0;
				break;
				
			case "seek":
				_currentState = HuggerState.Seek;
				PickRandomDestination();
				break;
				
			case "alert":
				_currentState = HuggerState.Alert;
				// Set an alert spot for testing
				SwitchToAlertState(GlobalPosition + new Vector2(200, 200));
				break;
				
			case "attack":
				if (_player != null)
				{
					_currentState = HuggerState.Attack;
					SwitchToAttackState();
				}
				else
				{
					GD.PrintErr("Cannot set Attack state - no player found");
					_currentState = HuggerState.ForceMove;
				}
				break;
				
			default:
				GD.PrintErr("Unknown debug state: " + stateName);
				_currentState = HuggerState.ForceMove;
				break;
		}
		
		//GD.Print("Hugger: Set state to " + _currentState);
	}

	public override void _PhysicsProcess(double delta)
	{
		// Update timers
        _stateTimer += delta;
        _seekTimer += delta;
        _debugDirectionChangeTimer += delta;
        
        // Debug print every few seconds
        if ((int)(_stateTimer * 10) % 20 == 0) // Print every ~2 seconds
        {
            //GD.Print("Hugger: Current state - " + _currentState + ", Position: " + GlobalPosition);
            //GD.Print("Hugger: Path points: " + _currentPath.Count + ", Current index: " + _currentPathIndex);
            //GD.Print("Hugger: Current velocity: " + Velocity);
        }

        // Force move debugging state - this will make it move regardless of tilemap or player
        if (_currentState == HuggerState.ForceMove)
        {
            HandleForceMoveState(delta);
            MoveAndSlide();
            return;
        }
		
		if (_tileMap == null || _player == null)
		{
			GD.PrintErr("Hugger: Missing tilemap or player reference");
			return;
		}

		// State machine logic
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
		
		// Check for player detection
        Vector2I playerTilePos = GetTilePosition(_player.GlobalPosition);
        Vector2I currentTilePos = GetTilePosition(GlobalPosition);
        _lastTilePosition = currentTilePos;

        // Skip detection if player is fully sunk in mud
        if (_player is Kid kid && kid.MudSinkingProgress >= 1.0f)
        {
            // Player is undetectable when fully sunk. Do something here maybe...?
        }
        else
        {
            if (IsPlayerInDetectionRadius(currentTilePos, playerTilePos, AttackDetectionRadius))
            {
                SwitchToAttackState();
            }
        }

		// Apply movement
		MoveAndSlide();
	}
    
    private void HandleForceMoveState(double delta)
    {
        // Change direction every 3 seconds
        if (_debugDirectionChangeTimer > 3.0)
        {
            _debugDirectionChangeTimer = 0;
            
            // Rotate 90 degrees
            _debugMoveDirection = new Vector2(-_debugMoveDirection.Y, _debugMoveDirection.X);
            GD.Print("Hugger: Changed force move direction to " + _debugMoveDirection);
        }
        
        // Apply a constant velocity in the current debug direction
        Velocity = _debugMoveDirection * SeekSpeed;
    }

	private void HandleSeekState(double delta)
	{
		// In seek mode, pick random destination every few seconds
		if (_seekTimer >= SeekNewDestinationTime)
		{
			PickRandomDestination();
			_seekTimer = 0;
		}
		
		// Follow current path to destination
		MoveAlongPath(SeekSpeed);
		
		// Handle sub-states for seeking
		switch (_seekSubState)
		{
			case SeekSubState.Moving:
				// Continue moving along the path
				MoveAlongPath(SeekSpeed);
				break;
				
			case SeekSubState.Pausing:
				// Pause for a moment before next movement
				if (_pauseTimer >= SeekPauseTime)
				{
					// Resume moving
					_pauseTimer = 0;
					_seekSubState = SeekSubState.Moving;
				}
				break;
		}
	}

	private void HandleAlertState(double delta)
	{
		// First part of alert: pathfind to alert spot
		if (_currentPath.Count > 0 && _currentPathIndex < _currentPath.Count)
		{
			MoveAlongPath(AlertSpeed);
		}
		// Once at the alert spot, search methodically
		else if (GlobalPosition.DistanceTo(_alertSpot) <= 20)
		{
			// Search in a circular pattern around the alert spot
			if (_stateTimer < 15) // Limit search time
			{
				SearchAroundAlertSpot();
			}
			else
			{
				// If search time exceeded, return to original position
				ReturnToOriginalPosition();
			}
		}
	}

	private void HandleAttackState(double delta)
	{
		// In attack mode, circle the player
		if (_player != null)
		{
			Vector2 toPlayer = _player.GlobalPosition - GlobalPosition;
			float distanceToPlayer = toPlayer.Length();
			
			if (distanceToPlayer > 150)
			{
				// Move closer to player if too far
				Vector2 direction = toPlayer.Normalized();
				Velocity = direction * AttackSpeed;
			}
			else if (distanceToPlayer < 100)
			{
				// Back away if too close
				Vector2 direction = -toPlayer.Normalized();
				Velocity = direction * (AttackSpeed * 0.7f);
			}
			else
			{
				// Circle around the player
				Vector2 circleDirection = new Vector2(-toPlayer.Y, toPlayer.X).Normalized();
				Velocity = circleDirection * AttackSpeed;
			}
		}
	}

	private void SwitchToAlertState(Vector2 alertLocation)
	{
		_currentState = HuggerState.Alert;
		_alertSpot = alertLocation;
		_stateTimer = 0;
		
		// Find path to alert spot
		_currentPath = FindPath(GlobalPosition, _alertSpot);
		_currentPathIndex = 0;
		
		// Reset searched tiles
		_searchedTiles.Clear();
		
		// Check for other huggers nearby
		FindNearbyHuggers();
		
		// Determine if this hugger is the search leader
		if (_nearbyHuggers.Count == 0 || 
		    (_nearbyHuggers.Count > 0 && GetInstanceId() < _nearbyHuggers[0].GetInstanceId()))
		{
			_isSearchLeader = true;
		}
	}

	private void SwitchToAttackState()
	{
		_currentState = HuggerState.Attack;
		_stateTimer = 0;
		// Clear path as we'll directly track the player
		_currentPath.Clear();
	}

	private void ReturnToSeekState()
	{
		_currentState = HuggerState.Seek;
		_stateTimer = 0;
		_seekTimer = 0;
		
		// Return to original position
		ReturnToOriginalPosition();
	}

	private void ReturnToOriginalPosition()
	{
		_currentPath = FindPath(GlobalPosition, _originalPosition);
		_currentPathIndex = 0;
		
		if (_currentPath.Count == 0)
		{
			// If no path found, just reset to seek state
			_currentState = HuggerState.Seek;
			_stateTimer = 0;
			_seekTimer = SeekNewDestinationTime; // Force a new destination pick
		}
	}

	private void PickRandomDestination()
	{
		// Choose a random radius between min and max
		int searchRadius = _random.Next(MinSearchRadius, MaxSearchRadius + 1);
		
		// Get current tile position
		Vector2I currentTilePos = GetTilePosition(GlobalPosition);
		
		// Try to find a valid destination tile
		for (int attempts = 0; attempts < 10; attempts++)
		{
			// Pick a random direction
			float angle = (float)(_random.NextDouble() * Math.PI * 2);
			Vector2 direction = new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle));
			
			// Calculate target tile position
			Vector2 targetPos = GlobalPosition + direction * (TILE_SIZE.X * searchRadius);
			Vector2I targetTilePos = GetTilePosition(targetPos);
			
			// Check if the tile is valid for movement
			if (IsTileValidForMovement(targetTilePos))
			{
				// Convert back to world position
				_targetPosition = _tileMap.MapToLocal(targetTilePos);
				
				// Find path to the target position
				_currentPath = FindPath(GlobalPosition, _targetPosition);
				_currentPathIndex = 0;
				
				if (_currentPath.Count > 0)
				{
					// Found a valid path, use this destination
					break;
				}
			}
		}
	}

	private void MoveAlongPath(float speed)
	{
		if (_currentPath.Count == 0 || _currentPathIndex >= _currentPath.Count)
		{
			Velocity = Vector2.Zero;
			return;
		}

		// Get next path position
		Vector2 nextPos = _currentPath[_currentPathIndex];
		Vector2 direction = (nextPos - GlobalPosition).Normalized();
		
		// Move towards the next point
		Velocity = direction * speed;
		
		// Check if we've reached the current path point
		if (GlobalPosition.DistanceTo(nextPos) < 10)
		{
			_currentPathIndex++;
			
			// If we've reached the end of the path
			if (_currentPathIndex >= _currentPath.Count)
			{
				Velocity = Vector2.Zero;
				
				// If we were in alert state and reached the alert spot
				if (_currentState == HuggerState.Alert && GlobalPosition.DistanceTo(_alertSpot) < 20)
				{
					// Start searching
					_searchedTiles.Clear();
				}
			}
		}
	}

	private List<Vector2> FindPath(Vector2 from, Vector2 to)
	{
		// A simple A* pathfinding implementation
		Vector2I startTile = GetTilePosition(from);
		Vector2I endTile = GetTilePosition(to);
		
		// Check if start or end is invalid
		if (!IsTileValidForMovement(startTile) || !IsTileValidForMovement(endTile))
		{
			return new List<Vector2>();
		}
		
		// If start and end are the same, no path needed
		if (startTile == endTile)
		{
			return new List<Vector2> { to };
		}
		
		// A* pathfinding
		Dictionary<Vector2I, Vector2I> cameFrom = new Dictionary<Vector2I, Vector2I>();
		Dictionary<Vector2I, float> gScore = new Dictionary<Vector2I, float>();
		Dictionary<Vector2I, float> fScore = new Dictionary<Vector2I, float>();
		List<Vector2I> openSet = new List<Vector2I>();
		HashSet<Vector2I> closedSet = new HashSet<Vector2I>();
		
		// Initialize
		openSet.Add(startTile);
		gScore[startTile] = 0;
		fScore[startTile] = HeuristicCost(startTile, endTile);
		
		while (openSet.Count > 0)
		{
			// Find the node in openSet with the lowest fScore
			Vector2I current = openSet[0];
			float lowestFScore = fScore[current];
			
			for (int i = 1; i < openSet.Count; i++)
			{
				if (fScore.ContainsKey(openSet[i]) && fScore[openSet[i]] < lowestFScore)
				{
					current = openSet[i];
					lowestFScore = fScore[current];
				}
			}
			
			// If we reached the goal
			if (current == endTile)
			{
				return ReconstructPath(cameFrom, current, from, to);
			}
			
			// Remove current from openSet
			openSet.Remove(current);
			closedSet.Add(current);
			
			// Check all neighbors
			foreach (Vector2I neighbor in GetNeighbors(current))
			{
				if (closedSet.Contains(neighbor))
				{
					continue; // Ignore already evaluated neighbors
				}
				
				// Calculate tentative gScore
				float tentativeGScore = gScore[current] + 1;
				
				// If neighbor is not in openSet, add it
				if (!openSet.Contains(neighbor))
				{
					openSet.Add(neighbor);
				}
				else if (gScore.ContainsKey(neighbor) && tentativeGScore >= gScore[neighbor])
				{
					continue; // This path is not better than the previous one
				}
				
				// This path is the best so far, record it
				cameFrom[neighbor] = current;
				gScore[neighbor] = tentativeGScore;
				fScore[neighbor] = gScore[neighbor] + HeuristicCost(neighbor, endTile);
			}
		}
		
		// No path found
		return new List<Vector2>();
	}

	private List<Vector2> ReconstructPath(Dictionary<Vector2I, Vector2I> cameFrom, Vector2I current, Vector2 from, Vector2 to)
	{
		List<Vector2> path = new List<Vector2>();
		
		// Add the destination
		path.Add(to);
		
		// Reconstruct the path
		while (cameFrom.ContainsKey(current))
		{
			current = cameFrom[current];
			// Add world position of each tile
			path.Insert(0, _tileMap.MapToLocal(current));
		}
		
		return path;
	}

	private List<Vector2I> GetNeighbors(Vector2I tile)
	{
		List<Vector2I> neighbors = new List<Vector2I>();
		
		// Check 4 adjacent tiles (or 8 if diagonal movement is allowed)
		Vector2I[] directions = new Vector2I[]
		{
			new Vector2I(1, 0),
			new Vector2I(-1, 0),
			new Vector2I(0, 1),
			new Vector2I(0, -1),
			// Uncomment for diagonal movement
			new Vector2I(1, 1),
			new Vector2I(-1, 1),
			new Vector2I(1, -1),
			new Vector2I(-1, -1)
		};
		
		foreach (Vector2I dir in directions)
		{
			Vector2I neighbor = tile + dir;
			if (IsTileValidForMovement(neighbor))
			{
				neighbors.Add(neighbor);
			}
		}
		
		return neighbors;
	}

	private float HeuristicCost(Vector2I from, Vector2I to)
	{
		// Manhattan distance
		return Math.Abs(to.X - from.X) + Math.Abs(to.Y - from.Y);
	}

	private bool IsTileValidForMovement(Vector2I tile)
	{
		// Check if the tilemap exists
		if (_tileMap == null)
		{
			GD.PrintErr("Hugger: TileMap is null in IsTileValidForMovement");
			return true; // Allow movement if no tilemap (for debugging)
		}
		
		try
		{
			// Get the tile data - fixed to be consistent with TileMapLayer methods
			TileData tileData = _tileMap.GetCellTileData(tile);
			
			// If there's no tile data, the tile may still be valid (empty tile)
			if (tileData == null)
			{
				// For debugging, assume empty tiles are valid
				return true;
			}
			
			// Hugger is a floating creature, so we ignore MoveSpeed constraints
			// Only check for collision
			bool hasCollision = false;
			try
			{
				for (int i = 0; i < tileData.GetCollisionPolygonsCount(0); i++)
				{
					hasCollision = true;
					break;
				}
			}
			catch (Exception e)
			{
				GD.PrintErr("Error checking collision: " + e.Message);
				// Assume no collision if there's an error
				hasCollision = false;
			}
			
			if (hasCollision) 
			{
				GD.Print("Tile at " + tile + " has collision, avoiding");
				return false;
			}
			
			// Tile is valid for movement by the floating Hugger
			return true;
		}
		catch (Exception e)
		{
			// If there's an error, log it and allow movement (for debugging)
			GD.PrintErr("Error in IsTileValidForMovement: " + e.Message + " for tile " + tile);
			return true;
		}
	}
    
    private Vector2I GetTilePosition(Vector2 worldPos)
	{
		if (_tileMap == null) return Vector2I.Zero;
		return _tileMap.LocalToMap(_tileMap.ToLocal(worldPos));
	}

	private bool IsPlayerInDetectionRadius(Vector2I huggerPos, Vector2I playerPos, int radius)
	{
		// Calculate Manhattan distance between hugger and player
		int distance = Math.Abs(playerPos.X - huggerPos.X) + Math.Abs(playerPos.Y - huggerPos.Y);
		return distance <= radius;
	}

	private void SearchAroundAlertSpot()
	{
		// Get current tile
		Vector2I currentTile = GetTilePosition(GlobalPosition);
		
		// Mark current tile as searched
		if (!_searchedTiles.Contains(currentTile))
		{
			_searchedTiles.Add(currentTile);
		}
		
		// Search in a spiral pattern
		Vector2I alertTile = GetTilePosition(_alertSpot);
		List<Vector2I> tilesToSearch = GetTilesInRadius(alertTile, AlertSearchRadius);
		
		// Filter out already searched tiles
		tilesToSearch.RemoveAll(tile => _searchedTiles.Contains(tile));
		
		// If there are other huggers, divide the search area
		if (_nearbyHuggers.Count > 0 && _isSearchLeader)
		{
			DivideTilesAmongHuggers(tilesToSearch);
		}
		
		// If no more tiles to search or search time exceeded
		if (tilesToSearch.Count == 0 || _stateTimer > 15)
		{
			ReturnToOriginalPosition();
			return;
		}
		
		// Find the nearest unsearched tile
		Vector2I nearestTile = GetNearestTile(currentTile, tilesToSearch);
		
		// Calculate path to the nearest tile
		Vector2 worldPos = _tileMap.MapToLocal(nearestTile);
		_currentPath = FindPath(GlobalPosition, worldPos);
		_currentPathIndex = 0;
	}

	private List<Vector2I> GetTilesInRadius(Vector2I center, int radius)
	{
		List<Vector2I> tiles = new List<Vector2I>();
		
		for (int x = -radius; x <= radius; x++)
		{
			for (int y = -radius; y <= radius; y++)
			{
				if (Math.Abs(x) + Math.Abs(y) <= radius)
				{
					Vector2I tile = new Vector2I(center.X + x, center.Y + y);
					if (IsTileValidForMovement(tile))
					{
						tiles.Add(tile);
					}
				}
			}
		}
		
		return tiles;
	}

	private Vector2I GetNearestTile(Vector2I from, List<Vector2I> tiles)
	{
		Vector2I nearest = tiles[0];
		int shortestDistance = int.MaxValue;
		
		foreach (Vector2I tile in tiles)
		{
			int distance = Math.Abs(tile.X - from.X) + Math.Abs(tile.Y - from.Y);
			if (distance < shortestDistance)
			{
				shortestDistance = distance;
				nearest = tile;
			}
		}
		
		return nearest;
	}

	private void FindNearbyHuggers()
	{
		_nearbyHuggers.Clear();
		
		// Find all huggers in the scene
		foreach (Node node in GetTree().GetNodesInGroup("huggers"))
		{
			if (node is Hugger hugger && hugger != this)
			{
				// Check if within a reasonable distance
				float distance = GlobalPosition.DistanceTo(hugger.GlobalPosition);
				if (distance < 300) // Arbitrary distance for "nearby"
				{
					_nearbyHuggers.Add(hugger);
				}
			}
		}
	}

	private void DivideTilesAmongHuggers(List<Vector2I> tiles)
	{
		// Simple division: divide the tiles into sectors based on angle from alert spot
		int huggerCount = _nearbyHuggers.Count + 1; // +1 for this hugger
		int sectorsPerHugger = 360 / huggerCount;
		
		// Get all huggers including this one
		List<Hugger> allHuggers = new List<Hugger>(_nearbyHuggers);
		allHuggers.Add(this);
		
		// Sort huggers by instance ID to ensure consistent assignments
		allHuggers.Sort((a, b) => a.GetInstanceId().CompareTo(b.GetInstanceId()));
		
		// Find my sector
		int myIndex = allHuggers.IndexOf(this);
		int startAngle = myIndex * sectorsPerHugger;
		int endAngle = (myIndex + 1) * sectorsPerHugger;
		
		// Filter tiles to only those in my sector
		Vector2 alertPos = _alertSpot;
		tiles.RemoveAll(tile => {
			Vector2 tilePos = _tileMap.MapToLocal(tile);
			Vector2 direction = tilePos - alertPos;
			float angle = (float)(Math.Atan2(direction.Y, direction.X) * 180 / Math.PI);
			if (angle < 0) angle += 360;
			
			return angle < startAngle || angle >= endAngle;
		});
	}
	
	// Called when an external alert occurs (e.g., chest piece landing)
	public void Alert(Vector2 alertPosition)
	{
		SwitchToAlertState(alertPosition);
	}
}
