using RubyFrisbee.Core;
using RubyFrisbee.Gameplay;
using RubyFrisbee.Player;
using RubyFrisbee.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace RubyFrisbee.Editor
{
    /// <summary>
    /// One-click scene bootstrap for Ruby's Frisbee game.
    /// Run via menu: Ruby Frisbee -> Setup Game Scene
    /// </summary>
    public static class RubyFrisbeeSceneSetup
    {
        const string ScenePath = "Assets/Scenes/GameScene.unity";
        const string FrisbeePrefabPath = "Assets/Prefabs/Frisbee.prefab";
        const string GroundLayerName = "Default";

        [MenuItem("Ruby Frisbee/Setup Game Scene")]
        public static void SetupGameScene()
        {
            EnsureFolders();
            ConfigureTagsAndLayers();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            CreateCamera();
            CreateBackground();
            var ground = CreateGround();
            var ruby = CreateRuby(ground.layer);
            var frisbeePrefab = CreateFrisbeePrefab();
            CreateSystems(frisbeePrefab);
            CreateUI();

            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("Ruby's Frisbee scene is ready! Press Play to start. Controls: Arrow keys / A-D to move, Space / W to jump.");
        }

        static void EnsureFolders()
        {
            CreateFolder("Assets/Scenes");
            CreateFolder("Assets/Prefabs");
            CreateFolder("Assets/Sprites");
            CreateFolder("Assets/Materials");
        }

        static void CreateFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            var parts = path.Split('/');
            var current = parts[0];
            for (var i = 1; i < parts.Length; i++)
            {
                var next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }

                current = next;
            }
        }

        static void ConfigureTagsAndLayers()
        {
            // Tags are defined in TagManager.asset; layers use Default for ground checks in v1.
        }

        static void CreateCamera()
        {
            var cameraGo = new GameObject("Main Camera");
            cameraGo.tag = "MainCamera";
            var camera = cameraGo.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 5f;
            camera.backgroundColor = new Color(0.53f, 0.81f, 0.98f);
            cameraGo.transform.position = new Vector3(0f, 0f, -10f);
            cameraGo.AddComponent<AudioListener>();
            cameraGo.AddComponent<CameraJuice>();
        }

        static void CreateBackground()
        {
            var sky = CreateSpriteObject("Sky", new Vector3(0f, 0f, 5f), new Vector2(30f, 12f), new Color(0.53f, 0.81f, 0.98f));
            sky.GetComponent<SpriteRenderer>().sortingOrder = -20;

            var grass = CreateSpriteObject("GrassField", new Vector3(0f, -3.5f, 4f), new Vector2(30f, 4f), new Color(0.30f, 0.62f, 0.28f));
            grass.GetComponent<SpriteRenderer>().sortingOrder = -10;

            var fence = CreateSpriteObject("Fence", new Vector3(0f, 0.5f, 3f), new Vector2(30f, 2f), new Color(0.55f, 0.38f, 0.22f));
            fence.GetComponent<SpriteRenderer>().sortingOrder = -15;
        }

        static GameObject CreateGround()
        {
            var ground = CreateSpriteObject("Ground", new Vector3(0f, -2.8f, 0f), new Vector2(30f, 1f), new Color(0.22f, 0.48f, 0.20f));
            ground.tag = "Ground";

            var collider = ground.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(30f, 1f);

            var rb = ground.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Static;

            return ground;
        }

        static GameObject CreateRuby(int groundLayer)
        {
            var ruby = CreateSpriteObject("Ruby", new Vector3(-4f, -2.1f, 0f), new Vector2(1.2f, 0.9f), new Color(0.77f, 0.64f, 0.35f));
            ruby.GetComponent<SpriteRenderer>().sortingOrder = 5;

            // Black saddle / mask accent
            var mask = CreateSpriteObject("Mask", Vector3.zero, new Vector2(0.5f, 0.35f), new Color(0.10f, 0.10f, 0.10f));
            mask.transform.SetParent(ruby.transform, false);
            mask.transform.localPosition = new Vector3(0.15f, 0.1f, -0.01f);
            mask.GetComponent<SpriteRenderer>().sortingOrder = 6;

            // Ears
            CreateEar(ruby.transform, new Vector3(-0.2f, 0.35f, -0.01f));
            CreateEar(ruby.transform, new Vector3(0.2f, 0.35f, -0.01f));

            var rb = ruby.AddComponent<Rigidbody2D>();
            rb.freezeRotation = true;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            var collider = ruby.AddComponent<CapsuleCollider2D>();
            collider.size = new Vector2(0.9f, 0.7f);

            var catchZone = ruby.AddComponent<CircleCollider2D>();
            catchZone.isTrigger = true;
            catchZone.radius = 0.55f;
            catchZone.offset = new Vector2(0.25f, 0.1f);

            var controller = ruby.AddComponent<RubyController>();
            LayerMask groundMask = 1 << groundLayer;
            SetPrivateField(controller, "groundLayer", groundMask);

            return ruby;
        }

        static void CreateEar(Transform parent, Vector3 localPosition)
        {
            var ear = CreateSpriteObject("Ear", localPosition, new Vector2(0.18f, 0.35f), new Color(0.10f, 0.10f, 0.10f));
            ear.transform.SetParent(parent, false);
            ear.GetComponent<SpriteRenderer>().sortingOrder = 7;
        }

        static Frisbee CreateFrisbeePrefab()
        {
            var frisbeeGo = CreateSpriteObject("Frisbee", Vector3.zero, new Vector2(1f, 1f), new Color(0.95f, 0.45f, 0.15f), GetFrisbeeSprite());
            frisbeeGo.GetComponent<SpriteRenderer>().sortingOrder = 10;
            frisbeeGo.tag = "Frisbee";

            var rb = frisbeeGo.AddComponent<Rigidbody2D>();
            rb.gravityScale = 1.2f;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            var collider = frisbeeGo.AddComponent<CircleCollider2D>();
            collider.isTrigger = true;
            collider.radius = 0.35f;

            var frisbee = frisbeeGo.AddComponent<Frisbee>();

            var prefab = PrefabUtility.SaveAsPrefabAsset(frisbeeGo, FrisbeePrefabPath);
            Object.DestroyImmediate(frisbeeGo);
            return prefab.GetComponent<Frisbee>();
        }

        static void CreateSystems(Frisbee frisbeePrefab)
        {
            var systems = new GameObject("Systems");
            systems.AddComponent<GameManager>();
            systems.AddComponent<ScoreManager>();

            var spawnerGo = new GameObject("FrisbeeSpawner");
            spawnerGo.transform.SetParent(systems.transform);
            var spawner = spawnerGo.AddComponent<FrisbeeSpawner>();
            spawner.SetPrefab(frisbeePrefab);
        }

        static void CreateUI()
        {
            var canvasGo = new GameObject("Canvas");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasGo.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasGo.GetComponent<CanvasScaler>().referenceResolution = new Vector2(1920, 1080);
            canvasGo.AddComponent<GraphicRaycaster>();

            var eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<UnityEngine.EventSystems.EventSystem>();
            eventSystem.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();

            var gameUI = canvasGo.AddComponent<GameUI>();

            var menuPanel = CreatePanel(canvasGo.transform, "MenuPanel", new Color(0f, 0f, 0f, 0.55f));
            var title = CreateText(menuPanel.transform, "Title", "Ruby's Frisbee", 72, TextAlignmentOptions.Center, new Vector2(0f, 120f));
            title.fontStyle = FontStyles.Bold;
            var subtitle = CreateText(menuPanel.transform, "Subtitle", "Catch the frisbees!", 28, TextAlignmentOptions.Center, new Vector2(0f, 40f));
            var menuHigh = CreateText(menuPanel.transform, "MenuHighScore", "High Score: 0", 32, TextAlignmentOptions.Center, new Vector2(0f, -40f));
            var playButton = CreateButton(menuPanel.transform, "PlayButton", "Play", new Vector2(0f, -140f));

            var hudPanel = CreatePanel(canvasGo.transform, "HudPanel", new Color(0f, 0f, 0f, 0f));
            var score = CreateText(hudPanel.transform, "ScoreText", "Score: 0", 36, TextAlignmentOptions.TopLeft, new Vector2(-800f, 450f));
            var combo = CreateText(hudPanel.transform, "ComboText", string.Empty, 30, TextAlignmentOptions.TopLeft, new Vector2(-800f, 400f));
            combo.color = new Color(1f, 0.85f, 0.2f);
            var lives = CreateText(hudPanel.transform, "LivesText", "Lives: 3", 36, TextAlignmentOptions.TopRight, new Vector2(800f, 450f));
            var hudHigh = CreateText(hudPanel.transform, "HighScoreText", "Best: 0", 28, TextAlignmentOptions.TopRight, new Vector2(800f, 400f));

            var gameOverPanel = CreatePanel(canvasGo.transform, "GameOverPanel", new Color(0f, 0f, 0f, 0.65f));
            gameOverPanel.SetActive(false);
            var gameOverTitle = CreateText(gameOverPanel.transform, "GameOverTitle", "Game Over", 64, TextAlignmentOptions.Center, new Vector2(0f, 100f));
            gameOverTitle.fontStyle = FontStyles.Bold;
            var finalScore = CreateText(gameOverPanel.transform, "FinalScore", "Score: 0", 36, TextAlignmentOptions.Center, new Vector2(0f, 20f));
            var gameOverHigh = CreateText(gameOverPanel.transform, "GameOverHighScore", "Best: 0", 32, TextAlignmentOptions.Center, new Vector2(0f, -40f));
            var restartButton = CreateButton(gameOverPanel.transform, "RestartButton", "Play Again", new Vector2(0f, -130f));
            var menuButton = CreateButton(gameOverPanel.transform, "MenuButton", "Main Menu", new Vector2(0f, -210f));

            var mobileControls = CreateMobileControls(canvasGo.transform);

            gameUI.WireReferences(
                score, combo, lives, hudHigh,
                menuPanel, hudPanel, gameOverPanel,
                menuHigh, playButton,
                finalScore, gameOverHigh,
                restartButton, menuButton,
                mobileControls);
        }

        static MobileControlsUI CreateMobileControls(Transform canvas)
        {
            var root = new GameObject("MobileControls");
            root.transform.SetParent(canvas, false);

            var rootRect = root.AddComponent<RectTransform>();
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;

            var left = CreateTouchButton(root.transform, "LeftButton", "\u2190", new Vector2(-750f, -420f), new Vector2(160f, 160f), new Color(1f, 1f, 1f, 0.25f));
            var right = CreateTouchButton(root.transform, "RightButton", "\u2192", new Vector2(-550f, -420f), new Vector2(160f, 160f), new Color(1f, 1f, 1f, 0.25f));
            var jump = CreateTouchButton(root.transform, "JumpButton", "Jump", new Vector2(700f, -420f), new Vector2(200f, 160f), new Color(0.18f, 0.55f, 0.34f, 0.85f));

            var leftHold = left.gameObject.AddComponent<HoldButton>();
            var rightHold = right.gameObject.AddComponent<HoldButton>();

            var mobileControls = root.AddComponent<MobileControlsUI>();
            mobileControls.WireReferences(root, leftHold, rightHold, jump);

            root.SetActive(false);
            return mobileControls;
        }

        static Button CreateTouchButton(Transform parent, string name, string label, Vector2 anchoredPosition, Vector2 size, Color color)
        {
            var buttonGo = new GameObject(name);
            buttonGo.transform.SetParent(parent, false);

            var rect = buttonGo.AddComponent<RectTransform>();
            rect.sizeDelta = size;
            rect.anchoredPosition = anchoredPosition;

            var image = buttonGo.AddComponent<Image>();
            image.color = color;

            var button = buttonGo.AddComponent<Button>();
            button.transition = Selectable.Transition.ColorTint;

            var labelGo = new GameObject("Label");
            labelGo.transform.SetParent(buttonGo.transform, false);
            var labelRect = labelGo.AddComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;

            var text = labelGo.AddComponent<TextMeshProUGUI>();
            text.text = label;
            text.fontSize = label.Length <= 2 ? 64 : 36;
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.white;

            return button;
        }

        static GameObject CreateSpriteObject(string name, Vector3 position, Vector2 size, Color color, Sprite sprite = null)
        {
            var go = new GameObject(name);
            go.transform.position = position;

            var spriteRenderer = go.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = sprite != null ? sprite : GetPlaceholderSprite();
            spriteRenderer.color = color;
            go.transform.localScale = new Vector3(size.x, size.y, 1f);

            return go;
        }

        const string PixelSpritePath = "Assets/Sprites/Pixel.png";
        const string FrisbeeSpritePath = "Assets/Sprites/Frisbee.png";

        static Sprite GetPlaceholderSprite()
        {
            return LoadOrCreateSprite(PixelSpritePath, 4, fillCircle: false, pixelsPerUnit: 4f);
        }

        static Sprite GetFrisbeeSprite()
        {
            return LoadOrCreateSprite(FrisbeeSpritePath, 64, fillCircle: true, pixelsPerUnit: 64f);
        }

        static Sprite LoadOrCreateSprite(string path, int size, bool fillCircle, float pixelsPerUnit)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (existing != null)
            {
                return existing;
            }

            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var center = (size - 1) * 0.5f;
            var radius = size * 0.5f;
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    if (!fillCircle)
                    {
                        texture.SetPixel(x, y, Color.white);
                        continue;
                    }

                    var dx = x - center;
                    var dy = y - center;
                    texture.SetPixel(x, y, dx * dx + dy * dy <= radius * radius ? Color.white : Color.clear);
                }
            }

            texture.Apply();
            var directory = System.IO.Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory) && !AssetDatabase.IsValidFolder(directory))
            {
                CreateFolder(directory.Replace('\\', '/'));
            }

            System.IO.File.WriteAllBytes(path, texture.EncodeToPNG());
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spritePixelsPerUnit = pixelsPerUnit;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();

            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        static GameObject CreatePanel(Transform parent, string name, Color color)
        {
            var panel = new GameObject(name);
            panel.transform.SetParent(parent, false);

            var rect = panel.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var image = panel.AddComponent<Image>();
            image.color = color;

            return panel;
        }

        static TextMeshProUGUI CreateText(Transform parent, string name, string content, int fontSize, TextAlignmentOptions alignment, Vector2 anchoredPosition)
        {
            var textGo = new GameObject(name);
            textGo.transform.SetParent(parent, false);

            var rect = textGo.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(800f, 100f);
            rect.anchoredPosition = anchoredPosition;

            var text = textGo.AddComponent<TextMeshProUGUI>();
            text.text = content;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = Color.white;

            return text;
        }

        static Button CreateButton(Transform parent, string name, string label, Vector2 anchoredPosition)
        {
            var buttonGo = new GameObject(name);
            buttonGo.transform.SetParent(parent, false);

            var rect = buttonGo.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(260f, 64f);
            rect.anchoredPosition = anchoredPosition;

            var image = buttonGo.AddComponent<Image>();
            image.color = new Color(0.18f, 0.55f, 0.34f);

            var button = buttonGo.AddComponent<Button>();

            var labelGo = new GameObject("Label");
            labelGo.transform.SetParent(buttonGo.transform, false);
            var labelRect = labelGo.AddComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;

            var text = labelGo.AddComponent<TextMeshProUGUI>();
            text.text = label;
            text.fontSize = 28;
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.white;

            return button;
        }

        static void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            field?.SetValue(target, value);
        }
    }
}
