using Godot;
using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using ProfileItemPredicate = ProfileRequests.ProfileItemPredicate;

public partial class SurvivorSquadEntry : Control
{
    [Export]
    string synergy;

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

    public override void _Ready()
    {
        GD.Print($"{synergy} ({BanjoAssets.supplimentaryData.SquadNames.ContainsKey(synergy)})");
        squadNameLabel.Text = BanjoAssets.supplimentaryData.SquadNames[synergy];
        squadIcon.Texture = BanjoAssets.supplimentaryData.SquadIcons[synergy];
        fortPointsIcon.Texture = BanjoAssets.supplimentaryData.SquadFortIcons[synergy];
        fortPointsLabel.Text = "+???";

        SetupAsync();
    }

    bool fortStatIsDirty = false;

    async void SetupAsync()
    {
        if (!await LoginRequests.TryLogin())
            return;

        leadSurvivorSlot.SetPredicate(CreateSlotPredicate(0));
        leadSurvivorSlot.SetFilter(leaderFilter);
        leadSurvivorSlot.SetChangeAction(CreateSurvivorChangeAction(0));
        leadSurvivorSlot.OnSlottedItemChanged += handle =>
        {
            for (int i = 0; i < survivorSlots.Length; i++)
                survivorSlots[i].Relink();
            fortStatIsDirty = true;
        };
        for (int i = 0; i < survivorSlots.Length; i++)
        {
            survivorSlots[i].SetPredicate(CreateSlotPredicate(i + 1));
            survivorSlots[i].SetFilter(standardFilter);
            survivorSlots[i].SetChangeAction(CreateSurvivorChangeAction(i + 1));
            survivorSlots[i].OnSlottedItemChanged += handle => fortStatIsDirty = true;
        }

        UpdateFortStat();
    }

    public override void _Process(double delta)
    {
        if (fortStatIsDirty)
            UpdateFortStat();
    }

    bool isRecalculating = false;
    async void UpdateFortStat()
    {
        if (isRecalculating)
            return;
        isRecalculating = true;


        int summedValue = 0;

        summedValue += (int?)(await leadSurvivorSlot.GetSlottedItem())?.GetItemRating(true) ?? 0;

        for (int i = 0; i < survivorSlots.Length; i++)
        {
            summedValue += (int?)(await survivorSlots[i].GetSlottedItem())?.GetItemRating(true) ?? 0;
        }
        if (IsInstanceValid(fortPointsLabel))
            fortPointsLabel.Text = $"+{summedValue}";

        isRecalculating = false;
        fortStatIsDirty = false;
    }

    bool SurvivorMatchesSquad(KeyValuePair<string, JsonObject> kvp)
    {
        bool result = kvp.Value?["attributes"]?["squad_id"]?.ToString() == BanjoAssets.supplimentaryData.SynergyToSquadId[synergy];
        return result;
    }

    ProfileItemPredicate CreateSlotPredicate(int slotIndex) => kvp =>
        SurvivorMatchesSquad(kvp) &&
        (int)kvp.Value["attributes"]["squad_slot_idx"] == slotIndex;


    Action<ProfileItemHandle, ProfileItemHandle> CreateSurvivorChangeAction(int slotIndex) => async (fromHandle, toHandle) =>
    {
        JsonObject body = null;
        if (toHandle.isValid)
        {
            //set slotted survivor
            body = new()
            {
                ["characterId"] = toHandle.profileItem.uuid,
                ["squadId"] = BanjoAssets.supplimentaryData.SynergyToSquadId[synergy],
                ["slotIndex"] = slotIndex
            };
        }
        else if (fromHandle.isValid)
        {
            //unslot slotted survivor
            body = new()
            {
                ["characterId"] = fromHandle.profileItem.uuid,
                ["squadId"] = "",
                ["slotIndex"] = 0
            };
        }

        if(body is not null && await LoginRequests.TryLogin())
        {
            LoadingOverlay.Instance.AddLoadingKey("changingSurvivor");
            await ProfileRequests.PerformProfileOperationUnsafe(FnProfiles.AccountItems, "AssignWorkerToSquad", body.ToString());
            LoadingOverlay.Instance.RemoveLoadingKey("changingSurvivor");
        }
    };


    static readonly ProfileItemPredicate standardFilter = kvp =>
        kvp.Value?["templateId"]?.ToString()?.Split(":")?[0] == "Worker" &&
        kvp.Value?["attributes"]?["squad_id"] is null &&
        kvp.Value?.GetTemplate()?["SubType"] is null;

    static readonly ProfileItemPredicate leaderFilter = kvp =>
        kvp.Value?["templateId"]?.ToString()?.Split(":")?[0] == "Worker" &&
        kvp.Value?["attributes"]?["squad_id"] is null &&
        kvp.Value?.GetTemplate()?["SubType"] is not null;

}
