using Godot;
using System;

public partial class ItemTierController : Node
{
    [Export]
    TextureRect[] tierImages;

    [Export]
    Color disabledColor = Colors.Black;

    [Export]
    Color regularColor = Colors.White;

    [Export]
    Color superchargeColor = Colors.Cyan;

    public void SetMaxTier(int maxTier)
    {
        maxTier = Mathf.Min(maxTier, tierImages.Length);
        for (int i = 0; i < maxTier; i++)
        {
            tierImages[i].Visible = true;
        }
        for (int i = maxTier; i < tierImages.Length; i++)
        {
            tierImages[i].Visible = false;
        }
    }

    public void SetTier(int tier)
    {
        tier = Mathf.Min(tier, tierImages.Length);
        for (int i = 0; i < tier; i++)
        {
            tierImages[i].SelfModulate = regularColor;
        }
        for (int i = tier; i < tierImages.Length; i++)
        {
            tierImages[i].SelfModulate = disabledColor;
        }
    }

    public void SetSuperchargedTier(int superchargedTier)
    {
        superchargedTier = Mathf.Min(superchargedTier, tierImages.Length);
        for (int i = 0; i < superchargedTier; i++)
        {
            tierImages[i].SelfModulate = superchargeColor;
        }
    }
}
