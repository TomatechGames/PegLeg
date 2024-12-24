using Godot;
using System.Threading;

public partial class HomebasePowerLevel : Control
{
    //todo: export this via BanjoBotAssets
    static DataTableCurve homebaseRatingCurve = new("res://External/DataTables/HomebaseRatingMapping.json", "UIMonsterRating");

    [Export]
    Label homebaseNumberLabel;
    [Export]
    Range homebaseNumberProgressBar;

    public override void _Ready()
    {
        GameAccount.ActiveAccountChanged += OnActiveAccountChanged;
    }

    CancellationTokenSource accountChangeCts = new();
    async void OnActiveAccountChanged(GameAccount _)
    {
        accountChangeCts = accountChangeCts.Regenerate(out var ct);

        if (currentProfile is not null)
        {
            currentProfile.OnItemAdded -= OnProfileItemChanged;
            currentProfile.OnItemUpdated -= OnProfileItemChanged;
            currentProfile.OnItemRemoved -= OnProfileItemChanged;

            currentProfile.OnStatChanged -= OnProfileStatChanged;

            currentProfile = null;
        }

        var account = GameAccount.activeAccount;
        if (!await account.Authenticate() || ct.IsCancellationRequested)
            return;

        var newProfile = await account.GetProfile(FnProfileTypes.AccountItems).Query();
        if (ct.IsCancellationRequested)
            return;

        currentProfile = newProfile;

        currentProfile.OnItemAdded += OnProfileItemChanged;
        currentProfile.OnItemUpdated += OnProfileItemChanged;
        currentProfile.OnItemRemoved += OnProfileItemChanged;

        currentProfile.OnStatChanged += OnProfileStatChanged;
    }

    GameProfile currentProfile;

    void OnProfileStatChanged(GameProfile _)
    {
        UpdateStatsVisuals(currentProfile.account.GetFORTStats());
    }

    void OnProfileItemChanged(GameItem item)
    {
        if (item.template.Type == "Worker" && item.attributes.ContainsKey("squad_id"))
            UpdateStatsVisuals(currentProfile.account.GetFORTStats());
    }

    private void UpdateStatsVisuals(FORTStats stats)
    {
        var homebaseRatingKey = 4 * (stats.fortitude + stats.offense + stats.resistance + stats.technology);
        var powerLevel = homebaseRatingCurve.Sample(homebaseRatingKey);
        homebaseNumberLabel.Text = Mathf.Floor(powerLevel).ToString();
        homebaseNumberProgressBar.Value = powerLevel % 1;
        TooltipText = $"Homebase Power: {Mathf.Floor(powerLevel)}\n({Mathf.Floor((powerLevel % 1) * 100)}% progress to {Mathf.Floor(powerLevel) + 1})";
    }

    public override void _ExitTree()
    {
        if (currentProfile is not null)
        {
            currentProfile.OnItemAdded -= OnProfileItemChanged;
            currentProfile.OnItemUpdated -= OnProfileItemChanged;
            currentProfile.OnItemRemoved -= OnProfileItemChanged;

            currentProfile.OnStatChanged -= OnProfileStatChanged;

            currentProfile = null;
        }
        GameAccount.ActiveAccountChanged -= OnActiveAccountChanged;
    }
}
