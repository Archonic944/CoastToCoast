using Godot;
using System;

public partial class DialogFunctions : Node
{
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
	}

	public void TriggerChestBreak()
	{
		var marsh = GetTree().Root.GetNode("Marsh");
		marsh.GetNode<AnimationPlayer>("kiddo/AnimationPlayer").Play("spin");
		//wait 0.8 seconds to do the chest break
		GetTree().CreateTimer(0.28f).Timeout += () =>
		{
			var cb = marsh.GetNode<ChestBreak>("ChestBreak");
			var cs = cb?.GetNodeOrNull<Sprite2D>("ChestSprite");
			if (cs != null)
			{
				cs.Frame = 1;
			}

			cb?.GetNodeOrNull<AudioStreamPlayer2D>("ChestBreakSound")?.Play();
			cb?.GetNodeOrNull<StaticBody2D>("StaticBody2D")?.QueueFree();
		};
	}

	public void PickupChestPieces()
	{
		var marsh = GetTree().Root.GetNode("Marsh");
		var chest = marsh.GetNode<ChestBreak>("ChestBreak");
		chest.GetNode<Sprite2D>("ChestSprite").Hide();
		chest.GetNode<AudioStreamPlayer2D>("ItemPickup").Play();
		marsh.GetNode<Kid>("kiddo").ChestPieces = 4;
		GetTree().GetCurrentScene().GetNode<Node2D>("UI/ChestPieceHUD")?.Show();
	}

	public void Respawn()
	{
		GetTree().ChangeSceneToFile("res://scenes/marsh.tscn");
	}

	public void Win()
	{
		GetTree().GetCurrentScene().SetMeta("win", true);
	}

	public bool HasWon()
	{
		return (bool) GetTree().GetCurrentScene().GetMeta("win");
	}
}
