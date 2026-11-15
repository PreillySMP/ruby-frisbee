using UnityEngine;
using UnityEngine.Audio;

namespace RubysFrisbee.Assets
{
    /// <summary>Explicit cue playback; never starts music or ambience automatically.</summary>
    [DisallowMultipleComponent]
    public sealed class RubyAudioPlayer : MonoBehaviour
    {
        public RubyAudioBank bank;
        public AudioSource musicSource;
        public AudioSource ambienceSource;
        public AudioSource sfxSource;
        public AudioSource uiSource;

        private static readonly string[] VolumeParameters =
        {
            "MasterVolumeDb", "MusicVolumeDb", "SfxVolumeDb", "UiVolumeDb", "AmbienceVolumeDb"
        };
        private readonly float[] pendingVolumes = new float[5];
        private readonly bool[] hasPendingVolume = new bool[5];
        private bool started;

        private void Awake()
        {
            ConfigureSource(musicSource, false);
            ConfigureSource(ambienceSource, false);
            ConfigureSource(sfxSource, false);
            ConfigureSource(uiSource, true);
        }

        private static void ConfigureSource(AudioSource source, bool isUi)
        {
            if (source == null) return;
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.dopplerLevel = 0f;
            source.ignoreListenerPause = isUi;
        }

        private void Start()
        {
            // Unity advises against AudioMixer.SetFloat from Awake/OnEnable.
            // Calls made before Start are retained, not lost or prematurely submitted.
            started = true;
            for (int i = 0; i < pendingVolumes.Length; ++i)
                if (hasPendingVolume[i]) ApplyUserVolume(i);
        }

        public void PlayCue(string key)
        {
            if (bank == null || !bank.TryGetEntry(key, out RubyAudioBank.Entry entry) ||
                entry.clip == null || entry.group == null)
            {
                Debug.LogWarning("Ruby audio cue is missing or unconfigured: " + key, this);
                return;
            }
            if (bank.mixer == null || entry.group.audioMixer != bank.mixer)
            {
                Debug.LogWarning("Ruby audio cue belongs to a different or missing mixer: " + key, this);
                return;
            }

            AudioSource source = SourceFor(entry.group);
            if (source == null)
            {
                Debug.LogWarning("Ruby audio cue has no matching configured source: " + key, this);
                return;
            }
            source.outputAudioMixerGroup = entry.group;
            float gain = SanitizeLinear(entry.volume);
            bool exclusive = source == musicSource || source == ambienceSource || entry.loop;
            if (exclusive)
            {
                source.Stop();
                source.clip = entry.clip;
                source.loop = entry.loop;
                source.volume = gain;
                source.Play();
            }
            else
            {
                // One-shots overlap on each bus. A new one-shot replaces a looping
                // SFX/UI cue, preventing its gain from scaling unrelated one-shots.
                if (source.loop) source.Stop();
                source.loop = false;
                source.clip = null;
                source.volume = 1f;
                source.PlayOneShot(entry.clip, gain);
            }
        }

        private AudioSource SourceFor(AudioMixerGroup group)
        {
            switch (group.name)
            {
                case "Music Mix": return musicSource;
                case "Ambience Mix": return ambienceSource;
                case "SFX Mix": return sfxSource;
                case "UI Mix": return uiSource;
                default: return null;
            }
        }

        public void PlayMusic()
        {
            RubyAudioBank.Entry entry = bank == null ? null :
                bank.FindFirstInGroup("Music Mix", true) ?? bank.FindFirstInGroup("Music Mix");
            if (entry == null)
            {
                Debug.LogWarning("Ruby audio bank contains no configured music cue.", this);
                return;
            }
            // Repeated requests do not restart the current song.
            if (musicSource != null && musicSource.isPlaying && musicSource.clip == entry.clip) return;
            PlayCue(entry.key);
        }

        public void StopMusic()
        {
            if (musicSource != null) musicSource.Stop();
        }

        public void ApplySnapshot(string name, float transitionSeconds)
        {
            AudioMixerSnapshot snapshot = bank == null ? null : bank.FindSnapshot(name);
            if (snapshot == null)
            {
                Debug.LogWarning("Ruby audio snapshot is missing: " + name, this);
                return;
            }
            float seconds = float.IsNaN(transitionSeconds) || float.IsInfinity(transitionSeconds)
                ? 0f : Mathf.Max(0f, transitionSeconds);
            snapshot.TransitionTo(seconds);
        }

        public void SetMasterVolume01(float value) { SetUserVolume(0, value); }
        public void SetMusicVolume01(float value) { SetUserVolume(1, value); }
        public void SetSfxVolume01(float value) { SetUserVolume(2, value); }
        public void SetUiVolume01(float value) { SetUserVolume(3, value); }
        public void SetAmbienceVolume01(float value) { SetUserVolume(4, value); }

        public static float LinearToDecibels(float value)
        {
            float linear = SanitizeLinear(value);
            // The native mixer's mute floor is -80 dB. Never evaluate log(0).
            return linear <= 0.0001f ? -80f : Mathf.Clamp(20f * Mathf.Log10(linear), -80f, 0f);
        }

        private static float SanitizeLinear(float value)
        {
            if (float.IsNaN(value)) return 0f;
            return Mathf.Clamp01(value);
        }

        private void SetUserVolume(int index, float value)
        {
            pendingVolumes[index] = SanitizeLinear(value);
            hasPendingVolume[index] = true;
            if (started) ApplyUserVolume(index);
        }

        private void ApplyUserVolume(int index)
        {
            if (bank == null || bank.mixer == null)
            {
                Debug.LogWarning("Ruby audio needs an assigned bank and mixer to set user volume.", this);
                return;
            }
            if (bank.mixer.SetFloat(VolumeParameters[index], LinearToDecibels(pendingVolumes[index])))
                hasPendingVolume[index] = false;
            else
                Debug.LogWarning("Ruby mixer parameter not exposed: " + VolumeParameters[index] +
                    ". Run Tools/Ruby's Frisbee/Setup or Repair.", this);
        }
    }
}
