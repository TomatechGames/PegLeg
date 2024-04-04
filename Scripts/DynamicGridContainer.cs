using Godot;
using System;
using System.Linq;

[Tool]
public partial class DynamicGridContainer : Container
{
	[Export] int minCols;
    [Export] bool compressSpacing;
    [Export] Vector2 spacing;

	public override Vector2 _GetMinimumSize()
    {
		//if horizontal shrink and expand, try fit as many as possible
        var children = GetChildren();
        int visibleChildCount = children.Count(c => (c as Control).Visible);
        if (visibleChildCount == 0)
            return Vector2.Zero;
		Vector2 firstChildMinSize = GetChild<Control>(0).GetCombinedMinimumSize();
		int rowCount = CalcGrid(visibleChildCount).Y;
		Vector2 newMinSize = new(
            (firstChildMinSize.X * minCols) + (Mathf.Max(spacing.X, 0) * (minCols - 1)),
            (firstChildMinSize.Y * rowCount) + (spacing.Y * (rowCount - 1))
            );
		return newMinSize;
    }

	bool disableSort = false;
	public void SetDisableSort(bool value)
	{
        disableSort = value;
		if (!disableSort)
			_Notification((int)NotificationSortChildren);
    }

	public override void _Notification(int what)
	{
		if (disableSort)
			return;
		if (what == NotificationSortChildren)
        {
			CustomMinimumSize = _GetMinimumSize();

            var children = GetChildren();
            int visibleChildCount = children.Count(c => (c as Control).Visible);
            if (visibleChildCount == 0)
				return;

			var grid = CalcGrid(visibleChildCount);
            int colCount = grid.X;
			Rect2 spacing = CalcSpacing(grid);

			int compressedCols = Mathf.Min(colCount, visibleChildCount);
			Vector2 compressionPadding = new(0,0);
			if (compressSpacing)
				compressionPadding.X = (Size.X - ((spacing.Position.X * (compressedCols - 1)) + (spacing.Size.X * compressedCols))) * 0.5f;

			int validIndex = 0;
			foreach (Control c in children)
			{
				if (c is null || !c.IsVisibleInTree())
					continue;
				int row = validIndex / colCount;
				int col = validIndex % colCount;
				FitChildInRect(c, new Rect2(((spacing.Position + spacing.Size) * new Vector2(col, row)) + compressionPadding, spacing.Size));
				validIndex++;
			}
		}
	}

	Vector2I CalcGrid(int visibleChildCount = -1)
	{
		Vector2 size = Size;
		Vector2 firstChildMinSize = GetChild<Control>(0).GetCombinedMinimumSize();
		if(visibleChildCount==-1)
			visibleChildCount = GetChildren().Count(c=>(c as Control).Visible);
		int colCount = Mathf.Max(Mathf.FloorToInt((size.X + Mathf.Max(spacing.X, 0)) / (firstChildMinSize.X + Mathf.Max(spacing.X, 0))), minCols);
		int rowCount = Mathf.CeilToInt((float)visibleChildCount / colCount);

		return new(colCount, rowCount);
	}

	Rect2 CalcSpacing(Vector2I grid)
	{
		Vector2 finalSpacing = spacing;
		Vector2 finalSize = GetChild<Control>(0).GetCombinedMinimumSize();

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
		return new(finalSpacing, finalSize);
	}
}
