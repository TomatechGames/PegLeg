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

    static List<string> loadingKeys = new();
    //the "loading key" system was originally intended for if multiple systems needed
    //to show a loading screen simultaniously, but that use case never arose
    public static void AddLoadingKey(string key)
    {
        loadingKeys.Add(key);
        Instance.SetWindowOpen(loadingKeys.Count > 0);
    }

    public static void RemoveLoadingKey(string key)
    {
        loadingKeys.Remove(key);
        Instance.SetWindowOpen(loadingKeys.Count > 0);
    }
}
