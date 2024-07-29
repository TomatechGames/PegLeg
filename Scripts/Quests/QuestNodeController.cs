using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata.Ecma335;
using System.Threading.Tasks;

public partial class QuestNodeController : Control
{
    [Signal]
    public delegate void PressedEventHandler();

    [Export]
    CheckButton unselectedButton;
    [Export]
    QuestViewer questViewer;
    [Export]
    Control questNodeParent;
    [Export]
    PackedScene questNodeScene;
    [Export]
    PackedScene questArrowScene;
    [Export]
    ScrollContainer scrollContainer;
    [Export]
    Control leftButtonParent;
    [Export]
    Control rightButtonParent;
    [Export(PropertyHint.ArrayType)]
	QuestNode[] questNodes = Array.Empty<QuestNode>();
    [Export(PropertyHint.ArrayType)]
    ShaderHook[] questArrows = Array.Empty<ShaderHook>();

    List<QuestNode> questNodeList;
    List<ShaderHook> questArrowList;


    const int maxNodesPerPage = 10;
	int nodesPerPage;
	List<QuestData> questDataList;
	int currentPage;
    int currentNodeIndex = 0;
    int currentQuestIndex = 0;
    bool isQuestOnPage = false;
    int maxPage;
    bool useArrows;

    public override void _Ready()
    {
        base._Ready();

        questNodeList = new(questNodes);
        questArrowList = new(questArrows);

        //start with 5 nodes and 5 arrows

        for (int i = questNodeList.Count; i < maxNodesPerPage; i++)
        {
            if (questArrowList.Count < i)
            {
                //add new arrow
                var newArrow = questArrowScene.Instantiate<ShaderHook>();
                questArrowList.Add(newArrow);
                questNodeParent.AddChild(newArrow);
            }

            //add new node
            var newNode = questNodeScene.Instantiate<QuestNode>();
            questNodeList.Add(newNode);
            questNodeParent.AddChild(newNode);
        }

        for (int i = 0; i < questNodeList.Count; i++)
        {
            int index = i;
            questNodeList[i].Pressed += () => OnQuestPressed(index);
        }
        ClearQuestNodes();
        questViewer.OnRefreshNeeded += RefreshSelectedQuestVisuals;
        QuestInterface.OnPinnedQuestsCleared += RefreshAllQuestVisuals;
    }

    private void RefreshAllQuestVisuals()
    {
        foreach (var item in questNodeList)
        {
            if (!item.Visible)
                continue;
            item.RefreshQuestNode();
        }
    }

    public void ClearQuestNodes()
    {
        for (int i = 0; i < questNodeList.Count; i++)
        {
            if (i > 0)
                questArrowList[i - 1].Visible = false;
            questNodeList[i].Visible = false;
        }
        leftButtonParent.Visible = false;
        rightButtonParent.Visible = false;
        //clear selected quest
    }


    public void SetQuestNodes(List<QuestData> newQuestDataList, bool useArrows, bool onlyShowIncomplete)
	{
        if (onlyShowIncomplete)
        {
            questDataList = newQuestDataList.Where(q=>q.isUnlocked && (!q.isComplete || useArrows)).ToList();
        }
        else
        {
            questDataList = newQuestDataList;
        }
        this.useArrows = useArrows;

        nodesPerPage = maxNodesPerPage;
        GD.Print("COUNT "+ questDataList.Count);
        if (questDataList.Count > nodesPerPage)
		{
			while ((questDataList.Count % nodesPerPage) < (nodesPerPage / 2))
			{
				nodesPerPage--;
				if (nodesPerPage < 5)
                {
                    GD.Print("Nodes Per Page less than 5");
                    break;//this should never happen
                }
            }
		}
		else
			nodesPerPage = questDataList.Count;

        var focusNode = questDataList.FirstOrDefault(q => !q.isComplete, null) ?? questDataList[^1];
        if(!useArrows)
            focusNode = questDataList.FirstOrDefault(q => q.isPinned) ?? questDataList[0];

        currentQuestIndex = questDataList.IndexOf(focusNode);
		currentPage = currentQuestIndex / nodesPerPage;
        maxPage = (questDataList.Count - 1) / nodesPerPage;
        SetPage(currentPage);
    }

