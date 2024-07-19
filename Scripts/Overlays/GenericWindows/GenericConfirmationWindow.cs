using Godot;
using System;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

public partial class GenericConfirmationWindow : ModalWindow
{
    [Export]
    Label header;
    [Export]
    Label content;
    [Export]
    Label warningLabel;
    [Export]
    Button cancelButton;
    [Export]
    Button negativeButton;
    [Export]
    Button positiveButton;

    static GenericConfirmationWindow instance;

    public override void _Ready()
    {
        base._Ready();
        instance = this;
        cancelButton.Pressed += Cancel;
        negativeButton.Pressed += () => SetResult(false);
        positiveButton.Pressed += () => SetResult(true);
    }

    bool isSelecting = false;
    bool allowCancel = false;
    bool? result = null;

    public static async Task ShowErrorForWebResult(JsonObject errorResult)
    {
        errorResult ??= new();
        if (!errorResult.ContainsKey("errorMessage"))
            errorResult["errorMessage"] = "(No Message Provided)";
        await instance.OpenConfirmationInst("Something Went Wrong", "\\_(*-*)_/", "", errorResult["errorMessage"].ToString(), "", false, 8);
    }


    public static async Task<bool?> OpenConfirmation(string headerText, string postiveText = "Confirm", string negativeText = "", string contextText = "", string warningText = "", bool allowCancel = true, int headerSpace = 8) =>
        await instance.OpenConfirmationInst(headerText, postiveText, negativeText, contextText, warningText, allowCancel, headerSpace);

    public async Task<bool?> OpenConfirmationInst(string headerText, string positiveText, string negativeText, string contextText, string warningText, bool allowCancel, int headerSpace)
    {
        for (int i = 0; i < headerSpace; i++)
        {
            headerText += " ";
        }
        header.Text = headerText;
        header.SetVisibleIfHasContent();

        content.Text = contextText;
        content.SetVisibleIfHasContent();

        warningLabel.Text = warningText;
        warningLabel.SetVisibleIfHasContent();

        this.allowCancel = allowCancel;
        cancelButton.Visible = allowCancel;

        positiveButton.Text = positiveText;

        negativeButton.Text = negativeText;
        negativeButton.Visible = !string.IsNullOrWhiteSpace(negativeText);

        isSelecting = true;
        result = null;
        SetWindowOpen(true);
        while (isSelecting)
            await this.WaitForFrame();
        SetWindowOpen(false);

        return result;
    }

    private void Cancel()
    {
        if (allowCancel)
            isSelecting = false;
    }

    public void SetResult(bool newResult)
    {
        isSelecting = false;
        result = newResult;
    }
}
