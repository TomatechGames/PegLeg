using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

public partial class CardPackOpener : Control
{
    public static CardPackOpener Instance { get; private set; }
    [Export]
    float pullTime = 0.25f;
    [Export]
    float pullHoldTime = 1;
    [Export]
    float pullFastSpeed = 3;
    [Export]
    bool sortByRarity = false;
    [Export]
    bool choicePacksFirst = true;
    [Export]
    Control clickArea;
    [Export]
    ShaderHook backgroundImage;
    [Export]
    AudioStreamPlayer music;
    [Export]
    Control pullButton;
    [Export]
    Color defaultBackgroundColor;
    [ExportGroup("Supply Crate")]
    [Export]
    AudioStreamPlayer startAudio;
    [Export]
    AudioStreamPlayer fallingAudio;
    [Export]
    AudioStreamPlayer landAudio;
    [Export]
    AudioStreamPlayer radioAudio;
    [Export]
    Control fallingCrate;
    [Export]
    Control landedCrate;
    [Export]
    Control landedCrateBG;
    [Export]
    Control landedCrateBGCards;
    [Export]
    TextureRect landedCrateLid;
    [Export]
    Texture2D lidBasicTexture;
    [Export]
    Texture2D lidFlippingTexture;
    [Export]
    CpuParticles2D confettiParticles;
    [Export]
    Control crateOpenButton;
    [ExportGroup("Cards")]
    [Export]
    GameItemEntry topCard;
    [Export]
    GameItemEntry prevCard;
    [Export]
    Control mainCardsParent;
    [Export]
    Control smallCardsOffset;
    [Export]
    int gapBetweenCards = 25;
    [Export]
    VBoxContainer smallCardParent;
    [Export]
    GameItemEntry[] smallCards = Array.Empty<GameItemEntry>();
    [ExportGroup("Choices")]
    [Export]
    CanvasGroup choiceCanvas;
    [Export]
    GameItemEntry[] choiceCards = Array.Empty<GameItemEntry>();
    [Export]
    ShaderHook choiceResultCard;
    [Export]
    Control choiceResultFGParent;
    [Export]
    Control choiceResultBGParent;
    [Export]
    Control skipChoiceButton;

    float smallOffsetAmount;
    public override void _Ready()
    {
        Instance = this;
        base._Ready();
        VisibilityChanged += async () =>
        {
            if (!Visible)
                return;
            await this.WaitForFrame();
            //smallOffsetAmount = Mathf.FloorToInt(topCard.Size.Y) + smallCardParent.GetThemeConstant("separation");
            smallCardParent.AddThemeConstantOverride("separation", gapBetweenCards-Mathf.FloorToInt(topCard.Size.Y));
            smallOffsetAmount = gapBetweenCards;
            GD.Print($"smallOffset: {smallOffsetAmount} ({Mathf.FloorToInt(topCard.Size.Y)}+{smallCardParent.GetThemeConstant("separation")})");
        };
        if (Visible)
        {
            Visible = false;
        }
        Visible = true;
        clickArea.Visible = false;
        choiceResultCard.Visible = false;
        crateOpenButton.Visible = false;
        backgroundImage.SetShaderFloat(0, "Transparancy");
        choiceCanvas.Scale = Vector2.Zero;
        choiceCanvas.SelfModulate = Colors.Transparent;
    }

