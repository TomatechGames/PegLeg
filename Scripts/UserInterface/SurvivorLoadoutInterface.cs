using Godot;
using System;
using System.Linq;
using System.Text.Json.Nodes;

public partial class SurvivorLoadoutInterface : Node
{
	[Export]
	OptionButton loadoutSelector;
	[Export]
	Button saveLoadoutButton;
    [Export]
    Button loadLoadoutButton;
    [Export]
    Button renameLoadoutButton;
    [Export]
    Button deleteLoadoutButton;
    [Export]
    Button clearSquadButton;
    [Export]
    string survivorLoadoutFilePath = "user://survivorLoadouts.json";
    JsonObject loadouts;

    public override void _Ready()
	{
        if (FileAccess.FileExists(survivorLoadoutFilePath))
        {
            using var survivorLoadoutFile = Godot.FileAccess.Open(survivorLoadoutFilePath, Godot.FileAccess.ModeFlags.Read);
            loadouts = JsonNode.Parse(survivorLoadoutFile.GetAsText()).AsObject();
        }
        loadouts ??= new();
        GenerateOptions();

        loadoutSelector.ItemSelected += OnLoadoutChanged;
        saveLoadoutButton.Pressed += OnLoadoutSave;
        loadLoadoutButton.Pressed += OnLoadoutLoad;
        renameLoadoutButton.Pressed += OnLoadoutRename;
        deleteLoadoutButton.Pressed += OnLoadoutDelete;
        clearSquadButton.Pressed += OnSquadClear;
        loadoutSelector.Selected = 0;
        saveLoadoutButton.Disabled = false;
        loadLoadoutButton.Disabled = true;
        renameLoadoutButton.Disabled = true;
        deleteLoadoutButton.Disabled = true;
    }

    public override void _Process(double delta)
    {
        eggTimer -= (float)(delta / Engine.TimeScale);
    }

    string loadoutName = null;

    private void OnLoadoutChanged(long index)
    {
        saveLoadoutButton.Disabled = true;
        loadLoadoutButton.Disabled = true;
        renameLoadoutButton.Disabled = true;
        deleteLoadoutButton.Disabled = true;
        if (index == 0)
        {
            saveLoadoutButton.Disabled = false;
            loadoutName = null;
        }
        else if (index >= 2)
        {
            saveLoadoutButton.Disabled = false;
            loadLoadoutButton.Disabled = false;
            renameLoadoutButton.Disabled = false;
            deleteLoadoutButton.Disabled = false;
            loadoutName = loadoutSelector.GetItemText((int)index);
        }
    }

    private async void OnLoadoutSave()
    {
        if(loadoutName is not null)
        {
            if (!(await GenericConfirmationWindow.OpenConfirmation(
                    "Overwrite Loadout?", 
                    "Overwrite", 
                    contextText: "The contents of the selected loadout will be overwritten"
                ) ?? false))
                return;
        }

        loadoutName ??= await GenericLineEditWindow.OpenLineEdit("Enter Survivor Loadout Name", allowCancel:true, validator: val =>
        {
            if (loadouts.ContainsKey(val))
                return "A survivor loadout with that name already exists";
            return string.IsNullOrWhiteSpace(val) ? "" : null;
        });

        if (loadoutName is null)
            return;

        var account = GameAccount.activeAccount;
        if (!await account.Authenticate())
            return;

        var allWorkers = (await account.GetProfile(FnProfileTypes.AccountItems).Query())
            .GetItems("Worker", item => item.attributes.ContainsKey("squad_id"));

        var groupedWorkers = allWorkers.GroupBy(item => item.attributes["squad_id"].ToString());

        JsonObject squadsMappings = new();
        foreach (var workers in groupedWorkers)
        {
            var squadArray = new JsonArray();
            for (int i = 0; i < 8; i++)
            {
                squadArray.Add("");
            }
            foreach (var item in workers)
            {
                squadArray[item.attributes["squad_slot_idx"].GetValue<int>()] = item.uuid;
            }
            squadsMappings[workers.Key] = squadArray;
        }

        loadouts[account.accountId] ??= new JsonObject();
        loadouts[account.accountId][loadoutName] = squadsMappings;

        ApplyLoadoutFileChange();
        loadoutSelector.Selected = loadouts.Select(kvp => kvp.Key).ToList().IndexOf(loadoutName) + 2;
        OnLoadoutChanged(loadoutSelector.Selected);
    }

