using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using ProfileItemPredicate = ProfileRequests.ProfileItemPredicate;

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

    public override async void _Ready()
    {
        //GD.Print($"{synergy} ({BanjoAssets.supplimentaryData.SquadNames.ContainsKey(synergy)})");
        squadNameLabel.Text = BanjoAssets.supplimentaryData.SquadNames[synergy];
        squadIcon.Texture = BanjoAssets.supplimentaryData.SquadIcons[synergy];
        fortPointsIcon.Texture = BanjoAssets.supplimentaryData.SquadFortIcons[synergy];
        fortPointsLabel.Text = "+???";
        Visible = false;

        if (!await LoginRequests.TryLogin())
            return;
        await ProfileRequests.GetProfile(FnProfiles.AccountItems);
        List<Task> tasks = new();

        leadSurvivorSlot.OnItemChangeRequested += slot => HandleChangeRequest(slot, 0);
        leadSurvivorSlot.OnItemChanged += handle =>
        {
            for (int i = 0; i < survivorSlots.Length; i++)
                survivorSlots[i].Refresh();
            statUpdateQueued = true;
        };

        bool leaderSlotUnlocked = ProfileRequests.ProfileItemExistsUnsafe(FnProfiles.AccountItems, kvp => SlotNodeMatch(kvp, 0));
        if(leaderSlotUnlocked)
            tasks.Add(leadSurvivorSlot.SetPredicate(kvp => SurvivorMatch(kvp, 0)));
        bool hasAnySlot = leaderSlotUnlocked;

        for (int i = 0; i < survivorSlots.Length; i++)
        {
            int slotIndex = i + 1;

            survivorSlots[i].OnItemChangeRequested += slot => HandleChangeRequest(slot, slotIndex);
            survivorSlots[i].OnItemChanged += handle => statUpdateQueued = true;

            bool slotUnlocked = ProfileRequests.ProfileItemExistsUnsafe(FnProfiles.AccountItems, kvp => SlotNodeMatch(kvp, slotIndex));
            if (slotUnlocked)
                tasks.Add(survivorSlots[i].SetPredicate(kvp => SurvivorMatch(kvp, slotIndex)));
            hasAnySlot |= slotUnlocked;
        }

        if (hasAnySlot)
            Visible = true;

        await Task.WhenAll(tasks);

        UpdateFortStat();
    }

    bool SlotNodeMatch(KeyValuePair<string, JsonObject> kvp, int slotIndex) =>
        slotRequirements.Length > slotIndex &&
        kvp.Value?["templateId"]?.ToString() == "HomebaseNode:questreward_" + slotRequirements[slotIndex].ToLower();

    bool SurvivorMatch(KeyValuePair<string, JsonObject> kvp, int slotIndex) =>
        kvp.Value?["attributes"]?["squad_id"]?.ToString() == BanjoAssets.supplimentaryData.SynergyToSquadId[synergy] &&
        (int)kvp.Value["attributes"]["squad_slot_idx"] == slotIndex;

    public override void _Process(double delta)
    {
        if (statUpdateQueued)
            UpdateFortStat();
    }

    bool isRecalculating = false;
    async void UpdateFortStat()
    {
        if (isRecalculating)
            return;
        statUpdateQueued = false;
        isRecalculating = true;


        int summedValue = (int?)(leadSurvivorSlot.slottedItem is ProfileItemHandle slottedItem ? await slottedItem?.GetItem():null)?.GetItemRating() ?? 0;
        //since we've already awaited the lead survivor, we can safely use GetItemUnsafe knowing that the profile is cached
        summedValue += survivorSlots.Select(slot => (int?)(slot.slottedItem?.GetItemUnsafe())?.GetItemRating() ?? 0).Sum();

        //for (int i = 0; i < survivorSlots.Length; i++)
        //{
        //    summedValue += (int?)(await survivorSlots[i].slottedItem?.GetItem())?.GetItemRating(true) ?? 0;
        //}

        if (IsInstanceValid(fortPointsLabel))
            fortPointsLabel.Text = $"+{summedValue}";

        isRecalculating = false;
    }

    static readonly ProfileItemPredicate standardFilter = kvp =>
        kvp.Value?["templateId"]?.ToString()?.Split(":")?[0] == "Worker" &&
        kvp.Value?["attributes"]?["squad_id"] is null &&
        kvp.Value?.GetTemplate()?["SubType"] is null;

    static readonly ProfileItemPredicate leaderFilter = kvp =>
        kvp.Value?["templateId"]?.ToString()?.Split(":")?[0] == "Worker" &&
        kvp.Value?["attributes"]?["squad_id"] is null &&
        kvp.Value?.GetTemplate()?["SubType"] is not null;

    async void HandleChangeRequest(InventoryItemSlot slot, int slotIndex)
    {

        var filter = slot == leadSurvivorSlot ? leaderFilter : standardFilter;
        var fromHandle = slot.slottedItem;
        var squadID = BanjoAssets.supplimentaryData.SynergyToSquadId[synergy];
        GameItemSelector.Instance.titleText = "Select a Survivor";
        GameItemSelector.Instance.overrideSurvivorSquad = squadID;
        GameItemSelector.Instance.allowEmptySelection = true;
        var selectedHandles = await GameItemSelector.Instance.OpenSelector(filter);

        //occurs when cancelled
        if (selectedHandles is null)
            return;

        var toHandle = selectedHandles.FirstOrDefault();
        JsonObject body = null;
        if (toHandle?.isValid ?? false)
        {
            //set slotted survivor
            body = new()
            {
                ["characterId"] = toHandle.itemID.uuid,
                ["squadId"] = squadID,
                ["slotIndex"] = slotIndex
            };
        }
        else if (fromHandle?.isValid ?? false)
        {
            //unslot slotted survivor
            body = new()
            {
                ["characterId"] = fromHandle.itemID.uuid,
                ["squadId"] = "",
                ["slotIndex"] = 0
            };
        }

        if (body is not null && await LoginRequests.TryLogin())
        {
            LoadingOverlay.AddLoadingKey("changingSurvivor");
            await ProfileRequests.PerformProfileOperationUnsafe(FnProfiles.AccountItems, "AssignWorkerToSquad", body.ToString());
            LoadingOverlay.RemoveLoadingKey("changingSurvivor");
        }
    }
}