    bool supplyCrateActive = false;
    void PlaySupplyAnimation()
    {
        MusicController.StopMusic();
        supplyCrateActive = true;

        startAudio.Play();
        fallingAudio.VolumeDb = -80;
        fallingAudio.Play();
        radioAudio.Stop();
        music.Stop();
        music.VolumeDb = 0;

        topCard.Scale = Vector2.Zero;

        landedCrate.Visible = false;
        landedCrate.GrowVertical = GrowDirection.Both;
        landedCrate.AnchorTop = 0.5f;
        landedCrate.AnchorBottom = 0.5f;
        landedCrate.AnchorLeft = 0.5f;
        landedCrate.AnchorRight = 0.5f;
        landedCrate.OffsetTop = 0;
        landedCrate.OffsetBottom = 0;
        landedCrate.OffsetLeft = 0;
        landedCrate.OffsetRight = 0;
        landedCrate.Scale = fallingCrate.Scale;

        landedCrateLid.SelfModulate = Colors.White;
        landedCrateLid.Texture = lidBasicTexture;
        landedCrateLid.OffsetTop = 0;
        landedCrateLid.OffsetBottom = 0;
        landedCrateLid.OffsetLeft = 0;
        landedCrateLid.OffsetRight = 0;
        landedCrateLid.Visible = false;
        landedCrateLid.Scale = fallingCrate.Scale;

        landedCrateBG.Visible = true;
        landedCrateBGCards.Visible = false;

        fallingCrate.Visible = true;
        fallingCrate.SelfModulate = Colors.Transparent;
        fallingCrate.OffsetBottom = -10000;

        confettiParticles.Restart();
        confettiParticles.Emitting = false;

        smallCardParent.AddThemeConstantOverride("separation", -Mathf.FloorToInt(topCard.Size.Y));

        var fallingTransition = GetTree().CreateTween().SetParallel();
        fallingTransition.TweenProperty(fallingCrate, "self_modulate", Colors.White, 0.25);
        fallingTransition.TweenProperty(fallingAudio, "volume_db", 0, 0.75f).SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Quad);
        fallingTransition.TweenProperty(fallingCrate, "offset_bottom", 0, 0.75f);
        //tween crate image down
        fallingTransition.Finished += () =>
        {
            landedCrate.Visible = true;
            landedCrateLid.Visible = true;
            fallingCrate.Visible = false;
            crateOpenButton.Visible = true;
            fallingAudio.Stop();
            landAudio.Play();
            music.Play();
            radioAudio.Play();
            var bgFade = GetTree().CreateTween().SetParallel().SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Quad);
            bgFade.TweenMethod(Callable.From<float>(val => backgroundImage.SetShaderFloat(val, "Transparancy")), 0f, 1f, 0.5)
                .SetEase(Tween.EaseType.Out);
            backgroundImage.Visible = true;
        };
    }

    void PlaySupplyOpenAnimation()
    {
        radioAudio.Stop();
        landedCrateLid.Texture = lidFlippingTexture;
        crateOpenButton.Visible = false;

        var lidJump = GetTree().CreateTween().SetParallel().SetTrans(Tween.TransitionType.Quad);
        lidJump.TweenProperty(landedCrateLid, "offset_bottom", -100, 0.25).SetEase(Tween.EaseType.Out);
        lidJump.TweenProperty(landedCrateLid, "offset_top", -100, 0.25).SetEase(Tween.EaseType.Out);
        lidJump.Chain().TweenProperty(landedCrateLid, "offset_bottom", 50, 0.24).SetEase(Tween.EaseType.In);
        lidJump.Parallel().TweenProperty(landedCrateLid, "offset_top", 50, 0.24).SetEase(Tween.EaseType.In);

        var lidSlideAmount = GD.RandRange(100, 150) * (GD.Randf() > 0.5 ? 1 : -1);
        var lidSlide = GetTree().CreateTween().SetParallel().SetTrans(Tween.TransitionType.Linear);
        lidSlide.TweenProperty(landedCrateLid, "offset_left", lidSlideAmount, 0.5);
        lidSlide.TweenProperty(landedCrateLid, "offset_right", lidSlideAmount, 0.5);
        lidSlide.TweenProperty(landedCrateLid, "self_modulate", Colors.Transparent, 0.25).SetDelay(0.2f);

        confettiParticles.Restart();

        lidSlide.Finished += () =>
        {
            supplyCrateActive = false;
            GD.Print("inactive");
        };
    }

    void PlaySupplyShrinkAnimation()
    {
        var prevGlobal = landedCrate.GlobalPosition;
        confettiParticles.Emitting = false;
        landedCrate.AnchorTop = 1;
        landedCrate.AnchorBottom = 1;
        landedCrate.GlobalPosition = prevGlobal + ((landedCrate.Size / 2) * (landedCrate.Scale - Vector2.One));
        landedCrate.OffsetBottom -= (landedCrate.Size.Y / 2);
        landedCrate.OffsetTop += (landedCrate.Size.Y / 2);

        var boxSlide = GetTree().CreateTween().SetParallel().SetTrans(Tween.TransitionType.Quint);
        boxSlide.TweenProperty(landedCrate, "offset_left", 225, 0.75).SetEase(Tween.EaseType.InOut).SetTrans(Tween.TransitionType.Quad);
        boxSlide.TweenProperty(landedCrate, "offset_right", 225, 0.75).SetEase(Tween.EaseType.InOut).SetTrans(Tween.TransitionType.Quad);
        boxSlide.TweenProperty(landedCrate, "offset_bottom", 0, 0.75).SetEase(Tween.EaseType.In);
        boxSlide.TweenProperty(landedCrate, "offset_top", 0, 0.75).SetEase(Tween.EaseType.In);
        boxSlide.TweenProperty(landedCrate, "scale", Vector2.One, 0.75).SetEase(Tween.EaseType.In);
    }

    async Task WaitForSupplyCrate()
    {
        while (supplyCrateActive)
        {
            await this.WaitForFrame();
        }
    }

    public async Task StartOpening(ProfileItemHandle[] items)
    {
        if (queuedItems.Count > 0)
            return;
        //bgFade.TweenProperty(backgroundImage, "self_modulate", Colors.White, 0.25f);
        clickArea.Visible = true;
        pullButton.Visible = false;
        backgroundImage.SelfModulate = defaultBackgroundColor;

        //start supply crate animation
        PlaySupplyAnimation();
        await this.WaitForTimer(1.25);

        //step 1: separate the choice cardpacks from the regular ones
        List<ProfileItemHandle> openableCardPacks = new();
        foreach (var item in items)
        {
            if (!(await item.GetItem())["templateId"].ToString().StartsWith("CardPack"))
                queuedItems.Add(item);
            else if ((await item.GetItem())["attributes"].AsObject().ContainsKey("options"))
                queuedItems.Add(item);
            else
                openableCardPacks.Add(item);
        }

        //step 2: send request to open all regular ones
        if (openableCardPacks.Count>0)
        {
            JsonArray cardpacksToOpen = new(default, openableCardPacks.Select(val => (JsonNode)val.uuid).ToArray());
            GD.Print("opening all these cardpacks:\n- " + openableCardPacks.Select(val => val.uuid).ToArray().Join("\n- "));
            JsonObject body = new()
            {
                ["cardPackItemIds"] = cardpacksToOpen
            };
            //LoadingOverlay.Instance.AddLoadingKey("LlamaOpenBulk");
            var resultNotification = (await ProfileRequests.PerformProfileOperation(FnProfiles.AccountItems, "OpenCardPackBatch", body.ToString()))["notifications"][0];
            var resultHandles = resultNotification["lootGranted"]["items"]
                .AsArray()
                .Select(val => ProfileItemHandle.CreateHandleUnsafe(FnProfiles.AccountItems, val["itemGuid"].ToString()))
                .ToList();
            queuedItems.AddRange(resultHandles);
        }


        //step 3: apply sorting
        var ordered = queuedItems.OrderBy(val => true);
        if (choicePacksFirst)
            ordered = ordered.ThenBy(val=> val.GetItemUnsafe()["templateId"].ToString().StartsWith("CardPack"));
        if (sortByRarity)
            ordered = ordered.ThenBy(val => val.GetItemUnsafe().GetTemplate().GetItemRarity());
        queuedItems = ordered.ToList();

        //step 4: display results based on user settings
        await WaitForSupplyCrate();
        PlaySupplyShrinkAnimation();
        await this.WaitForTimer(0.75);

        nextPullIndex = 0;
        SetCardItems(-1);
        smallCardParent.Visible = true;
        var cardsUnfold = GetTree().CreateTween().SetParallel().SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Quart);
        cardsUnfold.TweenMethod(Callable.From<int>(val => smallCardParent.AddThemeConstantOverride("separation", val - Mathf.FloorToInt(topCard.Size.Y))), 0, gapBetweenCards, 0.25f);

        for (int i = 0; i < choiceCards.Length; i++)
        {
            var index = i;
            choiceCards[i].Pressed += () => ApplyChoice(index);
        }

        pullButton.Visible = true;
        landedCrateBG.Visible = false;
        landedCrateBGCards.Visible = true;
    }

    public List<ProfileItemHandle> queuedItems = new();

    int nextPullIndex = 1;
	bool isPulling;
    bool isFast;

    Tween holdTween;
    Tween speedTween;
    public void StartPullCard()
    {
        holdTween = GetTree().CreateTween();
        holdTween.TweenInterval(pullHoldTime);
        holdTween.Finished += () =>
        {
            isFast = true;
            if (!isPulling)
                PullCard();
            TweenTimeScale(pullFastSpeed, pullFastSpeed*2, -10);
            
            //enable fast effects
        };
        if (!isPulling)
            PullCard();
    }
    static readonly Callable setTimeScaleCallable = Callable.From<float>(SetTimeScale);
    static void SetTimeScale(float newVal) => Engine.TimeScale = newVal;
    void TweenTimeScale(float target, float pitch, float vol)
    {
        speedTween?.Kill();
        speedTween = GetTree().CreateTween().SetParallel();
        speedTween.Pause();
        speedTween.TweenMethod(setTimeScaleCallable, Engine.TimeScale, target, 0.5f);
        speedTween.TweenProperty(music, "pitch_scale", pitch, 0.5f).SetTrans(Tween.TransitionType.Linear);
    }

    public void EndPullCard()
    {
        if (holdTween?.IsRunning()??false)
        {
            holdTween.Stop();
        }
        if (isFast)
        {
            isFast = false;
            TweenTimeScale(1, 1, 0);
            //disable fast effects
        }
    }

    public override void _Process(double delta)
    {
        base._Process(delta);
        if (speedTween?.IsValid() ?? false && Engine.TimeScale > 0)
        {
            var unscaledDelta = delta / Engine.TimeScale;
            speedTween.CustomStep(unscaledDelta);
        }
    }

    void SetCardItems(int index)
    {
        if (index > 0)
        {
            //prevCard.SetItemData(new(queuedItems[index-1].GetItemUnsafe()));
            prevCard.LinkProfileItem(queuedItems[index - 1]);
        }
        if (index >= 0 && index < queuedItems.Count)
        {
            //topCard.SetItemData(new(queuedItems[index].GetItemUnsafe()));
            topCard.LinkProfileItem(queuedItems[index]);
        }
        int remainder = Mathf.Max(0, queuedItems.Count - (index + 1));
        int cardCount = Mathf.Min(smallCards.Length, remainder);
        for (int i = 0; i < cardCount; i++)
        {
            //smallCards[i].SetItemData(new(queuedItems[index + i + 1].GetItemUnsafe()));
            smallCards[i].LinkProfileItem(queuedItems[index + i + 1]);
            smallCards[i].Modulate = Colors.White;
        }
        for (int i = cardCount; i < smallCards.Length; i++)
        {
            smallCards[i].Modulate = Colors.Transparent;
        }
    }

    void PullCard()
	{
        if (nextPullIndex>0)
        {
            prevCard.GlobalPosition = topCard.GlobalPosition + (topCard.Size*(topCard.Scale-Vector2.One)*0.5f);
            prevCard.Scale = topCard.Scale;
            prevCard.Rotation = 0;
        }

        if (nextPullIndex < queuedItems.Count)
        {
            topCard.GlobalPosition = smallCards[0].GlobalPosition;
            topCard.Scale = smallCards[0].Scale;

            smallCardsOffset.Position += new Vector2(0, smallOffsetAmount);
        }
        else
        {
            topCard.Scale = Vector2.Zero;
            pullButton.Visible = false;
            EndPullCard();
        }

        SetCardItems(nextPullIndex);

        bool pauseForChoice = false;
        //if current item is cardpack, add delay in fast mode or stop fast mode
        if(nextPullIndex < queuedItems.Count && queuedItems[nextPullIndex].GetItemUnsafe()["templateId"].ToString().StartsWith("CardPack"))
        {
            EndPullCard();
            pauseForChoice = true;
        }

        nextPullIndex++;

        isPulling = true;


        var tweener = GetTree().CreateTween().SetParallel().SetTrans(Tween.TransitionType.Quart);
        float delay = 0;

        if (nextPullIndex <= queuedItems.Count)
        {
            tweener.TweenProperty(topCard, "offset_top", 0, pullTime).SetEase(Tween.EaseType.Out);
            tweener.TweenProperty(topCard, "offset_bottom", 0, pullTime).SetEase(Tween.EaseType.Out);
            tweener.TweenProperty(topCard, "offset_left", 0, pullTime).SetEase(Tween.EaseType.In);
            tweener.TweenProperty(topCard, "offset_right", 0, pullTime).SetEase(Tween.EaseType.In);
            tweener.TweenProperty(topCard, "scale", Vector2.One * 1.5f, pullTime).SetEase(Tween.EaseType.In);

            tweener.TweenProperty(smallCardsOffset, "position:y", 0, pullTime);
            tweener.TweenProperty(backgroundImage, "self_modulate", topCard.LatestRarityColor, pullTime);
            delay = pullTime * 0.75f;
        }
        else
        {
            tweener.TweenProperty(landedCrate, "offset_bottom", 500, pullTime).SetEase(Tween.EaseType.InOut);
            tweener.TweenProperty(landedCrate, "offset_top", 500, pullTime).SetEase(Tween.EaseType.InOut);
            tweener.TweenProperty(backgroundImage, "self_modulate", defaultBackgroundColor, pullTime);
            landedCrateBG.Visible = true;
            landedCrateBGCards.Visible = false;
        }


        if (nextPullIndex > 0)
        {
            tweener.SetTrans(Tween.TransitionType.Quad);
            tweener.TweenProperty(prevCard, "offset_right", -450, pullTime * 0.5f).SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Quart).SetDelay(delay);
            tweener.TweenProperty(prevCard, "offset_top", 1000, pullTime*0.5f).SetEase(Tween.EaseType.In).SetDelay(delay);
            tweener.TweenProperty(prevCard, "scale", Vector2.Zero, pullTime * 0.5f).SetEase(Tween.EaseType.In).SetDelay(delay);
            tweener.TweenProperty(prevCard, "rotation", Mathf.DegToRad(-45), pullTime * 0.5f).SetEase(Tween.EaseType.In).SetDelay(delay);
        }

        tweener.Finished += async () =>
        {
            if (pauseForChoice)
            {
                //open choice pane
                GD.Print("opening choice");
                isPulling = false;
                pullButton.Visible = false;
                OpenChoices();
                return;
            }
            if(nextPullIndex > queuedItems.Count)
            {
                //show results grid
                GD.Print("results");
                if (speedTween?.IsValid() ?? false)
                    await ToSignal(speedTween, "finished");

                await CloseMenu();
                return;
            }
            if (isFast)
            {
                PullCard();
            }
            else
                isPulling = false;
        };

	}

    bool isChosing = false;
    void OpenChoices()
    {
        isChosing = true;
        skipChoiceButton.Visible = true;
        var choiceOpen = GetTree().CreateTween().SetParallel();
        choiceOpen.TweenProperty(choiceCanvas, "scale", Vector2.One, 0.25f).SetEase(Tween.EaseType.Out);
        choiceOpen.TweenProperty(choiceCanvas, "self_modulate", Colors.White, 0.25f);
        
        GD.Print(queuedItems[nextPullIndex - 1].GetItemUnsafe());
        var currentChoices = queuedItems[nextPullIndex - 1].GetItemUnsafe()["attributes"]["options"].AsArray();
        for (int i = 0; i < currentChoices.Count; i++)
        {
            var thisChoice = currentChoices[i];
            BanjoAssets.TryGetTemplate(thisChoice["itemType"].ToString(), out var itemData);
            var itemStack = itemData.CreateInstanceOfItem(thisChoice["quantity"].GetValue<int>(), thisChoice["attributes"].AsObject().Reserialise());
            choiceCards[i].SetItemData(new(itemStack));
            choiceCards[i].GetChild<Control>(0).SelfModulate = Colors.White;
            choiceCards[i].SetInteractable(true);
        }
    }

    async void ApplyChoice(int index)
    {
        if (!isChosing)
            return;
        isChosing = false;
        skipChoiceButton.Visible = false;

        //start the request now and await later, asynchronism baby!
        JsonObject body = new()
        {
            ["cardPackItemId"] = queuedItems[nextPullIndex - 1].uuid,
            ["selectionIdx"] = index
        };
        var operationTask = ProfileRequests.PerformProfileOperation(FnProfiles.AccountItems, "OpenCardPack", body.ToString());

        for (int i = 0; i < choiceCards.Length; i++)
        {
            choiceCards[i].SetInteractable(false);
        }

        var currentChoices = queuedItems[nextPullIndex - 1].GetItemUnsafe()["attributes"]["options"].AsArray();
        var resultChoice = currentChoices[index];
        BanjoAssets.TryGetTemplate(resultChoice["itemType"].ToString(), out var itemData);

        var resultChild = choiceCards[index].GetChild<Control>(0);
        resultChild.SelfModulate = Colors.Transparent;

        choiceResultCard.Visible = true;
        choiceResultCard.Reparent(choiceResultFGParent);
        choiceResultCard.Scale = Vector2.One;
        choiceResultCard.GlobalPosition = resultChild.GlobalPosition;
        choiceResultCard.OffsetLeft += choiceResultCard.Size.X * 0.5f;
        choiceResultCard.OffsetRight = choiceResultCard.OffsetLeft;
        choiceResultCard.OffsetTop += choiceResultCard.Size.Y * 0.5f;
        choiceResultCard.OffsetBottom = choiceResultCard.OffsetTop;
        choiceResultCard.SetShaderTexture(itemData.GetItemTexture(), "IconTexture");
        choiceResultCard.SetShaderColor(itemData.GetItemRarityColor(), "RarityColour");
        choiceResultCard.SetShaderBool(false, "Started");


        var choiceClose = GetTree().CreateTween().SetParallel();
        choiceClose.TweenProperty(choiceCanvas, "self_modulate", Colors.Transparent, 0.25);
        choiceClose.TweenProperty(choiceCanvas, "scale", Vector2.Zero, 0.25).SetEase(Tween.EaseType.In);

        var cardSlideUp = GetTree().CreateTween().SetParallel().SetTrans(Tween.TransitionType.Quad);
        cardSlideUp.TweenProperty(choiceResultCard, "offset_left",      60,   0.5);
        cardSlideUp.TweenProperty(choiceResultCard, "offset_right",     60,   0.5);
        cardSlideUp.TweenProperty(choiceResultCard, "offset_top",       -250,     0.5);
        cardSlideUp.TweenProperty(choiceResultCard, "offset_bottom",    -250,     0.5);
        cardSlideUp.TweenProperty(choiceResultCard, "scale", Vector2.One * 0.5f, 0.5);

        await this.WaitForTimer(0.15f);

        choiceResultCard.SetShaderBool(true, "Started");
        choiceResultCard.SetShaderFloat((float)(Time.GetTicksMsec()*0.001)-0.5f, "StartTime");

        await this.WaitForTimer(0.6f);


        choiceResultCard.Reparent(choiceResultBGParent);
        choiceResultCard.OffsetLeft = 60;
        choiceResultCard.OffsetRight = choiceResultCard.OffsetLeft;
        choiceResultCard.OffsetTop = -250;
        choiceResultCard.OffsetBottom = choiceResultCard.OffsetTop;


        var cardSlideDown = GetTree().CreateTween().SetParallel().SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.In);
        cardSlideDown.TweenProperty(choiceResultCard, "offset_top", -115, 0.25);
        cardSlideDown.TweenProperty(choiceResultCard, "offset_bottom", -115, 0.25);

        await this.WaitForTimer(0.25f);
        choiceResultCard.Visible = false;

        //var itemStack = itemData.CreateInstanceOfItem(resultChoice["quantity"].GetValue<int>(), resultChoice["attributes"].AsObject().Reserialise());
        //topCard.SetItemData(new(itemStack));
        //prevCard.SetItemData(new(itemStack));

        //wait for choice request

        //LoadingOverlay.Instance.AddLoadingKey("LlamaOpenBulk");
        var resultNotification = (await operationTask)["notifications"][0];
        var resultItem = resultNotification["lootGranted"]["items"][0];
        await queuedItems[nextPullIndex - 1].ReplaceWith(resultItem["itemProfile"].ToString(),resultItem["itemGuid"].ToString());
        GD.Print(resultNotification);

        pullButton.Visible = true;
    }

    public void SkipChoice()
    {
        if (!isChosing)
            return;
        isChosing = false;

        for (int i = 0; i < choiceCards.Length; i++)
        {
            choiceCards[i].SetInteractable(false);
        }

        var choiceClose = GetTree().CreateTween().SetParallel();
        choiceClose.TweenProperty(choiceCanvas, "self_modulate", Colors.Transparent, 0.25);
        choiceClose.TweenProperty(choiceCanvas, "scale", Vector2.Zero, 0.25).SetEase(Tween.EaseType.In);

        skipChoiceButton.Visible = false;
        pullButton.Visible = true;
        PullCard();
    }

    async Task CloseMenu()
    {
        MusicController.ResumeMusic(1);
        var exitAnim = GetTree().CreateTween().SetParallel();
        //bgFade.TweenProperty(backgroundImage, "self_modulate", Colors.Transparent, 0.25f);
        exitAnim.TweenMethod(Callable.From<float>(val => backgroundImage.SetShaderFloat(val, "Transparancy")), 1f, 0f, 0.25)
            .SetTrans(Tween.TransitionType.Quad)
            .SetEase(Tween.EaseType.In);
        exitAnim.TweenProperty(music, "volume_db", -80, 1)
            .SetTrans(Tween.TransitionType.Expo)
            .SetEase(Tween.EaseType.In);

        await this.WaitForTimer(0.25f);
        queuedItems.Clear();
        clickArea.Visible = false;
        isPulling = false;

    }
}
