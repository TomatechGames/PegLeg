using Godot;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

public class PerfTimer : IDisposable
{
	Stopwatch timer;
	string name;
	long milisThreshold;
	PerfTimer(string name, long milisThreshold)
	{
		this.name = name;
		this.milisThreshold = milisThreshold;
		if (!OS.HasFeature("editor"))
			return;
		timer = Stopwatch.StartNew();
	}

	public static PerfTimer Start(string name, long milisThreshold = 0) => new(name, milisThreshold);

	public void Dispose()
	{
		if (timer is null)
			return;
		timer.Stop();
		if (timer.ElapsedMilliseconds >= milisThreshold)
			GD.Print($"PerfTimer \"{name}\" ran for {timer.Elapsed} ({timer.ElapsedMilliseconds} ms)");
	}
}