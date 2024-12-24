using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

public partial class TempCompendiumInterface : Control
{
	[Export]
	ItemList itemList;
	[Export]
	LineEdit searchBox;

	static readonly string[] includedSources = new string[]
	{
		"Hero",
		"Schematic"
	};
	public override void _Ready()
	{
		VisibilityChanged += GenerateCompendiumEntries;
		searchBox.TextChanged += FilterItems;
		itemList.ItemSelected += InspectItem;

    }

    bool generated = false;
	List<GameItemTemplate> compendiumTemplates = new();

	void GenerateCompendiumEntries()
	{
		if (generated)
			return;
		generated = true;
        searchBox.Editable = false;

		//TODO: run asynchronously
		Dictionary<string, GameItemTemplate> uniqueTemplates = new();
        foreach (var source in includedSources)
        {
			if (BanjoAssets.TryGetSource(source, out var sourceObject))
			{
				Parallel.ForEach(sourceObject, sourceKVP =>
                {
                    var template = GameItemTemplate.Get(sourceKVP.Key);
					if (template.Tier != 1)
						return;

					lock (uniqueTemplates)
					{
						if (!uniqueTemplates.ContainsKey(template.DisplayName) || uniqueTemplates[template.DisplayName].RarityLevel < template.RarityLevel)
						{
							uniqueTemplates[template.DisplayName] = template;
						}
					}
				});
            }
        }
        compendiumTemplates = uniqueTemplates.Values
            .OrderBy(item => item.Type=="Hero" ? 0 : 1)
			.ThenBy(item => -item.RarityLevel)
			.ThenBy(item => item.Category)
			.ThenBy(item => item.SubType)
			.ThenBy(item => item.DisplayName.StartsWith("The ") ? item.DisplayName[4..] : item.DisplayName)
			.ToList();
		//compendiumTemplates.ForEach(i => i.GenerateSearchTags());
        FilterItems("");
        searchBox.Editable = true;
    }

	void FilterItems(string _)
	{
		var instructions = PLSearch.GenerateSearchInstructions(searchBox.Text);
		var filteredTemplates = compendiumTemplates.Where(item => PLSearch.EvaluateInstructions(instructions, item.rawData));

        itemList.Clear();
        foreach (var template in filteredTemplates)
        {
            var index = itemList.AddItem("", template.GetTexture());
			List<string> tooltipBody = new() { template.Description };
            if (template["searchTags"] is JsonArray searchTags)
                tooltipBody.Add(string.Join(", ", searchTags.Select(n => n.ToString()).ToArray()[1..]));
			itemList.SetItemTooltip(index, 
				CustomTooltip.GenerateSimpleTooltip(
					template.DisplayName, 
					null,
                    tooltipBody.ToArray(), 
					template.RarityColor.ToHtml()
					)
				);
			itemList.SetItemCustomFgColor(index, Colors.Black);
			var color = template.RarityColor;
			color.A *= 0.5f;
            itemList.SetItemCustomBgColor(index, color);
        }
    }

    private void InspectItem(long index)
    {
		var template = compendiumTemplates[(int)index];
		var item = template.CreateInstance();
		item.SetSeenLocal();
        GameItemViewer.Instance.ShowItem(item);
    }
}
