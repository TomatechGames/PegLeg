using Godot;
using System;
using System.Collections.Generic;
using System.Security.Principal;
using System.Threading.Tasks;

public partial class AccountSelector : ModalWindow
{
    [Export]
    Control selectorButtonLabel;
    [Export]
    Control selectorButtonLabelTarget;
    [Export]
    Control selectorButtonIcons;
    [Export]
    Foldout foldout;
    [Export]
    Button foldoutBtn;
    [Export]
    Control accountEntryParent;
    [Export]
    PackedScene accountEntryScene;
    List<GameAccountEntry> pooledAccounts = new();

    protected override string OpenSound => "WipeAppear";
    protected override string CloseSound => "WipeDisappear";

    public override void _Ready()
    {
        for (int i = 0; i < GameAccount.OwnedAccounts.Length; i++)
        {
            GenerateAccountEntry();
        }
    }

    public async void ReloadAccount()
    {
        using var _ = LoadingOverlay.CreateToken();
        await GameAccount.RefreshActiveAccount();
        var account = GameAccount.activeAccount;
        await account.GetProfile(FnProfileTypes.AccountItems).Query();
    }

    public async void OpenLogin()
    {
        var account = await LoginPopup.OpenLoginPopup();
        if (account?.isOwned ?? false)
            PopulateAccounts();
    }

    void PopulateAccounts()
    {
        var accounts = GameAccount.OwnedAccounts;
        for (int i = 0; i < accounts.Length; i++)
        {
            if (pooledAccounts.Count <= i)
                GenerateAccountEntry();
            pooledAccounts[i].Visible = true;
            pooledAccounts[i].SetAccount(accounts[i]);
        }
        for (int i = accounts.Length; i < pooledAccounts.Count; i++)
        {
            pooledAccounts[i].Visible = false;
        }
    }

    protected override void BuildTween(ref Tween tween, bool openState)
    {
        if (openState)
        {
            PopulateAccounts();
            selectorButtonIcons.Modulate = Colors.White;
            selectorButtonLabel.Modulate = Colors.Transparent;
            foldout.SetFoldoutStateImmediate(false);
        }
        else
        {
            foldout.SetFoldoutState(false);
        }
        foldoutBtn.Disabled = true;

        tween.SetParallel(false);
        tween.TweenInterval(openState ? 0 : 0.1f);
        tween.SetParallel();
        tween.TweenProperty(this, "Dummy", 1, 0.01); // silences "started with no Tweeners" error
        base.BuildTween(ref tween, openState);

        tween.TweenProperty(selectorButtonIcons, "modulate", openState ? Colors.Transparent : Colors.White, TweenTime);
        tween.TweenProperty(selectorButtonLabel, "modulate", openState ? Colors.White : Colors.Transparent, TweenTime);

        tween.TweenProperty(selectorButtonLabel, "custom_minimum_size:x", openState ? selectorButtonLabelTarget.GetCombinedMinimumSize().X : 0, TweenTime);

        tween.TweenInterval(0.1f);

        tween.Play();
        //tween.SetParallel(false);
    }

    void GenerateAccountEntry()
    {
        var accountEntry = accountEntryScene.Instantiate<GameAccountEntry>();
        accountEntry.Visible = false;
        accountEntry.Pressed += SelectAccount;
        accountEntryParent.AddChild(accountEntry);
        pooledAccounts.Add(accountEntry);
    }

    async void SelectAccount(string accountId)
    {
        if (await GameAccount.SetActiveAccount(accountId))
        {
            using var _ = LoadingOverlay.CreateToken();
            await GameAccount.activeAccount.GetProfile(FnProfileTypes.AccountItems).Query();
            SetWindowOpen(false);
        }
    }
    async void RemoveAccount(string accountId)
    {
        //show confirmation menu
        if (await GameAccount.SetActiveAccount(accountId))
        {
            SetWindowOpen(false);
        }
    }

    protected override void OnTweenFinished(bool openState)
    {
        base.OnTweenFinished(openState);
        if (openState)
        {
            foldoutBtn.Disabled = false;
            foldout.SetFoldoutState(true);
        }
    }
}
