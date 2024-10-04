using Godot;
using System;

public partial class MiscSettings : Control
{
	[Export(PropertyHint.File, "*.tscn")]
	string loginSceneFilePath;

    public override void _Ready()
	{
        LoginRequests.OnLoginFailAlertPressed += ReturnToLogin;
    }

    void ForgetAuthDebug()
    {
        LoginRequests.DebugClearToken();
    }

    void ForgetRefreshDebug()
    {
        LoginRequests.DebugClearRefresh();
    }

    void SetInterfaceScale(float newInterfaceScale)
    {
        GetWindow().ContentScaleFactor = newInterfaceScale;
    }

    void ReturnToLogin()
    {
        GetTree().ChangeSceneToFile(loginSceneFilePath);
    }

    public override void _ExitTree()
    {
        LoginRequests.OnLoginFailAlertPressed -= ReturnToLogin;
    }
}
