using Godot;
using System;
using DialogueManagerRuntime;

public partial class ChestBreak : Node2D, Interactable
{
	private bool _isBroken = false; 
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
		if (_isBroken)
		{
			return; // Chest is already broken, do nothing
		}

		DialogueManager.ShowDialogueBalloon(GD.Load("res://dialogue/ChestBreak.dialogue"), "start");
	}
}
