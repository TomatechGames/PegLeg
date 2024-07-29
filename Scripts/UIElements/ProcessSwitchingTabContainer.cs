using Godot;

public partial class ProcessSwitchingTabContainer : TabContainer
{
    public override void _Ready()
    {
        TabChanged += UpdateProcessingTab;
        foreach (var child in GetChildren())
        {
            child.ProcessMode = ProcessModeEnum.Disabled;
        }
        UpdateProcessingTab(CurrentTab);
    }

    Node activeTab = null;
    private void UpdateProcessingTab(long tab)
    {
        var tabChild = GetChild((int)tab);
        if (activeTab is not null)
            activeTab.ProcessMode = ProcessModeEnum.Disabled;
        activeTab = tabChild;
        activeTab.ProcessMode = ProcessModeEnum.Inherit;
    }
}
