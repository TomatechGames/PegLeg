using Godot;
using System;
using System.Collections.Generic;

public partial class LoadingOverlay : ModalWindow
{
    public static LoadingOverlay Instance { get; private set; }

    public override void _Ready()
    {
        base._Ready();
        Instance = this;
    }

    List<string> loadingKeys = new();
    public void AddLoadingKey(string key)
    {
        loadingKeys.Add(key);
        SetWindowOpen(loadingKeys.Count>0);
    }

    public void RemoveLoadingKey(string key)
    {
        loadingKeys.Remove(key);
        SetWindowOpen(loadingKeys.Count > 0);
    }
}
