using Godot;
using System;
using DialogueManagerRuntime;

public partial class WinRoomSignpost : Sprite2D,Interactable
{
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
	}

	public void Interact(CharacterBody2D player)
	{
		DialogueManager.ShowDialogueBalloon(GD.Load("res://dialogue/WinRoom.dialogue"), "signpost"); // I should probably make interactable dialogue generic
	}
}