    float eggTimer = 0;
    private async void OnLoadoutLoad()
    {
        if (eggTimer > 0)
            return;
        if (!(await GenericConfirmationWindow.OpenConfirmation(
                "Apply Loadout?", 
                "Apply", 
                contextText:"Survivors in this loadout will be slotted into their squads", 
                warningText: "Currently slotted survivors will be unslotted"
            ) ?? false))
            return;
        eggTimer = 3;

        try
        {
            LoadingOverlay.AddLoadingKey("applySurvivorLoadout");
            var account = GameAccount.activeAccount;
            if (!await account.Authenticate())
                return;

            var accountItems = await account.GetProfile(FnProfileTypes.AccountItems).Query();

            var existingWorkers = accountItems.GetItems("Worker", item => item.attributes.ContainsKey("squad_id"));

            JsonObject fullLoadout = loadouts[loadoutName].AsObject();
            JsonObject flattenedLoadout = new()
            {
                ["characterIds"] = new JsonArray(),
                ["squadIds"] = new JsonArray(),
                ["slotIndices"] = new JsonArray(),
            };
            foreach (var squad in fullLoadout)
            {
                var squadArray = squad.Value.AsArray();
                for (int i = 0; i < squadArray.Count; i++)
                {
                    string workerKey = squadArray[i].ToString();

                    if (workerKey == "")
                    {
                        continue;
                    }

                    //if (
                    //    existingWorkers.ContainsKey(workerKey) &&
                    //    existingWorkers[workerKey]["attributes"]["squad_id"].ToString() == squad.Key &&
                    //    existingWorkers[workerKey]["attributes"]["squad_slot_idx"].GetValue<int>() == i
                    //    )
                    //    continue;

                    flattenedLoadout["characterIds"].AsArray().Add(workerKey);
                    flattenedLoadout["squadIds"].AsArray().Add(squad.Key);
                    flattenedLoadout["slotIndices"].AsArray().Add(i);
                }
            }
            JsonObject unslotBody = new()
            {
                ["squadIds"] = new JsonArray(survivorSquads.Select(s => (JsonNode)s).ToArray()),
            }; ;
            await accountItems.PerformOperation("UnassignAllSquads", unslotBody.ToString());
            await accountItems.PerformOperation("AssignWorkerToSquadBatch", flattenedLoadout.ToString());
            await Helpers.WaitForTimer(0.1);
        }
        finally
        {
            LoadingOverlay.RemoveLoadingKey("applySurvivorLoadout");
        }
    }

    private async void OnLoadoutRename()
    {
        string newLoadoutName = await GenericLineEditWindow.OpenLineEdit("Enter Survivor Loadout Name", allowCancel: true, validator: val =>
        {
            if (val==loadoutName)
                return "...That's already the name of the loadout";
            if (loadouts.ContainsKey(val))
                return "A survivor loadout with that name already exists";
            return string.IsNullOrWhiteSpace(val) ? "" : null;
        });

        if (newLoadoutName is null)
            return;

        var loadoutInQuestion = loadouts[loadoutName];
        loadouts.Remove(loadoutName);
        loadouts.Add(newLoadoutName, loadoutInQuestion);
        loadoutName = newLoadoutName;

        ApplyLoadoutFileChange();
        loadoutSelector.Selected = loadouts.Select(kvp => kvp.Key).ToList().IndexOf(loadoutName) + 2;
        OnLoadoutChanged(loadoutSelector.Selected);
    }

