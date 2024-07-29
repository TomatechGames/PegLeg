using Godot;
using System;
using System.Linq;
using System.Text.Json.Nodes;

public partial class QuestHighlight : Control
{
    [Signal]
    public delegate void TooltipChangedEventHandler(string tooltip);
    [Signal]
    public delegate void IconChangedEventHandler(Texture2D icon);
    [Signal]
    public delegate void ColorChangedEventHandler(Color color);
    [Signal]
    public delegate void ProgressChangedEventHandler(float progress);


    [Export]
    bool useFirstRewardAsIcon = false;
    [Export]
    Color incompleteColor;
    [Export]
    Color completeColor;

    public void SetHighlightedQuest(QuestData questData)
    {
        Visible = questData.isUnlocked;
        ProcessMode = questData.isUnlocked ? ProcessModeEnum.Inherit : ProcessModeEnum.Disabled;
        if (!Visible)
            return;
        EmitSignal(SignalName.TooltipChanged, questData.questTemplate["DisplayName"].ToString());
        if (questData.isComplete)
        {
            EmitSignal(SignalName.ColorChanged, completeColor);
            EmitSignal(SignalName.ProgressChanged, 1);
        }
        else
        {
            EmitSignal(SignalName.ColorChanged, incompleteColor);

            float progressTotal = 0;
            var objectives = questData.questTemplate["Objectives"].AsArray();
            foreach (var objective in objectives)
            {
                int currentProgress = questData.questItem.GetItemUnsafe()["attributes"]["completion_" + objective["BackendName"].ToString()]?.GetValue<int>() ?? 0;
                int maxProgress = objective["Count"].GetValue<int>();
                progressTotal += ((float)currentProgress / maxProgress) / objectives.Count;
            }
            EmitSignal(SignalName.ProgressChanged, progressTotal);
        }

        if (useFirstRewardAsIcon)
        {
            var rewards = questData.questTemplate["Rewards"].AsArray();
            var firstReward = rewards.First();
            if (rewards.Any(r => r["Selectable"].GetValue<bool>()))
            {
                firstReward = rewards
                    .Where(r => r["Selectable"].GetValue<bool>())
                    .OrderBy(r => BanjoAssets.TryGetTemplate(r["Item"].ToString()).GetItemRarity())
                    .FirstOrDefault() ?? firstReward;
            }
            EmitSignal(SignalName.IconChanged, firstReward.AsObject().GetItemTexture());
        }
        else
        {
            EmitSignal(SignalName.IconChanged, questData.questTemplate.GetItemTexture());
        }
    }
}
