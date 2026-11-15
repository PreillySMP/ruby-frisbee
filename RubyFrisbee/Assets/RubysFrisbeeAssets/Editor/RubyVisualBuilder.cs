using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;
using static RubysFrisbee.Assets.Editor.RubyAssetUtility;

namespace RubysFrisbee.Assets.Editor
{
    internal static class RubyVisualBuilder
    {
        internal const string MaterialPath = Root + "/Generated/Materials/RubySprites.mat";

        internal static Material BuildMaterial(RubySetupReport report)
        {
            Shader shader = Shader.Find("Sprites/Default");
            if (!shader)
            {
                shader = Shader.Find("Unlit/Transparent");
                Require(shader, "Neither built-in Sprites/Default nor Unlit/Transparent is available. This package targets Built-in 2D; restore the built-in shaders before setup. No pink/error material will be saved.");
                report.warnings.Add("Sprites/Default was unavailable; using Unlit/Transparent. This fallback preserves transparency but not sprite vertex tint. Restore Sprites/Default and repair when using tint.");
            }
            EnsureParent(MaterialPath);
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            Require(material || !AssetDatabase.LoadMainAssetAtPath(MaterialPath), "Another asset occupies material path " + MaterialPath);
            if (!material) { material = new Material(shader) { name = "RubySprites" }; AssetDatabase.CreateAsset(material, MaterialPath); }
            else material.shader = shader;
            if (material.HasProperty("_Color")) material.SetColor("_Color", Color.white);
            EditorUtility.SetDirty(material);
            report.Generated(MaterialPath);
            return material;
        }

        static SpriteRenderer Renderer(GameObject go, Sprite sprite, string sortingLayer, Material material)
        {
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingLayerName = sortingLayer;
            renderer.sharedMaterial = material;
            return renderer;
        }
        static GameObject Child(GameObject parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            return go;
        }
        static void SavePrefab(GameObject root, string path, RubySetupReport report)
        {
            EnsureParent(path);
            Require(!AssetDatabase.LoadMainAssetAtPath(path) || AssetDatabase.LoadAssetAtPath<GameObject>(path), "Another asset occupies prefab path " + path);
            PrefabUtility.SaveAsPrefabAsset(root, path, out bool success);
            Require(success, "Unity could not save prefab " + path + ". Check write permissions and Console errors.");
            report.Generated(path); ++report.prefabCount;
            Object.DestroyImmediate(root);
        }

        internal static void BuildPrefabs(RubyAssetManifest manifest, RubySetupReport report)
        {
            var material = BuildMaterial(report);
            var active = SceneManager.GetActiveScene();
            // Build in a new owned scratch scene, never in an unsaved user scene.
            var scratch = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            try
            {
                SceneManager.SetActiveScene(scratch);
                var ruby = new GameObject("RubyVisual");
                Renderer(ruby, Sprite(manifest.defaultSprite), "RF_Character", material);
                var animator = ruby.AddComponent<Animator>();
                animator.runtimeAnimatorController = Load<RuntimeAnimatorController>(ControllerPath);
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                ruby.AddComponent<RubyAnimationDriver>();
                SavePrefab(ruby, PrefabPath("RubyVisual"), report);

                var ring = new GameObject("Ring");
                Renderer(ring, Sprite(manifest.ringSprite), "RF_Projectiles", material);
                SavePrefab(ring, PrefabPath("Ring"), report);

                foreach (var effect in manifest.effects)
                {
                    var root = new GameObject(effect.name);
                    var clip = manifest.clips.Single(c => c.name == effect.clip);
                    Renderer(root, Sprite(clip.keys[0].sprite), "RF_Effects", material);
                    var a = root.AddComponent<Animator>();
                    a.runtimeAnimatorController = Load<RuntimeAnimatorController>(EffectControllerPath(effect.name));
                    a.applyRootMotion = false;
                    a.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                    var helper = root.AddComponent<RubyOneShotEffect>();
                    helper.duration = effect.duration;
                    helper.autoDisable = !effect.loop;
                    SavePrefab(root, EffectPrefabPath(effect.name), report);
                }

                var backyard = new GameObject("Backyard");
                var env = manifest.environment;
                RenderRect(backyard, "Sky", env.skyPath, env.skyRect, env, "RF_Background", material);
                RenderRect(backyard, "Midground", env.midgroundPath, env.midgroundRect, env, "RF_Midground", material);
                var ground = Child(backyard, "Ground");
                var groundSprite = Sprite(env.groundPath);
                var groundRenderer = Renderer(ground, groundSprite, "RF_Ground", material);
                groundRenderer.drawMode = SpriteDrawMode.Tiled;
                groundRenderer.tileMode = SpriteTileMode.Continuous;
                float width = env.canvasWidth / env.pixelsPerUnit;
                float height = groundSprite.rect.height / groundSprite.pixelsPerUnit;
                groundRenderer.size = new Vector2(width, height);
                ground.transform.localPosition = Vector3.zero; // Pivot(0.5,0.75) sits on baseline zero.
                var collider = ground.AddComponent<BoxCollider2D>();
                // Art extends 1/4 of its height above the baseline. Collision surface stays at baseline y=0.
                float depth = height * 0.75f;
                collider.size = new Vector2(width, depth);
                collider.offset = new Vector2(0f, -depth * 0.5f);
                foreach (var prop in env.props ?? Array.Empty<PropPlacementDef>())
                {
                    var child = Child(backyard, prop.name);
                    Renderer(child, Sprite(prop.path), "RF_Props", material);
                    child.transform.localPosition = new Vector3(prop.x, prop.y, 0f); // Already WORLD units; do not divide again.
                }
                SavePrefab(backyard, PrefabPath("Backyard"), report);
                AssetDatabase.SaveAssets();
            }
            finally
            {
                if (active.IsValid() && active.isLoaded) SceneManager.SetActiveScene(active);
                if (scratch.IsValid() && scratch.isLoaded) EditorSceneManager.CloseScene(scratch, true); // Only our own scratch scene.
            }
        }

