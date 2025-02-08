using Godot;
using System;

public partial class PerkEntry : Control
{
    [Signal]
    public delegate void NameChangedEventHandler(string name);
    [Signal]
    public delegate void RarityIconChangedEventHandler(Texture2D rarityIcon);
    [Signal]
    public delegate void RarityIconVisibilityChangedEventHandler(bool newValue);
    [Signal]
    public delegate void LockVisibilityChangedEventHandler(bool newValue);
    [Signal]
    public delegate void LockTextChangedEventHandler(string newValue);
    [Signal]
    public delegate void ElementIconChangedEventHandler(Texture2D rarityIcon);
    [Signal]
    public delegate void ElementIconVisibilityChangedEventHandler(bool newValue);
    [Signal]
    public delegate void InteractableChangedEventHandler(bool newValue);
    [Signal]
    public delegate void PressedEventHandler(int index, string alterationId, bool replaceable);

    static readonly string[] rarityToImage = new string[]
    {
        "ExportedImages/T-Items-WeaponPerkUp-Generic-L.png",
        "ExportedImages/T-Items-WeaponPerkUp-Uncommon-L.png",
        "ExportedImages/T-Items-WeaponPerkUp-Rare-L.png",
        "ExportedImages/T-Items-WeaponPerkUp-Epic-L.png",
        "ExportedImages/T-Items-WeaponPerkUp-Legendary-L.png",
        "ExportedImages/T-Items-WeaponRePerk-Resource-L.png"
    };

    string linkedAlteration;
    int linkedIndex;
    bool isLocked;

    public void SetPerkAlteration(string alterationId, bool hasRarity = false, int index = 0)
    {
        linkedAlteration = alterationId;
        linkedIndex = index;
        if (alterationId is not null)
        {
            if (GameItemTemplate.Get(alterationId) is GameItemTemplate alteration)
            {
                EmitSignal(SignalName.NameChanged, alteration.DisplayName);

                if (hasRarity)
                {
                    int rarity = alteration.RarityLevel;
                    if (alterationId.StartsWith("Alteration:aid_g_"))
                        rarity = 6;
                    EmitSignal(SignalName.RarityIconChanged, BanjoAssets.GetReservedTexture(rarityToImage[rarity - 1]));
                    EmitSignal(SignalName.RarityIconVisibilityChanged, true);
                }
                else
                    EmitSignal(SignalName.RarityIconVisibilityChanged, false);

                if (alteration.ContainsKey("ImagePaths"))
                {
                    EmitSignal(SignalName.ElementIconChanged, alteration.GetTexture());
                    EmitSignal(SignalName.ElementIconVisibilityChanged, true);
                }
                else
                    EmitSignal(SignalName.ElementIconVisibilityChanged, false);
            }
            else if(alterationId == "")
            {
                EmitSignal(SignalName.NameChanged, "Empty Perk Slot");
                EmitSignal(SignalName.RarityIconVisibilityChanged, false);
                EmitSignal(SignalName.ElementIconVisibilityChanged, false);
            }
            else
            {
                EmitSignal(SignalName.NameChanged, "Unknown Perk (Probably Legacy)");
                EmitSignal(SignalName.RarityIconVisibilityChanged, true);
                EmitSignal(SignalName.RarityIconChanged, BanjoAssets.defaultIcon);
                EmitSignal(SignalName.ElementIconVisibilityChanged, false);
            }

        }
        else
        {
            EmitSignal(SignalName.NameChanged, "Preview perk possibilities");
            EmitSignal(SignalName.RarityIconVisibilityChanged, true);
            EmitSignal(SignalName.RarityIconChanged, BanjoAssets.defaultIcon);
            EmitSignal(SignalName.ElementIconVisibilityChanged, false);
        }
    }

    public void SetInteractable(bool newValue)
    {
        EmitSignal(SignalName.InteractableChanged, newValue);
    }

    public void SetLocked(bool newValue)
    {
        isLocked = newValue;
        EmitSignal(SignalName.LockVisibilityChanged, newValue);
    }

    public void SetLockText(string text)
    {
        EmitSignal(SignalName.LockTextChanged, text);
    }

    public void Press()
    {
        EmitSignal(SignalName.Pressed, linkedIndex, linkedAlteration, isLocked);
    }
}
