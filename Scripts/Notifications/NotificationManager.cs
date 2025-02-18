using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class NotificationManager : Control
{
	[Export]
	PackedScene notificationScene;
	[Export]
	Control notifParent;
    [Export]
    Label queueLabel;
    [Export]
	int notificationPoolAmount = 3;
	[Export]
	float cooldown = 0.1f;
	NotificationEntry[] notificationInstances;

    static Queue<NotificationData> notificationQueue = new();
    static List<NotificationData> notificationHistory = new();

    static NotificationManager instance;

	public static void PushNotification(NotificationData data)
	{
		notificationQueue.Enqueue(data);
        notificationHistory.Add(data);

        if (instance is null)
            return;
        instance.UpdateLabel();
        if (!instance.isShowing)
            instance.ShowNotification();
	}

	public override void _Ready()
	{
		instance = this;
		var possibleNotifInstances = notifParent.GetChildren()
			.Select(n => n is Control c ? c : null)
			.Where(c => c is not null)
			.ToArray();
		List<NotificationEntry> pool = new();
        for (int i = 0; i < possibleNotifInstances.Length; i++)
        {
            possibleNotifInstances[i].Visible = false;
			if (pool.Count > notificationPoolAmount || possibleNotifInstances[i] is not NotificationEntry currentEntry)
			{
				continue;
			}
			pool.Add(currentEntry);
        }
        for (int i = pool.Count; i < notificationPoolAmount; i++)
        {
			var newEntry = notificationScene.Instantiate<NotificationEntry>();
            notifParent.AddChild(newEntry);
			newEntry.Visible = false;
            pool.Add(newEntry);
        }
		notificationInstances = pool.ToArray();
    }

	bool isShowing = false;
	async void ShowNotification()
    {
        isShowing = true;

		NotificationEntry nextEntry = notificationInstances.FirstOrDefault(inst => !inst.openState);

        while (nextEntry is null)
		{
			await Helpers.WaitForTimer(cooldown);
            if (!IsInsideTree())
                return;
            nextEntry = notificationInstances.FirstOrDefault(inst => !inst.openState);
        }

		if (!nextEntry.Visible)
		{
            notifParent.MoveChild(nextEntry, 0);
			nextEntry.Visible = true;
        }

		var notif = notificationQueue.Dequeue();
        UpdateLabel();
        nextEntry.SetNotifData(notif);
        await Helpers.WaitForFrame();
        await Helpers.WaitForFrame();
        if (!IsInsideTree())
            return;
        nextEntry.Open();
        UpdateLabel();

        var window = GetWindow();
        if (!window.HasFocus() || window.Mode == Window.ModeEnum.Minimized)
            window.RequestAttention();

        await Helpers.WaitForTimer(cooldown);
		if (!IsInsideTree())
			return;

		if (notificationQueue.Count == 0)
		{
			isShowing = false;
			return;
		}
		ShowNotification();

	}

	void UpdateLabel()
	{
        if (notificationQueue.Count > 0 && !notificationInstances.Any(inst => !inst.openState))
        {
            queueLabel.Visible = true;
            queueLabel.Text = $"+{notificationQueue.Count}";
        }
        else
            queueLabel.Visible = false;
        if (notificationQueue.Count == 0 || isShowing)
            return;
    }

    public override void _ExitTree()
    {
		instance = null;
    }
}

public class NotificationData
{
    public string header { get; init; }
    public string body { get; init; }
    public Texture2D icon { get; init; }
    public Color color { get; init; }
	public AudioStream sound { get; init; }
    public bool useSound { get; init; } = true;
    public event Action response;
    bool hasResponded = false;
    public void InvokeResponse()
    {
        if (hasResponded)
            return;
        response?.Invoke();
        hasResponded = true;
    }
}