        static void RenderRect(GameObject parent, string name, string path, RectDef rect, EnvironmentDef env, string layer, Material material)
        {
            var go = Child(parent, name);
            var sprite = Sprite(path);
            Renderer(go, sprite, layer, material);
            float pivotX = sprite.pivot.x / sprite.rect.width;
            float pivotY = sprite.pivot.y / sprite.rect.height;
            // Viewport origin is top-left. World origin is horizontally centered at the ground baseline.
            go.transform.localPosition = new Vector3(
                (rect.x + rect.width * pivotX - env.canvasWidth * 0.5f) / env.pixelsPerUnit,
                (env.baselineY - rect.y - rect.height * (1f - pivotY)) / env.pixelsPerUnit, 0f);
            go.transform.localScale = new Vector3(
                rect.width / env.pixelsPerUnit / sprite.bounds.size.x,
                rect.height / env.pixelsPerUnit / sprite.bounds.size.y, 1f);
        }

        internal static void CreatePreviewIfMissing(RubyAssetManifest manifest, RubySetupReport report)
        {
            if (File.Exists(ScenePath) || AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath))
            {
                report.Generated(ScenePath);
                report.warnings.Add("Existing preview scene was preserved without opening or overwriting it. Delete that generated scene explicitly if a fresh tester is desired.");
                return;
            }
            for (int i = 0; i < SceneManager.sceneCount; ++i)
                if (SceneManager.GetSceneAt(i).path == ScenePath)
                {
                    report.warnings.Add("An open preview scene has no saved asset. Save it yourself; setup will not overwrite or close it.");
                    throw new InvalidOperationException("The preview scene is open but missing on disk. Save it before setup can validate the package.");
                }
            EnsureParent(ScenePath);
            var previous = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            try
            {
                SceneManager.SetActiveScene(scene);
                var env = manifest.environment;
                var cameraObject = new GameObject("Ruby Preview Camera");
                var camera = cameraObject.AddComponent<Camera>();
                camera.orthographic = true;
                camera.orthographicSize = env.canvasHeight / env.pixelsPerUnit * 0.5f;
                camera.transform.position = new Vector3(0f, (env.baselineY - env.canvasHeight * 0.5f) / env.pixelsPerUnit, -10f);
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.57f, 0.81f, 0.97f);
                // Preview camera is deliberately untagged and has no AudioListener: no duplicate-main/listener side effects while open additively.
                PrefabUtility.InstantiatePrefab(Load<GameObject>(PrefabPath("Backyard")), scene);
                var ruby = (GameObject)PrefabUtility.InstantiatePrefab(Load<GameObject>(PrefabPath("RubyVisual")), scene);
                var ring = (GameObject)PrefabUtility.InstantiatePrefab(Load<GameObject>(PrefabPath("Ring")), scene);
                ring.transform.position = new Vector3(2.5f, 1.5f, 0f);
                var audio = (GameObject)PrefabUtility.InstantiatePrefab(Load<GameObject>(PrefabPath("RubyAudioRig")), scene);
                var controls = new GameObject("Ruby Asset Preview Controls").AddComponent<RubyAssetPreview>();
                controls.ruby = ruby.GetComponent<RubyAnimationDriver>();
                controls.audioPlayer = audio.GetComponent<RubyAudioPlayer>();
                var effects = new RubyOneShotEffect[manifest.effects.Length];
                for (int i = 0; i < manifest.effects.Length; ++i)
                {
                    var fx = (GameObject)PrefabUtility.InstantiatePrefab(Load<GameObject>(EffectPrefabPath(manifest.effects[i].name)), scene);
                    fx.transform.position = new Vector3((i - 3) * 1.5f, 2.5f, 0f);
                    effects[i] = fx.GetComponent<RubyOneShotEffect>();
                    fx.SetActive(false);
                }
                controls.effects = effects;
                Require(EditorSceneManager.SaveScene(scene, ScenePath, false), "Could not save preview scene " + ScenePath + ". User scenes remain untouched.");
                report.Generated(ScenePath);
                report.warnings.Add("Preview camera is untagged and no listener is serialized. In Play mode the tester adds a listener only if no enabled listener exists; existing user listeners are never disabled.");
            }
            finally
            {
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
                if (scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene, true); // Only the scene created by this method.
            }
        }
    }
}
