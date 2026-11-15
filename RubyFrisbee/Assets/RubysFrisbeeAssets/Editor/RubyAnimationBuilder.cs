using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.U2D;
using Object = UnityEngine.Object;
using static RubysFrisbee.Assets.Editor.RubyAssetUtility;

namespace RubysFrisbee.Assets.Editor
{
    internal static class RubyAnimationBuilder
    {
        internal static void InstallSortingLayers(RubyAssetManifest manifest)
        {
            var tagManager = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset").FirstOrDefault();
            Require(tagManager, "Cannot load ProjectSettings/TagManager.asset; sorting layer installation cannot continue.");
            var serialized = new SerializedObject(tagManager);
            var layers = serialized.FindProperty("m_SortingLayers");
            Require(layers != null && layers.isArray, "Unity TagManager sorting layer serialization is unavailable. Add the RF sorting layers manually, then retry.");
            var names = new HashSet<string>(StringComparer.Ordinal);
            var ids = new HashSet<int>();
            for (int i = 0; i < layers.arraySize; ++i)
            {
                var entry = layers.GetArrayElementAtIndex(i);
                names.Add(entry.FindPropertyRelative("name").stringValue);
                ids.Add(entry.FindPropertyRelative("uniqueID").intValue);
            }
            bool changed = false;
            foreach (var name in manifest.sortingLayers)
            {
                if (names.Contains(name)) continue;
                int id;
                do { id = BitConverter.ToInt32(Guid.NewGuid().ToByteArray(), 0) & int.MaxValue; } while (id == 0 || !ids.Add(id));
                int index = layers.arraySize;
                layers.InsertArrayElementAtIndex(index);
                var entry = layers.GetArrayElementAtIndex(index);
                entry.FindPropertyRelative("name").stringValue = name;
                entry.FindPropertyRelative("uniqueID").intValue = id;
                var locked = entry.FindPropertyRelative("locked");
                if (locked != null) locked.boolValue = false;
                names.Add(name); changed = true;
            }
            if (changed)
            {
                // Only m_SortingLayers was touched. Do not write serialized YAML or replace tags/physics layers.
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(tagManager);
                AssetDatabase.SaveAssets();
            }
        }

        internal static void ImportTextures(RubyAssetManifest manifest, RubySetupReport report)
        {
            foreach (var def in manifest.textures)
            {
                string path = PathFor(def.path);
                if (!(AssetImporter.GetAtPath(path) is TextureImporter importer))
                {
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                    importer = AssetImporter.GetAtPath(path) as TextureImporter;
                }
                Require(importer != null, "No TextureImporter found for " + path + ". Supply a supported PNG texture.");
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                settings.spriteAlignment = (int)SpriteAlignment.Custom;
                settings.spritePivot = new Vector2(def.pivotX, def.pivotY);
                settings.spriteBorder = new Vector4(def.borderLeft, def.borderBottom, def.borderRight, def.borderTop);
                settings.spritePixelsPerUnit = def.pixelsPerUnit;
                settings.spriteMeshType = SpriteMeshType.FullRect;
                settings.spriteExtrude = 0;
                importer.SetTextureSettings(settings);
                importer.alphaSource = TextureImporterAlphaSource.FromInput;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.isReadable = false;
                importer.sRGBTexture = true;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.wrapMode = def.wrapRepeat ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
                importer.filterMode = FilterMode.Bilinear;
                importer.maxTextureSize = def.maxSize;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.crunchedCompression = false;
                // Clear common platform overrides left by earlier revisions of these package-owned textures.
                foreach (var platform in new[] { "Standalone", "Android", "iPhone", "WebGL", "Windows Store Apps" })
                    importer.ClearPlatformTextureSettings(platform);
                importer.SaveAndReimport();
                var sprite = Sprite(def.path);
                Require(def.borderLeft + def.borderRight <= sprite.rect.width && def.borderBottom + def.borderTop <= sprite.rect.height,
                    "Nine-slice borders exceed imported sprite dimensions: " + path + ". Increase maxSize or fix the manifest border.");
                report.importedTexturePaths.Add(path);
                ++report.textureCount;
            }
        }

