using Godot;
using System;
using System.Linq;
using DialogueManagerRuntime;

public partial class MarshDialogAreas : Node2D
{
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		Area2D beforeSticks = GetNode<Area2D>("BeforeSticks");
		beforeSticks.BodyEntered += (body) =>
		{
			if (body is Kid)
			{
				DoDialogue(beforeSticks, "res://dialogue/Sticks.dialogue", "before_sticks");
			}
		};
		Area2D afterSticks = GetNode<Area2D>("AfterSticks");
		afterSticks.BodyEntered += (body) =>
		{
			if (body is Kid)
			{
				DoDialogue(afterSticks, "res://dialogue/Sticks.dialogue", "after_sticks");
			}
		};
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
	}

	private void DoDialogue(Area2D area, string dialoguePath, string dialogueStart)
	{
		DialogueManager.ShowDialogueBalloon(GD.Load(dialoguePath), dialogueStart);
		area.SetDeferred("monitoring", false); // Can't set property directly in signal handler
	}
}