    private async void OnLoadoutDelete()
    {
        if (!(await GenericConfirmationWindow.OpenConfirmation(
                "Delete Loadout?", 
                "Delete", 
                contextText: "The selected loadout will be deleted",
                warningText: "This action cannot be undone"
           ) ?? false))
            return;

        loadouts.Remove(loadoutName);

        ApplyLoadoutFileChange();
        GenerateOptions();
        loadoutSelector.Selected = 0;
        OnLoadoutChanged(loadoutSelector.Selected);
    }

    static readonly string[] survivorSquads = new string[]
    {
        "squad_attribute_medicine_emtsquad",
        "squad_attribute_medicine_trainingteam",
        "squad_attribute_arms_fireteamalpha",
        "squad_attribute_arms_closeassaultsquad",
        "squad_attribute_scavenging_scoutingparty",
        "squad_attribute_scavenging_gadgeteers",
        "squad_attribute_synthesis_corpsofengineering",
        "squad_attribute_synthesis_thethinktank",
    };

    private async void OnSquadClear()
    {
        if (!(await GenericConfirmationWindow.OpenConfirmation(
                "Clear Squad?", 
                "Clear", 
                contextText: "All slotted survivors in all squads will be unslotted"
            ) ?? false))
            return;

        try
        {
            LoadingOverlay.AddLoadingKey("clearSurvivorLoadout");
            var account = GameAccount.activeAccount;
            if (!await account.Authenticate())
                return;

            var accountItems = await account.GetProfile(FnProfileTypes.AccountItems).Query();
            var existingWorkers = accountItems.GetItems("Worker", item => item.attributes.ContainsKey("squad_id"));

            JsonObject unslotBody = new()
            {
                ["squadIds"] = new JsonArray(survivorSquads.Select(s => (JsonNode)s).ToArray()),
            }; ;
            await accountItems.PerformOperation("UnassignAllSquads", unslotBody.ToString());
        }
        finally
        {
            LoadingOverlay.RemoveLoadingKey("clearSurvivorLoadout");
        }
    }

    void ApplyLoadoutFileChange()
    {
        using var survivorLoadoutFile = FileAccess.Open(survivorLoadoutFilePath, FileAccess.ModeFlags.Write);
        survivorLoadoutFile.StoreString(loadouts.ToString());
        GenerateOptions();
    }

    void GenerateOptions()
    {
        loadoutSelector.Clear();
        loadoutSelector.AddItem("[Create New Loadout]");
        loadoutSelector.AddSeparator("Loadouts");
        foreach (var kvp in loadouts)
        {
            loadoutSelector.AddItem(kvp.Key);
        }
    }

    [Export]
    Texture2D recycleIcon;
    [Export]
    Texture2D collectionIcon;

    static readonly GameItemPredicate recycleFilter = item =>
        item.template.Type is string type && (type == "Worker" || type == "Schematic" || type == "Hero" || type == "Defender") &&
        !(item.template["IsPermanent"]?.GetValue<bool>() ?? false) &&
        !item.templateId.Contains("ammo") && !item.templateId.Contains("floor_defender")&&
        !item.templateId.Contains("player_jump_pad") && !item.templateId.Contains("ingredient");

    async void DebugRecycle()
    {
        GameItem[] filteredItems = null;
        try
        {
            LoadingOverlay.AddLoadingKey("gettingItems");
            var account = GameAccount.activeAccount;
            if (!await account.Authenticate())
                return;

            var accountItems = await account.GetProfile(FnProfileTypes.AccountItems).Query();

            filteredItems = accountItems.GetItems(recycleFilter);

            foreach (var item in filteredItems)
            {
                await item.IsCollected();
            }
        }
        finally
        {
            LoadingOverlay.RemoveLoadingKey("gettingItems");
        }

        GameItemSelector.Instance.SetRecycleDefaults();
        var recycleItems = await GameItemSelector.Instance.OpenSelector(filteredItems);

        GD.Print("Items: \n" + recycleItems.Select(item => item.uuid).ToArray().Join("\n"));
    }
}
