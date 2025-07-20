using Godot;
using System;

public partial class StartMenu : Control
{
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		GetNode<Button>("Button").Pressed += () =>
		{
			GetNode<Button>("Button").Disabled = true;
			CreateTween().TweenProperty(this, "modulate", Colors.Transparent, 0.5f).Finished += () => GetTree().ChangeSceneToFile("res://scenes/marsh.tscn");
		};
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
	}
}
