using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.U2D;
using static RubysFrisbee.Assets.Editor.RubyAssetUtility;

namespace RubysFrisbee.Assets.Editor
{
    internal static class RubyPackageValidator
    {
        static void Check(RubySetupReport report, string label, Action action)
        {
            try { action(); }
            catch (Exception e) { report.errors.Add(label + ": " + e.Message); }
        }

        internal static void Validate(RubyAssetManifest manifest, RubySetupReport report)
        {
            Check(report, "Sorting layers", () =>
            {
                var names = new HashSet<string>(SortingLayer.layers.Select(l => l.name));
                foreach (string name in manifest.sortingLayers) Require(names.Contains(name), "Missing sorting layer " + name);
                var installedOrder = SortingLayer.layers.Select(l => l.name).Where(n => RequiredLayers.Contains(n)).ToArray();
                if (!installedOrder.SequenceEqual(RequiredLayers))
                    report.warnings.Add("Existing RF sorting layers are in a nonstandard order. Their positions were preserved intentionally; review the order manually in Tags and Layers.");
            });
            foreach (var def in manifest.textures)
                Check(report, "Texture " + def.path, () => ValidateTexture(def));
            foreach (var def in manifest.clips)
                Check(report, "Clip " + def.name, () => ValidateClip(def));
            Check(report, "Ruby controller", () => ValidateController(manifest.controller));
            foreach (var def in manifest.effects)
                Check(report, "Effect " + def.name, () => ValidateEffect(def, manifest));
            foreach (var def in manifest.atlases)
                Check(report, "Atlas " + def.name, () =>
                {
                    var atlas = Load<SpriteAtlas>(AtlasPath(def.name));
                    var settings = atlas.GetPackingSettings();
                    Require(!settings.enableRotation && !settings.enableTightPacking && settings.padding == 4, "Packing must disable rotation/tight packing with padding 4.");
                    Require(atlas.IsIncludeInBuild(), "Atlas is not included in builds.");
                    Require(atlas.GetPlatformSettings("DefaultTexturePlatform").maxTextureSize == def.maxSize, "Atlas maxSize differs from manifest.");
                    var paths = atlas.GetPackables().Select(AssetDatabase.GetAssetPath).OrderBy(p => p).ToArray();
                    Require(paths.SequenceEqual(def.folders.Select(PathFor).OrderBy(p => p)), "Atlas source folders differ from manifest.");
                });
            Check(report, "RubyVisual prefab", () =>
            {
                var root = Load<GameObject>(PrefabPath("RubyVisual"));
                var renderer = ValidateRenderer(root, "RF_Character", manifest.defaultSprite);
                var animator = root.GetComponent<Animator>();
                Require(animator && animator.runtimeAnimatorController == Load<RuntimeAnimatorController>(ControllerPath) && !animator.applyRootMotion, "Ruby root Animator/controller is missing or root motion is enabled.");
                Require(root.GetComponent<RubyAnimationDriver>(), "RubyAnimationDriver must be on the SpriteRenderer/Animator root.");
                Require(!root.GetComponent<Rigidbody2D>(), "Generated visual prefab must not force a Rigidbody2D.");
            });
            Check(report, "Ring prefab", () => ValidateRenderer(Load<GameObject>(PrefabPath("Ring")), "RF_Projectiles", manifest.ringSprite));
            Check(report, "Backyard prefab", () => ValidateBackyard(manifest.environment));
            Check(report, "Sprite material", () =>
            {
                var material = Load<Material>(RubyVisualBuilder.MaterialPath);
                Require(material.shader && (material.shader.name == "Sprites/Default" || material.shader.name == "Unlit/Transparent"), "Missing or unexpected sprite shader.");
                Require(material.shader.isSupported, "Selected sprite shader is unsupported on this graphics device.");
            });
            Check(report, "Audio", () => ValidateAudio(manifest));
            Check(report, "Preview scene", () => Load<SceneAsset>(ScenePath));
        }

