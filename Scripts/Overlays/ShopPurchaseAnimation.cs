using Godot;
using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

public partial class ShopPurchaseAnimation : Control
{
    static ShopPurchaseAnimation instance;

    [Export]
    PackedScene cartItem;

    [Export]
    ModalWindow modalWindow;
    [Export]
    Control cartScaleNode;
    [Export]
    Control cartRotationNode;
    [Export]
    Control itemParent;
    [Export]
    Control finalText;

    [Export]
    float itemFallOffset = 100;
    [Export]
    float cartGrowDuration = 0.5f;
    [Export]
    float itemFallDurationSingle = 0.3f;
    [Export]
    float itemFallDurationTotal = 0.75f;
    [Export]
    float cartLeaveDuration = 0.5f;
    [Export]
    float textEnterDelay = 0.2f;
    [Export]
    float textStayDuration = 0.6f;
    [Export]
    float textLeaveDuration = 0.3f;

    public override void _Ready()
    {
        cartScaleNode.Scale = Vector2.Zero;
        instance = this;
    }

    public static void PlayAnimation(Texture2D itemTexture, int count, Task holdUntil=null) =>
        instance?.PlayAnimationInst(itemTexture, count, holdUntil);

    bool lockAnimation = false;
    async void PlayAnimationInst(Texture2D itemTexture, int count, Task holdUntil)
    {
        if (lockAnimation)
            return;
        lockAnimation = true;
        //too many items will cause issues
        count = Mathf.Min(count, 40);
        bool fastAnimations = AppConfig.Get("misc", "fastAnimations", false);
        GD.Print(fastAnimations);
        modalWindow.SetWindowOpen(true);
        cartRotationNode.Modulate = Colors.White;
        cartRotationNode.RotationDegrees = 0;
        cartRotationNode.OffsetLeft = 0;
        cartRotationNode.OffsetRight = 0;
        cartScaleNode.Scale = Vector2.Zero;
        finalText.Scale = Vector2.Zero;

        List<TextureRect> animatableItems = new();
        for (int i = itemParent.GetChildCount(); i < count; i++)
        {
            itemParent.AddChild(cartItem.Instantiate());
        }
        for (int i = 0; i < count; i++)
        {
            var item = itemParent.GetChild<Control>(i);
            item.Visible = true;
            var texture = item.GetChild<TextureRect>(0);
            texture.SelfModulate = Colors.Transparent;
            texture.OffsetBottom = -itemFallOffset;
            texture.Texture = itemTexture;
            if (i % 20 >= 10)
                texture.OffsetLeft = 10;
            animatableItems.Add(texture);
        }
        for (int i = count; i < itemParent.GetChildCount(); i++)
        {
            itemParent.GetChild<Control>(i).Visible = false;
        }

        if (!fastAnimations)
        {
            var cartScaleTween = GetTree().CreateTween().SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Back);
            cartScaleTween.TweenProperty(cartScaleNode, "scale", Vector2.One, cartGrowDuration);

            await Helpers.WaitForTimer(cartGrowDuration * 0.25);

            await AnimateItems(animatableItems);

            var cartRotationTween = GetTree().CreateTween().SetTrans(Tween.TransitionType.Cubic).SetParallel();
            cartRotationTween.TweenProperty(cartRotationNode, "rotation_degrees", -30, cartLeaveDuration).SetEase(Tween.EaseType.Out);
            cartRotationTween.TweenProperty(cartRotationNode, "position:x", 300, cartLeaveDuration).SetEase(Tween.EaseType.In);
            cartRotationTween.TweenProperty(cartRotationNode, "modulate", Colors.Transparent, cartLeaveDuration).SetEase(Tween.EaseType.In);

            await Helpers.WaitForTimer(textEnterDelay);
        }
        finalText.RotationDegrees = -270;
        var textAppearTween = GetTree().CreateTween().SetParallel().SetTrans(Tween.TransitionType.Cubic);
        textAppearTween.TweenProperty(finalText, "rotation_degrees", 0, cartLeaveDuration);
        textAppearTween.TweenProperty(finalText, "scale", Vector2.One, cartLeaveDuration);

        await Helpers.WaitForTimer(cartLeaveDuration + textStayDuration);

        if(holdUntil is not null)
        {
            await Task.WhenAny(
                holdUntil,
                Helpers.WaitForTimer(10)
            );
        }

        var textLeaveTween = GetTree().CreateTween().SetParallel();
        textLeaveTween.TweenProperty(finalText, "scale", Vector2.Zero, textLeaveDuration).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.In);

        await Helpers.WaitForTimer(textLeaveDuration);

        modalWindow.SetWindowOpen(false);
        lockAnimation = false;
    }

    async Task AnimateItems(List<TextureRect> animatableItems)
    {
        float timeBetweenItems = (itemFallDurationTotal - itemFallDurationSingle) / (animatableItems.Count+1);
        for (int i = 0; i < animatableItems.Count; i++)
        {
            await Helpers.WaitForTimer(timeBetweenItems * (i == 0 ? 0.5f : 1));
            var texture = animatableItems[^(i + 1)];
            var itemFallTween = GetTree().CreateTween().SetParallel();
            itemFallTween.TweenProperty(texture, "self_modulate", Colors.White, itemFallDurationSingle * 0.75);
            itemFallTween.TweenProperty(texture, "offset_bottom", 0, itemFallDurationSingle)
                .SetEase(Tween.EaseType.Out)
                .SetTrans(Tween.TransitionType.Bounce);
        }
        await Helpers.WaitForTimer(itemFallDurationSingle+(timeBetweenItems * 0.5));
    }
}
