using System;

namespace RubysFrisbee.Assets.Editor
{
    [Serializable] internal sealed class RubyAssetManifest
    {
        public int schemaVersion = 1;
        public string packageVersion = "1.0.0";
        public string root = "Assets/RubysFrisbeeAssets";
        public TextureDef[] textures;
        public ClipDef[] clips;
        public ControllerDef controller;
        public string[] sortingLayers;
        public AtlasDef[] atlases;
        public AudioDef[] audio;
        public EffectDef[] effects;
        public EnvironmentDef environment;
        public string defaultSprite;
        public string ringSprite;
    }
    [Serializable] internal sealed class TextureDef
    {
        public string path;
        public float pixelsPerUnit = 128;
        public float pivotX = 0.5f, pivotY = 0.5f;
        public float borderLeft, borderBottom, borderRight, borderTop;
        public bool wrapRepeat;
        public int maxSize = 2048;
    }
    [Serializable] internal sealed class FrameKey { public float time; public string sprite; }
    [Serializable] internal sealed class ClipEventDef { public float time; public string cue; }
    [Serializable] internal sealed class ClipDef
    {
        public string name;
        public float fps = 24;
        public float length;
        public bool loop;
        public FrameKey[] keys;
        public ClipEventDef[] events;
    }
    [Serializable] internal sealed class ParameterDef
    {
        public string name, type;
        public float defaultFloat;
        public bool defaultBool;
        public int defaultInt;
    }
    [Serializable] internal sealed class StateDef
    {
        public string name, clip;
        public float speed = 1;
        public float x, y;
        public bool isDefault;
    }
    [Serializable] internal sealed class ConditionDef { public string parameter, mode; public float threshold; }
    [Serializable] internal sealed class TransitionDef
    {
        public string from, to;
        public bool hasExitTime;
        public float exitTime = 1;
        public float duration;
        public ConditionDef[] conditions;
    }
    [Serializable] internal sealed class ControllerDef
    {
        public ParameterDef[] parameters;
        public StateDef[] states;
        public TransitionDef[] transitions;
    }
    [Serializable] internal sealed class AtlasDef { public string name; public string[] folders; public int maxSize = 2048; }
    [Serializable] internal sealed class AudioDef
    {
        public string key, path, group;
        public bool loop;
        public float volume = 1;
    }
    [Serializable] internal sealed class EffectDef { public string name, clip; public bool loop; public float duration; }
    [Serializable] internal sealed class RectDef { public float x, y, width, height; }
    [Serializable] internal sealed class PropPlacementDef { public string name, path; public float x, y; }
    [Serializable] internal sealed class EnvironmentDef
    {
        public string skyPath, midgroundPath, groundPath;
        public float canvasWidth = 1920, canvasHeight = 1080, baselineY = 900, pixelsPerUnit = 128;
        public RectDef skyRect, midgroundRect;
        public PropPlacementDef[] props;
    }
}