        static void ValidateTexture(TextureDef def)
        {
            string path = PathFor(def.path);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            Require(importer != null, "TextureImporter missing.");
            var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings);
            Require(importer.textureType == TextureImporterType.Sprite && importer.spriteImportMode == SpriteImportMode.Single, "Expected Single sprite import.");
            Require(settings.spriteMeshType == SpriteMeshType.FullRect && settings.spriteAlignment == (int)SpriteAlignment.Custom, "Expected FullRect mesh with custom pivot.");
            Require(Near(settings.spritePivot.x, def.pivotX) && Near(settings.spritePivot.y, def.pivotY) && Near(settings.spritePixelsPerUnit, def.pixelsPerUnit), "Pivot or PPU differs from manifest.");
            Require(settings.spriteBorder == new Vector4(def.borderLeft, def.borderBottom, def.borderRight, def.borderTop), "Nine-slice border differs from manifest.");
            Require(importer.alphaIsTransparency && !importer.mipmapEnabled && importer.textureCompression == TextureImporterCompression.Uncompressed && !importer.crunchedCompression, "Expected alpha transparency, no mipmaps, uncompressed texture.");
            Require(importer.wrapMode == (def.wrapRepeat ? TextureWrapMode.Repeat : TextureWrapMode.Clamp) && importer.maxTextureSize == def.maxSize, "Texture wrapping or maxSize differs from manifest.");
            var sprite = Sprite(def.path);
            Require(Near(sprite.pixelsPerUnit, def.pixelsPerUnit), "Imported sprite PPU differs from manifest.");
            Require(Near(sprite.pivot.x / sprite.rect.width, def.pivotX) && Near(sprite.pivot.y / sprite.rect.height, def.pivotY), "Imported sprite pivot differs from manifest.");
            Require(sprite.border == new Vector4(def.borderLeft, def.borderBottom, def.borderRight, def.borderTop), "Imported sprite border differs from manifest.");
        }

        static void ValidateClip(ClipDef def)
        {
            var clip = Load<AnimationClip>(ClipPath(def.name));
            var bindings = AnimationUtility.GetObjectReferenceCurveBindings(clip);
            Require(bindings.Length == 1 && bindings[0].path == "" && bindings[0].type == typeof(SpriteRenderer) && bindings[0].propertyName == "m_Sprite", "Clip must have one root SpriteRenderer.m_Sprite object reference curve.");
            Require(AnimationUtility.GetCurveBindings(clip).Length == 0, "Unexpected numeric/root-motion curves on sprite clip.");
            var keys = AnimationUtility.GetObjectReferenceCurve(clip, bindings[0]);
            bool hasEndpoint = keys.Length == def.keys.Length + 1;
            Require(keys.Length == def.keys.Length || hasEndpoint, "Frame count differs from manifest.");
            for (int i = 0; i < def.keys.Length; ++i)
                Require(Near(keys[i].time, def.keys[i].time) && keys[i].value == Sprite(def.keys[i].sprite), "Frame timestamp or sprite mismatch at index " + i);
            if (hasEndpoint)
            {
                var end = keys[keys.Length - 1];
                Require(Near(end.time, def.length) && end.value == Sprite(def.loop ? def.keys[0].sprite : def.keys[def.keys.Length - 1].sprite), "Missing or incorrect endpoint key.");
            }
            float frame = 1f / def.fps;
            Require(Near(clip.frameRate, def.fps), "Clip frameRate differs from manifest.");
            // Unity 6000.0's clip.length is one sample past an explicit endpoint key.
            Require(Near(clip.length, def.length) || Near(clip.length, def.length + frame), "Clip length differs from manifest.");
            Require(AnimationUtility.GetAnimationClipSettings(clip).loopTime == def.loop, "Clip loop setting differs from manifest.");
            var events = AnimationUtility.GetAnimationEvents(clip);
            var expected = (def.events ?? Array.Empty<ClipEventDef>()).OrderBy(e => e.time).ToArray();
            Require(events.Length == expected.Length, "Animation cue event count differs from manifest.");
            for (int i = 0; i < events.Length; ++i)
                Require(events[i].functionName == "OnAnimationCue" && Near(events[i].time, expected[i].time) && events[i].stringParameter == expected[i].cue, "Animation cue event mismatch at index " + i);
        }