    void RefreshSelectedQuestVisuals()
    {
        if (!isQuestOnPage)
            return;
        questNodeList[currentNodeIndex].RefreshQuestNode();
    }

    void OnQuestPressed(int nodeIndex)
    {
        currentNodeIndex = nodeIndex;
        int pageStartIndex = currentPage * nodesPerPage;
        currentQuestIndex = pageStartIndex + nodeIndex;
        isQuestOnPage = true;
        questViewer.SetupQuest(questDataList[currentQuestIndex]);
        if (questDataList[currentQuestIndex].questItem is ProfileItemHandle itemHandle && itemHandle.GetItemUnsafe()?["attributes"]?["item_seen"]?.GetValue<bool>()!=true)
        {
            itemHandle.GetItemUnsafe()["attributes"]["item_seen"] = true;
            GD.Print($"test: {itemHandle.GetItemUnsafe()["attributes"]?["item_seen"]?.GetValue<bool>()}");
            string content = @$"{{""itemIds"": [""{itemHandle.profileItem.uuid}""]}}";
            ProfileRequests.PerformProfileOperation(FnProfiles.AccountItems, "MarkItemSeen", content).RunSafely();
        }
        EmitSignal(SignalName.Pressed);
    }

    public void NextPage() => AddPage(1);
    public void PrevPage() => AddPage(-1);
    public void AddPage(int offset) => SetPage(currentPage+offset);

	public async void SetPage(int newPage)
	{
        int prevPage = currentPage;
        currentPage = Mathf.Clamp(newPage, 0, maxPage);

        isQuestOnPage = false;
        QuestNode pressOnComplete = null;
        int pageStartIndex = currentPage * nodesPerPage;
        int pageLength = Mathf.Min(nodesPerPage, questDataList.Count - pageStartIndex);
        for (int i = 0; i < pageLength; i++)
        {
            var thisQuest = questDataList[pageStartIndex + i];
            questNodeList[i].Visible = true;
            //apply quest to node
            questNodeList[i].SetupQuestNode(thisQuest, unselectedButton.ButtonGroup);
            isQuestOnPage |= pageStartIndex + i == currentNodeIndex;
            if (pageStartIndex + i == currentQuestIndex)
            {
                isQuestOnPage = true;
                pressOnComplete = questNodeList[i];
            }

            if (i > 0 && i <= questArrowList.Count)
            {
                if (useArrows)
                {
                    var prevQuest = questDataList[pageStartIndex + i - 1];
                    //set arrow effects
                    questArrowList[i - 1].SetShaderBool(prevQuest.isUnlocked && !prevQuest.isComplete, "Animation");
                    questArrowList[i - 1].SetShaderBool(prevQuest.isComplete, "UseCompleteColor");
                    questArrowList[i - 1].Visible = true;
                }
                else
                {
                    questArrowList[i - 1].Visible = false;
                }
            }
        }
        for (int i = pageLength; i < questNodeList.Count; i++)
        {
            questNodeList[i].Visible = false;
            if (i > 0 && i <= questArrowList.Count)
                questArrowList[i - 1].Visible = false;
        }

        leftButtonParent.Visible = currentPage > 0;
        rightButtonParent.Visible = currentPage < maxPage;

        await this.WaitForFrame();
        await this.WaitForFrame();

        if (prevPage > currentPage)
        {
            scrollContainer.ScrollHorizontal = 999999;
        }
        else if (prevPage < currentPage)
        {
            scrollContainer.ScrollHorizontal = 0;
        }
        if (!isQuestOnPage)
            unselectedButton.ButtonPressed = true;
        else
            pressOnComplete?.Press();
    }
}
