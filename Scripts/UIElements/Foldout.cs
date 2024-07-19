using Godot;
using System;

public partial class Foldout : Control
{
    [Signal]
    public delegate void NameChangedEventHandler(string name);
    [Signal]
    public delegate void NotificationVisibleEventHandler(bool visible);

    [Export]
	public Container elementContainer { get; private set; }
	[Export]
	Control foldoutInteractionPanel;
	[Export]
	Control foldoutTarget;
    [Export]
    Control foldoutChildParent;
    [Export]
	Control rotationTarget;
    [Export]
	Control notification;
    [Export]
	float openRotation;
    [Export]
    float closedRotation;
    [Export]
    float extraSpace = 4;
    [Export]
	float foldoutTime = 0.25f;

    public override void _Ready()
    {
		elementContainer.ItemRectChanged += RefreshExpandedHeight;
        ItemRectChanged += RefreshExpandedHeight;
		rotationTarget.Rotation = closedRotation;
		foldoutTarget.CustomMinimumSize = Vector2.Up * foldoutInteractionPanel.Size.Y;
		Size = Vector2.Zero;
		RefreshExpandedHeight();
    }
	float totalSize = 0; 
    private void RefreshExpandedHeight()
    {
		float newSize = foldoutInteractionPanel.Size.Y + extraSpace + elementContainer.Size.Y;
		bool hasChanged = newSize != totalSize;
        totalSize = newSize;
        if (currentFoldoutState && !(foldoutTween?.IsRunning() ?? false) && hasChanged)
            foldoutTarget.CustomMinimumSize = Vector2.Up * (newSize);
    }

	Tween foldoutTween = null;
    public void ToggleFoldoutState()=>
		SetFoldoutState(!currentFoldoutState);

    bool currentFoldoutState = false;
	public void SetFoldoutState(bool value)
	{
		if (currentFoldoutState == value)
			return;
		currentFoldoutState = value;
		if(foldoutTween?.IsRunning() ?? false)
			foldoutTween.Kill();
        foldoutTween = GetTree().CreateTween().SetParallel().SetTrans(Tween.TransitionType.Quint);

		//foldoutTween.SetEase(value ? Tween.EaseType.Out : Tween.EaseType.In);

        foldoutTween.TweenProperty(foldoutTarget, "custom_minimum_size:y", foldoutInteractionPanel.Size.Y + (value ? extraSpace + elementContainer.Size.Y : 0), foldoutTime);
		foldoutTween.TweenProperty(rotationTarget, "rotation",Mathf.DegToRad(value ? openRotation : closedRotation), foldoutTime);
    }

    public void SetName(string name) => EmitSignal(SignalName.NameChanged, name);
    public void SetNotification(bool visible) => EmitSignal(SignalName.NotificationVisible, visible);

	public void AddFoldoutChild(Node node) => foldoutChildParent.AddChild(node);
    public Godot.Collections.Array<Node> GetFoldoutChildren() => foldoutChildParent.GetChildren();
    public void RemoveFoldoutChild(Node node) => foldoutChildParent.RemoveChild(node);
}