        static void ValidateController(ControllerDef def)
        {
            var controller = Load<AnimatorController>(ControllerPath);
            Require(controller.layers.Length == 1 && controller.layers[0].name == "Base Layer", "Expected exactly one Base Layer.");
            Require(controller.parameters.Length == def.parameters.Length, "Animator parameter count differs from manifest.");
            foreach (var expected in def.parameters)
            {
                var actual = controller.parameters.SingleOrDefault(p => p.name == expected.name);
                Require(actual != null && actual.type.ToString() == expected.type && actual.defaultBool == expected.defaultBool && actual.defaultInt == expected.defaultInt && Near(actual.defaultFloat, expected.defaultFloat), "Parameter/default mismatch: " + expected.name);
            }
            var sm = controller.layers[0].stateMachine;
            Require(sm.states.Length == def.states.Length && sm.stateMachines.Length == 0 && sm.entryTransitions.Length == 0, "Unexpected states/submachines/entry transitions.");
            var states = sm.states.ToDictionary(s => s.state.name, s => s.state);
            foreach (var state in def.states)
                Require(states.TryGetValue(state.name, out var actual) && actual.motion == Load<AnimationClip>(ClipPath(state.clip)) && Near(actual.speed, state.speed) && !actual.writeDefaultValues, "State mismatch: " + state.name);
            Require(sm.defaultState == states[def.states.Single(s => s.isDefault).name], "Default state differs from manifest.");
            foreach (var source in def.states.Select(s => s.name).Concat(new[] { "Any" }))
            {
                var expected = def.transitions.Where(t => t.from == source).ToArray();
                var actual = source == "Any" ? sm.anyStateTransitions : states[source].transitions;
                Require(actual.Length == expected.Length, "Transition count mismatch from " + source);
                for (int i = 0; i < actual.Length; ++i)
                {
                    var a = actual[i]; var e = expected[i];
                    Require(a.destinationState == states[e.to] && a.hasExitTime == e.hasExitTime && Near(a.exitTime, e.exitTime) && a.hasFixedDuration && Near(a.duration, 0) && !a.canTransitionToSelf && Near(a.offset, 0), "Transition mismatch: " + source + " -> " + e.to);
                    Require(a.conditions.Length == e.conditions.Length, "Condition count mismatch: " + source + " -> " + e.to);
                    for (int k = 0; k < e.conditions.Length; ++k)
                        Require(a.conditions[k].parameter == e.conditions[k].parameter && a.conditions[k].mode.ToString() == e.conditions[k].mode && Near(a.conditions[k].threshold, e.conditions[k].threshold), "Condition mismatch: " + source + " -> " + e.to);
                }
            }
            foreach (string trigger in new[] { "GameOver", "Recover" })
            {
                var candidates = sm.anyStateTransitions.Where(t => t.conditions.Any(c => c.parameter == trigger && c.mode == AnimatorConditionMode.If)).ToArray();
                Require(candidates.Length > 0, "Any State must use trigger " + trigger + ".");
                var expectedGate = trigger == "GameOver" ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot;
                Require(candidates.All(t => t.conditions.Any(c => c.parameter == "IsGameOver" && c.mode == expectedGate)), "Any State " + trigger + " must be gated by IsGameOver=" + (trigger == "GameOver") + ".");
            }
        }

