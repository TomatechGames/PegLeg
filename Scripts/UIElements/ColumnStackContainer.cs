using Godot;
using System;
using System.Linq;

[Tool]
public partial class ColumnStackContainer : Container
{
    [Export]
    int columns = 3;
    [Export]
    Vector2 spacing = new(5, 5);

    public override Vector2 _GetMinimumSize()
    {
        float[] heights = new float[columns];
        var children = GetChildren().Cast<Control>();
        foreach (var item in children)
        {
            if (!item.Visible)
                continue;
            int thisIndex = GetSmallestIndex(heights);
            bool isFirst = heights[thisIndex] == 0;
            heights[thisIndex] += item.GetCombinedMinimumSize().Y + (isFirst ? 0 : spacing.Y);
        }
        return new(0, heights.Max());
    }

    static int GetSmallestIndex<T>(T[] heightArray) where T : IComparable<T>
    {
        if (heightArray.Length == 0)
            return 0;
        int currentSmallest = 0;
        for (int i = 1; i < heightArray.Length; i++)
        {
            if (heightArray[currentSmallest].CompareTo(heightArray[i]) > 0)
                currentSmallest = i;
        }
        return currentSmallest;
    }

    public override void _Notification(int what)
    {
        if (what == NotificationSortChildren)
        {
            float cellWidth = (Size.X - (spacing.X * (columns - 1))) / columns;
            float[] heights = new float[columns];
            int[] childCounts = new int[columns];
            var children = GetChildren().Cast<Control>();
            foreach (var item in children)
            {
                if (!item.Visible)
                    continue;
                int thisIndex = GetSmallestIndex(heights);
                bool isFirst = childCounts[thisIndex] == 0;
                childCounts[thisIndex]++;

                item.Position = new(
                    (cellWidth + spacing.X) * thisIndex,
                    heights[thisIndex] + (isFirst ? 0 : spacing.Y)
                    );

                item.CustomMinimumSize = new(
                    cellWidth,
                    item.CustomMinimumSize.Y
                    );
                item.Size = item.GetCombinedMinimumSize();

                heights[thisIndex] += item.Size.Y + (isFirst ? 0 : spacing.Y);
            }
        }
    }
}
