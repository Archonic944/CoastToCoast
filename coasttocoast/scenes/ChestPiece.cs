using Godot;
using System;
using System.Collections;
using System.Linq;

public partial class ChestPiece : Node2D, Interactable
{
	private Vector2 _startPos;
	private Vector2 _endPos;
	private float _timer;
	private const float _duration = 0.6f;
	private const float _arcHeight = 64f;
	private bool _thrown;

	private AudioStreamPlayer _whoosh;
	private AudioStreamPlayer2D _extWhoosh;
	private AnimationPlayer _anim;
	private Area2D _area;

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		_whoosh = GetNode<AudioStreamPlayer>("Whoosh");
		_extWhoosh = GetNode<AudioStreamPlayer2D>("ExtendedWhoosh");
		_anim = GetNode<AnimationPlayer>("AnimationPlayer");
		_area = GetNode<Area2D>("Area2D");
		_area.Monitoring = false;
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
		if (_thrown)
		{
			_timer += (float)delta;
			float t = Math.Min(_timer / _duration, 1f);
			Vector2 pos = _startPos.Lerp(_endPos, t);
			pos = new Vector2(pos.X, pos.Y - (float)Math.Sin(t * Mathf.Pi) * _arcHeight);
			GlobalPosition = pos;
			if (t >= 1f)
				OnLanded();
		}
	}

	public void StartThrow(Vector2 targetPosition)
	{
		_startPos = GlobalPosition;
		_endPos = targetPosition;
		_timer = 0f;
		_thrown = true;
		_area.Monitoring = false;
		_whoosh.Play();
		_extWhoosh.Play();
		_anim.Play("spin");
	}

	private void OnLanded()
	{
		_thrown = false;
		_extWhoosh.Stop();
		_anim.Play("RESET");
		_area.Monitoring = true;
		CheckTerrainAndAlert();
	}

	private void CheckTerrainAndAlert()
	{
		var scene = GetTree().GetCurrentScene();
		var tml = scene.GetNode<TileMapLayer>("Feature");
		if (tml != null)
		{
			var cell = tml.LocalToMap(tml.ToLocal(GlobalPosition));
			var tileData = tml.GetCellTileData(cell);
			if (tileData != null && tileData.GetTerrain() == TerrainType.Leaves)
			{
				// play bush shake sound
				var shakePlayer = GetNode<AudioStreamPlayer2D>("WhiteBushShaken");
				shakePlayer.Play();
				// alert nearest huggers
				var huggers = GetTree().GetNodesInGroup("huggers").OfType<Hugger>()
					.OrderBy(h => h.GlobalPosition.DistanceTo(GlobalPosition))
					.Take(5);
				foreach (var h in huggers)
					h.Alert(GlobalPosition);
			}
		}
	}

	public void Interact(CharacterBody2D player)
	{
		if (!_thrown)
		{
			GetNode<Area2D>("Area2D").Monitorable = false;
			GetNode<AudioStreamPlayer>("PickupPiece").Play();
			if (player is Kid kid)
				kid.ChestPieces++;
			// Pickup animation (tween chest piece to scale smaller and smaller and move towards player)
			var tween = CreateTween();
			tween.SetParallel();
			tween.TweenProperty(this, "scale", new Vector2(0.1f, 0.1f), 0.3f)
				.SetTrans(Tween.TransitionType.Sine)
				.SetEase(Tween.EaseType.Out);
			tween.TweenProperty(this, "position", player.GlobalPosition, 0.3f)
				.SetTrans(Tween.TransitionType.Sine)
				.SetEase(Tween.EaseType.Out);
			tween.Finished += QueueFree;
		}
	}
}