        internal static void BuildClips(RubyAssetManifest manifest, RubySetupReport report)
        {
            foreach (var def in manifest.clips)
            {
                string path = ClipPath(def.name);
                EnsureParent(path);
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                Require(clip || !AssetDatabase.LoadMainAssetAtPath(path), "Expected an AnimationClip, but another asset occupies " + path);
                if (!clip)
                {
                    clip = new AnimationClip { name = def.name };
                    AssetDatabase.CreateAsset(clip, path);
                }
                // Repair this generated clip in place, keeping the main asset GUID and local file ID.
                foreach (var binding in AnimationUtility.GetCurveBindings(clip)) AnimationUtility.SetEditorCurve(clip, binding, null);
                foreach (var binding in AnimationUtility.GetObjectReferenceCurveBindings(clip)) AnimationUtility.SetObjectReferenceCurve(clip, binding, null);
                clip.name = def.name;
                clip.legacy = false;
                clip.frameRate = def.fps;
                var keys = new List<ObjectReferenceKeyframe>();
                foreach (var key in def.keys) keys.Add(new ObjectReferenceKeyframe { time = key.time, value = Sprite(key.sprite) });
                var spriteBinding = EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite");
                AnimationUtility.SetObjectReferenceCurve(clip, spriteBinding, keys.ToArray());
                ApplyClipRange(clip, def);
                // Unity 6000.0 reports clip.length as one sample past the last key, so an
                // explicit endpoint key makes every clip one frame too long. Add it only
                // when this Editor treats length as the last key time.
                if (clip.length + 0.0001f < def.length)
                {
                    keys.Add(new ObjectReferenceKeyframe
                    {
                        time = def.length,
                        value = Sprite(def.loop ? def.keys[0].sprite : def.keys[def.keys.Length - 1].sprite)
                    });
                    AnimationUtility.SetObjectReferenceCurve(clip, spriteBinding, keys.ToArray());
                    ApplyClipRange(clip, def);
                }
                AnimationUtility.SetAnimationEvents(clip, (def.events ?? Array.Empty<ClipEventDef>()).Select(e => new AnimationEvent
                {
                    time = e.time, functionName = "OnAnimationCue", stringParameter = e.cue,
                    messageOptions = SendMessageOptions.DontRequireReceiver
                }).ToArray());
                EditorUtility.SetDirty(clip);
                report.Generated(path); ++report.clipCount;
            }
            AssetDatabase.SaveAssets();
        }

        static void ApplyClipRange(AnimationClip clip, ClipDef def)
        {
            var clipSettings = AnimationUtility.GetAnimationClipSettings(clip);
            clipSettings.loopTime = def.loop;
            clipSettings.loopBlend = false;
            clipSettings.startTime = 0f;
            clipSettings.stopTime = def.length;
            AnimationUtility.SetAnimationClipSettings(clip, clipSettings);
        }

        static AnimatorController ResetController(string path, string name)
        {
            EnsureParent(path);
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            Require(controller || !AssetDatabase.LoadMainAssetAtPath(path), "Expected an AnimatorController, but another asset occupies " + path);
            if (!controller) controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            Require(controller, "Unity could not create controller " + path);
            controller.name = name;
            controller.layers = Array.Empty<AnimatorControllerLayer>();
            controller.parameters = Array.Empty<AnimatorControllerParameter>();
            // Only disposable subassets INSIDE this generated controller are rebuilt. Main GUID is retained.
            foreach (var child in AssetDatabase.LoadAllAssetsAtPath(path))
                if (child && child != controller) Object.DestroyImmediate(child, true);
            controller.AddLayer("Base Layer");
            var layers = controller.layers;
            layers[0].defaultWeight = 1f;
            controller.layers = layers;
            return controller;
        }

