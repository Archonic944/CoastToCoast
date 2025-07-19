using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class Hugger : CharacterBody2D
{
    [Export] public NodePath PlayerTarget;
    [Export] public NodePath TileMapPath;
    [Export] public float SeekSpeed = 150.0f;
    [Export] public float AlertTravelSpeed = 700.0f;
    [Export] public float AlertSearchSpeed = 320.0f;
    [Export] public float AttackSpeed = 400.0f;

    [Export] public int MinSearchRadius = 2;
    [Export] public int MaxSearchRadius = 4;
    [Export] public int AlertSearchRadius = 5;
    [Export] public int AttackDetectionRadius = 3;

    [Export] public float SeekNewDestinationTime = 3.0f;
    [Export] public float SeekPauseTime = 1.5f;

    private Vector2I _tileSize = new Vector2I(64, 64);

    private enum HuggerState { Seek, AlertTravel, AlertSearch, Attack, ForceMove }
    private enum SeekSubState { Moving, Pausing }

    private HuggerState _currentState = HuggerState.Seek;
    private SeekSubState _seekSubState = SeekSubState.Moving;

    private Vector2 _originalPosition;
    private Vector2 _targetPosition;
    private Vector2 _alertSpot;
    private List<Vector2I> _alertTilesToSearch;
    private readonly List<Vector2> _currentPath = new();
    private int _currentPathIndex = 0;

    private double _stateTimer = 0.0;
    private double _seekTimer = 0.0;
    private double _pauseTimer = 0.0;

    private readonly Random _random = new();
    private TileMapLayer _tileMap;
    private Node2D _player;

    private AStar2D _astar = new AStar2D();
    private Dictionary<Vector2I, int> _tileIdMap = new();

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

        _tileMap = !TileMapPath.IsEmpty ? GetNode<TileMapLayer>(TileMapPath) : GetTree().GetCurrentScene().GetNodeOrNull<TileMapLayer>("Feature") ?? GetTree().GetCurrentScene().GetNodeOrNull<TileMapLayer>("BG");
        if (_tileMap != null)
        {
            _tileSize = _tileMap.TileSet?.TileSize ?? _tileSize;
            BuildAStarGraph();
        }
        else
        {
            GD.PrintErr("Hugger: TileMap not found – path‑finding disabled.");
        }

        _player = !PlayerTarget.IsEmpty ? GetNode<Node2D>(PlayerTarget) : GetTree().GetCurrentScene().GetNodeOrNull<Node2D>("kiddo");
        if (_player == null)
        {
            GD.PrintErr("Hugger: Player not found for targeting.");
            QueueFree();
            return;
        }

        _attachPoint = _player.GetNode<Node2D>("HuggerAttachPoints/0");

        AddToGroup("huggers");
    }

    private void BuildAStarGraph()
    {
        var usedRect = _tileMap.GetUsedRect();
        int idCounter = 0;
        for (int x = usedRect.Position.X; x < usedRect.Position.X + usedRect.Size.X; x++)
        for (int y = usedRect.Position.Y; y < usedRect.Position.Y + usedRect.Size.Y; y++)
        {
            var tile = new Vector2I(x, y);
            if (!IsTileValidForMovement(tile))
                continue;
            var localPos = _tileMap.MapToLocal(tile);
            int id = idCounter++;
            _tileIdMap[tile] = id;
            _astar.AddPoint(id, localPos);
        }

        Vector2I[] dirs = {
            new Vector2I(1,0), new Vector2I(-1,0), new Vector2I(0,1), new Vector2I(0,-1),
            new Vector2I(1,1), new Vector2I(-1,1), new Vector2I(1,-1), new Vector2I(-1,-1)
        };
        foreach (var kv in _tileIdMap)
        {
            var tile = kv.Key;
            int id = kv.Value;
            foreach (var d in dirs)
            {
                var n = tile + d;
                if (_tileIdMap.TryGetValue(n, out int nid))
                    _astar.ConnectPoints(id, nid, false);
            }
        }
    }

    private List<Vector2> PathBetween(Vector2 from, Vector2 to, bool convertFrom = true, bool convertTo = true)
    {
        var startTile = convertFrom ? GetTilePosition(from) : ConvertTo2I(from);
        var endTile = convertTo ? GetTilePosition(to) : ConvertTo2I(to);
        if (!_tileIdMap.TryGetValue(startTile, out int startId) || !_tileIdMap.TryGetValue(endTile, out int endId))
            return new List<Vector2>();
        var localPath = _astar.GetPointPath(startId, endId);
        var worldPath = new List<Vector2>(localPath.Length);
        foreach (var lp in localPath)
            worldPath.Add(_tileMap.ToGlobal(lp));
        return worldPath;
    }

    public override void _PhysicsProcess(double delta)
    {
        _stateTimer += delta;
        _seekTimer += delta;
        if (_currentState == HuggerState.ForceMove)
            return;
        if (_tileMap == null || _player == null)
            return;
        switch (_currentState)
        {
            case HuggerState.Seek:
                HandleSeekState(delta);
                break;
            case HuggerState.AlertTravel:
                HandleAlertTravelState(delta);
                break;
            case HuggerState.AlertSearch:
                HandleAlertSearchState(delta);
                break;
            case HuggerState.Attack:
                HandleAttackState(delta);
                break;
        }
        var playerTile = GetTilePosition(_player.GlobalPosition);
        var myTile = GetTilePosition(GlobalPosition);
        if (_currentState != HuggerState.Attack && _currentState != HuggerState.AlertTravel && IsPlayerInDetectionRadius(myTile, playerTile, AttackDetectionRadius))
            SwitchToAttackState();
        MoveAndSlide();
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

    private void HandleAlertTravelState(double _) 
    {
        if (_currentPathIndex < _currentPath.Count)
        {
            MoveAlongPath(AlertTravelSpeed);
        }
        else
        {
            _currentState = HuggerState.AlertSearch;
            _stateTimer = 0.0;
            InitializeAlertSearch();
        }
    }

    private void InitializeAlertSearch()
    {
        _alertTilesToSearch = GetTilesInRadius(GetTilePosition(_alertSpot), AlertSearchRadius);
        var minY = _alertTilesToSearch.Min(t => t.Y);
        _alertTilesToSearch.Sort((a, b) =>
        {
            int yc = a.Y.CompareTo(b.Y);
            if (yc != 0) return yc;
            int ro = a.Y - minY;
            return (ro & 1) == 0 ? a.X.CompareTo(b.X) : b.X.CompareTo(a.X);
        });
        if (_alertTilesToSearch.Count == 0) { ReturnToSeekState(); return; }
        var firstWorld = _tileMap.ToGlobal(_tileMap.MapToLocal(_alertTilesToSearch[0]));
        _currentPath.Clear();
        _currentPath.AddRange(PathBetween(GlobalPosition, firstWorld));
        _currentPathIndex = 0;
    }

    private void HandleAlertSearchState(double delta)
    {
        if (_stateTimer <= 5.0)
        {
            MoveAlongPath(AlertSearchSpeed);
            if (GetTilePosition(GlobalPosition) == _alertTilesToSearch[0])
            {
                _alertTilesToSearch.RemoveAt(0);
                if (_alertTilesToSearch.Count > 0)
                {
                    var nextWorld = _tileMap.ToGlobal(_tileMap.MapToLocal(_alertTilesToSearch[0]));
                    _currentPath.Clear();
                    _currentPath.AddRange(PathBetween(GlobalPosition, nextWorld));
                    _currentPathIndex = 0;
                }
                else
                {
                    ReturnToSeekState();
                }
            }
        }
        else
        {
            ReturnToSeekState();
        }
    }

    private void HandleAttackState(double delta)
    {
        var kid = _player as Kid;
        bool good = kid != null && kid.iframes <= 0 && !kid.IsBeingHugged && Mathf.IsZeroApprox(kid.MudSinkingProgress);
        if (!_isLatching && !_isLatched)
        {
            if (!good)
            {
                _badStateTimer += delta;
                if (_badStateTimer > _badThreshold) { ReturnToSeekState(); return; }
            }
            else _isLatching = true;
        }
        if (_isLatching)
        {
            if (_attachPoint != null)
            {
                var dir = (_attachPoint.GlobalPosition - GlobalPosition).Normalized();
                Velocity = dir * AttackSpeed * 1.5f;
                if (GlobalPosition.DistanceTo(_attachPoint.GlobalPosition) < 15)
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
                GlobalPosition = _attachPoint.GlobalPosition;
            Velocity = Vector2.Zero;
            _latchTimer += delta;
            GetNode<Sprite2D>("Sprite2D").Offset = new Vector2(2 * GD.Randf() - 0.5f, 2 * GD.Randf() - 0.5f);
            if (_latchTimer > _detachTime || (kid != null && kid.MudSinkingProgress >= 1.0f))
            {
                kid?.StopHugging();
                if (kid != null) kid.iframes = 30;
                var randomLocal = _tileMap.ToLocal(GlobalPosition + new Vector2(GD.Randf()*1000 - 500, GD.Randf()*1000 - 500));
                var closest = _astar.GetClosestPoint(randomLocal);
                var localPos = _astar.GetPointPosition(closest);
                var worldPos = _tileMap.ToGlobal(localPos);
                SwitchToAlertState(worldPos);
            }
            return;
        }
        var to = _player.GlobalPosition - GlobalPosition;
        var dist = to.Length();
        if (dist > 150) Velocity = to.Normalized() * AttackSpeed;
        else if (dist < 100) Velocity = -to.Normalized() * (AttackSpeed * 0.7f);
        else Velocity = new Vector2(-to.Y, to.X).Normalized() * AttackSpeed;
    }

    private void SwitchToAlertState(Vector2 alertLocation)
    {
        _currentState = HuggerState.AlertTravel;
        _alertSpot = alertLocation;
        _stateTimer = 0.0;
        _currentPath.Clear();
        _currentPath.AddRange(PathBetween(GlobalPosition, alertLocation));
        _currentPathIndex = 0;
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
        var current = GetTilePosition(GlobalPosition);
        Vector2I[] offs = { Vector2I.Left, Vector2I.Right, Vector2I.Up, Vector2I.Down };
        foreach (var o in offs)
        {
            var n = current + o;
            if (IsTileValidForMovement(n))
            {
                var worldN = _tileMap.ToGlobal(_tileMap.MapToLocal(n));
                _currentPath.Clear();
                _currentPath.AddRange(PathBetween(GlobalPosition, worldN));
                _currentPathIndex = 0;
                _seekSubState = SeekSubState.Moving;
                _seekTimer = SeekNewDestinationTime;
                return;
            }
        }
        _seekSubState = SeekSubState.Moving;
        _seekTimer = SeekNewDestinationTime;
    }

    private Vector2I GetTilePosition(Vector2 worldPos)
        => _tileMap.LocalToMap(_tileMap.ToLocal(worldPos));

    private void PickRandomDestination()
    {
        int r = _random.Next(MinSearchRadius, MaxSearchRadius + 1);
        for (int i = 0; i < 10; i++)
        {
            var angle = (float)(_random.NextDouble() * Math.PI * 2);
            var dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            var candidate = GlobalPosition + dir * (_tileSize.X * r);
            var tile = GetTilePosition(candidate);
            if (!IsTileValidForMovement(tile)) continue;
            var world = _tileMap.ToGlobal(_tileMap.MapToLocal(tile));
            _currentPath.Clear();
            _currentPath.AddRange(PathBetween(GlobalPosition, world));
            _currentPathIndex = 0;
            if (_currentPath.Count > 0) break;
        }
    }

    private void MoveAlongPath(float speed)
    {
        if (_currentPathIndex >= _currentPath.Count)
        {
            Velocity = Vector2.Zero;
            return;
        }
        var next = _currentPath[_currentPathIndex];
        Velocity = (next - GlobalPosition).Normalized() * speed;
        if (GlobalPosition.DistanceTo(next) < 10)
            _currentPathIndex++;
    }

    private bool IsPlayerInDetectionRadius(Vector2I me, Vector2I pl, int r)
        => Math.Abs(pl.X - me.X) + Math.Abs(pl.Y - me.Y) <= r;

    private bool IsTileValidForMovement(Vector2I tile)
    {
        try
        {
            var data = _tileMap.GetCellTileData(tile);
            if (data != null && data.GetCollisionPolygonsCount(0) > 0)
                return false;
        }
        catch { }
        return true;
    }

    private List<Vector2I> GetTilesInRadius(Vector2I center, int radius)
    {
        var list = new List<Vector2I>();
        for (int x = -radius; x <= radius; x++)
        for (int y = -radius; y <= radius; y++)
            if (Math.Abs(x) + Math.Abs(y) <= radius)
            {
                var t = new Vector2I(center.X + x, center.Y + y);
                if (IsTileValidForMovement(t)) list.Add(t);
            }
        return list;
    }

    private Vector2I ConvertTo2I(Vector2 v)
        => new Vector2I((int)Math.Round(v.X / _tileSize.X), (int)Math.Round(v.Y / _tileSize.Y));

    private Vector2I GetNearestTile(Vector2I from, List<Vector2I> tiles)
    {
        var best = tiles[0];
        var bd = int.MaxValue;
        foreach (var t in tiles)
        {
            var d = Math.Abs(t.X - from.X) + Math.Abs(t.Y - from.Y);
            if (d < bd) { bd = d; best = t; }
        }
        return best;
    }

    // Called via CallGroup from Raindrop
    public void RaindropAt(Vector2 location)
    {
        //Only alerts them if 1/4 chance hits and if they're within 800 pixels
        if (_currentState == HuggerState.Seek && GlobalPosition.DistanceTo(location) < 800 && _random.NextDouble() < 0.125)
        {
            SwitchToAlertState(location);
        }
    }

    public void Alert(Vector2 alertPosition) => SwitchToAlertState(alertPosition);
}
