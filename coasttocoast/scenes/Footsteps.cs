using Godot;
using System;

public partial class Footsteps : AudioStreamPlayer
{
	public override void _Ready()
	{
		Finished += () => Play();
	}
}
