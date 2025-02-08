using Godot;
using System;
using System.Linq;

public partial class CurrencyHighlight : GameItemEntry
{
    public static CurrencyHighlight Instance { get; private set; }

    public override void _Ready()
    {
        base._Ready();
        Instance = this;
        Visible = false;
        GameAccount.ActiveAccountChanged += OnAccountChanged;
        SetCurrencyTemplate(GameItemTemplate.Get("AccountResource:eventcurrency_scaling"));
    }

    public override void _ExitTree()
    {
        base._ExitTree();
        GameAccount.ActiveAccountChanged -= OnAccountChanged;
    }

    void OnAccountChanged() => SetCurrencyTemplate(currentTemplate);

    GameItemTemplate currentTemplate;

    public async void SetCurrencyTemplate(GameItemTemplate currencyTemplate)
    {
        currentTemplate = currencyTemplate;
        var account = GameAccount.activeAccount;
        if (!await account.Authenticate())
        {
            ClearItem();
            Visible = false;
            return;
        }
        var profileItem = (await account.GetProfile(FnProfileTypes.AccountItems).Query()).GetTemplateItems(currencyTemplate.TemplateId).FirstOrDefault();
        if (profileItem is not null)
        {
            SetItem(profileItem);
            Visible = true;
        }
    }
}
