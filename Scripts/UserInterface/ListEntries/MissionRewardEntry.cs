using Godot;
using System;

public partial class MissionRewardEntry : Control, IRecyclableEntry
{
    [Export]
    MissionEntry missionEntry;
    [Export]
    GameItemEntry itemEntry;
    public Control node => this;

    IRecyclableElementProvider<MissionRewardPair> provider;
    public void SetRecyclableElementProvider(IRecyclableElementProvider provider)
    {
        if(provider is IRecyclableElementProvider<MissionRewardPair> rewardProvider)
            this.provider = rewardProvider;
    }

    public void SetRecycleIndex(int index)
    {
        if (provider is null)
            return;
        var pair = provider.GetRecycleElement(index);
        missionEntry.SetMission(pair.mission);
        pair.item.SetRewardNotification();
        itemEntry.SetItem(pair.item);
    }
}
