using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;

namespace RubysFrisbee.Assets.Editor
{
    /// <summary>Called by package orchestration after scripts and assets have imported.</summary>
    internal static class RubyAudioSetup
    {
        private static readonly string[] BusNames = { "Music", "SFX", "UI", "Ambience" };
        private static readonly string[] ParameterNames =
        {
            "MasterVolumeDb", "MusicVolumeDb", "SfxVolumeDb", "UiVolumeDb", "AmbienceVolumeDb"
        };

        internal static void Build(string rootPath, AudioDef[] definitions)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play mode before repairing Ruby audio assets.");
            if (EditorApplication.isCompiling)
                throw new InvalidOperationException("Wait for script compilation before building Ruby audio.");
            rootPath = NormalizeRoot(rootPath);
            string mixerPath = rootPath + "/Audio/RubyAudio.mixer";
            AssetDatabase.ImportAsset(mixerPath, ImportAssetOptions.ForceSynchronousImport);
            AudioMixer mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(mixerPath);
            if (mixer == null)
                throw new InvalidOperationException("Ruby native mixer failed to import: " + mixerPath +
                    ". Restore the supplied RubyAudio.mixer and its .meta, then reimport. " +
                    "No mixer is synthesized with private Unity APIs.");

            Dictionary<string, AudioMixerGroup> leaves = ValidateMixer(mixer, mixerPath);
            AudioMixerSnapshot gameplay = RequireSnapshot(mixer, "Gameplay", mixerPath);
            AudioMixerSnapshot menu = RequireSnapshot(mixer, "Menu", mixerPath);
            AudioMixerSnapshot paused = RequireSnapshot(mixer, "Paused", mixerPath);
            List<AudioDef> valid = Preflight(rootPath, definitions, leaves);
            var entries = new List<RubyAudioBank.Entry>();
            foreach (AudioDef definition in valid)
            {
                string clipPath = AssetPath(rootPath, definition.path);
                string bus = BusName(definition.group);
                ConfigureImport(clipPath, bus);
                AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(clipPath);
                if (clip == null)
                    throw new InvalidOperationException("Ruby audio failed to load as AudioClip after import: " + clipPath);
                entries.Add(new RubyAudioBank.Entry
                {
                    key = definition.key,
                    clip = clip,
                    group = leaves[bus],
                    volume = definition.volume,
                    loop = definition.loop
                });
            }
            entries.Sort((a, b) => string.Compare(a.key, b.key, StringComparison.Ordinal));

            string bankPath = rootPath + "/Generated/Audio/RubyAudioBank.asset";
            string prefabPath = rootPath + "/Generated/Prefabs/RubyAudioRig.prefab";
            EnsureFolder(rootPath + "/Generated/Audio");
            EnsureFolder(rootPath + "/Generated/Prefabs");
            UnityEngine.Object existing = AssetDatabase.LoadMainAssetAtPath(bankPath);
            if (existing == null && File.Exists(bankPath))
                throw new InvalidOperationException("Existing Ruby audio bank could not be imported; refusing to replace it: " + bankPath);
            if (existing != null && !(existing is RubyAudioBank))
                throw new InvalidOperationException("Refusing to replace a non-audio-bank asset: " + bankPath);
            RubyAudioBank bank = existing as RubyAudioBank;
            if (bank == null)
            {
                bank = ScriptableObject.CreateInstance<RubyAudioBank>();
                AssetDatabase.CreateAsset(bank, bankPath);
            }
            // Repair updates the existing main asset in place: its GUID stays intact.
            bank.mixer = mixer;
            bank.gameplay = gameplay;
            bank.menu = menu;
            bank.paused = paused;
            bank.entries = entries.ToArray();
            EditorUtility.SetDirty(bank);
            BuildPrefab(prefabPath, bank, leaves);
            AssetDatabase.SaveAssets();
            Debug.Log("Ruby audio configured: " + entries.Count + " cues; Gameplay/Menu/Paused presets. " +
                "Music and ambience remain opt-in. " + prefabPath);
        }

        private static Dictionary<string, AudioMixerGroup> ValidateMixer(AudioMixer mixer, string path)
        {
            AudioMixerGroup[] all = mixer.FindMatchingGroups(string.Empty);
            RequireGroup(all, "Master", path);
            var leaves = new Dictionary<string, AudioMixerGroup>(StringComparer.Ordinal);
            foreach (string bus in BusNames)
            {
                RequireGroup(all, bus, path);
                AudioMixerGroup leaf = RequireGroup(all, bus + " Mix", path);
                AudioMixerGroup[] nested = mixer.FindMatchingGroups("Master/" + bus + "/" + bus + " Mix");
                if (Array.IndexOf(nested, leaf) < 0)
                    throw new InvalidOperationException("Ruby mixer group must be nested beneath Master/" +
                        bus + ": " + bus + " Mix. Restore " + path);
                leaves.Add(bus, leaf);
            }
            foreach (string parameter in ParameterNames)
            {
                if (!mixer.GetFloat(parameter, out float ignored))
                    throw new InvalidOperationException("Ruby mixer must expose " + parameter + ". Restore " + path);
            }
            return leaves;
        }

