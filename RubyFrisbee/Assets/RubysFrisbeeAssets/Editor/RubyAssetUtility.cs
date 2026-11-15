using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RubysFrisbee.Assets.Editor
{
    [Serializable] internal sealed class RubySetupReport
    {
        public string packageVersion, utc, unityVersion;
        public bool success;
        public int textureCount, clipCount, controllerCount, atlasCount, prefabCount, generatedAssetCount;
        public List<string> completedStages = new List<string>();
        public List<string> generatedPaths = new List<string>();
        public List<string> importedTexturePaths = new List<string>();
        public List<string> errors = new List<string>();
        public List<string> warnings = new List<string>();
        public void Generated(string path)
        {
            if (!generatedPaths.Contains(path)) generatedPaths.Add(path);
            generatedAssetCount = generatedPaths.Count;
        }
    }

    internal static class RubyAssetUtility
    {
        internal const string Root = "Assets/RubysFrisbeeAssets";
        internal const string ManifestPath = Root + "/Config/asset-manifest.json";
        internal const string ReportPath = Root + "/Generated/SetupReport.json";
        internal static readonly string[] RequiredLayers =
        {
            "RF_Background", "RF_Midground", "RF_Ground", "RF_Props",
            "RF_Character", "RF_Projectiles", "RF_Effects", "RF_UI"
        };

        internal static void Require(bool ok, string message)
        {
            if (!ok) throw new InvalidOperationException(message);
        }
        internal static bool Finite(float x) => !float.IsNaN(x) && !float.IsInfinity(x);
        internal static bool Near(float a, float b) => Mathf.Abs(a - b) < 0.0001f;
        internal static string PathFor(string relative)
        {
            Require(!string.IsNullOrWhiteSpace(relative), "Manifest contains an empty asset path.");
            string p = relative.Replace('\\', '/');
            Require(!Path.IsPathRooted(p) && !p.Contains(':') && !p.Split('/').Any(s => s == ".." || s == "." || s.Length == 0),
                "Asset path must be package-relative without traversal: " + relative);
            return Root + "/" + p;
        }
        internal static string NameFor(string name)
        {
            Require(!string.IsNullOrWhiteSpace(name) && name != "." && name != ".." &&
                name.IndexOfAny(new[] { '/', '\\', ':', '*', '?', '"', '<', '>', '|' }) < 0 &&
                !name.Any(char.IsControl) && name.Trim() == name,
                "Invalid generated asset name: '" + name + "'.");
            return name;
        }
        internal static string ClipPath(string name) => Root + "/Generated/Animation/Clips/" + NameFor(name) + ".anim";
        internal static string EffectControllerPath(string name) => Root + "/Generated/Animation/FX/" + NameFor(name) + ".controller";
        internal static string EffectPrefabPath(string name) => Root + "/Generated/Prefabs/FX/" + NameFor(name) + ".prefab";
        internal static string AtlasPath(string name) => Root + "/Generated/Atlases/" + NameFor(name) + ".spriteatlas";
        internal static string PrefabPath(string name) => Root + "/Generated/Prefabs/" + NameFor(name) + ".prefab";
        internal const string ControllerPath = Root + "/Generated/Animation/Ruby.controller";
        internal const string ScenePath = Root + "/Generated/Scenes/RubyAssetPreview.unity";

        internal static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            Require(!string.IsNullOrEmpty(parent), "Cannot create asset folder: " + path);
            EnsureFolder(parent);
            string guid = AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
            Require(!string.IsNullOrEmpty(guid), "Unity could not create folder " + path + ". Check project write permissions.");
        }
        internal static void EnsureParent(string path) => EnsureFolder(Path.GetDirectoryName(path).Replace('\\', '/'));
        internal static T Load<T>(string path) where T : Object
        {
            T value = AssetDatabase.LoadAssetAtPath<T>(path);
            Require(value, "Missing or not imported as " + typeof(T).Name + ": " + path + ". Check Console importer errors, then run Setup or Repair.");
            return value;
        }
        internal static Sprite Sprite(string relative) => Load<Sprite>(PathFor(relative));

        internal static RubyAssetManifest ReadManifest()
        {
            Require(File.Exists(ManifestPath), "Ruby manifest is missing: " + ManifestPath + ". Finish importing the complete package before setup.");
            var m = JsonUtility.FromJson<RubyAssetManifest>(File.ReadAllText(ManifestPath));
            Require(m != null && m.schemaVersion == 1, "Ruby manifest must use schemaVersion 1.");
            Require(m.root == Root, "Manifest root must be exactly " + Root + "; this installer will not modify another asset tree.");
            Require(!string.IsNullOrWhiteSpace(m.packageVersion), "Manifest packageVersion is required.");
            ValidateManifest(m);
            return m;
        }

        internal static void Unique(IEnumerable<string> names, string kind)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string name in names) Require(seen.Add(NameFor(name)), "Duplicate " + kind + " name: " + name);
        }
        internal static void ValidateManifest(RubyAssetManifest m)
        {
            Require(m.textures != null && m.textures.Length > 0 && m.textures.All(t => t != null), "Manifest needs texture definitions.");
            var textures = new HashSet<string>(StringComparer.Ordinal);
            foreach (var t in m.textures)
            {
                string path = PathFor(t.path);
                Require(textures.Add(t.path), "Duplicate texture path: " + t.path);
                Require(File.Exists(path), "Texture file not found: " + path);
                Require(Finite(t.pixelsPerUnit) && t.pixelsPerUnit > 0 && Finite(t.pivotX) && Finite(t.pivotY) &&
                    t.pivotX >= 0 && t.pivotX <= 1 && t.pivotY >= 0 && t.pivotY <= 1, "Invalid PPU or pivot: " + path);
                Require(new[] { t.borderLeft, t.borderBottom, t.borderRight, t.borderTop }.All(v => Finite(v) && v >= 0), "Invalid nine-slice border: " + path);
                Require(ValidSize(t.maxSize), "maxSize must be a power of two from 32 to 16384: " + path);
            }
            Action<string> knownSprite = p => Require(p != null && textures.Contains(p), "Sprite is not declared in textures: " + p);
            knownSprite(m.defaultSprite); knownSprite(m.ringSprite);
            Require(m.clips != null && m.clips.Length > 0 && m.clips.All(c => c != null), "Manifest needs animation clips.");
            Unique(m.clips.Select(c => c.name), "clip");
            foreach (var c in m.clips)
            {
                Require(Finite(c.length) && c.length > 0 && Finite(c.fps) && c.fps > 0, "Clip length/fps must be positive: " + c.name);
                Require(c.keys != null && c.keys.Length > 0 && c.keys.All(k => k != null), "Clip has no frame keys: " + c.name);
                float previous = -1f;
                foreach (var k in c.keys)
                {
                    knownSprite(k.sprite);
                    Require(Finite(k.time) && k.time >= 0 && k.time < c.length && k.time > previous,
                        "Clip keys must be strictly increasing, nonnegative and before length (endpoint is added automatically): " + c.name);
                    previous = k.time;
                }
                Require(Near(c.keys[0].time, 0f), "First frame must start at time zero: " + c.name);
                foreach (var e in c.events ?? Array.Empty<ClipEventDef>())
                    Require(e != null && Finite(e.time) && e.time >= 0 && e.time <= c.length && !string.IsNullOrWhiteSpace(e.cue), "Invalid cue event: " + c.name);
            }
            var clips = m.clips.ToDictionary(c => c.name, StringComparer.Ordinal);
            Require(m.controller != null && m.controller.parameters != null && m.controller.states != null && m.controller.states.Length > 0 && m.controller.transitions != null,
                "Controller requires parameters, states and transitions arrays.");
            Require(m.controller.parameters.All(p => p != null) && m.controller.states.All(s => s != null) && m.controller.transitions.All(t => t != null), "Controller arrays contain null entries.");
            Unique(m.controller.parameters.Select(p => p.name), "parameter");
            Unique(m.controller.states.Select(s => s.name), "state");
            Require(m.controller.states.Count(s => s.isDefault) == 1, "Controller must have exactly one default state.");
            var parameters = m.controller.parameters.ToDictionary(p => p.name, StringComparer.Ordinal);
            var requiredParameters = new Dictionary<string, string>
            {
                {"MoveSpeed","Float"}, {"VerticalSpeed","Float"}, {"IsGrounded","Bool"}, {"IsSitting","Bool"}, {"IsGameOver","Bool"},
                {"Catch","Trigger"}, {"Celebrate","Trigger"}, {"GameOver","Trigger"}, {"Recover","Trigger"}
            };
            foreach (var pair in requiredParameters)
                Require(parameters.TryGetValue(pair.Key, out var def) && def.type == pair.Value, "Controller requires parameter " + pair.Key + " of type " + pair.Value + ".");
            Require(parameters["IsGrounded"].defaultBool && !parameters["IsSitting"].defaultBool && !parameters["IsGameOver"].defaultBool, "Default bools must be IsGrounded=true, IsSitting=false, IsGameOver=false.");
            foreach (var p in m.controller.parameters)
                Require(Enum.TryParse<AnimatorControllerParameterType>(p.type, out var type) && Enum.IsDefined(typeof(AnimatorControllerParameterType), type), "Unknown Animator parameter type: " + p.type);
            var states = new HashSet<string>(m.controller.states.Select(s => s.name), StringComparer.Ordinal);
            Require(!states.Contains("Any"), "The state name Any is reserved for Any State transitions.");
            foreach (var s in m.controller.states)
                Require(clips.ContainsKey(s.clip ?? "") && Finite(s.speed) && s.speed > 0 && Finite(s.x) && Finite(s.y), "State has invalid clip/speed/position: " + s.name);
            foreach (var t in m.controller.transitions)
            {
                Require((t.from == "Any" || states.Contains(t.from ?? "")) && states.Contains(t.to ?? ""), "Transition refers to unknown states: " + t.from + " -> " + t.to);
                Require(Finite(t.exitTime) && t.exitTime >= 0, "Transition has invalid exitTime: " + t.from + " -> " + t.to);
                Require(t.conditions != null && (t.hasExitTime || t.conditions.Length > 0), "Transition needs conditions or exit time: " + t.from + " -> " + t.to);
                foreach (var condition in t.conditions)
                {
                    Require(condition != null && condition.parameter != null && parameters.ContainsKey(condition.parameter), "Transition has an unknown parameter: " + t.from + " -> " + t.to);
                    Require(Enum.TryParse<UnityEditor.Animations.AnimatorConditionMode>(condition.mode, out var mode) && Enum.IsDefined(typeof(UnityEditor.Animations.AnimatorConditionMode), mode), "Unknown condition mode: " + condition.mode);
                    string type = parameters[condition.parameter].type;
                    bool boolMode = condition.mode == "If" || condition.mode == "IfNot";
                    Require(Finite(condition.threshold) &&
                        ((type == "Bool" && boolMode) || (type == "Trigger" && condition.mode == "If") ||
                         (type == "Float" && (condition.mode == "Greater" || condition.mode == "Less")) ||
                         (type == "Int" && !boolMode)), "Condition mode/type mismatch for " + condition.parameter);
                }
            }
            foreach (string trigger in new[] { "GameOver", "Recover" })
            {
                var candidates = m.controller.transitions.Where(t => t.from == "Any" && t.conditions.Any(c => c.parameter == trigger && c.mode == "If")).ToArray();
                string gate = trigger == "GameOver" ? "If" : "IfNot";
                Require(candidates.Length > 0 && candidates.All(t => t.conditions.Any(c => c.parameter == "IsGameOver" && c.mode == gate)),
                    "Any State " + trigger + " requires its Trigger condition and IsGameOver " + gate + " gate.");
            }
            var defaultState = m.controller.states.Single(s => s.isDefault);
            Require(defaultState.name == "Idle" && m.defaultSprite == clips[defaultState.clip].keys[0].sprite,
                "The default controller state must be Idle, and defaultSprite must match its first frame.");
            Require(m.effects != null && m.effects.Length == 7 && m.effects.All(e => e != null), "Manifest must supply exactly seven FX definitions.");
            Unique(m.effects.Select(e => e.name), "effect");
            foreach (var e in m.effects)
                Require(clips.TryGetValue(e.clip ?? "", out var c) && c.loop == e.loop && Finite(e.duration) && Near(e.duration, c.length), "FX loop/duration must match its clip: " + e.name);
            Require(m.atlases != null && m.atlases.Length > 0 && m.atlases.All(a => a != null), "Manifest needs atlas definitions.");
            Unique(m.atlases.Select(a => a.name), "atlas");
            foreach (var a in m.atlases)
            {
                Require(ValidSize(a.maxSize) && a.folders != null && a.folders.Length > 0, "Atlas needs valid maxSize and folders: " + a.name);
                foreach (var folder in a.folders) Require(AssetDatabase.IsValidFolder(PathFor(folder)), "Atlas source folder not imported: " + PathFor(folder));
            }
            Require(m.sortingLayers != null && RequiredLayers.All(l => m.sortingLayers.Contains(l)), "Manifest must declare all eight RF sorting layer names.");
            Unique(m.sortingLayers, "sorting layer");
            Require(m.audio != null && m.audio.Length > 0 && m.audio.All(a => a != null && !string.IsNullOrWhiteSpace(a.key)), "Manifest audio definitions/keys are missing.");
            foreach (var a in m.audio) PathFor(a.path);
            var env = m.environment;
            Require(env != null && Finite(env.pixelsPerUnit) && env.pixelsPerUnit > 0 && Finite(env.canvasWidth) && env.canvasWidth > 0 &&
                Finite(env.canvasHeight) && env.canvasHeight > 0 && Finite(env.baselineY), "Environment viewport dimensions/PPU/baseline are invalid.");
            knownSprite(env.skyPath); knownSprite(env.midgroundPath); knownSprite(env.groundPath);
            foreach (var rect in new[] { env.skyRect, env.midgroundRect })
                Require(rect != null && Finite(rect.x) && Finite(rect.y) && Finite(rect.width) && rect.width > 0 && Finite(rect.height) && rect.height > 0, "Environment sky/midground rendered rectangles are required.");
            var ground = m.textures.Single(t => t.path == env.groundPath);
            Require(ground.wrapRepeat && Near(ground.pivotX, 0.5f) && Near(ground.pivotY, 0.75f), "Ground texture must repeat and use pivot (0.5,0.75).");
            foreach (var p in env.props ?? Array.Empty<PropPlacementDef>())
            {
                Require(p != null && Finite(p.x) && Finite(p.y), "Prop positions must be finite world-space coordinates.");
                NameFor(p.name); knownSprite(p.path);
            }
            Unique((env.props ?? Array.Empty<PropPlacementDef>()).Select(p => p.name).Concat(new[] { "Sky", "Midground", "Ground" }), "environment child");
        }
        static bool ValidSize(int n) => n >= 32 && n <= 16384 && (n & (n - 1)) == 0;
    }
}
