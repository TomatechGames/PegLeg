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
    Control displayPanel;
    [Export]
    AudioStreamPlayer music;
    [Export]
    Control pullButton;
    [Export]
    Color defaultBackgroundColor;
    [Export]
    Control glowFlare;

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
    Control crateOpenButton;

    [ExportGroup("Llama")]
    [Export]
    int minLlamaImpacts = 3;
    [Export]
    int impactParticlePool;
    [ExportSubgroup("Sounds")]
    [Export]
    AudioStreamPlayer[] greetingEffects;
    [Export]
    AudioStreamPlayer[] greetingVoices;
    [Export]
    AudioStreamPlayer hoverEffect;
    [Export]
    AudioStreamPlayer impactEffect;
    [Export]
    AudioStreamPlayer impactVoice;
    [Export]
    AudioStreamPlayer miniImpactVoice;
    [Export]
    AudioStreamPlayer burstEffect;
    [Export]
    AudioStreamPlayer burstVoice;
    [Export]
    AudioStreamPlayer miniBurstVoice;
    [ExportSubgroup("Nodes")]
    [Export]
    PackedScene impactParticlesScene;
    [Export]
    Control impactParticleParent;
    [Export]
    Control llamaGlow;
    [Export]
    Control standardLlamaButton;
    [Export]
    Control smallLlamaButton;
    [Export]
    CpuParticles2D confettiParticles;
    [Export]
    LlamaEntry llamaEntry;
    [Export]
    Control fullLlama;
    [Export]
    Control standardLlamaPartParent;
    Control[] standardLlamaParts;
    [Export]
    Control smallLlamaPartParent;
    Control[] smallLlamaParts;

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

    ShaderHook cardChangeEffect;
    bool isOpen;
    bool isSmall;
    bool cardPacksPrepared;
    bool llamaBurstComplate;
    int llamaHits;
    Control fromPanel;
    JsonObject defaultLlamaItem = BanjoAssets.CreateInstanceOfItem(BanjoAssets.TryGetTemplate("CardPack:cardpack_bronze"));
    Control[] impactParticleContainers;
    CpuParticles2D[] impactParticles;
    int nextPullIndex = 1;
    bool choicesOnly;
    bool isPulling;
    bool isFast;


    public List<ProfileItemHandle> queuedChoices = new();
    public List<ProfileItemHandle> queuedItemHandles = new();
    public List<JsonObject> queuedItems = new();
    int TotalQueueLength => queuedChoices.Count + (choicesOnly ? 0 : queuedItems.Count);

    public override void _Ready()
    {
        Instance = this;
        standardLlamaParts = standardLlamaPartParent.GetChildren().Select(n => n as Control).ToArray();
        smallLlamaParts = smallLlamaPartParent.GetChildren().Select(n => n as Control).ToArray();
        cardChangeEffect = topCard.GetNode<ShaderHook>("%ChangeEffect");
        Visible = true;
        displayPanel.Visible = false;
        choiceResultCard.Visible = false;
        crateOpenButton.Visible = false;
        displayPanel.Visible = false;
        choiceCanvas.Scale = Vector2.Zero;
        choiceCanvas.SelfModulate = Colors.Transparent;
        ProcessMode = ProcessModeEnum.Disabled;

        impactParticlePool = Mathf.Max(impactParticlePool, 1);
        impactParticles = new CpuParticles2D[impactParticlePool];
        impactParticleContainers = new Control[impactParticlePool];
        for (int i = 0; i < impactParticlePool; i++)
        {
            var impactParticleContainer = impactParticlesScene.Instantiate() as Control;
            impactParticleParent.AddChild(impactParticleContainer);
            impactParticleContainers[i] = impactParticleContainer;
            impactParticles[i] = impactParticleContainer.GetChild<CpuParticles2D>(0);
            int index = i;
            llamaEntry.GradientChanged += g => impactParticles[index].ColorInitialRamp = g;
        }
    }

    public async Task StartOpening(ProfileItemHandle[] cardPacks, Control fromPanel, JsonObject llamaItem = null, JsonObject shopPurchaseBody = null, bool skipReveal = false)
    {
        if (isOpen)
        {
            GD.Print("Still Open");
            return;
        }
        ProcessMode = ProcessModeEnum.Inherit;
        isOpen = true;
        //await this.WaitForFrame();

        llamaHits = 0;
        llamaItem ??= defaultLlamaItem;
        llamaEntry.SetItemData(llamaItem);
        GD.Print("Tier: "+llamaEntry.LlamaTier);
        //bgFade.TweenProperty(backgroundImage, "self_modulate", Colors.White, 0.25f);
        displayPanel.Visible = true;
        pullButton.Visible = false;
        cardPacksPrepared = false;
        llamaBurstComplate = false;
        this.fromPanel = fromPanel;
        glowFlare.Scale = Vector2.Zero;
        fullLlama.Visible = true;
        fullLlama.Scale = Vector2.One;
        LlamaScale(false);
        smallLlamaPartParent.Visible = false;
        standardLlamaPartParent.Visible = false;
        smallLlamaButton.Visible = false;
        standardLlamaButton.Visible = false;

        topCard.Scale = Vector2.Zero;
        confettiParticles.Restart();
        confettiParticles.Emitting = false;
        for (int i = 0; i < impactParticles.Length; i++)
        {
            var particles = impactParticles[i];
            if (particles is null)
                continue;
            particles.Restart();
            particles.Emitting = false;
        }

        //start llama animation
        ResizePanelOpen();
        await this.WaitForTimer(0.31);

        cardPacks ??= Array.Empty<ProfileItemHandle>();

        JsonObject[] extraItems = null;
        ProfileItemHandle[] extraItemHandles = null;
        ProfileItemHandle[] extraCardPacks = null;

        if (shopPurchaseBody is not null)
        {
            var shopResult = await ProfileRequests.PerformProfileOperation(FnProfileTypes.Common, "PurchaseCatalogEntry", shopPurchaseBody.ToString());
            var shopResultItems = shopResult["notifications"]
                .AsArray()
                .First(val => val["type"].ToString() == "CatalogPurchase")["lootResult"]["items"]
                    .AsArray()
                    .Select(var => var.AsObject()
                    )
                .ToArray();

            var extraItemData = shopResultItems
                .Where(val => val?["itemGuid"] is not null && !(val["itemType"]?.ToString().StartsWith("CardPack") ?? false));

            extraItems = extraItemData
                    .Select(val => BanjoAssets.CreateInstanceOfItem(
                        BanjoAssets.TryGetTemplate(val["itemType"].ToString()),
                        (int)val["quantity"],
                        val["attributes"]?.Reserialise().AsObject(),
                        val["itemProfile"] + ":" + val["itemGuid"]
                        ))
                    .ToArray();

            extraItemHandles = extraItemData
                .Where(val => (int)val["quantity"] == 1)
                .Select(val => ProfileItemHandle.CreateHandleUnsafe(new(LoginRequests.AccountID, val["itemProfile"].ToString(), val["itemGuid"].ToString())))
                .ToArray();

            extraCardPacks = shopResultItems
                    .Where(val => val.AsObject().ContainsKey("itemGuid") && (val["itemType"]?.ToString().StartsWith("CardPack") ?? false))
                    .Select(val => ProfileItemHandle.CreateHandleUnsafe(new(LoginRequests.AccountID, val["itemProfile"].ToString(), val["itemGuid"].ToString())))
                    .ToArray();
        }
        extraItems ??= Array.Empty<JsonObject>();
        extraItemHandles ??= Array.Empty<ProfileItemHandle>();
        extraCardPacks ??= Array.Empty<ProfileItemHandle>();
        extraCardPacks = extraCardPacks.Union(cardPacks).ToArray();

        //step 1: separate the choice cardpacks from the regular ones
        List<ProfileItemHandle> openableCardPacks = new();
        foreach (var item in extraCardPacks)
        {
            if (!(await item.GetItem())["attributes"].AsObject().ContainsKey("options"))
                openableCardPacks.Add(item);
        }
        extraCardPacks = extraCardPacks.Except(openableCardPacks).ToArray();

        //step 2: send request to open all regular ones
        if (openableCardPacks.Count>0)
        {
            JsonArray cardpacksToOpen = new(default, openableCardPacks.Select(val => (JsonNode)val.itemID.uuid).ToArray());
            GD.Print("opening all these cardpacks:\n- " + openableCardPacks.Select(val => val.itemID.uuid).ToArray().Join("\n- "));
            JsonObject body = new()
            {
                ["cardPackItemIds"] = cardpacksToOpen
            };
            //LoadingOverlay.Instance.AddLoadingKey("LlamaOpenBulk");

            //TODO: handle errors
            //TODO: merge amounts of identical item stacks
            var resultNotification = (await ProfileRequests.PerformProfileOperation(FnProfileTypes.AccountItems, "OpenCardPackBatch", body.ToString()))["notifications"][0];
            
            var resultItemData = resultNotification["lootGranted"]["items"].AsArray()
                .Where(val => val?["itemGuid"] is not null && !(val["itemType"]?.ToString().StartsWith("CardPack") ?? false));

            var resultItems = resultItemData
                .Select(val => BanjoAssets.CreateInstanceOfItem(
                    BanjoAssets.TryGetTemplate(val["itemType"].ToString()),
                    (int)val["quantity"],
                    val["attributes"]?.Reserialise().AsObject() ??
                        ProfileRequests.GetCachedProfileItemInstance(new(LoginRequests.AccountID, val["itemProfile"].ToString(), val["itemGuid"].ToString()))
                        ["attributes"]?.Reserialise().AsObject(),
                    val["itemProfile"] + ":" + val["itemGuid"]
                    ))
                .ToArray();

            var resultItemHandles = resultItemData
                .Where(val => (int)val["quantity"]==1)
                .Select(val => ProfileItemHandle.CreateHandleUnsafe(new(LoginRequests.AccountID, val["itemProfile"].ToString(), val["itemGuid"].ToString())))
                .ToArray();

            var resultCardPacks = resultNotification["lootGranted"]["items"].AsArray()
                .Where(val => val.AsObject().ContainsKey("itemGuid") && (val["itemType"]?.ToString().StartsWith("CardPack") ?? false))
                .Select(val => ProfileItemHandle.CreateHandleUnsafe(new(LoginRequests.AccountID, val["itemProfile"].ToString(), val["itemGuid"].ToString())))
                .ToArray();
            GD.Print("LlamaResult: "+ resultNotification["lootGranted"]["items"].ToString());

            var exceptions = resultNotification["lootGranted"]["items"]
                .AsArray()
                .Where(val => !val.AsObject().ContainsKey("itemGuid"))
                .ToArray();
            if (exceptions.Length > 0)
                GD.Print("Exception: " + exceptions[0]);

            queuedChoices.AddRange(resultCardPacks);
            queuedItemHandles.AddRange(resultItemHandles);
            queuedItems.AddRange(resultItems);
        }

        queuedChoices.AddRange(extraCardPacks);
        queuedItemHandles.AddRange(extraItemHandles);
        queuedItems.AddRange(extraItems);
        cardPacksPrepared = true;

        //step 2.5: wait for user to proceed
        await WaitForCardPackBurst();
        GD.Print("wait complete");

        if (!IsInsideTree() || !isOpen)
            return;
        GD.Print("phew");


        //step 3: apply sorting
        if (sortByRarity)
        {
            var orderedChoices = queuedChoices.OrderBy(val => val.GetItemUnsafe().GetTemplate().GetItemRarity());
            queuedChoices = orderedChoices.ToList();

            var orderedItems = queuedItems.OrderBy(val => val.GetTemplate().GetItemRarity());
            queuedItems = orderedItems.ToList();
        }

        //step 4: display results based on user settings

        choicesOnly = skipReveal;
        if(skipReveal && queuedChoices.Count == 0)
        {
            await ShowRecyclePopup();
            CloseMenu();
            return;
        }

        nextPullIndex = 0;
        SetCardItems(-1);
        smallCardParent.Visible = true;
        var cardsUnfold = GetTree().CreateTween().SetParallel().SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Quart);
        cardsUnfold.TweenProperty(this, "CurrentCardSeparation", gapBetweenCards, 0.25f);

        for (int i = 0; i < choiceCards.Length; i++)
        {
            var index = i;
            choiceCards[i].Pressed += () => ApplyChoice(index);
        }

        pullButton.Visible = true;
    }

    int CurrentCardSeparation
    {
        get => smallCardParent.GetThemeConstant("separation") + Mathf.FloorToInt(topCard.Size.Y);
        set => smallCardParent.AddThemeConstantOverride("separation", value - Mathf.FloorToInt(topCard.Size.Y));
    }

    void ResizePanelOpen()
    {
        var startingLocation = fromPanel.GetGlobalRect();
        displayPanel.GlobalPosition = startingLocation.Position;
        displayPanel.Size = startingLocation.Size;
        displayPanel.Modulate = Colors.Transparent;

        MusicController.StopMusic();
        topCard.Scale = Vector2.Zero;
        music.Play();
        music.VolumeDb = 0;
        greetingEffects[llamaEntry.LlamaTier].Play();
        UISounds.PlaySound("PanelAppear");

        smallCardParent.Visible = false;

        var resizePanelTween = GetTree().CreateTween().SetParallel().SetTrans(Tween.TransitionType.Quad);
        resizePanelTween.TweenProperty(displayPanel, "modulate", Colors.White, 0.1);
        resizePanelTween.TweenProperty(displayPanel, "offset_top", 3, 0.2).SetDelay(0.1);
        resizePanelTween.TweenProperty(displayPanel, "offset_bottom", -3, 0.2).SetDelay(0.1);
        resizePanelTween.TweenProperty(displayPanel, "offset_left", 3, 0.2).SetDelay(0.1);
        resizePanelTween.TweenProperty(displayPanel, "offset_right", -3, 0.2).SetDelay(0.1);

        resizePanelTween.Finished += () =>
        {
            greetingVoices[isSmall ? 3 : llamaEntry.LlamaTier].Play();
            (isSmall ? smallLlamaButton : standardLlamaButton).Visible = true;
            smallCardParent.Visible = true;
            CurrentCardSeparation = 0;
        };
    }

    //wibbly wobbly music theory
    static float[] impactProgression = new float[]
    {
        0.00f/8,
        1.00f/8,
        2.00f/8,
        2.66f/8,
        4.00f/8,
        5.66f/8,
        7.00f/8,
    };

    void PlayImpactSound()
    {
        int octave = 1 + (llamaHits / 8);
        impactEffect.PitchScale = octave + impactProgression[llamaHits % 8];
        impactEffect.Play();
    }

    void SetLlamaHover(bool value)
    {
        if (cardPacksPrepared && llamaHits > minLlamaImpacts)
            return;
        if (value)
            hoverEffect.Play();
        LlamaScale(value);
        GlowScale(value);
    }

    void GlowScale(bool value)
    {
        var glowScaleTween = GetTree().CreateTween().SetParallel().SetTrans(Tween.TransitionType.Cubic);
        glowScaleTween.TweenProperty(glowFlare, "scale", value ? Vector2.One : Vector2.Zero, 0.25f);
    }
    void LlamaScale(bool value)
    {
        var llamaScaleTween = GetTree().CreateTween().SetParallel().SetTrans(Tween.TransitionType.Cubic);
        llamaScaleTween.TweenProperty(fullLlama, "scale", Vector2.One * (value ? 1 : 0.9f), 0.25f);
    }

    void HitLlama()
    {
        if (!cardPacksPrepared || llamaHits < minLlamaImpacts)
        {
            //play impact sound and voiceline
            if (llamaHits == 0)
            {
                greetingVoices[isSmall ? 3 : llamaEntry.LlamaTier].Stop();
                (isSmall ? miniImpactVoice : impactVoice).Play();
            }
            int impactPartcilesIndex = llamaHits % impactParticles.Length;

            impactParticleContainers[impactPartcilesIndex].GlobalPosition = GetGlobalMousePosition();
            impactParticles[impactPartcilesIndex].Restart();

            PlayImpactSound();
            llamaHits++;
            return;
        }
        llamaHits++;
        GlowScale(false);

        smallLlamaButton.Visible = false;
        standardLlamaButton.Visible = false;
        PlayImpactSound();
        burstEffect.Play();
        (isSmall ? miniImpactVoice : impactVoice).Stop();
        (isSmall ? miniBurstVoice : burstVoice).Play();

        Control[] llamaParts = isSmall ? smallLlamaParts : standardLlamaParts;
        //crateOpenButton.Visible = false;

        fullLlama.Visible = false;
        var llamaPartsParent = isSmall ? smallLlamaPartParent : standardLlamaPartParent;
        llamaPartsParent.Visible = true;

        var llamaBurstTween = GetTree().CreateTween().SetParallel().SetTrans(Tween.TransitionType.Cubic);
        foreach (var part in llamaParts)
        {
            part.OffsetTop = 0;
            part.OffsetBottom = 0;
            part.OffsetLeft = 0;
            part.OffsetRight = 0;
            part.Rotation = 0;
            part.Scale = Vector2.One;
            float hOffset = ((part.PivotOffset.X / part.Size.X) - 0.5f) * 500;
            llamaBurstTween.TweenProperty(part, "offset_top", 1000, 1.25f).SetEase(Tween.EaseType.In).SetTrans(Tween.TransitionType.Quad);
            llamaBurstTween.TweenProperty(part, "offset_left",  hOffset, 1.25f).SetTrans(Tween.TransitionType.Linear);
            llamaBurstTween.TweenProperty(part, "offset_right", hOffset, 1.25f).SetTrans(Tween.TransitionType.Linear);
            llamaBurstTween.TweenProperty(part, "rotation", Mathf.DegToRad(GD.RandRange(480, 740) * (GD.Randf() > 0.5 ? 1 : -1)), 1.25f).SetEase(Tween.EaseType.Out);
            llamaBurstTween.TweenProperty(part, "scale", Vector2.Zero, 1.25f).SetEase(Tween.EaseType.In);
        }

        confettiParticles.Restart();

        llamaBurstTween.Finished += () =>
        {
            llamaBurstComplate = true;
        };
    }

    async Task WaitForCardPackBurst()
    {
        while (IsInsideTree() && isOpen && !llamaBurstComplate)
        {
            await this.WaitForFrame();
        }
    }

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

            //enable fast effects
            if (nextPullIndex < TotalQueueLength)
                TweenTimeScale(pullFastSpeed, pullFastSpeed * 2);
        };
        if (!isPulling)
            PullCard();
    }
    static readonly Callable setTimeScaleCallable = Callable.From<float>(SetTimeScale);
    static void SetTimeScale(float newVal) => Engine.TimeScale = newVal;
    void TweenTimeScale(float target, float pitch, float time = 0.5f)
    {
        speedTween?.Kill();
        speedTween = GetTree().CreateTween().SetParallel();
        speedTween.Pause();
        speedTween.TweenMethod(setTimeScaleCallable, Engine.TimeScale, target, time);
        speedTween.TweenProperty(music, "pitch_scale", pitch, time).SetTrans(Tween.TransitionType.Linear);
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
            TweenTimeScale(1, 1);
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
            //prevCard.LinkProfileItem(queuedChoices[index - 1]);
            SetSingleCardItem(index - 1, prevCard);
        }
        if (index >= 0 && index < TotalQueueLength)
        {
            //topCard.SetItemData(new(queuedItems[index].GetItemUnsafe()));
            //topCard.LinkProfileItem(queuedChoices[index]);
            SetSingleCardItem(index, topCard);
        }
        int remainder = Mathf.Max(0, TotalQueueLength - (index + 1));
        int cardCount = Mathf.Min(smallCards.Length, remainder);
        for (int i = 0; i < cardCount; i++)
        {
            //smallCards[i].SetItemData(new(queuedItems[index + i + 1].GetItemUnsafe()));
            //smallCards[i].LinkProfileItem(queuedChoices[index + i + 1]);
            SetSingleCardItem(index + i + 1, smallCards[i]);
            smallCards[i].Modulate = Colors.White;
        }
        for (int i = cardCount; i < smallCards.Length; i++)
        {
            smallCards[i].Modulate = Colors.Transparent;
        }
    }

    void SetSingleCardItem(int index, GameItemEntry card)
    {
        if (choicesOnly)
        {
            card.LinkProfileItem(queuedChoices[index]);
            return;
        }
        //GD.Print("INDEX: " + index);
        if (index>=queuedItems.Count)
        {
            index -= queuedItems.Count;
            //choice card
            card.LinkProfileItem(queuedChoices[index]);
        }
        else
        {
            //regular item
            card.SetItemData(queuedItems[index]);
        }
    }

    void PullCard()
	{
        if (nextPullIndex>0)
        {
            prevCard.Scale = topCard.Scale;
            prevCard.GlobalPosition = topCard.GlobalPosition/* + (topCard.Size * (topCard.Scale - Vector2.One) * 0.5f)*/;
            prevCard.Rotation = 0;

            prevCard.FixControlOffsets();
        }

        if (nextPullIndex < TotalQueueLength)
        {
            topCard.Scale = smallCards[0].Scale;
            topCard.GlobalPosition = smallCards[0].GlobalPosition;
            topCard.Rotation = 0;

            topCard.FixControlOffsets();

            smallCardsOffset.Position += new Vector2(0, gapBetweenCards);
        }
        else
        {
            GlowScale(false);
            topCard.Scale = Vector2.Zero;
            pullButton.Visible = false;
            EndPullCard();
        }

        SetCardItems(nextPullIndex);

        bool pauseForChoice = false;
        //if current item is cardpack, add delay in fast mode or stop fast mode
        if (nextPullIndex < TotalQueueLength && nextPullIndex >= queuedItems.Count)
        {
            EndPullCard();
            pauseForChoice = true;
        }

        nextPullIndex++;

        isPulling = true;


        var tweener = GetTree().CreateTween().SetParallel().SetTrans(Tween.TransitionType.Quart);
        float delay = 0;

        if (nextPullIndex <= TotalQueueLength)
        {
            tweener.TweenProperty(topCard, "offset_top", 0, pullTime)
                .SetEase(Tween.EaseType.Out);
            tweener.TweenProperty(topCard, "offset_bottom", 0, pullTime)
                .SetEase(Tween.EaseType.Out);
            tweener.TweenProperty(topCard, "offset_left", 0, pullTime)
                .SetEase(Tween.EaseType.In);
            tweener.TweenProperty(topCard, "offset_right", 0, pullTime)
                .SetEase(Tween.EaseType.In);

            tweener.TweenProperty(topCard, "rotation", Mathf.DegToRad(-360), pullTime * 0.5f)
                .SetEase(Tween.EaseType.Out)
                .SetTrans(Tween.TransitionType.Quad);
            tweener.TweenProperty(topCard, "scale", Vector2.One * 1.5f, pullTime)
                .SetEase(Tween.EaseType.In);

            tweener.TweenProperty(smallCardsOffset, "position:y", 0, pullTime);
            tweener.TweenProperty(glowFlare, "self_modulate", topCard.LatestRarityColor, pullTime);
            delay = pullTime * 0.75f;
        }


        if (nextPullIndex > 0)
        {
            tweener.TweenProperty(prevCard, "offset_right", -450, pullTime * 0.5f)
                .SetEase(Tween.EaseType.Out)
                .SetTrans(Tween.TransitionType.Quart)
                .SetDelay(delay);
            tweener.TweenProperty(prevCard, "offset_top", 1000, pullTime * 0.5f)
                .SetEase(Tween.EaseType.In)
                .SetTrans(Tween.TransitionType.Quad)
                .SetDelay(delay);

            tweener.TweenProperty(prevCard, "scale", Vector2.Zero, pullTime * 0.5f)
                .SetEase(Tween.EaseType.In)
                .SetTrans(Tween.TransitionType.Quad)
                .SetDelay(delay);
            tweener.TweenProperty(prevCard, "rotation", Mathf.DegToRad(-720), pullTime * 0.5f)
                .SetEase(Tween.EaseType.In)
                .SetTrans(Tween.TransitionType.Quad)
                .SetDelay(delay);
        }

        tweener.Finished += async () =>
        {
            if(nextPullIndex > TotalQueueLength)
            {
                if (speedTween?.IsValid() ?? false)
                    await ToSignal(speedTween, "finished");
                EndPullCard();
                TweenTimeScale(1, 1, 0.1f);
                await ToSignal(speedTween, "finished");

                await ShowRecyclePopup();
                CloseMenu();
                return;
            }
            GlowScale(true);
            if (pauseForChoice)
            {
                //open choice panel
                GD.Print("opening choice");
                isPulling = false;
                pullButton.Visible = false;
                OpenChoices();
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

        int nextChoiceIndex = nextPullIndex - queuedItems.Count;
        GD.Print(queuedChoices[nextChoiceIndex - 1].GetItemUnsafe());
        var currentChoices = queuedChoices[nextChoiceIndex - 1].GetItemUnsafe()["attributes"]["options"]?.AsArray();
        if (currentChoices is null)
        {
            choiceOpen.Kill();
            SkipChoice(false);
            return;
        }
        for (int i = 0; i < currentChoices.Count; i++)
        {
            var thisChoice = currentChoices[i];
            BanjoAssets.TryGetTemplate(thisChoice["itemType"].ToString(), out var itemData);
            var itemStack = itemData.CreateInstanceOfItem(thisChoice["quantity"].GetValue<int>(), thisChoice["attributes"]?.AsObject().Reserialise());
            choiceCards[i].SetItemData(itemStack);
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

        int nextChoiceIndex = nextPullIndex - queuedItems.Count;
        //start the request now and await later, asynchronism baby!
        JsonObject body = new()
        {
            ["cardPackItemId"] = queuedChoices[nextChoiceIndex - 1].itemID.uuid,
            ["selectionIdx"] = index
        };
        var operationTask = ProfileRequests.PerformProfileOperation(FnProfileTypes.AccountItems, "OpenCardPack", body.ToString());

        for (int i = 0; i < choiceCards.Length; i++)
        {
            choiceCards[i].SetInteractable(false);
        }

        var currentChoices = queuedChoices[nextChoiceIndex - 1].GetItemUnsafe()["attributes"]["options"].AsArray();
        var resultChoice = currentChoices[index];
        BanjoAssets.TryGetTemplate(resultChoice["itemType"].ToString(), out var itemTemplate);
        var itemInstance = itemTemplate.CreateInstanceOfItem((int?)resultChoice["quantity"] ?? 1, resultChoice["attributes"]?.AsObject().Reserialise() ?? null);

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
        choiceResultCard.SetShaderTexture(itemInstance.GetItemTexture(), "IconTexture");
        choiceResultCard.SetShaderColor(itemTemplate.GetItemRarityColor(), "RarityColour");
        choiceResultCard.SetShaderBool(false, "Started");


        var choiceClose = GetTree().CreateTween().SetParallel();
        choiceClose.TweenProperty(choiceCanvas, "self_modulate", Colors.Transparent, 0.25);
        choiceClose.TweenProperty(choiceCanvas, "scale", Vector2.Zero, 0.25).SetEase(Tween.EaseType.In);

        var cardSlideUp = GetTree().CreateTween().SetParallel().SetTrans(Tween.TransitionType.Quad);
        cardSlideUp.TweenProperty(choiceResultCard, "offset_left",      60,         0.3);
        cardSlideUp.TweenProperty(choiceResultCard, "offset_right",     60,         0.3);
        cardSlideUp.TweenProperty(choiceResultCard, "offset_top",       -250,       0.3);
        cardSlideUp.TweenProperty(choiceResultCard, "offset_bottom",    -250,       0.3);
        cardSlideUp.TweenProperty(choiceResultCard, "scale", Vector2.One * 0.5f,    0.3);

        choiceResultCard.SetShaderBool(true, "Started");
        choiceResultCard.SetShaderFloat((float)(Time.GetTicksMsec()*0.001)+0.1f, "StartTime");

        GD.Print(choiceResultCard.GetShaderFloat("time"));
        GD.Print(choiceResultCard.GetShaderFloat("StartTime"));

        await this.WaitForTimer(0.4f);

        var resultNotification = (await operationTask)["notifications"][0];

        choiceResultCard.Reparent(choiceResultBGParent);
        choiceResultCard.OffsetLeft = 60;
        choiceResultCard.OffsetRight = choiceResultCard.OffsetLeft;
        choiceResultCard.OffsetTop = -250;
        choiceResultCard.OffsetBottom = choiceResultCard.OffsetTop;


        var cardSlideDown = GetTree().CreateTween().SetParallel().SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.In);
        cardSlideDown.TweenProperty(choiceResultCard, "offset_top", -115, 0.25);
        cardSlideDown.TweenProperty(choiceResultCard, "offset_bottom", -115, 0.25);

        await this.WaitForTimer(0.2f);
        if (cardChangeEffect is not null)
            cardChangeEffect.Visible = true;
        CardChangeEffectLevel = 0;
        var changeEffectTween = GetTree().CreateTween();
        changeEffectTween.TweenProperty(this, "CardChangeEffectLevel", 1, 1);
        changeEffectTween.TweenProperty(this, "CardChangeEffectLevel", 2, 0.5);

        await this.WaitForTimer(1f);

        choiceResultCard.Visible = false;

        JsonNode resultItem = resultNotification["lootGranted"]["items"][0];
        await queuedChoices[nextChoiceIndex - 1].ReplaceWith(new(LoginRequests.AccountID, resultItem["itemProfile"].ToString(), resultItem["itemGuid"].ToString()));
        GD.Print(resultNotification);

        await this.WaitForTimer(0.5f);
        if (cardChangeEffect is not null)
            cardChangeEffect.Visible = true;

        //reopen choices if the result is another cardpack
        if (resultItem?["itemType"].ToString().StartsWith("CardPack")??false)
            OpenChoices();
        else
            pullButton.Visible = true;
    }

    float CardChangeEffectLevel
    {
        get => cardChangeEffect?.GetShaderFloat("progress") ?? 0;
        set => cardChangeEffect?.SetShaderFloat(value, "progress");
    }

    void SkipChoice() => SkipChoice(true);
    void SkipChoice(bool withContinue)
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
        if (withContinue)
            PullCard();
    }

    async Task ShowRecyclePopup()
    {
        var resultItems = queuedChoices
                    .Union(queuedItemHandles)
                    .Where(handle =>
                        handle.GetItemUnsafe().GetTemplate() is JsonObject template && template["Type"]?.ToString() is string type &&
                        (type == "Worker" || type == "Schematic" || type == "Hero" || type == "Defender") &&
                        !(template["IsPermanent"]?.GetValue<bool>() ?? false));

        if (resultItems.Any())
        {
            foreach (var profileItem in resultItems)
            {
                profileItem.GetItemUnsafe().GenerateItemSearchTags();
            }
            GameItemSelector.Instance.SetRecycleDefaults();
            GameItemSelector.Instance.allowCancel = false;
            GameItemSelector.Instance.allowEmptySelection = true;
            var toRecycle = await GameItemSelector.Instance.OpenSelector(resultItems, null);
            if (toRecycle.Length > 0)
            {
                JsonObject content = new()
                {
                    ["targetItemIds"] = new JsonArray(toRecycle.Select(handle => (JsonNode)handle.itemID.uuid).ToArray())
                };
                LoadingOverlay.AddLoadingKey("recycling");
                await ProfileRequests.PerformProfileOperation(FnProfileTypes.AccountItems, "RecycleItemBatch", content.ToString());
                LoadingOverlay.RemoveLoadingKey("recycling");
            }
        }
    }

    async void CloseMenu()
    {
        MusicController.ResumeMusic();
        var startingLocation = fromPanel.GetGlobalRect();
        var exitAnim = GetTree().CreateTween().SetParallel().SetTrans(Tween.TransitionType.Quad);
        //bgFade.TweenProperty(backgroundImage, "self_modulate", Colors.Transparent, 0.25f);
        exitAnim.TweenProperty(displayPanel, "global_position", startingLocation.Position, 0.2);
        exitAnim.TweenProperty(displayPanel, "size", startingLocation.Size, 0.2);
        exitAnim.TweenProperty(displayPanel, "modulate", Colors.Transparent, 0.1).SetDelay(0.2);
        exitAnim.TweenProperty(music, "volume_db", -80, 1)
            .SetTrans(Tween.TransitionType.Expo)
            .SetEase(Tween.EaseType.In);

        await this.WaitForTimer(0.31f);

        queuedChoices.Clear();
        queuedItemHandles.Clear();
        queuedItems.Clear();
        displayPanel.Visible = false;
        isPulling = false;
        isOpen = false;
        ProcessMode = ProcessModeEnum.Disabled;
    }
}
