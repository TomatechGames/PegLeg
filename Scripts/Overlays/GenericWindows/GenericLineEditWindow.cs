using Godot;
using System;
using System.Threading.Tasks;

public partial class GenericLineEditWindow : ModalWindow
{
    [Export]
    Label header;
    [Export]
    Label content;
    [Export]
    LineEdit textBox;
    [Export]
    Button cancelButton;
    [Export]
    Button confirmButton;
    [Export]
    Label warningLabel;

    static GenericLineEditWindow instance;

    public override void _Ready()
    {
        base._Ready();
        instance = this;
        textBox.TextChanged += OnTextChanged;
        cancelButton.Pressed += Cancel;
        confirmButton.Pressed += Confirm;
    }

    bool allowCancel;
    bool didCancel = false;
    bool isEditingText = false;
    Func<string, string> validator;

    public static async Task<string> OpenLineEdit(string headerText, string contextText = "", string defaultText = "", string placeholder = "", bool allowCancel = true, Func<string, string> validator = null)=>
        await instance.OpenLineEditInst(headerText, contextText, defaultText, placeholder, allowCancel, validator);

    async Task<string> OpenLineEditInst(string headerText, string contextText, string defaultText, string placeholder, bool allowCancel, Func<string, string> validator)
    {
        this.validator = validator ?? (val => string.IsNullOrWhiteSpace(val) ? "" : null);
        header.Text = headerText;
        header.SetVisibleIfHasContent();

        content.Text = contextText;
        content.SetVisibleIfHasContent();

        textBox.Text = defaultText;
        OnTextChanged(defaultText);

        textBox.PlaceholderText = placeholder;
        isEditingText = true;

        this.allowCancel = allowCancel;
        cancelButton.Visible = allowCancel;
        didCancel = false;

        SetWindowOpen(true);
        while (isEditingText)
            await this.WaitForFrame();
        SetWindowOpen(false);

        bool isValid = this.validator(textBox.Text) is null;

        return (!didCancel && isValid) ? textBox.Text : null;
    }

    public void Cancel()
    {
        if (allowCancel)
        {
            didCancel = true;
            isEditingText = false;
        }
    }

    public void Confirm()
    {
        if (validator(textBox.Text) is null)
            isEditingText = false;
    }

    private void OnTextChanged(string newText)
    {
        string validationResult = validator(newText);
        warningLabel.Text = validationResult;
        warningLabel.SetVisibleIfHasContent();
        confirmButton.Disabled = validationResult is not null;
    }
}