        internal static void BuildControllers(RubyAssetManifest manifest, RubySetupReport report)
        {
            var controller = ResetController(ControllerPath, "Ruby");
            foreach (var p in manifest.controller.parameters)
                controller.AddParameter(new AnimatorControllerParameter
                {
                    name = p.name, type = (AnimatorControllerParameterType)Enum.Parse(typeof(AnimatorControllerParameterType), p.type),
                    defaultBool = p.defaultBool, defaultFloat = p.defaultFloat, defaultInt = p.defaultInt
                });
            var machine = controller.layers[0].stateMachine;
            var states = new Dictionary<string, AnimatorState>(StringComparer.Ordinal);
            foreach (var def in manifest.controller.states)
            {
                var state = machine.AddState(def.name, new Vector3(def.x, def.y, 0));
                state.motion = Load<AnimationClip>(ClipPath(def.clip));
                state.speed = def.speed;
                state.writeDefaultValues = false;
                states.Add(def.name, state);
                if (def.isDefault) machine.defaultState = state;
            }
            foreach (var def in manifest.controller.transitions)
            {
                var transition = def.from == "Any" ? machine.AddAnyStateTransition(states[def.to]) : states[def.from].AddTransition(states[def.to]);
                transition.hasExitTime = def.hasExitTime;
                transition.exitTime = def.exitTime;
                transition.hasFixedDuration = true;
                transition.duration = 0f; // Sprite frames must never cross-fade.
                transition.offset = 0f;
                transition.canTransitionToSelf = false;
                transition.interruptionSource = TransitionInterruptionSource.None;
                foreach (var c in def.conditions)
                    transition.AddCondition((AnimatorConditionMode)Enum.Parse(typeof(AnimatorConditionMode), c.mode), c.threshold, c.parameter);
                EditorUtility.SetDirty(transition);
            }
            EditorUtility.SetDirty(machine); EditorUtility.SetDirty(controller);
            report.Generated(ControllerPath); ++report.controllerCount;
            foreach (var effect in manifest.effects)
            {
                string path = EffectControllerPath(effect.name);
                var fx = ResetController(path, effect.name);
                var sm = fx.layers[0].stateMachine;
                var state = sm.AddState("Play");
                state.motion = Load<AnimationClip>(ClipPath(effect.clip));
                state.writeDefaultValues = false;
                sm.defaultState = state;
                EditorUtility.SetDirty(sm); EditorUtility.SetDirty(fx);
                report.Generated(path); ++report.controllerCount;
            }
            AssetDatabase.SaveAssets();
        }

        internal static void BuildAtlases(RubyAssetManifest manifest, RubySetupReport report)
        {
            foreach (var def in manifest.atlases)
            {
                string path = AtlasPath(def.name);
                EnsureParent(path);
                var atlas = AssetDatabase.LoadAssetAtPath<SpriteAtlas>(path);
                Require(atlas || !AssetDatabase.LoadMainAssetAtPath(path), "Expected a SpriteAtlas, but another asset occupies " + path);
                if (!atlas) { atlas = new SpriteAtlas { name = def.name }; AssetDatabase.CreateAsset(atlas, path); }
                atlas.SetIsVariant(false);
                var packing = atlas.GetPackingSettings();
                packing.enableRotation = false; packing.enableTightPacking = false; packing.padding = 4;
                atlas.SetPackingSettings(packing);
                var texture = atlas.GetTextureSettings();
                texture.generateMipMaps = false; texture.readable = false;
                texture.sRGB = true; texture.filterMode = FilterMode.Bilinear;
                atlas.SetTextureSettings(texture);
                var platform = atlas.GetPlatformSettings("DefaultTexturePlatform");
                platform.maxTextureSize = def.maxSize;
                platform.textureCompression = TextureImporterCompression.Uncompressed;
                platform.crunchedCompression = false;
                atlas.SetPlatformSettings(platform);
                foreach (var target in new[] { "Standalone", "Android", "iPhone", "WebGL", "Windows Store Apps" })
                {
                    var settings = atlas.GetPlatformSettings(target);
                    settings.overridden = false;
                    settings.maxTextureSize = def.maxSize;
                    atlas.SetPlatformSettings(settings);
                }
                atlas.SetIncludeInBuild(true);
                var previous = atlas.GetPackables();
                if (previous.Length > 0) atlas.Remove(previous);
                atlas.Add(def.folders.Select(f => (Object)Load<DefaultAsset>(PathFor(f))).ToArray());
                EditorUtility.SetDirty(atlas);
                report.Generated(path); ++report.atlasCount;
            }
            AssetDatabase.SaveAssets();
            // Native assets are ready for Unity's build-time packing. No global atlas mode/project setting is changed.
        }
    }
}