        static SpriteRenderer ValidateRenderer(GameObject root, string layer, string spritePath)
        {
            var renderer = root.GetComponent<SpriteRenderer>();
            Require(renderer && renderer.sprite == Sprite(spritePath), "Root SpriteRenderer or initial sprite mismatch on " + root.name);
            Require(renderer.sortingLayerName == layer, "Wrong sorting layer on " + root.name + "; expected " + layer);
            Require(renderer.sharedMaterial == Load<Material>(RubyVisualBuilder.MaterialPath), "Wrong material on " + root.name);
            return renderer;
        }
        static void ValidateEffect(EffectDef def, RubyAssetManifest manifest)
        {
            var controller = Load<AnimatorController>(EffectControllerPath(def.name));
            Require(controller.layers.Length == 1 && controller.parameters.Length == 0, "FX controller must have one layer and no parameters.");
            var sm = controller.layers[0].stateMachine;
            Require(sm.states.Length == 1 && sm.defaultState == sm.states[0].state && sm.defaultState.name == "Play" && sm.defaultState.motion == Load<AnimationClip>(ClipPath(def.clip)), "FX controller must have one default Play state with the assigned clip.");
            Require(sm.anyStateTransitions.Length == 0 && sm.defaultState.transitions.Length == 0, "Single-state FX should not contain transitions.");
            var root = Load<GameObject>(EffectPrefabPath(def.name));
            ValidateRenderer(root, "RF_Effects", manifest.clips.Single(c => c.name == def.clip).keys[0].sprite);
            var animator = root.GetComponent<Animator>();
            var helper = root.GetComponent<RubyOneShotEffect>();
            Require(animator && animator.runtimeAnimatorController == controller && !animator.applyRootMotion, "FX root Animator mismatch.");
            Require(helper && helper.autoDisable == !def.loop && Near(helper.duration, def.duration), "FX one-shot helper duration/loop mismatch.");
        }

        static void ValidateBackyard(EnvironmentDef env)
        {
            var root = Load<GameObject>(PrefabPath("Backyard"));
            Action<string, string, RectDef, string> rectCheck = (name, path, rect, layer) =>
            {
                var child = root.transform.Find(name);
                Require(child, "Missing backdrop child " + name);
                var renderer = ValidateRenderer(child.gameObject, layer, path);
                var sprite = renderer.sprite;
                var position = new Vector3((rect.x + rect.width * sprite.pivot.x / sprite.rect.width - env.canvasWidth * 0.5f) / env.pixelsPerUnit,
                    (env.baselineY - rect.y - rect.height * (1f - sprite.pivot.y / sprite.rect.height)) / env.pixelsPerUnit, 0f);
                Require(Vector3.Distance(child.localPosition, position) < 0.001f &&
                    Near(sprite.bounds.size.x * child.localScale.x, rect.width / env.pixelsPerUnit) && Near(sprite.bounds.size.y * child.localScale.y, rect.height / env.pixelsPerUnit), "Viewport-pixel placement/scale mismatch: " + name);
            };
            rectCheck("Sky", env.skyPath, env.skyRect, "RF_Background");
            rectCheck("Midground", env.midgroundPath, env.midgroundRect, "RF_Midground");
            var ground = root.transform.Find("Ground"); Require(ground, "Missing Ground child.");
            var groundRenderer = ValidateRenderer(ground.gameObject, "RF_Ground", env.groundPath);
            float width = env.canvasWidth / env.pixelsPerUnit;
            float height = groundRenderer.sprite.rect.height / groundRenderer.sprite.pixelsPerUnit;
            Require(ground.localPosition == Vector3.zero && ground.localScale == Vector3.one && groundRenderer.drawMode == SpriteDrawMode.Tiled &&
                groundRenderer.tileMode == SpriteTileMode.Continuous && Near(groundRenderer.size.x, width) && Near(groundRenderer.size.y, height), "Ground must repeat over canvas width with natural height and baseline-zero pivot.");
            var collider = ground.GetComponent<BoxCollider2D>();
            Require(collider && !collider.isTrigger && Near(collider.size.x, width) && Near(collider.size.y, height * 0.75f) && Near(collider.offset.y + collider.size.y * 0.5f, 0), "Ground collider must have its top at baseline y=0.");
            foreach (var prop in env.props ?? Array.Empty<PropPlacementDef>())
            {
                var child = root.transform.Find(prop.name); Require(child, "Missing optional prop " + prop.name);
                ValidateRenderer(child.gameObject, "RF_Props", prop.path);
                Require(Near(child.localPosition.x, prop.x) && Near(child.localPosition.y, prop.y) && child.localScale == Vector3.one, "Prop x/y are WORLD units and must not be divided by PPU: " + prop.name);
            }
        }

