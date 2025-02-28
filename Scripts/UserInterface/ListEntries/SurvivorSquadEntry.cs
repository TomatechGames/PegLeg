using Godot;
using System;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading;

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
    GameAccount overrideAccount;

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
        leadSurvivorSlot.SetSlotData(
                FnProfileTypes.AccountItems,
                "Worker",
                BanjoAssets.supplimentaryData.SynergyToSquadId[synergy],
                0,
                "HomebaseNode:questreward_" + slotRequirements[0].ToLower()
            );

        for (int i = 0; i < survivorSlots.Length; i++)
        {
            int slotIndex = i + 1;

            survivorSlots[i].OnItemChangeRequested += slot => HandleChangeRequest(slot, slotIndex);
            survivorSlots[i].OnSlotItemChanged += handle => statUpdateQueued = true;

            survivorSlots[i].SetSlotData(
                    FnProfileTypes.AccountItems,
                    "Worker",
                    BanjoAssets.supplimentaryData.SynergyToSquadId[synergy],
                    slotIndex,
                    "HomebaseNode:questreward_" + slotRequirements[slotIndex].ToLower()
                );
        }

        SetOverrideAccount();
        GameAccount.ActiveAccountChanged += OnActiveAccountChanged;
    }

    void OnActiveAccountChanged()
    {
        if (overrideAccount is null)
            UpdateAccount();
    }

    public void SetOverrideAccount(GameAccount account = null)
    {
        overrideAccount = account;
        UpdateAccount();
    }

    CancellationTokenSource accountChangeCts;
    public async void UpdateAccount()
    {
        //show loading icon?
        Visible = false;
        fortPointsLabel.Text = "+???";

        accountChangeCts.CancelAndRegenerate(out var ct);

        var account = overrideAccount ?? GameAccount.activeAccount;
        var newProfile = await account.GetProfile(FnProfileTypes.AccountItems).Query();
        if (newProfile is null || ct.IsCancellationRequested)
            return;

        bool hasAnySlot = slotRequirements.Distinct().Any(requirement =>
            newProfile.GetFirstTemplateItem("HomebaseNode:questreward_" + requirement.ToLower()) is not null
        );

        leadSurvivorSlot.SetOverrideAccount(overrideAccount);

        for (int i = 0; i < survivorSlots.Length; i++)
        {
            survivorSlots[i].SetOverrideAccount(overrideAccount);
        }

        if (hasAnySlot)
            Visible = true;

        UpdateFortStat();
    }


    public override void _Process(double delta)
    {
        if (statUpdateQueued)
            UpdateFortStat();
        statUpdateQueued = false;
    }

    void UpdateFortStat()
    {
        int summedValue = leadSurvivorSlot.slottedItem?.Rating ?? 0;
        summedValue += survivorSlots.Select(slot => slot.slottedItem?.Rating ?? 0).Sum();

        if (IsInstanceValid(fortPointsLabel))
            fortPointsLabel.Text = $"+{summedValue}";
    }

    static readonly Predicate<GameItem> standardFilter = item =>
        item.attributes?["squad_id"] is null &&
        item.template.SubType is null;

    static readonly Predicate<GameItem> leaderFilter = item =>
        item.attributes?["squad_id"] is null &&
        item.template.SubType is not null;

    async void HandleChangeRequest(InventoryItemSlot slot, int slotIndex)
    {
        var profile = slot.currentProfile;
        if (!(profile?.account.isOwned ?? false))
            return;

        var filter = slot == leadSurvivorSlot ? leaderFilter : standardFilter;
        var fromItem = slot.slottedItem;
        var squadID = BanjoAssets.supplimentaryData.SynergyToSquadId[synergy];

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
            using var _ = LoadingOverlay.CreateToken();
            await profile.PerformOperation("AssignWorkerToSquad", body.ToString());
            profile.account.GetFORTStats(true);
        }
    }
}
