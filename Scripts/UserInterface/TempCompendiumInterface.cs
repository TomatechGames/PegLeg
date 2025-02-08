using Godot;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

public partial class TempCompendiumInterface : Control, IRecyclableElementProvider<GameItem>
{
	[Export]
	RecycleListContainer itemList;
	[Export]
	LineEdit searchBox;
	[Export]
	Control loadingIcon;

	static readonly string[] includedSources = new string[]
	{
		"Hero",
		"Schematic"
	};
	public override void _Ready()
	{
		VisibilityChanged += GenerateCompendiumEntries;
		searchBox.TextChanged += FilterItems;
		itemList.SetProvider(this);
		//itemList. += InspectItem;
    }


    bool generated = false;
	List<GameItem> compendiumEntries = new();
	List<GameItem> filteredEntries = new();
	public GameItem GetRecycleElement(int index) => filteredEntries?[index];

	public int GetRecycleElementCount() => filteredEntries?.Count ?? 0;

    async void GenerateCompendiumEntries()
	{
		if (generated)
			return;
		generated = true;
        searchBox.Editable = false;
		loadingIcon.Visible = true;
        itemList.Visible = false;

        //TODO: run asynchronously
        ConcurrentDictionary<string, GameItemTemplate> uniqueTemplates = new();
		List<Task> sourceTasks = new();
        foreach (var source in includedSources)
        {
			var sourceTask = Task.Run(() =>
            {
                if (BanjoAssets.TryGetSource(source, out var sourceObject))
                {
					//GD.Print("aa");
                    Parallel.ForEach(sourceObject, sourceKVP =>
                    {
                        var template = GameItemTemplate.Get(sourceKVP.Key);
                        if (template.Tier != 1)
                            return;

                        template.GenerateSearchTags();
						template.GetTexture();

                        uniqueTemplates.AddOrUpdate(
                                template.DisplayName,
                                template,
                                (k, v) => v.RarityLevel < template.RarityLevel ? template : v
                            );
                    });
                }
            });
			sourceTasks.Add(sourceTask);
        }
		await Task.WhenAll(sourceTasks);
        List<GameItem> orderedItems = null;
		await Task.Run(() =>
        {
			orderedItems = uniqueTemplates.Values
				.OrderBy(item => item.Type == "Hero" ? 0 : 1)
				.ThenBy(item => -item.RarityLevel)
				.ThenBy(item => item.Category)
				.ThenBy(item => item.SubType)
				.ThenBy(item => item.DisplayName.StartsWith("The ") ? item.DisplayName[4..] : item.DisplayName)
				.Select(item => item.CreateInstance())
				.ToList();
        });
        compendiumEntries = orderedItems ?? new();
        //compendiumTemplates.ForEach(i => i.GenerateSearchTags());
        FilterItems("");
        searchBox.Editable = true;
        loadingIcon.Visible = false;
		itemList.Visible = true;
    }

	void FilterItems(string _)
	{
		var instructions = PLSearch.GenerateSearchInstructions(searchBox.Text);
		filteredEntries = string.IsNullOrWhiteSpace(searchBox.Text) ? compendiumEntries : compendiumEntries.Where(item => PLSearch.EvaluateInstructions(instructions, item.RawData)).ToList();

        GD.Print("filteredEntries: " + filteredEntries.Count);
        itemList.UpdateList(true);
    }
}
