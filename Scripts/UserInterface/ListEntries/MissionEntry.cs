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
    [Signal]
    public delegate void TheaterCategoryChangedEventHandler(string theatreCat);

    [Export]
    bool controlModifierParentLayoutProps = true;
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
    Control highlightedRewardParent;

    [Export]
    Texture2D defaultBackground;

    FnMission currentMission = null;
    GameItemPredicate highlightedItemPredicate;

    public Control node => this;

    IRecyclableElementProvider<FnMission> missionProvider;
    public void SetRecyclableElementProvider(IRecyclableElementProvider provider)
    {
        if(provider is IRecyclableElementProvider<FnMission> newProvider)
            missionProvider = newProvider;
    }

    public void SetRecycleIndex(int index)
    {
        if (missionProvider is null)
            return;
        SetMissionData(missionProvider.GetRecycleElement(index));
    }

    public void SetMissionData(FnMission mission)
	{
        currentMission = mission;
        var missionGen = currentMission.missionGenerator;

        EmitSignal(SignalName.IconChanged, currentMission.missionGenerator.GetTexture(FnItemTextureType.Icon));
        EmitSignal(SignalName.VenturesIndicatorVisible, currentMission.theaterCat == "v");
        EmitSignal(SignalName.TheaterCategoryChanged, currentMission.theaterCat);
        EmitSignal(SignalName.PowerLevelChanged, currentMission.powerLevel.ToString());
        EmitSignal(SignalName.BackgroundChanged, currentMission.backgroundTexture ?? defaultBackground);
        EmitSignal(SignalName.NameChanged, missionGen.template.DisplayName);
        EmitSignal(SignalName.DescriptionChanged, missionGen.template.Description);
        EmitSignal(SignalName.LocationChanged, currentMission.zoneTheme.template.DisplayName);

        string eventFlag = currentMission.tileData["requirements"]["eventFlag"].ToString();
        bool hasEventFlag = !string.IsNullOrWhiteSpace(eventFlag);

        //TODO: if a mission has a quest requirement, mission entries should have the option of listing it

        EmitSignal(SignalName.TooltipChanged, CustomTooltip.GenerateSimpleTooltip(
            missionGen.template.DisplayName,
            null,
            new string[]
            {
                missionGen.template.Description,
                $"Located in: {currentMission.zoneTheme.template.DisplayName}" +
                (hasEventFlag ? $"\nEvent Flag: {eventFlag}":"")
            }
            ));

        if (currentMission.alertModifiers.Length>0)
        {
            if(alertModifierLayout is not null && alertModifierParent is not null)
            {
                for (int i = 0; i < alertModifierParent.GetChildCount(); i++)
                {
                    var alertChild = alertModifierParent.GetChild<Control>(i);
                    if (currentMission.alertModifiers.Length <= i)
                    {
                        alertChild.Visible = false;
                        alertChild.ProcessMode = ProcessModeEnum.Disabled;
                        continue;
                    }

                    alertChild.Visible = true;
                    alertChild.ProcessMode = ProcessModeEnum.Inherit;

                    if (alertChild is TextureRect textureChild)
                    {
                        var modifierTemplate = currentMission.alertModifiers[i].template;
                        textureChild.Texture = modifierTemplate.GetTexture();
                        textureChild.TooltipText = modifierTemplate.DisplayName;
                    }
                    else if(alertChild is GameItemEntry gameItemChild)
                    {
                        gameItemChild.SetItem(currentMission.alertModifiers[i]);
                    }
                }

                if (controlModifierParentLayoutProps)
                {
                    alertModifierParent.SizeFlagsHorizontal = currentMission.alertModifiers.Length == 5 ? SizeFlags.ShrinkCenter : SizeFlags.ExpandFill;
                    if (currentMission.alertModifiers.Length == 6)
                    {
                        var seventhChild = alertModifierParent.GetChild(6) as Control;
                        seventhChild.Visible = true;
                        seventhChild.SelfModulate = Colors.Transparent;
                    }
                }

                alertModifierLayout.Visible = true;
                alertModifierLayout.ProcessMode = ProcessModeEnum.Inherit;
            }
            if(alertRewardLayout is not null && alertRewardParent is not null)
            {
                alertRewardLayout.Visible = true;
                alertRewardLayout.ProcessMode = ProcessModeEnum.Inherit;
                ApplyItems(currentMission.alertRewardItems, alertRewardParent);
            }
        }
        else
        {
            if (alertModifierLayout is not null)
            {
                alertModifierLayout.Visible = false;
                alertModifierLayout.ProcessMode = ProcessModeEnum.Disabled;
            }
            if (alertRewardLayout is not null)
            {
                alertRewardLayout.Visible = false;
                alertRewardLayout.ProcessMode = ProcessModeEnum.Disabled;
            }
        }

        if (missionRewardParent is not null)
            ApplyItems(currentMission.rewardItems, missionRewardParent);

        UpdateHighlightedItems();
    }

    public void SetHighlightFilter(GameItemPredicate predicate)
    {
        highlightedItemPredicate = predicate;
        if (currentMission is not null)
            UpdateHighlightedItems();
    }

    void UpdateHighlightedItems()
    {
        if (highlightedRewardParent is not null && highlightedItemPredicate is not null)
        {
            ApplyItems(currentMission.allItems.Where(item=>highlightedItemPredicate(item)).ToArray(), missionRewardParent);
        }
    }

    static void ApplyItems(GameItem[] itemArray, Control parent)
    {
        for (int i = 0; i < parent.GetChildCount(); i++)
        {
            var controlChild = parent.GetChild<GameItemEntry>(i);
            if (itemArray.Length <= i)
            {
                controlChild.Visible = false;
                controlChild.ProcessMode = ProcessModeEnum.Disabled;
                continue;
            }
            controlChild.Visible = true;
            controlChild.ProcessMode = ProcessModeEnum.Inherit;

            bool isRewardBundle = itemArray[i].template.Name.ToLower().StartsWith("zcp_");
            controlChild.addXToAmount = isRewardBundle;
            controlChild.compactifyAmount = !isRewardBundle;
            //controlChild.includeAmountInName = !isRewardBundle;

            controlChild.SetItem(itemArray[i]);
            controlChild.SetRewardNotification();

            if (!isRewardBundle)
                controlChild.SetInteractableSmart();
            else
                controlChild.SetInteractable(false);
        }
    }
}
