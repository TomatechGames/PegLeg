using Godot;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

public partial class MissionEntry : Control, IRecyclableEntry
{
    [Signal]
    public delegate void NameChangedEventHandler(string name);
    [Signal]
    public delegate void DescriptionChangedEventHandler(string description);
    [Signal]
    public delegate void LocationChangedEventHandler(string location);
    [Signal]
    public delegate void PowerLevelChangedEventHandler(string powerLevel);
    [Signal]
    public delegate void TooltipChangedEventHandler(string tooltip);
    [Signal]
    public delegate void IconChangedEventHandler(Texture2D icon);
    [Signal]
    public delegate void BackgroundChangedEventHandler(Texture2D background);
    [Signal]
    public delegate void VenturesIndicatorVisibleEventHandler(bool visible);

    [Export]
    Control alertModifierLayout;
    [Export]
    Control alertModifierParent;

    [Export]
    Control missionRewardParent;
    [Export]
    Control alertRewardLayout;
    [Export]
    Control alertRewardParent;
    [Export]
    Texture2D defaultBackground;

    public Control node => this;

    IRecyclableElementProvider<MissionData> missionProvider;
    public void SetRecyclableElementProvider(IRecyclableElementProvider provider)
    {
        if(provider is IRecyclableElementProvider<MissionData> newProvider)
            missionProvider = newProvider;
    }

    public void SetRecycleIndex(int index)
    {
        if (missionProvider is null)
            return;
        SetMissionData(missionProvider.GetRecycleElement(index));
    }

    MissionData currentMissionData = null;
    public void SetMissionData(MissionData missionData_)
	{
        currentMissionData = missionData_;
        //apply data to element
        var generator = currentMissionData.missionJson["missionGenerator"].AsObject();
        var tile = currentMissionData.missionJson["tile"].AsObject();
        var zoneTheme = tile["zoneTheme"].AsObject();

        EmitSignal(SignalName.IconChanged, generator.GetItemTexture(BanjoAssets.TextureType.Icon));
        EmitSignal(SignalName.VenturesIndicatorVisible, currentMissionData.theaterCat == "v");
        EmitSignal(SignalName.PowerLevelChanged, currentMissionData.powerLevel.ToString());

        if (generator.GetItemTexture(null, BanjoAssets.TextureType.LoadingScreen) is Texture2D missionLoadingScreen)
            EmitSignal(SignalName.BackgroundChanged, missionLoadingScreen);
        else if(zoneTheme.GetItemTexture(null, BanjoAssets.TextureType.LoadingScreen) is Texture2D zoneLoadingScreen)
            EmitSignal(SignalName.BackgroundChanged, zoneLoadingScreen);
        else
            EmitSignal(SignalName.BackgroundChanged, defaultBackground);

        EmitSignal(SignalName.NameChanged, generator["DisplayName"].ToString());
        EmitSignal(SignalName.DescriptionChanged, generator["Description"].ToString());
        EmitSignal(SignalName.LocationChanged, tile["zoneTheme"]["DisplayName"].ToString());

        //$"{generator["DisplayName"]}\n{generator["Description"]}\nLocated in: {tile["zoneTheme"]["DisplayName"]}"
        EmitSignal(SignalName.TooltipChanged, CustomTooltip.GenerateSimpleTooltip(
            generator["DisplayName"].ToString(),
            null,
            new string[]
            {
                generator["Description"].ToString(),
                $"Located in: {tile["zoneTheme"]["DisplayName"]}\n" +
                $"Test: {tile["requirements"]["eventFlag"]}"
            }
            ));

        if (currentMissionData.missionJson["missionAlert"] is JsonObject missionAlert)
        {
            var alertModifiers = missionAlert["modifiers"].AsArray();
            for (int i = 0; i < alertModifierParent.GetChildCount(); i++)
            {
                var controlChild = alertModifierParent.GetChild<TextureRect>(i);
                if (alertModifiers.Count <= i)
                {
                    controlChild.Visible = false;
                    controlChild.ProcessMode = ProcessModeEnum.Disabled;
                    continue;
                }
                controlChild.Visible = true;
                controlChild.ProcessMode = ProcessModeEnum.Inherit;
                var itemData = alertModifiers[i].GetTemplate();

                controlChild.Texture = itemData.GetItemTexture();
                controlChild.TooltipText = itemData["DisplayName"].ToString();
            }
            alertModifierParent.SizeFlagsHorizontal = alertModifiers.Count == 5 ? SizeFlags.ShrinkCenter : SizeFlags.ExpandFill;
            if (alertModifiers.Count == 6)
            {
                var seventhChild = alertModifierParent.GetChild(6) as Control;
                seventhChild.Visible = true;
                seventhChild.SelfModulate = Colors.Transparent;
            }

            alertRewardLayout.Visible = true;
            alertRewardLayout.ProcessMode = ProcessModeEnum.Inherit;
            alertModifierLayout.Visible = true;
            alertModifierLayout.ProcessMode = ProcessModeEnum.Inherit;

            ApplyItems(missionAlert["rewards"].AsArray(), alertRewardParent);
        }
        else
        {
            alertRewardLayout.Visible = false;
            alertRewardLayout.ProcessMode = ProcessModeEnum.Disabled;
            alertModifierLayout.Visible = false;
            alertModifierLayout.ProcessMode = ProcessModeEnum.Disabled;
        }

        ApplyItems(currentMissionData.missionJson["missionRewards"].AsArray(), missionRewardParent);
    }

    static void ApplyItems(JsonArray itemArray, Control parent)
    {
        for (int i = 0; i < parent.GetChildCount(); i++)
        {
            var controlChild = parent.GetChild<GameItemEntry>(i);
            if (itemArray.Count <= i)
            {
                controlChild.Visible = false;
                controlChild.ProcessMode = ProcessModeEnum.Disabled;
                continue;
            }
            controlChild.Visible = true;
            controlChild.ProcessMode = ProcessModeEnum.Inherit;

            bool isRewardBundle = itemArray[i].GetTemplate()["Name"].ToString().ToLower().StartsWith("zcp_");
            controlChild.addXToAmount = isRewardBundle;
            controlChild.compactifyAmount = !isRewardBundle;
            //controlChild.includeAmountInName = !isRewardBundle;

            controlChild.SetItemData(itemArray[i].AsObject());
            controlChild.SetRewardNotification();

            if (!isRewardBundle)
                controlChild.SetInteractableSmart();
            else
                controlChild.SetInteractable(false);
        }
    }
}
