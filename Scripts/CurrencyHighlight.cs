using Godot;
using System;
using System.Linq;

public partial class CurrencyHighlight : GameItemEntry
{
    public static CurrencyHighlight Instance { get; private set; }

    public override async void _Ready()
    {
        base._Ready();
        Instance = this;
        if (await LoginRequests.TryLogin())
            SetCurrencyType("AccountResource:eventcurrency_scaling");
    }

    public async void SetCurrencyType(string type)
    {
        var profileItem = (await ProfileRequests.GetProfileItems(FnProfileTypes.AccountItems, type)).FirstOrDefault();
        if (profileItem.Value is not null)
        {
            LinkProfileItem(await ProfileItemHandle.CreateHandle(new(FnProfileTypes.AccountItems, profileItem.Key)));
        }
    }
}
