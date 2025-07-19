using Godot;
using System;
using DialogueManagerRuntime;

public partial class RainBegin : Area2D
{
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		// Connect the body entered signal to a method
		BodyEntered += (body =>
		{
			if (body is Kid)
			{
				GetParent().GetNode<RainSpawner>("RainSpawner").Disabled = false;
			}

			GetParent().GetNode<AudioStreamPlayer>("RainAmbience").Play();
			DialogueManager.ShowDialogueBalloon(GD.Load("res://dialogue/Rain.dialogue"), "start");
			QueueFree();
		});
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
	}
}
