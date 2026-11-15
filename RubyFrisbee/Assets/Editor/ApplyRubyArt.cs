using RubyFrisbee.Gameplay;
using RubyFrisbee.Player;
using RubyFrisbee.UI;
using RubysFrisbee.Assets;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace RubyFrisbee.Editor
{
    /// <summary>
    /// Drops the imported Ruby art, yard, VFX, UI paw icons, and audio into the existing game scene.
    /// </summary>
    public static class ApplyRubyArt
    {
        const string VisualPath = "Assets/RubysFrisbeeAssets/Generated/Prefabs/RubyVisual.prefab";
        const string AudioPath = "Assets/RubysFrisbeeAssets/Generated/Prefabs/RubyAudioRig.prefab";
        const string YardPath = "Assets/RubysFrisbeeAssets/Generated/Prefabs/Backyard.prefab";
        const string RingPath = "Assets/RubysFrisbeeAssets/Textures/Props/FlyingRing.png";
        const string TennisBallPath = "Assets/RubysFrisbeeAssets/Textures/Props/tennis_ball.png";
        const string PawFullPath = "Assets/RubysFrisbeeAssets/Textures/UI/icon_paw_life_full.png";
        const string PawEmptyPath = "Assets/RubysFrisbeeAssets/Textures/UI/icon_paw_life_empty.png";
        const string TitleWordmarkPath = "Assets/RubysFrisbeeAssets/Textures/UI/title_wordmark.png";
        const string PanelMenuPath = "Assets/RubysFrisbeeAssets/Textures/UI/panel_menu.png";
        const string PanelGameOverPath = "Assets/RubysFrisbeeAssets/Textures/UI/panel_game_over.png";
        const string ButtonPrimaryNormalPath = "Assets/RubysFrisbeeAssets/Textures/UI/button_primary_normal.png";
        const string ButtonPrimaryPressedPath = "Assets/RubysFrisbeeAssets/Textures/UI/button_primary_pressed.png";
        const string ButtonSecondaryNormalPath = "Assets/RubysFrisbeeAssets/Textures/UI/button_secondary_normal.png";
        const string ButtonSecondaryPressedPath = "Assets/RubysFrisbeeAssets/Textures/UI/button_secondary_pressed.png";
        const string RealRubyBarkPath = "Assets/Audio/RubyBark.mp3";
        const string AudioBankPath = "Assets/RubysFrisbeeAssets/Generated/Audio/RubyAudioBank.asset";
        const string FrisbeePath = "Assets/Prefabs/Frisbee.prefab";

        [MenuItem("Ruby Frisbee/Apply Imported Art")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("Stop Play mode before applying imported art.");
                return;
            }

            var ruby = FindRoot("Ruby");
            var ground = FindRoot("Ground");
            if (ruby == null || ground == null)
            {
                Debug.LogError("Open GameScene, with Ruby and Ground in it, before applying imported art.");
                return;
            }

            float groundTop = GroundTop(ground);
            ConfigureCamera(groundTop);
            ConfigureRubyPhysicsAndCollider(ruby, groundTop);

            var visual = PlaceVisual(ruby);
            foreach (var renderer in ruby.GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (renderer.transform == visual.transform || renderer.transform.IsChildOf(visual.transform))
                {
                    renderer.enabled = true;
                    continue;
                }

                renderer.enabled = false;
            }

            foreach (string name in new[] { "Sky", "GrassField", "Fence", "Ground" })
            {
                var old = FindRoot(name);
                if (old != null)
                {
                    foreach (var renderer in old.GetComponents<SpriteRenderer>())
                    {
                        renderer.enabled = false;
                    }
                }
            }

            var audio = PlaceAudio();
            PlaceYard(groundTop);
            DressFrisbee();
            ConfigureSpawner(groundTop);
            ConfigureUI();

            var stage = ruby.GetComponent<RubyStage>();
            if (stage == null)
            {
                stage = ruby.AddComponent<RubyStage>();
            }

            var so = new SerializedObject(stage);
            so.FindProperty("visual").objectReferenceValue = visual.GetComponent<RubyAnimationDriver>();
            so.FindProperty("audioPlayer").objectReferenceValue = audio.GetComponent<RubyAudioPlayer>();
            so.FindProperty("takeoffEffect").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/RubysFrisbeeAssets/Generated/Prefabs/FX/takeoff_grass.prefab");
            so.FindProperty("landingEffect").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/RubysFrisbeeAssets/Generated/Prefabs/FX/landing_dust_grass.prefab");
            so.FindProperty("catchEffect").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/RubysFrisbeeAssets/Generated/Prefabs/FX/catch_sparkle.prefab");
            so.FindProperty("airCatchEffect").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/RubysFrisbeeAssets/Generated/Prefabs/FX/air_catch_burst.prefab");
            so.FindProperty("comboEffect").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/RubysFrisbeeAssets/Generated/Prefabs/FX/combo_celebration.prefab");
            so.FindProperty("missEffect").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/RubysFrisbeeAssets/Generated/Prefabs/FX/miss_impact_puff.prefab");
            so.FindProperty("ringTrailEffect").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/RubysFrisbeeAssets/Generated/Prefabs/FX/ring_flight_trail.prefab");
            so.FindProperty("tennisBallSprite").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>(TennisBallPath);

            var jumpProp = so.FindProperty("jumpSprites");
            if (jumpProp != null)
            {
                jumpProp.arraySize = 48;
                for (int i = 0; i < 48; i++)
                {
                    string framePath = $"Assets/RubysFrisbeeAssets/Textures/Characters/Jump/ruby_jump_{i:00}.png";
                    jumpProp.GetArrayElementAtIndex(i).objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>(framePath);
                }
            }

            AssetDatabase.ImportAsset(RealRubyBarkPath, ImportAssetOptions.ForceSynchronousImport);
            var realBarkClip = AssetDatabase.LoadAssetAtPath<AudioClip>(RealRubyBarkPath);
            if (realBarkClip != null)
            {
                so.FindProperty("realRubyBarkClip").objectReferenceValue = realBarkClip;
                RegisterRealBarkInBank(realBarkClip);
            }

            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(ruby.scene);
            EditorSceneManager.SaveOpenScenes();
            Debug.Log("Imported Ruby art, multi-side Frisbee spawner, VFX, and UI configured in GameScene!");
        }

        static void ConfigureCamera(float groundTop)
        {
            var camGo = FindRoot("Main Camera");
            if (camGo == null)
            {
                return;
            }

            var cam = camGo.GetComponent<Camera>();
            if (cam != null)
            {
                cam.orthographic = true;
                // Backyard canvas is 1080px tall at 128 PPU (8.4375 units tall -> orthoSize = 4.21875)
                cam.orthographicSize = 4.21875f;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.47f, 0.72f, 0.92f, 1f);
            }

            // Center camera vertically on the Backyard artwork (baseline is 900px from top of 1080px canvas -> +2.8125 units above groundTop)
            camGo.transform.position = new Vector3(0f, groundTop + 2.8125f, -10f);
        }

        static void ConfigureRubyPhysicsAndCollider(GameObject ruby, float groundTop)
        {
            // Keep Ruby root at unit scale so colliders and GroundCheck (-0.45) are clean and unstretched
            ruby.transform.localScale = Vector3.one;
            ruby.transform.position = new Vector3(-3.2f, groundTop + 0.45f, 0f);

            var capsule = ruby.GetComponent<CapsuleCollider2D>();
            if (capsule != null)
            {
                capsule.direction = CapsuleDirection2D.Horizontal;
                capsule.offset = Vector2.zero;
                capsule.size = new Vector2(1.15f, 0.90f);
            }

            var catchZone = ruby.GetComponent<CircleCollider2D>();
            if (catchZone != null)
            {
                catchZone.isTrigger = true;
                catchZone.offset = new Vector2(0.55f, 0.35f);
                catchZone.radius = 0.85f;
            }

            var rb = ruby.GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                rb.gravityScale = 2.15f;
            }

            var controller = ruby.GetComponent<RubyController>();
            if (controller != null)
            {
                var so = new SerializedObject(controller);
                so.FindProperty("moveSpeed").floatValue = 5.2f;
                so.FindProperty("groundAcceleration").floatValue = 38f;
                so.FindProperty("dashSpeed").floatValue = 8.8f;
                so.FindProperty("dashDuration").floatValue = 0.18f;
                so.FindProperty("jumpForce").floatValue = 7.1f;
                so.FindProperty("baseGravityScale").floatValue = 2.15f;
                so.FindProperty("fallGravityMultiplier").floatValue = 1.28f;
                so.FindProperty("jumpWindupDuration").floatValue = 0.055f;
                so.FindProperty("groundCheckRadius").floatValue = 0.22f;
                so.FindProperty("minX").floatValue = -6.5f;
                so.FindProperty("maxX").floatValue = 6.5f;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        static void ConfigureSpawner(float groundTop)
        {
            var spawner = Object.FindFirstObjectByType<FrisbeeSpawner>(FindObjectsInactive.Include);
            if (spawner == null)
            {
                return;
            }

            var tennisSprite = AssetDatabase.LoadAssetAtPath<Sprite>(TennisBallPath);
            var so = new SerializedObject(spawner);
            so.FindProperty("useSmartYardTargeting").boolValue = true;
            so.FindProperty("spawnSideMode").enumValueIndex = (int)SpawnSideMode.BothSides;
            so.FindProperty("leftSideSpawnChance").floatValue = 0.5f;
            so.FindProperty("spawnEdgeX").floatValue = 8.1f;
            so.FindProperty("spawnHeightRange").vector2Value = new Vector2(-1.1f, 0.45f);
            so.FindProperty("landingTargetXRange").vector2Value = new Vector2(-5.9f, 5.9f);
            so.FindProperty("arcApexHeightRange").vector2Value = new Vector2(-0.35f, 1.15f);
            so.FindProperty("targetGroundY").floatValue = groundTop + 0.08f;
            so.FindProperty("minHorizontalTravel").floatValue = 5.0f;
            so.FindProperty("bonusBallChance").floatValue = 0.16f;
            so.FindProperty("doubleThrowChanceAtHighDifficulty").floatValue = 0.15f;
            if (tennisSprite != null)
            {
                so.FindProperty("bonusBallSprite").objectReferenceValue = tennisSprite;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void ConfigureUI()
        {
            var gameUI = Object.FindFirstObjectByType<GameUI>(FindObjectsInactive.Include);
            if (gameUI == null)
            {
                return;
            }

            var subtitleTf = gameUI.transform.Find("MenuPanel/Subtitle");
            if (subtitleTf != null && subtitleTf.TryGetComponent<TextMeshProUGUI>(out var subtitleTmp))
            {
                subtitleTmp.text = "Catch rings from both sides & bounce-catch tennis balls!\nMove: A/D or \u2190/\u2192   \u2022   Jump: Space/W   \u2022   Dash: Shift/S";
                subtitleTmp.fontSize = 24;
            }

            var canvasScaler = gameUI.GetComponent<CanvasScaler>();
            if (canvasScaler != null)
            {
                canvasScaler.matchWidthOrHeight = 0.5f;
            }

            var hudPanel = gameUI.transform.Find("HudPanel");
            if (hudPanel != null)
            {
                AnchorHudText(hudPanel.Find("ScoreText"), new Vector2(0f, 1f), new Vector2(40f, -28f), TextAlignmentOptions.TopLeft);
                AnchorHudText(hudPanel.Find("ComboText"), new Vector2(0f, 1f), new Vector2(40f, -76f), TextAlignmentOptions.TopLeft);
                AnchorHudText(hudPanel.Find("LivesText"), new Vector2(1f, 1f), new Vector2(-240f, -28f), TextAlignmentOptions.TopRight);
                AnchorHudText(hudPanel.Find("HighScoreText"), new Vector2(1f, 1f), new Vector2(-40f, -82f), TextAlignmentOptions.TopRight);
            }

            var uiSo = new SerializedObject(gameUI);

            var pawFull = AssetDatabase.LoadAssetAtPath<Sprite>(PawFullPath);
            var pawEmpty = AssetDatabase.LoadAssetAtPath<Sprite>(PawEmptyPath);
            if (hudPanel != null && pawFull != null && pawEmpty != null)
            {
                var pawContainer = hudPanel.Find("PawLivesContainer");
                if (pawContainer == null)
                {
                    var containerGo = new GameObject("PawLivesContainer", typeof(RectTransform));
                    containerGo.transform.SetParent(hudPanel, false);
                    pawContainer = containerGo.transform;
                }

                var containerRect = pawContainer.GetComponent<RectTransform>();
                containerRect.anchorMin = new Vector2(1f, 1f);
                containerRect.anchorMax = new Vector2(1f, 1f);
                containerRect.pivot = new Vector2(1f, 1f);
                containerRect.anchoredPosition = Vector2.zero;
                containerRect.sizeDelta = Vector2.zero;

                var pawIcons = new Image[3];
                for (int i = 0; i < 3; i++)
                {
                    string iconName = $"PawLife_{i}";
                    var iconTf = pawContainer.Find(iconName);
                    GameObject iconGo;
                    if (iconTf == null)
                    {
                        iconGo = new GameObject(iconName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                        iconGo.transform.SetParent(pawContainer, false);
                    }
                    else
                    {
                        iconGo = iconTf.gameObject;
                    }

                    var rect = iconGo.GetComponent<RectTransform>();
                    rect.anchorMin = new Vector2(1f, 1f);
                    rect.anchorMax = new Vector2(1f, 1f);
                    rect.pivot = new Vector2(1f, 1f);
                    rect.sizeDelta = new Vector2(48f, 48f);
                    rect.anchoredPosition = new Vector2(-168f + i * 56f, -22f);

                    var img = iconGo.GetComponent<Image>();
                    img.sprite = pawFull;
                    img.preserveAspect = true;
                    pawIcons[i] = img;
                }

                uiSo.FindProperty("pawFullSprite").objectReferenceValue = pawFull;
                uiSo.FindProperty("pawEmptySprite").objectReferenceValue = pawEmpty;
                var iconsProp = uiSo.FindProperty("pawLifeIcons");
                iconsProp.arraySize = 3;
                for (int i = 0; i < 3; i++)
                {
                    iconsProp.GetArrayElementAtIndex(i).objectReferenceValue = pawIcons[i];
                }
            }

            ConfigureInteractiveMenuAndGameOverCards(gameUI.transform, uiSo);
            uiSo.ApplyModifiedPropertiesWithoutUndo();
        }

        static void ConfigureInteractiveMenuAndGameOverCards(Transform canvasTransform, SerializedObject uiSo)
        {
            var panelMenuSprite = AssetDatabase.LoadAssetAtPath<Sprite>(PanelMenuPath);
            var panelGameOverSprite = AssetDatabase.LoadAssetAtPath<Sprite>(PanelGameOverPath);
            var wordmarkSprite = AssetDatabase.LoadAssetAtPath<Sprite>(TitleWordmarkPath);
            var btnPrimaryNorm = AssetDatabase.LoadAssetAtPath<Sprite>(ButtonPrimaryNormalPath);
            var btnPrimaryPress = AssetDatabase.LoadAssetAtPath<Sprite>(ButtonPrimaryPressedPath);
            var btnSecNorm = AssetDatabase.LoadAssetAtPath<Sprite>(ButtonSecondaryNormalPath);
            var btnSecPress = AssetDatabase.LoadAssetAtPath<Sprite>(ButtonSecondaryPressedPath);

            Color darkEspresso = new(0.22f, 0.15f, 0.10f, 1f);
            Color warmBronze = new(0.38f, 0.24f, 0.12f, 1f);
            Color moodGreen = new(0.16f, 0.40f, 0.20f, 1f);

            // --- MENU PANEL & CARD ---
            var menuPanel = canvasTransform.Find("MenuPanel");
            if (menuPanel != null)
            {
                // Remove dark full-screen overlay so Ruby is in bright daylight and directly clickable!
                if (menuPanel.TryGetComponent<Image>(out var menuBg))
                {
                    menuBg.color = Color.clear;
                    menuBg.raycastTarget = false;
                }

                var menuCard = GetOrCreateCard(menuPanel, "MenuCard", panelMenuSprite, new Vector2(350f, -10f), new Vector2(680f, 700f));

                // Wordmark Logo
                var wordmarkTf = menuCard.Find("WordmarkLogo");
                if (wordmarkTf == null)
                {
                    var wmGo = new GameObject("WordmarkLogo", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                    wmGo.transform.SetParent(menuCard, false);
                    wordmarkTf = wmGo.transform;
                }
                var wmRect = wordmarkTf.GetComponent<RectTransform>();
                wmRect.anchoredPosition = new Vector2(0f, 225f);
                wmRect.sizeDelta = new Vector2(460f, 190f);
                var wmImg = wordmarkTf.GetComponent<Image>();
                wmImg.sprite = wordmarkSprite;
                wmImg.preserveAspect = true;
                wmImg.raycastTarget = false;

                var oldTitle = menuPanel.Find("Title") ?? menuCard.Find("Title");
                if (oldTitle != null)
                {
                    oldTitle.SetParent(menuCard, false);
                    oldTitle.gameObject.SetActive(wordmarkSprite == null);
                }

                var subtitle = menuPanel.Find("Subtitle") ?? menuCard.Find("Subtitle");
                if (subtitle != null)
                {
                    subtitle.SetParent(menuCard, false);
                    StyleCardText(subtitle, "Catch rings from both sides & bounce-catch tennis balls!\nMove: A/D or \u2190/\u2192   \u2022   Jump: Space/W   \u2022   Dash: Shift/S", 20, FontStyles.Normal, darkEspresso, new Vector2(0f, 92f), new Vector2(580f, 70f));
                }

                var menuHigh = menuPanel.Find("MenuHighScore") ?? menuCard.Find("MenuHighScore");
                if (menuHigh != null)
                {
                    menuHigh.SetParent(menuCard, false);
                    StyleCardText(menuHigh, menuHigh.GetComponent<TextMeshProUGUI>()?.text ?? "High Score: 0", 28, FontStyles.Bold, warmBronze, new Vector2(0f, 34f), new Vector2(540f, 50f));
                }

                var menuMood = GetOrCreateCardText(menuCard, "MenuMoodText", "Click Ruby in the yard to pet her, or toss a treat!", 19, FontStyles.Italic, moodGreen, new Vector2(0f, -22f), new Vector2(580f, 58f));
                uiSo.FindProperty("menuMoodText").objectReferenceValue = menuMood;

                var playBtnTf = menuPanel.Find("PlayButton") ?? menuCard.Find("PlayButton");
                if (playBtnTf != null)
                {
                    playBtnTf.SetParent(menuCard, false);
                    StyleCardButton(playBtnTf.GetComponent<Button>(), "Play Game", new Vector2(0f, -108f), new Vector2(330f, 84f), 30, btnPrimaryNorm, btnPrimaryPress);
                }

                var menuPetBtn = GetOrCreateCardButton(menuCard, "MenuPetButton", "Pet Ruby", new Vector2(-194f, -210f), new Vector2(182f, 66f), 22, btnSecNorm, btnSecPress);
                var menuTreatBtn = GetOrCreateCardButton(menuCard, "MenuTreatButton", "Toss Treat", new Vector2(0f, -210f), new Vector2(182f, 66f), 22, btnSecNorm, btnSecPress);
                var menuBallBtn = GetOrCreateCardButton(menuCard, "MenuBallButton", "Toss Ball", new Vector2(194f, -210f), new Vector2(182f, 66f), 22, btnSecNorm, btnSecPress);

                uiSo.FindProperty("menuPetButton").objectReferenceValue = menuPetBtn;
                uiSo.FindProperty("menuTreatButton").objectReferenceValue = menuTreatBtn;
                uiSo.FindProperty("menuBallButton").objectReferenceValue = menuBallBtn;
            }

            // --- GAME OVER PANEL & CARD ---
            var gameOverPanel = canvasTransform.Find("GameOverPanel");
            if (gameOverPanel != null)
            {
                if (gameOverPanel.TryGetComponent<Image>(out var goBg))
                {
                    goBg.color = Color.clear;
                    goBg.raycastTarget = false;
                }

                var goCard = GetOrCreateCard(gameOverPanel, "GameOverCard", panelGameOverSprite, new Vector2(350f, -10f), new Vector2(680f, 680f));

                var goTitle = gameOverPanel.Find("GameOverTitle") ?? goCard.Find("GameOverTitle");
                if (goTitle != null)
                {
                    goTitle.SetParent(goCard, false);
                    StyleCardText(goTitle, "Good Try, Ruby!", 46, FontStyles.Bold, darkEspresso, new Vector2(0f, 228f), new Vector2(560f, 70f));
                }

                var finalScore = gameOverPanel.Find("FinalScore") ?? goCard.Find("FinalScore");
                if (finalScore != null)
                {
                    finalScore.SetParent(goCard, false);
                    StyleCardText(finalScore, finalScore.GetComponent<TextMeshProUGUI>()?.text ?? "Score: 0", 34, FontStyles.Bold, darkEspresso, new Vector2(0f, 156f), new Vector2(520f, 54f));
                }

                var goHigh = gameOverPanel.Find("GameOverHighScore") ?? goCard.Find("GameOverHighScore");
                if (goHigh != null)
                {
                    goHigh.SetParent(goCard, false);
                    StyleCardText(goHigh, goHigh.GetComponent<TextMeshProUGUI>()?.text ?? "Best: 0", 26, FontStyles.Bold, warmBronze, new Vector2(0f, 108f), new Vector2(520f, 48f));
                }

                var goMood = GetOrCreateCardText(goCard, "GameOverMoodText", "Ruby looks a little sad... Pet her or toss a treat to cheer her up!", 19, FontStyles.Italic, moodGreen, new Vector2(0f, 44f), new Vector2(580f, 58f));
                uiSo.FindProperty("gameOverMoodText").objectReferenceValue = goMood;

                var goPetBtn = GetOrCreateCardButton(goCard, "GameOverPetButton", "Cheer Up", new Vector2(-194f, -34f), new Vector2(182f, 66f), 22, btnSecNorm, btnSecPress);
                var goTreatBtn = GetOrCreateCardButton(goCard, "GameOverTreatButton", "Give Treat", new Vector2(0f, -34f), new Vector2(182f, 66f), 22, btnSecNorm, btnSecPress);
                var goBallBtn = GetOrCreateCardButton(goCard, "GameOverBallButton", "Toss Ball", new Vector2(194f, -34f), new Vector2(182f, 66f), 22, btnSecNorm, btnSecPress);

                uiSo.FindProperty("gameOverPetButton").objectReferenceValue = goPetBtn;
                uiSo.FindProperty("gameOverTreatButton").objectReferenceValue = goTreatBtn;
                uiSo.FindProperty("gameOverBallButton").objectReferenceValue = goBallBtn;

                var restartBtnTf = gameOverPanel.Find("RestartButton") ?? goCard.Find("RestartButton");
                if (restartBtnTf != null)
                {
                    restartBtnTf.SetParent(goCard, false);
                    StyleCardButton(restartBtnTf.GetComponent<Button>(), "Play Again", new Vector2(0f, -132f), new Vector2(320f, 78f), 28, btnPrimaryNorm, btnPrimaryPress);
                }

                var menuBtnTf = gameOverPanel.Find("MenuButton") ?? goCard.Find("MenuButton");
                if (menuBtnTf != null)
                {
                    menuBtnTf.SetParent(goCard, false);
                    StyleCardButton(menuBtnTf.GetComponent<Button>(), "Main Menu", new Vector2(0f, -218f), new Vector2(240f, 64f), 24, btnSecNorm, btnSecPress);
                }
            }
        }

        static Transform GetOrCreateCard(Transform parent, string name, Sprite bgSprite, Vector2 anchoredPos, Vector2 size)
        {
            var cardTf = parent.Find(name);
            GameObject cardGo;
            if (cardTf == null)
            {
                cardGo = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                cardGo.transform.SetParent(parent, false);
            }
            else
            {
                cardGo = cardTf.gameObject;
            }

            var rect = cardGo.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = size;

            var img = cardGo.GetComponent<Image>();
            if (bgSprite != null)
            {
                img.sprite = bgSprite;
                img.type = Image.Type.Sliced;
                img.color = Color.white;
            }
            else
            {
                img.color = new Color(0.96f, 0.94f, 0.85f, 0.96f);
            }

            return cardGo.transform;
        }

        static TextMeshProUGUI GetOrCreateCardText(
            Transform parent,
            string name,
            string textContent,
            int fontSize,
            FontStyles style,
            Color color,
            Vector2 anchoredPos,
            Vector2 size)
        {
            var tf = parent.Find(name);
            if (tf == null)
            {
                var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
                go.transform.SetParent(parent, false);
                tf = go.transform;
            }

            StyleCardText(tf, textContent, fontSize, style, color, anchoredPos, size);
            return tf.GetComponent<TextMeshProUGUI>();
        }

        static void StyleCardText(
            Transform tf,
            string textContent,
            int fontSize,
            FontStyles style,
            Color color,
            Vector2 anchoredPos,
            Vector2 size)
        {
            if (tf.TryGetComponent<RectTransform>(out var rect))
            {
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = anchoredPos;
                rect.sizeDelta = size;
            }

            if (tf.TryGetComponent<TextMeshProUGUI>(out var tmp))
            {
                tmp.text = textContent;
                tmp.fontSize = fontSize;
                tmp.fontStyle = style;
                tmp.color = color;
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.raycastTarget = false;
            }
        }

        static Button GetOrCreateCardButton(
            Transform parent,
            string name,
            string label,
            Vector2 anchoredPos,
            Vector2 size,
            int fontSize,
            Sprite normalSprite,
            Sprite pressedSprite)
        {
            var tf = parent.Find(name);
            GameObject btnGo;
            if (tf == null)
            {
                btnGo = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
                btnGo.transform.SetParent(parent, false);

                var labelGo = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
                labelGo.transform.SetParent(btnGo.transform, false);
            }
            else
            {
                btnGo = tf.gameObject;
            }

            var btn = btnGo.GetComponent<Button>();
            StyleCardButton(btn, label, anchoredPos, size, fontSize, normalSprite, pressedSprite);
            return btn;
        }

        static void StyleCardButton(
            Button btn,
            string label,
            Vector2 anchoredPos,
            Vector2 size,
            int fontSize,
            Sprite normalSprite,
            Sprite pressedSprite)
        {
            if (btn == null)
            {
                return;
            }

            var rect = btn.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = size;

            var img = btn.GetComponent<Image>();
            if (normalSprite != null)
            {
                img.sprite = normalSprite;
                img.type = Image.Type.Sliced;
                img.color = Color.white;
                btn.transition = Selectable.Transition.SpriteSwap;
                btn.spriteState = new SpriteState
                {
                    pressedSprite = pressedSprite != null ? pressedSprite : normalSprite,
                    selectedSprite = normalSprite,
                    highlightedSprite = normalSprite
                };
            }

            var labelTf = btn.transform.Find("Label");
            if (labelTf != null)
            {
                var lRect = labelTf.GetComponent<RectTransform>();
                lRect.anchorMin = Vector2.zero;
                lRect.anchorMax = Vector2.one;
                lRect.offsetMin = Vector2.zero;
                lRect.offsetMax = Vector2.zero;

                var tmp = labelTf.GetComponent<TextMeshProUGUI>();
                if (tmp != null)
                {
                    tmp.text = label;
                    tmp.fontSize = fontSize;
                    tmp.fontStyle = FontStyles.Bold;
                    tmp.color = new Color(1f, 0.98f, 0.94f, 1f);
                    tmp.alignment = TextAlignmentOptions.Center;
                    tmp.raycastTarget = false;
                }
            }
        }

        static void AnchorHudText(Transform textTf, Vector2 cornerAnchor, Vector2 anchoredPos, TextAlignmentOptions alignment)
        {
            if (textTf == null)
            {
                return;
            }

            if (textTf.TryGetComponent<RectTransform>(out var rect))
            {
                rect.anchorMin = cornerAnchor;
                rect.anchorMax = cornerAnchor;
                rect.pivot = cornerAnchor;
                rect.sizeDelta = new Vector2(520f, 60f);
                rect.anchoredPosition = anchoredPos;
            }

            if (textTf.TryGetComponent<TextMeshProUGUI>(out var tmp))
            {
                tmp.alignment = alignment;
            }
        }

        static GameObject FindRoot(string name)
        {
            foreach (var transform in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (transform.name == name && transform.parent == null)
                {
                    return transform.gameObject;
                }
            }

            return null;
        }

        static float GroundTop(GameObject ground)
        {
            var collider = ground.GetComponent<BoxCollider2D>();
            if (collider == null)
            {
                return ground.transform.position.y;
            }

            Vector2 top = collider.offset + Vector2.up * (collider.size.y * 0.5f);
            return ground.transform.TransformPoint(top).y;
        }

        static GameObject PlaceVisual(GameObject ruby)
        {
            var existing = ruby.transform.Find("RubyVisual");
            GameObject visual;
            if (existing != null)
            {
                visual = existing.gameObject;
            }
            else
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(VisualPath);
                visual = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                visual.transform.SetParent(ruby.transform, false);
            }

            var capsule = ruby.GetComponent<CapsuleCollider2D>();
            float feet = capsule != null ? capsule.offset.y - capsule.size.y * 0.5f : -0.45f;
            visual.transform.localPosition = new Vector3(0f, feet, 0f);
            visual.transform.localScale = new Vector3(1.35f, 1.35f, 1f);
            visual.transform.localRotation = Quaternion.identity;
            return visual;
        }

        static GameObject PlaceAudio()
        {
            var existing = FindRoot("RubyAudioRig");
            if (existing != null)
            {
                return existing;
            }

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AudioPath);
            return (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        }

        static void PlaceYard(float groundTop)
        {
            var existing = FindRoot("Backyard");
            GameObject yard = existing;
            if (yard == null)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(YardPath);
                yard = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            }

            yard.transform.position = new Vector3(0f, groundTop, 0f);
            // Slightly widen background layers so wider aspect ratios (>16:9) never show bars on the sides
            var sky = yard.transform.Find("Sky");
            if (sky != null)
            {
                var s = sky.localScale;
                sky.localScale = new Vector3(Mathf.Max(s.x, 1.25f), s.y, 1f);
            }

            var midground = yard.transform.Find("Midground");
            if (midground != null)
            {
                var s = midground.localScale;
                midground.localScale = new Vector3(Mathf.Max(s.x, 1.25f), s.y, 1f);
            }

            var yardGround = yard.transform.Find("Ground");
            if (yardGround != null && yardGround.TryGetComponent<SpriteRenderer>(out var groundSr))
            {
                groundSr.size = new Vector2(Mathf.Max(groundSr.size.x, 19f), groundSr.size.y);
            }

            foreach (var collider in yard.GetComponentsInChildren<Collider2D>(true))
            {
                collider.enabled = false;
            }
        }

        static void DressFrisbee()
        {
            var ring = AssetDatabase.LoadAssetAtPath<Sprite>(RingPath);
            if (ring == null)
            {
                Debug.LogError("Flying ring sprite is missing at " + RingPath);
                return;
            }

            var contents = PrefabUtility.LoadPrefabContents(FrisbeePath);
            var renderer = contents.GetComponent<SpriteRenderer>();
            renderer.sprite = ring;
            renderer.color = Color.white;
            renderer.sortingLayerName = "RF_Projectiles";
            float largest = Mathf.Max(ring.bounds.size.x, ring.bounds.size.y);
            if (largest > 0.01f)
            {
                contents.transform.localScale = Vector3.one * (0.9f / largest);
            }

            var rb = contents.GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                rb.gravityScale = 0.52f;
            }

            var frisbee = contents.GetComponent<Frisbee>();
            if (frisbee != null)
            {
                var fSo = new SerializedObject(frisbee);
                fSo.FindProperty("groundY").floatValue = -2.22f;
                fSo.FindProperty("spinSpeed").floatValue = 420f;
                fSo.ApplyModifiedPropertiesWithoutUndo();
            }

            PrefabUtility.SaveAsPrefabAsset(contents, FrisbeePath);
            PrefabUtility.UnloadPrefabContents(contents);
        }

        static void RegisterRealBarkInBank(AudioClip realBarkClip)
        {
            var bank = AssetDatabase.LoadAssetAtPath<RubyAudioBank>(AudioBankPath);
            if (bank == null || bank.entries == null)
            {
                return;
            }

            UnityEngine.Audio.AudioMixerGroup sfxGroup = null;
            for (int i = 0; i < bank.entries.Length; i++)
            {
                var entry = bank.entries[i];
                if (entry == null)
                {
                    continue;
                }

                if (sfxGroup == null && entry.group != null && entry.group.name == "SFX Mix")
                {
                    sfxGroup = entry.group;
                }

                if (entry.key == "ruby_bark")
                {
                    entry.clip = realBarkClip;
                    entry.volume = 0.95f;
                    EditorUtility.SetDirty(bank);
                    return;
                }
            }

            if (sfxGroup != null)
            {
                var updated = new RubyAudioBank.Entry[bank.entries.Length + 1];
                System.Array.Copy(bank.entries, updated, bank.entries.Length);
                updated[bank.entries.Length] = new RubyAudioBank.Entry
                {
                    key = "ruby_bark",
                    clip = realBarkClip,
                    group = sfxGroup,
                    volume = 0.95f,
                    loop = false
                };
                bank.entries = updated;
                EditorUtility.SetDirty(bank);
            }
        }
    }
}
