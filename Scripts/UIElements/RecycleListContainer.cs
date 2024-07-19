using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class RecycleListContainer : ScrollContainer
{
    [Export]
    float elementSpace;
    [Export]
    DynamicGridContainer linkedGrid;
    [Export]
    Control offsetControl;
    [Export]
    Control paddingControl;
    [Export]
    Control elementParent;
    [Export]
    PackedScene elementScene;

    IRecyclableEntry basis;
    Vector2 basisSize;
    Queue<IRecyclableEntry> pooledEntries = new();
    Dictionary<int, IRecyclableEntry> activeEntries = new();
    IRecyclableElementProvider linkedProvider;

    public override async void _Ready()
    {
        base._Ready();

        basis = elementScene.Instantiate<IRecyclableEntry>();
        await this.WaitForFrame();
        basis.node.Visible = false;
        basis.node.Size = Vector2.Zero;
        basisSize = basis.GetBasisSize();
        GD.Print($"basis {basisSize} initialised from {basis.node.Name}");

        //SetProvider(new TestRecyclableElementProvider());
    }

    public void SetProvider(IRecyclableElementProvider provider)
    {
        linkedProvider = provider;
        for (int i = 0; i < activeEntries.Count; i++)
        {
            activeEntries[i].LinkRecyclableElementProvider(provider);
        }
        UpdateList(true);
    }

    int lastStartingIndex = 0;
    int lastOnScreenElements = 0;
    bool lockList = false;
    public void UpdateList(bool force = false)
    {
        if (linkedProvider is null || !Visible || lockList)
            return;
        lockList = true;
        try
        {
            var elementSize = basis.GetBasisSize();
            float elementHeight = elementSize.Y;
            int columns = linkedGrid?.CalcGrid(elementSize, 1).X ?? 1;

            //calculate how many elements fit on screen (cols*(ceil(heightOfElement/heightOfThis)+1))
            int totalElements = linkedProvider.GetRecycleElementCount();
            int newOnScreenElements = columns * (Mathf.CeilToInt(Size.Y / (elementHeight + elementSpace)) + 1);

            //use scrollVertical and elementParent y pos to get the offset height
            float offsetHeight = (GlobalPosition.Y - elementParent.GlobalPosition.Y) + offsetControl.Size.Y;
            int fakeElements = totalElements - newOnScreenElements;
            int maxFakeRows = Mathf.Max(Mathf.CeilToInt((float)fakeElements / columns), 0);
            int startingRow = Mathf.Clamp(Mathf.FloorToInt(offsetHeight / (elementHeight + elementSpace)), 0, maxFakeRows);
            offsetHeight = startingRow * (elementHeight + elementSpace);
            //GD.Print($"offsetCols: {offsetHeight / (elementHeight + elementSpace)}");
            offsetControl.CustomMinimumSize = new(0, offsetHeight);

            //set the indexes of the elements
            int newStartingIndex = startingRow * columns;
            if (lastStartingIndex != newStartingIndex || lastOnScreenElements != newOnScreenElements || force)
            {
                //GD.Print($"relinking ({lastStartingIndex}->{newStartingIndex}) ({lastOnScreenElements}->{newOnScreenElements}) (force:{force})");
                //GD.Print($"activeEntries: {activeEntries.Count}");
                //GD.Print($"basis size: ({elementSize.X}x{elementSize.Y}) collumns: {columns}");
                //GD.Print($"visible area: ({Size.X}x{Size.Y})");
                if (activeEntries.Count > lastOnScreenElements)
                    GD.PushWarning("what happen?");
                int lastEndIndex = Mathf.Min(lastStartingIndex + lastOnScreenElements, totalElements);
                int newEndIndex = Mathf.Min(newStartingIndex + newOnScreenElements, totalElements);

                if (lastStartingIndex>=newEndIndex || lastEndIndex<=newStartingIndex)
                {
                    force = true;
                }

                if (force)
                {
                    //complete clear and relink
                    foreach (var item in activeEntries)
                    {
                        pooledEntries.Enqueue(item.Value);
                        item.Value.node.Visible = false;
                    }
                    activeEntries.Clear();
                    for (int i = newStartingIndex; i < newEndIndex; i++)
                    {
                        //GD.Print("force adding " + i);
                        var item = pooledEntries.Count > 0 ? pooledEntries.Dequeue() : GenerateNewPoolEntry();
                        activeEntries[i] = item;
                        activeEntries[i].SetRecycleIndex(i);
                        activeEntries[i].node.Visible = true;
                        activeEntries[i].node.MoveToFront();
                    }
                }
                else
                {
                    if (lastStartingIndex <= newStartingIndex)
                    {
                        //collect elements that went offscreen
                        for (int i = lastStartingIndex; i < newStartingIndex; i++)
                        {
                            //GD.Print("removing " + i);
                            var item = activeEntries[i];
                            pooledEntries.Enqueue(item);
                            activeEntries.Remove(i);
                            item.node.Visible = false;
                        }
                    }
                    if (lastEndIndex >= newEndIndex)
                    {
                        //collect elements that went offscreen
                        for (int i = newEndIndex; i < lastEndIndex; i++)
                        {
                            //GD.Print("removing " + i);
                            var item = activeEntries[i];
                            pooledEntries.Enqueue(item);
                            activeEntries.Remove(i);
                            item.node.Visible = false;
                        }
                    }

                    if (lastStartingIndex >= newStartingIndex)
                    {
                        //deploy elements and send them to back
                        for (int i = lastStartingIndex - 1; i > newStartingIndex - 1; i--)
                        {
                            //GD.Print("adding " + i);
                            var item = pooledEntries.Count > 0 ? pooledEntries.Dequeue() : GenerateNewPoolEntry();
                            activeEntries[i] = item;
                            activeEntries[i].SetRecycleIndex(i);
                            activeEntries[i].node.Visible = true;
                            elementParent.MoveChild(activeEntries[i].node, 0);
                        }
                    }
                    if (lastEndIndex <= newEndIndex)
                    {
                        //deploy elements and send them to front
                        for (int i = lastEndIndex; i < newEndIndex; i++)
                        {
                            //GD.Print("adding " + i);
                            var item = pooledEntries.Count > 0 ? pooledEntries.Dequeue() : GenerateNewPoolEntry();
                            activeEntries[i] = item;
                            activeEntries[i].SetRecycleIndex(i);
                            activeEntries[i].node.Visible = true;
                            activeEntries[i].node.MoveToFront();
                        }
                    }
                }

                lastStartingIndex = newStartingIndex;
                lastOnScreenElements = newOnScreenElements;

            }

            //pad out the remaining space

            int remainingElements = totalElements - (newStartingIndex + newOnScreenElements);
            int remainingRows = Mathf.Max(Mathf.CeilToInt((float)remainingElements / columns), 0);
            float paddingHeight = remainingRows * (elementHeight + elementSpace);
            //GD.Print($"paddingCols: {paddingHeight / (elementHeight+elementSpace)}");
            paddingControl.CustomMinimumSize = new(0, paddingHeight);
        }
        finally
        {
            lockList = false;   
        }
    }

    IRecyclableEntry GenerateNewPoolEntry()
    {
        var spawnedEntry = elementScene.Instantiate<IRecyclableEntry>();
        spawnedEntry.LinkRecyclableElementProvider(linkedProvider);
        elementParent.AddChild(spawnedEntry.node);
        return spawnedEntry;
    }

    int lastScroll;
    Vector2 lastSize;
    public override void _Process(double delta)
    {
        if (lastSize!=Size)
            UpdateList();
        lastSize = Size;
        if (lastScroll != ScrollVertical)
            UpdateList();
        lastScroll = ScrollVertical;
    }
}

public class TestRecyclableElementProvider : IRecyclableElementProvider
{
    public int GetRecycleElementCount()
    {
        return 25;
    }
}

public interface IRecyclableEntry
{
    public Control node { get; }
    public void LinkRecyclableElementProvider(IRecyclableElementProvider provider);

    public Vector2 GetBasisSize() => node.Size;

    public void SetRecycleIndex(int index);
}

public interface IRecyclableElementProvider 
{
    public int GetRecycleElementCount();
}

public interface IRecyclableElementProvider<T> : IRecyclableElementProvider
{
    public T GetRecycleElement(int index);

    public void OnElementSelected(int index) { }
}
