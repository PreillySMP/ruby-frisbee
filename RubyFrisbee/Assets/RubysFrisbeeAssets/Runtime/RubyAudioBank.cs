using System;
using UnityEngine;
using UnityEngine.Audio;

namespace RubysFrisbee.Assets
{
    /// <summary>Generated cue references. Customize a copy outside Generated.</summary>
    [CreateAssetMenu(fileName = "RubyAudioBank", menuName = "Ruby's Frisbee/Audio Bank")]
    public sealed class RubyAudioBank : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            public string key;
            public AudioClip clip;
            public AudioMixerGroup group;
            [Range(0f, 1f)] public float volume = 1f;
            public bool loop;
        }

        public AudioMixer mixer;
        public AudioMixerSnapshot gameplay;
        public AudioMixerSnapshot menu;
        public AudioMixerSnapshot paused;
        public Entry[] entries = Array.Empty<Entry>();

        /// <summary>Small bank: allocation-free lookup without a stale runtime cache.</summary>
        public bool TryGetEntry(string key, out Entry entry)
        {
            entry = null;
            if (string.IsNullOrWhiteSpace(key) || entries == null) return false;
            foreach (Entry candidate in entries)
            {
                if (candidate == null || !string.Equals(candidate.key, key, StringComparison.Ordinal)) continue;
                entry = candidate;
                return true;
            }
            return false;
        }

        public Entry FindFirstInGroup(string groupName, bool requireLoop = false)
        {
            if (entries == null) return null;
            foreach (Entry entry in entries)
            {
                if (entry != null && entry.clip != null && entry.group != null &&
                    string.Equals(entry.group.name, groupName, StringComparison.Ordinal) &&
                    (!requireLoop || entry.loop)) return entry;
            }
            return null;
        }

        public AudioMixerSnapshot FindSnapshot(string name)
        {
            if (string.Equals(name, "Gameplay", StringComparison.OrdinalIgnoreCase)) return gameplay;
            if (string.Equals(name, "Menu", StringComparison.OrdinalIgnoreCase)) return menu;
            if (string.Equals(name, "Paused", StringComparison.OrdinalIgnoreCase)) return paused;
            return null;
        }
    }
}