        private static AudioMixerGroup RequireGroup(AudioMixerGroup[] groups, string name, string path)
        {
            AudioMixerGroup found = null;
            foreach (AudioMixerGroup group in groups)
            {
                if (group == null || !string.Equals(group.name, name, StringComparison.Ordinal)) continue;
                if (found != null)
                    throw new InvalidOperationException("Ruby mixer has duplicate group '" + name + "': " + path);
                found = group;
            }
            if (found == null)
                throw new InvalidOperationException("Ruby mixer group '" + name + "' was not imported: " + path);
            return found;
        }

        private static AudioMixerSnapshot RequireSnapshot(AudioMixer mixer, string name, string path)
        {
            AudioMixerSnapshot snapshot = mixer.FindSnapshot(name);
            if (snapshot == null)
                throw new InvalidOperationException("Ruby mixer snapshot '" + name + "' was not imported: " + path);
            return snapshot;
        }

        private static List<AudioDef> Preflight(string root, AudioDef[] definitions,
            Dictionary<string, AudioMixerGroup> leaves)
        {
            var valid = new List<AudioDef>();
            var keys = new HashSet<string>(StringComparer.Ordinal);
            var pathBuses = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (AudioDef definition in definitions ?? Array.Empty<AudioDef>())
            {
                if (definition == null || string.IsNullOrWhiteSpace(definition.key))
                    throw new InvalidOperationException("Ruby audio manifest contains a null or empty cue key.");
                if (!keys.Add(definition.key))
                    throw new InvalidOperationException("Ruby audio cue key is duplicated: " + definition.key);
                string bus = BusName(definition.group);
                if (!leaves.ContainsKey(bus))
                    throw new InvalidOperationException("Unknown Ruby audio group for " + definition.key +
                        ": " + definition.group + ". Use Music, SFX, UI or Ambience (or their Mix leaf names).");
                if (float.IsNaN(definition.volume) || float.IsInfinity(definition.volume) ||
                    definition.volume < 0f || definition.volume > 1f)
                    throw new InvalidOperationException("Ruby cue volume must be finite in [0,1]: " + definition.key);
                string path = AssetPath(root, definition.path);
                if (!File.Exists(path))
                {
                    if (string.Equals(definition.key, "ambience_stream_loop", StringComparison.Ordinal))
                    {
                        Debug.LogWarning("Optional water-texture cue was not supplied; skipping ambience_stream_loop: " + path);
                        continue;
                    }
                    throw new FileNotFoundException("Required Ruby audio cue is missing: " + definition.key, path);
                }
                if (!(AssetImporter.GetAtPath(path) is AudioImporter))
                    throw new InvalidOperationException("Ruby cue is not imported as audio: " + path);
                bool streaming = bus == "Music" || bus == "Ambience";
                if (pathBuses.TryGetValue(path, out string previousBus) &&
                    streaming != (previousBus == "Music" || previousBus == "Ambience"))
                    throw new InvalidOperationException("One audio file cannot use both streaming and SFX import profiles: " + path);
                pathBuses[path] = bus;
                valid.Add(definition);
            }
            return valid;
        }

        private static void ConfigureImport(string path, string bus)
        {
            AudioImporter importer = AssetImporter.GetAtPath(path) as AudioImporter;
            if (importer == null)
                throw new InvalidOperationException("Missing AudioImporter: " + path);
            bool streaming = bus == "Music" || bus == "Ambience";
            AudioImporterSampleSettings settings = importer.defaultSampleSettings;
            settings.loadType = streaming ? AudioClipLoadType.Streaming : AudioClipLoadType.DecompressOnLoad;
            settings.compressionFormat = streaming ? AudioCompressionFormat.Vorbis : AudioCompressionFormat.PCM;
            settings.quality = streaming ? 0.7f : 1f;
            settings.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
            settings.preloadAudioData = !streaming;
            importer.defaultSampleSettings = settings;
            importer.loadInBackground = streaming;
            // These are 2D cues: retain authored mono/stereo rather than blindly downmixing.
            // A mono source remains mono. Review any spatialized copies separately.
            importer.forceToMono = false;
            importer.ambisonic = false;
            importer.SaveAndReimport();
            // Existing platform-specific overrides are deliberately not destroyed.
        }

