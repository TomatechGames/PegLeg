using Godot;

public partial class NotificationEntry : Control
{
    [Signal]
    public delegate void HeaderChangedEventHandler(string header);
    [Signal]
    public delegate void BodyChangedEventHandler(string body);
    [Signal]
    public delegate void IconChangedEventHandler(Texture2D icon);
    [Signal]
    public delegate void ColorChangedEventHandler(Color color);
    [Signal]
    public delegate void NotifOpenedEventHandler();
    [Signal]
    public delegate void NotifClosedEventHandler();

    [Export]
    bool autoBindVisibility = true;
    [Export]
    protected AudioStream fallbackSound;
    [Export]
    protected AudioStreamPlayer player;
    protected NotificationData currentData;
    public bool openState { get; private set; }

    public override void _Ready()
    {
        if (autoBindVisibility)
        {
            NotifOpened += () => Visible = true;
            NotifClosed += () => Visible = false;
        }
    }

    public virtual void SetNotifData(NotificationData notifData)
    {
        currentData = notifData;

        EmitSignal(SignalName.HeaderChanged, notifData.header);
        EmitSignal(SignalName.BodyChanged, notifData.body);
        EmitSignal(SignalName.IconChanged, notifData.icon);
        EmitSignal(SignalName.ColorChanged, notifData.color);
    }

    public virtual void Open()
    {
        openState = true;
        player.Stop();
        if (currentData.useSound)
        {
            player.Stream = currentData.sound ?? fallbackSound;
            player.Play();
        }
        EmitSignal(SignalName.NotifOpened);
    }

	public virtual void Press() =>
		currentData?.InvokeResponse();

    public virtual void Dismiss()
    {
        openState = false;
        EmitSignal(SignalName.NotifClosed);
    }
}
