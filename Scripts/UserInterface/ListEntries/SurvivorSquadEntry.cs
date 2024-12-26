using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

public partial class SurvivorSquadEntry : Control
{
    [Export]
    string synergy;

    [Export(PropertyHint.ArrayType)]
    string[] slotRequirements;

    [ExportGroup("References")]
    [Export]
    Label squadNameLabel;

    [Export]
    TextureRect squadIcon;

    [Export]
    Label fortPointsLabel;

    [Export]
    TextureRect fortPointsIcon;

    [Export]
    InventoryItemSlot leadSurvivorSlot;

    [Export(PropertyHint.ArrayType)]
    InventoryItemSlot[] survivorSlots;

    bool statUpdateQueued = false;
    GameProfile targetProfile;

    public override void _Ready()
    {
        //GD.Print($"{synergy} ({BanjoAssets.supplimentaryData.SquadNames.ContainsKey(synergy)})");
        squadNameLabel.Text = BanjoAssets.supplimentaryData.SquadNames[synergy];
        squadIcon.Texture = BanjoAssets.supplimentaryData.SquadIcons[synergy];
        fortPointsIcon.Texture = BanjoAssets.supplimentaryData.SquadFortIcons[synergy];

        leadSurvivorSlot.OnItemChangeRequested += slot => HandleChangeRequest(slot, 0);
        leadSurvivorSlot.OnSlotItemChanged += _ =>
        {
            for (int i = 0; i < survivorSlots.Length; i++)
                survivorSlots[i].UpdateItem();
            statUpdateQueued = true;
        };

        for (int i = 0; i < survivorSlots.Length; i++)
        {
            int slotIndex = i + 1;

            survivorSlots[i].OnItemChangeRequested += slot => HandleChangeRequest(slot, slotIndex);
            survivorSlots[i].OnSlotItemChanged += handle => statUpdateQueued = true;
        }

        SetAccount(GameAccount.activeAccount);
    }

    CancellationTokenSource accountChangeCts;
    async void SetAccount(GameAccount account)
    {
        //show loading icon?
        Visible = false;
        fortPointsLabel.Text = "+???";
        targetProfile = null;

        accountChangeCts?.Cancel();
        accountChangeCts = new();
        var ct = accountChangeCts.Token;

        if (!await account.Authenticate() || ct.IsCancellationRequested)
            return;

        var newProfile = await account.GetProfile(FnProfileTypes.AccountItems).Query();
        if (ct.IsCancellationRequested)
            return;

        List<Task> tasks = new();

        bool leaderSlotUnlocked = newProfile.GetTemplateItems("HomebaseNode:questreward_" + slotRequirements[0].ToLower()).Length > 0;
        if (leaderSlotUnlocked)
            tasks.Add(leadSurvivorSlot.SetSlotData(FnProfileTypes.AccountItems, "Worker", item => SurvivorMatch(item, 0)));
        bool hasAnySlot = leaderSlotUnlocked;

        for (int i = 0; i < survivorSlots.Length; i++)
        {
            int slotIndex = i + 1;

            bool slotUnlocked = newProfile.GetTemplateItems("HomebaseNode:questreward_" + slotRequirements[slotIndex].ToLower()).Length > 0;
            if (slotUnlocked)
                tasks.Add(survivorSlots[i].SetSlotData(FnProfileTypes.AccountItems, "Worker", kvp => SurvivorMatch(kvp, slotIndex)));
            hasAnySlot |= slotUnlocked;
        }


        await Task.WhenAll(tasks);
        if (ct.IsCancellationRequested)
            return;

        targetProfile = newProfile;

        if (hasAnySlot)
            Visible = true;

        UpdateFortStat();
    }

    bool SurvivorMatch(GameItem item, int slotIndex) =>
        item.attributes?["squad_id"]?.ToString() == BanjoAssets.supplimentaryData.SynergyToSquadId[synergy] &&
        (int)item.attributes["squad_slot_idx"] == slotIndex;

    public override void _Process(double delta)
    {
        if (statUpdateQueued)
            UpdateFortStat();
    }

    void UpdateFortStat()
    {
        int summedValue = leadSurvivorSlot.slottedItem?.Rating ?? 0;
        summedValue += survivorSlots.Select(slot => slot.slottedItem?.quantity ?? 0).Sum();

        if (IsInstanceValid(fortPointsLabel))
            fortPointsLabel.Text = $"+{summedValue}";
    }

    static readonly GameItemPredicate standardFilter = item =>
        item.attributes?["squad_id"] is null &&
        item.template.SubType is null;

    static readonly GameItemPredicate leaderFilter = item =>
        item.attributes?["squad_id"] is null &&
        item.template.SubType is not null;

    async void HandleChangeRequest(InventoryItemSlot slot, int slotIndex)
    {
        var filter = slot == leadSurvivorSlot ? leaderFilter : standardFilter;
        var fromItem = slot.slottedItem;
        var squadID = BanjoAssets.supplimentaryData.SynergyToSquadId[synergy];


        GameProfile profile = null;
        if (profile is null)
            return;
        await profile.Query();

        GameItemSelector.Instance.RestoreDefaults();
        GameItemSelector.Instance.titleText = "Select a Survivor";
        GameItemSelector.Instance.overrideSurvivorSquad = squadID;
        GameItemSelector.Instance.allowEmptySelection = true;
        var selectedHandles = await GameItemSelector.Instance.OpenSelector(profile.GetItems("Worker", filter));

        //occurs when cancelled
        if (selectedHandles is null)
            return;

        var toHandle = selectedHandles.FirstOrDefault();
        JsonObject body = null;
        if (toHandle?.profile is not null)
        {
            //set slotted survivor
            body = new()
            {
                ["characterId"] = toHandle.uuid,
                ["squadId"] = squadID,
                ["slotIndex"] = slotIndex
            };
        }
        else if (fromItem?.profile is not null)
        {
            //unslot slotted survivor
            body = new()
            {
                ["characterId"] = fromItem.uuid,
                ["squadId"] = "",
                ["slotIndex"] = 0
            };
        }

        if (body is not null && await profile?.account.Authenticate() && profile.profileId == FnProfileTypes.AccountItems)
        {
            LoadingOverlay.AddLoadingKey("changingSurvivor");
            await profile.PerformOperation("AssignWorkerToSquad", body.ToString());
            LoadingOverlay.RemoveLoadingKey("changingSurvivor");
        }
    }
}
