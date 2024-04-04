using Godot;
using System;
using System.Text.Json.Nodes;

public partial class HeroAbilityEntry : Node
{
    [Signal]
    public delegate void NameChangedEventHandler(string name);

    [Signal]
    public delegate void DescriptionChangedEventHandler(string description);

    [Signal]
    public delegate void NameAndDescriptionChangedEventHandler(string nameAndDescription);

    [Signal]
    public delegate void IconChangedEventHandler(Texture2D name);

    [Signal]
    public delegate void LockChangedEventHandler(string lockText);

    [Signal]
    public delegate void WarningChangedEventHandler(string warningText);

    [Signal]
    public delegate void LockVisibleEventHandler(bool showLocked);

    [Signal]
    public delegate void WarningVisibleEventHandler(bool showWarning);

    [Export]
    string lockText;

    public void SetAbility(JsonObject heroAbility, bool locked = false, string warning = null)
	{
        string name = heroAbility["ItemName"].ToString();
        string description = heroAbility["ItemDescription"].ToString();
        if (heroAbility.ContainsKey("AbilityStats"))
        {
            description = description.Replace("[Damage.Value]", heroAbility["AbilityStats"]["Damage"].ToString());
            description = description.Replace("[Damage]", heroAbility["AbilityStats"]["Damage"].ToString());
        }

        EmitSignal(SignalName.NameChanged, name);
        EmitSignal(SignalName.DescriptionChanged, description);
        EmitSignal(SignalName.NameAndDescriptionChanged, name+"\n"+description);

        EmitSignal(SignalName.IconChanged, heroAbility.GetItemTexture(BanjoAssets.TextureType.Icon));

        EmitSignal(SignalName.WarningVisible, warning is not null);
        EmitSignal(SignalName.WarningChanged, warning ?? "");

        EmitSignal(SignalName.LockVisible, locked);
        EmitSignal(SignalName.LockChanged, locked ? lockText : "");
	}
}
