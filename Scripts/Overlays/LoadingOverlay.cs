using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class LoadingOverlay : ModalWindow
{
    [Signal]
    public delegate void ProgressChangedEventHandler(float totalProgress);
    public static LoadingOverlay Instance { get; private set; }

    public override void _Ready()
    {
        base._Ready();
        Instance = this;
    }

    static Dictionary<string, float> loadingKeys = new();

    public static void AddLoadingKey(string key)
    {
        if (!loadingKeys.ContainsKey(key))
            loadingKeys.Add(key, 0);
        SetProgress(key, 0);
        Instance.SetWindowOpen(true);
    }
    public static void SetProgress(string key, float value)
    {
        if (!loadingKeys.ContainsKey(key))
            return;
        loadingKeys[key] = value;
        float total = loadingKeys.Values.Sum() / loadingKeys.Count;
        Instance.EmitSignal(SignalName.ProgressChanged, total);
    }

    public static void RemoveLoadingKey(string key)
    {
        if (!loadingKeys.ContainsKey(key))
            return;
        loadingKeys[key] = 1;
        float total = loadingKeys.Count == 0 ? 1 : loadingKeys.Values.Sum() / loadingKeys.Count;
        if (Mathf.RoundToInt(total*100)==100)
        {
            loadingKeys.Clear();
            Instance.SetWindowOpen(false);
        }
    }
}
