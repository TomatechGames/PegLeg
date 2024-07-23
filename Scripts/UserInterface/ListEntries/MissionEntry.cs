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

    public void SetMissionData(MissionData missionData)
	{
        //apply data to element
        var generator = missionData.missionJson["missionGenerator"].AsObject();
        var tile = missionData.missionJson["tile"].AsObject();
        var zoneTheme = tile["zoneTheme"].AsObject();

        EmitSignal(SignalName.IconChanged, generator.GetItemTexture(BanjoAssets.TextureType.Icon));
        EmitSignal(SignalName.VenturesIndicatorVisible, missionData.theaterCat == "v");
        EmitSignal(SignalName.PowerLevelChanged, missionData.powerLevel.ToString());

        if (generator.GetItemTexture(null, BanjoAssets.TextureType.LoadingScreen) is Texture2D missionLoadingScreen)
            EmitSignal(SignalName.BackgroundChanged, missionLoadingScreen);
        else if(zoneTheme.GetItemTexture(null, BanjoAssets.TextureType.LoadingScreen) is Texture2D zoneLoadingScreen)
            EmitSignal(SignalName.BackgroundChanged, zoneLoadingScreen);
        else
            EmitSignal(SignalName.BackgroundChanged, defaultBackground);

        EmitSignal(SignalName.NameChanged, generator["DisplayName"].ToString());
        EmitSignal(SignalName.DescriptionChanged, generator["Description"].ToString());
        EmitSignal(SignalName.LocationChanged, tile["zoneTheme"]["DisplayName"].ToString());

        EmitSignal(SignalName.TooltipChanged, $"{generator["DisplayName"]}\n{generator["Description"]}\nLocated in: {tile["zoneTheme"]["DisplayName"]}");

        ApplyItems(missionData.missionJson["missionRewards"].AsArray(), missionRewardParent);

        if (missionData.missionJson["missionAlert"] is JsonObject missionAlert)
        {
            var alertModifiers = missionAlert["modifiers"].AsArray();
            for (int i = 0; i < alertModifierParent.GetChildCount(); i++)
            {
                var controlChild = alertModifierParent.GetChild<TextureRect>(i);
                if (alertModifiers.Count <= i)
                {
                    controlChild.Visible = false;
                    continue;
                }
                controlChild.Visible = true;
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

            ApplyItems(missionAlert["rewards"].AsArray(), alertRewardParent);

            alertRewardLayout.Visible = true;
            alertModifierLayout.Visible = true;
        }
        else
        {
            alertRewardLayout.Visible = false;
            alertModifierLayout.Visible = false;
        }
    }

    static void ApplyItems(JsonArray itemArray, Control parent)
    {
        for (int i = 0; i < parent.GetChildCount(); i++)
        {
            var controlChild = parent.GetChild<GameItemEntry>(i);
            if (itemArray.Count <= i)
            {
                controlChild.Visible = false;
                continue;
            }
            controlChild.Visible = true;

            bool isRewardBundle = itemArray[i].GetTemplate()["Name"].ToString().ToLower().StartsWith("zcp_");
            controlChild.addXToAmount = isRewardBundle;
            controlChild.compactifyAmount = !isRewardBundle;
            controlChild.includeAmountInName = !isRewardBundle;

            controlChild.SetItemData(itemArray[i].AsObject());

            if (!isRewardBundle)
                controlChild.SetInteractableSmart();
            else
                controlChild.SetInteractable(false);
        }
    }
}
