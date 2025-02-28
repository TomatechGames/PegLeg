using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class LoadingOverlay : ModalWindow
{
    [Signal]
    public delegate void ProgressChangedEventHandler(float totalProgress);

    static LoadingOverlay instance;
    static Dictionary<Guid, LoadingOverlayToken> loadingTokens = new();
    [Export]
    RichTextLabel progressLabel;

    public override void _Ready()
    {
        base._Ready();
        instance = this;
        bool complete = !loadingTokens.Any(t => !t.Value.disposed);
        SetWindowOpen(!complete);
    }

    static void UpdateLoadingState()
    {
        bool complete = !loadingTokens.Any(t => !t.Value.disposed);
        //GD.PushWarning("tokens complete: " + complete);
        if (complete == instance?.IsOpen)
            instance?.SetWindowOpen(!complete);
        if (complete)
        {
            loadingTokens.Clear();
            return;
        }
        //GD.PushWarning("tokens: " + loadingTokens.Count);
        instance?.UpdateLoadingProgress();
    }

    void UpdateLoadingProgress()
    {
        float totalProgress = loadingTokens.Select(t => t.Value.progress).Sum();
        float totalMaxProgress = loadingTokens.Select(t => t.Value.maxProgress).Sum();
        float progressPercent = totalProgress / totalMaxProgress;
        EmitSignal(SignalName.ProgressChanged, progressPercent);
        if (progressLabel is not null)
            progressLabel.Text = string.Join("\n", loadingTokens.Where(t => !t.Value.disposed).Select(t => t.Value.ProgressText));
    }

    public static LoadingOverlayToken CreateToken(string taskName = null, float initialProgress = 0, float maxProgress = 1) =>
        new(taskName, initialProgress, maxProgress);

    public struct LoadingOverlayToken : IDisposable
    {
        Guid guid = Guid.NewGuid();
        public bool disposed { get; private set;} = false;
        public string taskName { get; private set; }
        public float progress { get; private set; }
        public float maxProgress { get; private set; }

        public string ProgressText => taskName + (maxProgress > 0 ? $"({progress}/{maxProgress})" : "");

        public LoadingOverlayToken(string taskName = null, float initialProgress = 0, float maxProgress = 1)
        {
            this.taskName = taskName;
            progress = initialProgress;
            this.maxProgress = Mathf.Max(maxProgress, 0);

            //GD.PushWarning($"adding token \"{taskName}\" ({guid})");
            loadingTokens.Add(guid, this);
            UpdateLoadingState();
        }

        public void SetLoadingProgress(float newProgress)=>
            SetLoadingProgress(newProgress, maxProgress);
        public void SetLoadingProgress(float newProgress, float maxProgress)
        {
            if (disposed)
                return;
            this.maxProgress = Mathf.Max(maxProgress, 0);
            progress = Mathf.Clamp(newProgress, 0, maxProgress);
        }

        public void Dispose()
        {
            if (disposed)
                return;
            //GD.PushWarning($"disposing token \"{taskName}\" ({guid})");
            progress = maxProgress;
            if (loadingTokens.ContainsKey(guid))
            {
                disposed = true;
                //GD.PushWarning($"removing token \"{taskName}\" ({guid})");
                loadingTokens.Remove(guid);
                UpdateLoadingState();
            }
        }
    }
}
