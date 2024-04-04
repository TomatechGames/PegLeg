using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;

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
    List<CompendiumEntry> compendiumEntries = new();
	struct CompendiumEntry
	{
        public string displayName;
		public Texture2D texture;
		public string templateId;
		public int rarity;
		public bool isHero;
		public string extraSearchTerm;
	}

	void GenerateCompendiumEntries()
	{
		if (generated)
			return;
		generated = true;

        foreach (var source in includedSources)
        {
			if (BanjoAssets.TryGetSource(source, out var sourceFile))
			{
                foreach (var item in sourceFile)
                {
					if (item.Value["Tier"].GetValue<int>() != 1)
						continue;
					else
					{
						string entryName = $"{item.Value["ItemName"]}";
						string extraSearchTerm = $"[{item.Value["Rarity"]}]";
						if (source == "Hero" && BanjoAssets.TryGetTemplate(item.Value["HeroPerk"], out var ability))
						{
							extraSearchTerm += ability["ItemName"].ToString();
						}
						compendiumEntries.Add(new()
						{ 
							displayName = entryName, 
							isHero = source == "Hero", 
							texture = item.Value.AsObject().GetItemTexture(), 
							templateId = item.Key, 
							rarity = item.Value.AsObject().GetItemRarity(), 
							extraSearchTerm = extraSearchTerm
						});
					}
                }
            }
        }
		compendiumEntries = compendiumEntries.OrderBy(val => val.isHero ? 0 : 1).ThenBy(val => -val.rarity).ThenBy(val => val.displayName).ToList();
		FilterItems("");
    }

	void FilterItems(string searchTerm)
	{
		itemList.Clear();
        foreach (var item in compendiumEntries)
        {
			if (
				!string.IsNullOrWhiteSpace(searchTerm) && 
				!item.displayName.ToLower().Contains(searchTerm.ToLower()) &&
                (!item.extraSearchTerm?.ToLower().Contains(searchTerm.ToLower()) ?? false)
				)
				continue;
            var index = itemList.AddItem(item.displayName, item.texture);
            itemList.SetItemMetadata(index, item.templateId);
			itemList.SetItemCustomFgColor(index, Colors.Black);
			var color = BanjoAssets.GetRarityColor(item.rarity);
			color.A *= 0.5f;
            itemList.SetItemCustomBgColor(index, color);
        }
    }

    private async void InspectItem(long index)
    {
		string templateId = (string)itemList.GetItemMetadata((int)index);
		if (BanjoAssets.TryGetTemplate(templateId, out var template))
			await GameItemViewer.Instance.SetItem(template.CreateInstanceOfItem(1, new() { ["item_seen"] = true }));
    }
}
