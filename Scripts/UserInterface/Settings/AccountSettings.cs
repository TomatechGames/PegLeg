using Godot;
using System;

public partial class AccountSettings : Control
{
	[Export(PropertyHint.File, "*.tscn")]
	string loginSceneFilePath;
	[Export]
	Button forgetLoginButton;

    [Export]
    Button goToLoginButton;

    public override void _Ready()
	{
		VisibilityChanged += () =>
		{
			if(IsVisibleInTree())
				forgetLoginButton.Disabled = !LoginRequests.HasDeviceDetails;
		};
		forgetLoginButton.Pressed += () =>
		{
			LoginRequests.DeleteDeviceDetails();
			forgetLoginButton.Disabled = true;
		};
        goToLoginButton.Pressed += () =>
        {
			GetTree().ChangeSceneToFile(loginSceneFilePath);
        };

    }
}
