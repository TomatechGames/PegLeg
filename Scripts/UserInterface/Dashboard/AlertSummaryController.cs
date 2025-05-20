using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

public partial class AlertSummaryController : Control
{
	[Export(PropertyHint.ArrayType)]
	string[] targetTemplateIds;
	[Export]
	Control rewardRowParent;
    [Export]
    Control loadingIcon;

    AlertRewardRow[] rewardRows;

	public override async void _Ready()
	{
		rewardRows = rewardRowParent.GetChildren().Select(c => new AlertRewardRow(c as Control)).ToArray();
        GameMission.OnMissionsUpdated += CountRewards;
        GameMission.OnMissionsInvalidated += ClearRewards;
        ClearRewards();
        await GameMission.UpdateMissions();
    }

    public override void _ExitTree()
    {
        GameMission.OnMissionsUpdated -= CountRewards;
        GameMission.OnMissionsInvalidated -= ClearRewards;
    }

    void ClearRewards()
    {
        for (int i = 0; i < rewardRows.Length; i++)
        {
			rewardRows[i].Visible = false;
        }
        loadingIcon.Visible = true;
    }

    CancellationTokenSource rewardCTS = new();
    async void CountRewards()
    {
        var missions = GameMission.currentMissions;
        if (missions is null)
            return;
        rewardCTS.CancelAndRegenerate(out var ct);

        ClearRewards();
		Dictionary<string, ZoneTotals> totals = [];

        await Task.Run(() =>
        {
            foreach (var mission in missions)
            {
                foreach (var item in mission.alertRewardItems ?? [])
                {
                    var tid = item.templateId;
                    if (!targetTemplateIds.Contains(tid))
                        continue;
                    ZoneTotals current = totals.TryGetValue(tid, out var existing) ? existing : new();
                    current += mission.TheaterCat switch
                    {
                        "s" => new(item.quantity, 0, 0, 0, 0),
                        "p" => new(0, item.quantity, 0, 0, 0),
                        "c" => new(0, 0, item.quantity, 0, 0),
                        "t" => new(0, 0, 0, item.quantity, 0),
                        "v" => new(0, 0, 0, 0, item.quantity),
                        _ => new()
                    };
                    totals[tid] = current;
                }
            }
        }, ct);

        for (int i = 0; i < rewardRows.Length; i++)
        {
            if (
                i >= targetTemplateIds.Length || 
                GameItemTemplate.Get(targetTemplateIds[i]) is not GameItemTemplate template ||
                !totals.TryGetValue(targetTemplateIds[i], out var outcome)
                )
            {
                rewardRows[i].Visible = false;
                continue;
            }
            rewardRows[i].SetValues(template, outcome);
        }
        loadingIcon.Visible = false;
    }

    struct ZoneTotals
    {
        public int S;
        public int P;
        public int C;
        public int T;
        public int V;

        public ZoneTotals()
        {
            S = 0;
            P = 0;
            C = 0;
            T = 0;
            V = 0;
        }

        public ZoneTotals(int s, int p, int c, int t, int v)
        {
            S = s;
            P = p;
            C = c; 
            T = t;
            V = v;
        }

        public static ZoneTotals operator +(ZoneTotals left, ZoneTotals right)
        {
            left.S += right.S;
            left.P += right.P;
            left.C += right.C;
            left.T += right.T;
            left.V += right.V;
            return left;
        }
    }

	struct AlertRewardRow
	{
		Control row;
		TextureRect icon;
		Label name;
        ColorRect color;

		Label stonewood;
		Label plankerton;
		Label canny;
		Label twine;
		Label total;
        Label ventures;

        public AlertRewardRow(Control parent)
        {
			row = parent;
			icon = parent.GetNode<TextureRect>("%Icon");
            name = parent.GetNode<Label>("%Name");
            color = parent.GetNode<ColorRect>("%Color");

            stonewood = parent.GetNode<Label>("%Stonewood");
            plankerton = parent.GetNode<Label>("%Plankerton");
            canny = parent.GetNode<Label>("%Canny");
            twine = parent.GetNode<Label>("%Twine");
            total = parent.GetNode<Label>("%Total");
            ventures = parent.GetNode<Label>("%Ventures");
        }

		public bool Visible
		{
			get => row.Visible;
			set => row.Visible = value;
		}

		public void SetValues(GameItemTemplate t, ZoneTotals v)
		{
            Visible = true;
			icon.Texture = t.GetTexture();
			name.Text = t.DisplayName;
            name.TooltipText = t.DisplayName;
            color.Color = t.RarityColor;


            stonewood.Text = v.S.Compactify();
            stonewood.TooltipText = v.S.ToString();
            plankerton.Text = v.P.Compactify();
            plankerton.TooltipText = v.P.ToString();
            canny.Text = v.C.Compactify();
            canny.TooltipText = v.C.ToString();
            twine.Text = v.T.Compactify();
            twine.TooltipText = v.T.ToString();

            total.Text = (v.S + v.P + v.C + v.T).Compactify();
            total.TooltipText = (v.S + v.P + v.C + v.T).ToString();

            ventures.Text = v.V.Compactify();
            ventures.TooltipText = v.V.ToString();
        }
    }
}