        private static void BuildPrefab(string path, RubyAudioBank bank,
            Dictionary<string, AudioMixerGroup> leaves)
        {
            UnityEngine.Object existing = AssetDatabase.LoadMainAssetAtPath(path);
            if (existing == null && File.Exists(path))
                throw new InvalidOperationException("Existing Ruby audio prefab could not be imported; refusing to replace it: " + path);
            if (existing != null && !(existing is GameObject))
                throw new InvalidOperationException("Refusing to replace a non-prefab asset: " + path);
            bool loaded = existing != null;
            GameObject rig = loaded ? PrefabUtility.LoadPrefabContents(path) : new GameObject("RubyAudioRig");
            try
            {
                RubyAudioPlayer player = rig.GetComponent<RubyAudioPlayer>();
                if (player == null) player = rig.AddComponent<RubyAudioPlayer>();
                player.bank = bank;
                player.musicSource = EnsureSource(rig, "Music", leaves["Music"], 64, false);
                player.ambienceSource = EnsureSource(rig, "Ambience", leaves["Ambience"], 192, false);
                player.sfxSource = EnsureSource(rig, "SFX", leaves["SFX"], 128, false);
                player.uiSource = EnsureSource(rig, "UI", leaves["UI"], 32, true);
                RubyAudioBank.Entry music = bank.FindFirstInGroup("Music Mix", true) ?? bank.FindFirstInGroup("Music Mix");
                player.musicSource.clip = music == null ? null : music.clip;
                player.musicSource.loop = music != null && music.loop;
                player.musicSource.volume = music == null ? 1f : music.volume;
                // Optional stream water is NOT a default backyard wind bed.
                player.ambienceSource.clip = null;
                if (PrefabUtility.SaveAsPrefabAsset(rig, path) == null)
                    throw new InvalidOperationException("Could not save Ruby audio prefab: " + path +
                        ". Call audio setup outside AssetDatabase.StartAssetEditing.");
            }
            finally
            {
                if (loaded) PrefabUtility.UnloadPrefabContents(rig);
                else UnityEngine.Object.DestroyImmediate(rig);
            }
        }

        private static AudioSource EnsureSource(GameObject rig, string name, AudioMixerGroup group,
            int priority, bool ignoreListenerPause)
        {
            Transform child = rig.transform.Find(name);
            if (child == null)
            {
                var go = new GameObject(name);
                go.transform.SetParent(rig.transform, false);
                child = go.transform;
            }
            AudioSource source = child.GetComponent<AudioSource>();
            if (source == null) source = child.gameObject.AddComponent<AudioSource>();
            source.Stop();
            source.clip = null;
            source.outputAudioMixerGroup = group;
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.dopplerLevel = 0f;
            source.volume = 1f;
            source.pitch = 1f;
            source.loop = false;
            source.mute = false;
            source.priority = priority;
            source.ignoreListenerPause = ignoreListenerPause;
            source.bypassEffects = false;
            source.bypassListenerEffects = false;
            source.bypassReverbZones = true;
            return source;
        }

        private static string BusName(string group)
        {
            if (group == null) return string.Empty;
            string name = group.Trim();
            if (name.EndsWith(" Mix", StringComparison.OrdinalIgnoreCase))
                name = name.Substring(0, name.Length - 4);
            foreach (string bus in BusNames)
                if (string.Equals(name, bus, StringComparison.OrdinalIgnoreCase)) return bus;
            return name;
        }

        private static string NormalizeRoot(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Ruby package root is required.");
            path = path.Replace('\\', '/').TrimEnd('/');
            if (!path.StartsWith("Assets/", StringComparison.Ordinal) ||
                path.Contains("//") || Array.IndexOf(path.Split('/'), "..") >= 0 ||
                Array.IndexOf(path.Split('/'), ".") >= 0 || !AssetDatabase.IsValidFolder(path))
                throw new ArgumentException("Ruby package root must be an existing project Assets subfolder: " + path);
            return path;
        }

        private static string AssetPath(string root, string relative)
        {
            if (string.IsNullOrWhiteSpace(relative)) throw new ArgumentException("Ruby audio path is required.");
            relative = relative.Replace('\\', '/');
            string[] parts = relative.Split('/');
            if (Path.IsPathRooted(relative) || relative.Contains(":") ||
                Array.IndexOf(parts, "..") >= 0 || Array.IndexOf(parts, ".") >= 0 ||
                Array.IndexOf(parts, string.Empty) >= 0 || relative.StartsWith("Assets/", StringComparison.Ordinal))
                throw new ArgumentException("Ruby audio paths must be relative to the package root: " + relative);
            return root + "/" + relative;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int slash = path.LastIndexOf('/');
            string parent = path.Substring(0, slash);
            EnsureFolder(parent);
            if (string.IsNullOrEmpty(AssetDatabase.CreateFolder(parent, path.Substring(slash + 1))))
                throw new IOException("Could not create generated Ruby audio folder: " + path);
        }
    }
}
