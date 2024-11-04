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
        string name = heroAbility["DisplayName"].ToString();
        string description = heroAbility["Description"]?.ToString();
        //JsonObject abilityStats = heroAbility["AbilityStats"]?.AsObject() ?? new();

        //description = description.Replace("+[Damage]",          "+" + (ScalarAsPercentage(abilityStats["Damage"]        ?.GetValue<float>()) ?? "?") + "%");
        //description = description.Replace("+[Ability.Line4]",   "+" + (ScalarAsPercentage(abilityStats["AbilityLine4"]  ?.GetValue<float>()) ?? "?") + "%");
        //description = description.Replace("+[Ability.Line5]",   "+" + (ScalarAsPercentage(abilityStats["AbilityLine5"]  ?.GetValue<float>()) ?? "?") + "%");

        //description = description.Replace("[Damage]", abilityStats["Damage"]?.ToString() ?? "?");
        //description = description.Replace("[Damage.Value]", abilityStats["Damage"]?.ToString() ?? "?");
        //description = description.Replace("[AbilityWeaponDamage.Value]", abilityStats["Damage"]?.ToString() ?? "?");

        //description = description.Replace("[Ability.Line2]", abilityStats["AbilityLine2"]?.ToString() ?? "?");
        //description = description.Replace("[Ability.Line3]", abilityStats["AbilityLine3"]?.ToString() ?? "?");

        //description = description.Replace("[FireRate.Value]", abilityStats["FireRate"]?.ToString() ?? "?");
        //description = description.Replace("[Ability.RateOfFire]", abilityStats["FireRate"]?.ToString() ?? "?");

        //description = description.Replace("[Ability.Distance]", ScalarAsTiles(abilityStats["Distance"]?.GetValue<float>()) ?? "?");

        //description = description.Replace("[Radius]", (ScalarAsTiles(abilityStats["Radius"]?.GetValue<float>()) ?? "?")+ " tile");

        //description = description.Replace("[Duration]", abilityStats["Duration"]?.ToString() ?? "?");
        //description = description.Replace("[Ability.Duration]", abilityStats["Duration"]?.ToString() ?? "?");

        EmitSignal(SignalName.NameChanged, name);
        EmitSignal(SignalName.DescriptionChanged, description);
        EmitSignal(SignalName.NameAndDescriptionChanged, name+"\n"+description);

        EmitSignal(SignalName.IconChanged, heroAbility.GetItemTexture(ItemTextureType.Icon));

        EmitSignal(SignalName.WarningVisible, warning is not null);
        EmitSignal(SignalName.WarningChanged, warning ?? "");

        EmitSignal(SignalName.LockVisible, locked);
        EmitSignal(SignalName.LockChanged, locked ? lockText : "");
	}
}
