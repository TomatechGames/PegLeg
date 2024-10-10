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
    List<CompendiumEntry> compendiumEntries = new();
	struct CompendiumEntry
	{
        public string displayName;
        public string description;
        public Texture2D texture;
		public string templateId;
		public int rarity;
        public string rarityCol;
        public bool isHero;
        public string category;
        public string subtype;
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
				Parallel.ForEach(sourceFile, item =>
                {
					if (item.Value["Tier"].GetValue<int>() != 1)
						return;
					else
					{
						string entryName = item.Value["DisplayName"].ToString();
						string extraSearchTerm = $"[{item.Value["Rarity"]}]";
						if (source == "Hero" && BanjoAssets.TryGetTemplate("Ability:"+item.Value["HeroPerkName"], out var ability))
						{
							extraSearchTerm += " "+ability["DisplayName"].ToString();
						}
						CompendiumEntry result = new()
						{
							displayName = entryName,
							description = item.Value["Description"]?.ToString(),
                            isHero = source == "Hero",
							texture = item.Value.AsObject().GetItemTexture(),
							templateId = item.Key,
							rarity = item.Value.AsObject().GetItemRarity(),
                            rarityCol = item.Value.AsObject().GetItemRarityColor().ToHtml(),
                            category = item.Value.AsObject()["Category"]?.ToString() ?? "Hero",
                            subtype = item.Value.AsObject()["SubType"].ToString(),
                            extraSearchTerm = extraSearchTerm
						};
						lock (compendiumEntries)
                        {
                            if (!compendiumEntries.Exists(e => e.displayName == result.displayName && e.rarity >= result.rarity))
								compendiumEntries.Add(result);
							compendiumEntries.Remove(compendiumEntries.FirstOrDefault(e => e.displayName == result.displayName && e.rarity < result.rarity));
                        }
					}
                });
            }
        }
		compendiumEntries = compendiumEntries
			.OrderBy(val => val.isHero ? 0 : 1)
			.ThenBy(val => -val.rarity)
			.ThenBy(val => val.category)
			.ThenBy(val => val.subtype)
			.ThenBy(val => val.displayName.StartsWith("The ") ? val.displayName[4..] : val.displayName)
			.ToList();
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
            var index = itemList.AddItem("", item.texture);
            itemList.SetItemMetadata(index, item.templateId);
			itemList.SetItemTooltip(index, 
				CustomTooltip.GenerateSimpleTooltip(
					item.displayName, 
					null, 
					new string[] {
						item.description,
						item.extraSearchTerm
					}, 
					item.rarityCol
					)
				);
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
			await GameItemViewer.Instance.ShowItem(template.CreateInstanceOfItem(1, new() { ["item_seen"] = true }));
    }
}
