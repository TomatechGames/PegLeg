using Godot;
using System;
using System.Drawing;
using System.Linq;

[Tool]
public partial class DynamicGridContainer : Container
{
    [Export(PropertyHint.Range, "1, 10, or_greater")] int minCols = 1;
    [Export] bool compressSpacing;
    [Export] Vector2 spacing;

	bool lockMinSize = false;
	public override Vector2 _GetMinimumSize()
    {
		if(lockMinSize)
			return Vector2.Zero;
        lockMinSize = true;
        var children = GetChildren();
        int visibleChildCount = children.Count(c => (c as Control).Visible);
        if (visibleChildCount == 0)
        {
            lockMinSize = false;
            return Vector2.Zero;
        }

		Vector2 firstChildMinSize = GetChild<Control>(0).GetCombinedMinimumSize();

		int rowCount = CalcGrid(firstChildMinSize, visibleChildCount).Y;

		Vector2 newMinSize = new(
            (firstChildMinSize.X * minCols) + (Mathf.Max(spacing.X, 0) * (minCols - 1)),
            (firstChildMinSize.Y * rowCount) + (spacing.Y * (rowCount - 1))
            );
		lockMinSize = false;
		return newMinSize;
    }

	bool disableSort = false;
	public void SetDisableSort(bool value)
	{
        disableSort = value;
		if (!disableSort)
			_Notification((int)NotificationSortChildren);
    }

	bool lockLayout = false;
	public override void _Notification(int what)
	{
		if (disableSort || lockLayout)
			return;
		if (what == NotificationSortChildren)
        {
            lockLayout = true;
            CustomMinimumSize += Vector2.One * 0.1f;
            CustomMinimumSize -= Vector2.One * 0.1f;
            //GetCombinedMinimumSize();

            var children = GetChildren();
            int visibleChildCount = children.Count(c => (c as Control).Visible);
            if (visibleChildCount == 0)
            {
                lockLayout = false;
                return;
            }

			Vector2 firstChildMinSize = GetChild<Control>(0).GetCombinedMinimumSize();

            var grid = CalcGrid(firstChildMinSize, visibleChildCount);
            int colCount = grid.X;
			Rect2 cellSizeAndSpacing = CalcSpacing(grid, spacing, firstChildMinSize);

            int compressedCols = Mathf.Min(colCount, visibleChildCount);
            Vector2 compressionPadding = new(0, 0);
            if (compressSpacing)
                compressionPadding.X = (Size.X - ((cellSizeAndSpacing.Position.X * (compressedCols - 1)) + (cellSizeAndSpacing.Size.X * compressedCols))) * 0.5f;


            int validIndex = 0;
			foreach (Control c in children)
			{
				if (c is null || !c.IsVisibleInTree())
					continue;
				int row = validIndex / colCount;
				int col = validIndex % colCount;
				FitChildInRect(c, new Rect2(((cellSizeAndSpacing.Position + cellSizeAndSpacing.Size) * new Vector2(col, row)) + compressionPadding, cellSizeAndSpacing.Size));
				validIndex++;
            }
            lockLayout = false;
        }
	}

    public virtual Vector2I CalcGrid(Vector2 childSize, int visibleChildCount)
	{
		Vector2 size = Size;
		if(visibleChildCount==-1)
			visibleChildCount = GetChildren().Count(c=>(c as Control).Visible);

		int colCount = Mathf.Max(Mathf.FloorToInt((size.X + Mathf.Max(spacing.X, 0)) / (childSize.X + Mathf.Max(spacing.X, 0))), minCols);
		int rowCount = Mathf.CeilToInt((float)visibleChildCount / colCount);

		Vector2I newGrid = new(colCount, rowCount);

        return newGrid;
	}

    public virtual Rect2 CalcSpacing(Vector2I grid, Vector2 currentSpacing, Vector2 currentSize)
	{
		Vector2 finalSpacing = currentSpacing;
		Vector2 finalSize = currentSize;

		if(compressSpacing)
            return new(finalSpacing, finalSize);

        if (finalSpacing.X < 0)
		{
			if (grid.X > 1)
				finalSpacing.X = (Size.X - (finalSize.X * grid.X)) / (grid.X - 1);
			else
				finalSpacing.X = 0;
		}
		else
		{
			if (grid.X > 1)
				finalSize.X = (Size.X - (finalSpacing.X * grid.X - 1)) / grid.X;
			else
				finalSize.X = Size.X;
		}

        Rect2 newSpacingRect = new(finalSpacing, finalSize);

        return newSpacingRect;
	}

}
