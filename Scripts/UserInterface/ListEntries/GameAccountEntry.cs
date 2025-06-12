using Godot;
using System;

public partial class GameAccountEntry : Control
{
    [Signal]
    public delegate void NameChangedEventHandler(string name);

    [Signal]
    public delegate void TooltipChangedEventHandler(string tooltip);

    [Signal]
    public delegate void IconChangedEventHandler(Texture2D icon);

    [Signal]
    public delegate void AuthenticatedChangedEventHandler(bool isAuthed);

    [Signal]
    public delegate void PressedEventHandler(string accountId);
    [Signal]
    public delegate void DeletedEventHandler(string accountId);

    [Export]
    Texture2D defaultIcon;
    [Export]
    bool useActiveAccount = false;
    public GameAccount currentAccount { get; protected set; }

    public override void _Ready()
    {
        GameAccount.ActiveAccountChanged += SetActiveAccount;
        SetActiveAccount();
    }

    public void SetAccount(GameAccount account)
    {
        if (useActiveAccount)
            return;
        SetAccountInternal(account);
    }

    void SetActiveAccount()
    {
        if (!useActiveAccount)
            return;
        SetAccountInternal(GameAccount.activeAccount);
    }

    void SetAccountInternal(GameAccount account)
    {
        if (account == currentAccount)
        {
            UpdateAccount();
            currentAccount.UpdateIcon();
            return;
        }
        if (currentAccount is not null)
            currentAccount.OnAccountUpdated -= UpdateAccount;
        currentAccount = account;
        if (currentAccount is not null)
            currentAccount.OnAccountUpdated += UpdateAccount;
        UpdateAccount();
        currentAccount.UpdateIcon();
    }

    void UpdateAccount()
    {
        EmitSignal(SignalName.NameChanged, currentAccount.DisplayName);
        EmitSignal(SignalName.IconChanged, currentAccount.ProfileIcon ?? defaultIcon);
        EmitSignal(SignalName.AuthenticatedChanged, currentAccount.isAuthed);

        string tooltipText = CustomTooltip.GenerateSimpleTooltip(
                $" {currentAccount.DisplayName}   ",
                null,
                [
                    currentAccount.isAuthed ? "Logged In" : (currentAccount.isOwned ? $"Login Failure:\n\"{currentAccount.loginFailureMessage}\"" : "External")
                ],
                Colors.Blue.ToHtml()
            );
        EmitSignal(SignalName.TooltipChanged, tooltipText);
    }

    public void Press()
    {
        if (currentAccount is null)
            return;
        EmitSignal(SignalName.Pressed, currentAccount.accountId);
    }

    public async void Exchange()
    {
        if (currentAccount is null)
            return;
        GD.Print(await currentAccount.GenerateExchangeCode());
    }

    public void Delete()
    {
        if (currentAccount is null)
            return;
        EmitSignal(SignalName.Deleted, currentAccount.accountId);
    }

    public override void _ExitTree()
    {
        if (useActiveAccount)
            GameAccount.ActiveAccountChanged -= SetActiveAccount;
        if (currentAccount is not null)
            currentAccount.OnAccountUpdated -= UpdateAccount;
    }
}