        internal static void ValidateAudio(RubyAssetManifest manifest)
        {
            var mixer = Load<AudioMixer>(Root + "/Audio/RubyAudio.mixer");
            foreach (string name in new[] { "Master", "Music", "SFX", "UI", "Ambience", "Music Mix", "SFX Mix", "UI Mix", "Ambience Mix" })
                Require(mixer.FindMatchingGroups(name).Any(g => g && g.name == name), "Native mixer did not import group '" + name + "'. Reimport Audio/RubyAudio.mixer; inspect mixer import errors before retrying.");
            foreach (string name in new[] { "Gameplay", "Menu", "Paused" })
                Require(mixer.FindSnapshot(name), "Native mixer is missing snapshot '" + name + "'. Setup cannot be marked successful.");
            foreach (string name in new[] { "MasterVolumeDb", "MusicVolumeDb", "SfxVolumeDb", "UiVolumeDb", "AmbienceVolumeDb" })
                Require(mixer.GetFloat(name, out _), "Native mixer is missing exposed volume parameter '" + name + "'.");
            var bank = Load<RubyAudioBank>(Root + "/Generated/Audio/RubyAudioBank.asset");
            Require(bank.mixer == mixer && bank.gameplay == mixer.FindSnapshot("Gameplay") && bank.menu == mixer.FindSnapshot("Menu") && bank.paused == mixer.FindSnapshot("Paused"), "Audio bank mixer/snapshot references are incomplete.");
            // Mirror the audio builder's sole optional-file exception; every other declared cue is required.
            var expectedAudio = manifest.audio.Where(d => d.key != "ambience_stream_loop" || System.IO.File.Exists(PathFor(d.path))).ToArray();
            Require(bank.entries != null && bank.entries.Length == expectedAudio.Length, "Audio bank entry count differs from manifest (excluding an absent optional ambience_stream_loop).");
            foreach (var def in expectedAudio)
            {
                var entry = bank.entries.FirstOrDefault(e => e != null && e.key == def.key);
                string group = (def.group ?? "").Trim();
                if (!group.EndsWith(" Mix", StringComparison.OrdinalIgnoreCase)) group += " Mix";
                Require(entry != null && entry.clip == Load<AudioClip>(PathFor(def.path)) && entry.group && entry.group.audioMixer == mixer &&
                    string.Equals(entry.group.name, group, StringComparison.OrdinalIgnoreCase) && entry.loop == def.loop && Near(entry.volume, def.volume), "Audio bank entry is invalid: " + def.key);
            }
            var rig = Load<GameObject>(PrefabPath("RubyAudioRig"));
            var player = rig.GetComponent<RubyAudioPlayer>();
            Require(player && player.bank == bank, "Audio rig/player does not reference the generated bank.");
            var sources = new[] { player.musicSource, player.ambienceSource, player.sfxSource, player.uiSource };
            var groups = new[] { "Music Mix", "Ambience Mix", "SFX Mix", "UI Mix" };
            for (int i = 0; i < sources.Length; ++i)
                Require(sources[i] && sources[i].outputAudioMixerGroup && sources[i].outputAudioMixerGroup.audioMixer == mixer && sources[i].outputAudioMixerGroup.name == groups[i] && !sources[i].playOnAwake,
                    "Audio rig source must route to " + groups[i] + " with playOnAwake disabled.");
        }
    }
}
