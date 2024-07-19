using Godot;
using Godot.Collections;
using System;

[GlobalClass]
public partial class BanjoSuppliments : Resource
{
    static Texture2D emptyTexture = new();
    [Export]
    public Dictionary<string, Texture2D> AmmoIcons { get; private set; } = new()
    {
        ["Shells \u0027n\u0027 Slugs"] = emptyTexture,
        ["Light Bullets"] = emptyTexture,
        ["Medium Bullets"] = emptyTexture,
        ["Heavy Bullets"] = emptyTexture,
        ["Explosive"] = emptyTexture,
        ["Energy Cell"] = emptyTexture,
    };

    [Export]
    public Dictionary<string, Texture2D> SquadIcons { get; private set; } = new()
    {
        ["Doctor"] = emptyTexture,
        ["Trainer"] = emptyTexture,
        ["Marksman"] = emptyTexture,
        ["Martial Artist"] = emptyTexture,
        ["Explorer"] = emptyTexture,
        ["Gadgeteer"] = emptyTexture,
        ["Engineer"] = emptyTexture,
        ["Inventor"] = emptyTexture,
    };

    [Export]
    public Dictionary<string, Texture2D> SquadFortIcons { get; private set; } = new()
    {
        ["Doctor"] = emptyTexture,
        ["Trainer"] = emptyTexture,
        ["Marksman"] = emptyTexture,
        ["Martial Artist"] = emptyTexture,
        ["Explorer"] = emptyTexture,
        ["Gadgeteer"] = emptyTexture,
        ["Engineer"] = emptyTexture,
        ["Inventor"] = emptyTexture,
    };

    [Export]
    public Dictionary<string, Texture2D> PersonalityIcons { get; private set; } = new()
    {
        ["IsAdventurous"] = emptyTexture,
        ["IsAnalytical"] = emptyTexture,
        ["IsCompetitive"] = emptyTexture,
        ["IsCooperative"] = emptyTexture,
        ["IsCurious"] = emptyTexture,
        ["IsDependable"] = emptyTexture,
        ["IsDreamer"] = emptyTexture,
        ["IsPragmatic"] = emptyTexture
    };

    [Export]
    public Dictionary<string, Texture2D> SetBonusIcons { get; private set; } = new()
    {
        ["IsTrapDamageLow"] = emptyTexture,
        ["IsRangedDamageLow"] = emptyTexture,
        ["IsMeleeDamageLow"] = emptyTexture,
        ["IsTrapDurabilityHigh"] = emptyTexture,
        ["IsAbilityDamageLow"] = emptyTexture,
        ["IsMaxHealthHigh"] = emptyTexture,
        ["IsMaxShieldHigh"] = emptyTexture,
        ["IsShieldRegenLow"] = emptyTexture
    };

    [Export]
    public Dictionary<string, string> SynergyToSquadId { get; private set; } = new()
    {
        ["Doctor"] = "squad_attribute_medicine_emtsquad",
        ["Trainer"] = "squad_attribute_medicine_trainingteam",
        ["Marksman"] = "squad_attribute_arms_fireteamalpha",
        ["Martial Artist"] = "squad_attribute_arms_closeassaultsquad",
        ["Explorer"] = "squad_attribute_scavenging_scoutingparty",
        ["Gadgeteer"] = "squad_attribute_scavenging_gadgeteers",
        ["Engineer"] = "squad_attribute_synthesis_corpsofengineering",
        ["Inventor"] = "squad_attribute_synthesis_thethinktank",
    };

    [Export]
    public Dictionary<string, string> SquadNames { get; private set; }  = new()
    {
        ["Doctor"] = "EMT Squad",
        ["Trainer"] = "Training Team",
        ["Marksman"] = "Fire Team",
        ["Martial Artist"] = "Close Assault Squad",
        ["Explorer"] = "Scouting Party",
        ["Gadgeteer"] = "Gadgeteers",
        ["Engineer"] = "Corps of Engineering",
        ["Inventor"] = "The Think Tank",
    };
}
