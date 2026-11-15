using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace RubysFrisbee.Assets.Editor
{
    /// <summary>Play-mode soundboard using public AudioSource/AudioMixer APIs only.</summary>
    [CustomEditor(typeof(RubyAudioPlayer))]
    internal sealed class RubyAudioPlayerEditor : UnityEditor.Editor
    {
        private int selectedCue;
        private float transitionSeconds = 0.25f;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var player = (RubyAudioPlayer)target;
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Play-mode Soundboard", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Enter Play mode with this rig and one active AudioListener. " +
                "Nothing plays automatically. Water ambience is an optional cue, not a wind bed. " +
                "Test a copy of the generated rig to keep repair-owned assets unchanged.", MessageType.Info);
            using (new EditorGUI.DisabledScope(!Application.isPlaying || player.bank == null))
            {
                var keys = new List<string>();
                if (player.bank != null && player.bank.entries != null)
                    foreach (RubyAudioBank.Entry entry in player.bank.entries)
                        if (entry != null && entry.clip != null) keys.Add(entry.key);
                if (keys.Count > 0)
                {
                    selectedCue = Mathf.Clamp(selectedCue, 0, keys.Count - 1);
                    selectedCue = EditorGUILayout.Popup("Cue", selectedCue, keys.ToArray());
                    if (GUILayout.Button("Play Selected Cue")) player.PlayCue(keys[selectedCue]);
                }
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Play Music")) player.PlayMusic();
                    if (GUILayout.Button("Stop Music")) player.StopMusic();
                    if (GUILayout.Button("Stop All Sources"))
                    {
                        Stop(player.musicSource);
                        Stop(player.ambienceSource);
                        Stop(player.sfxSource);
                        Stop(player.uiSource);
                    }
                }
                transitionSeconds = EditorGUILayout.Slider("Transition (seconds)", transitionSeconds, 0f, 2f);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Gameplay")) player.ApplySnapshot("Gameplay", transitionSeconds);
                    if (GUILayout.Button("Menu")) player.ApplySnapshot("Menu", transitionSeconds);
                    if (GUILayout.Button("Paused")) player.ApplySnapshot("Paused", transitionSeconds);
                }
                DrawVolume(player, "Master", "MasterVolumeDb", player.SetMasterVolume01);
                DrawVolume(player, "Music", "MusicVolumeDb", player.SetMusicVolume01);
                DrawVolume(player, "SFX", "SfxVolumeDb", player.SetSfxVolume01);
                DrawVolume(player, "UI", "UiVolumeDb", player.SetUiVolume01);
                DrawVolume(player, "Ambience", "AmbienceVolumeDb", player.SetAmbienceVolume01);
            }
        }

        private static void Stop(AudioSource source)
        {
            if (source != null) source.Stop();
        }

        private static void DrawVolume(RubyAudioPlayer player, string label, string parameter, Action<float> setter)
        {
            float value = 1f;
            if (player.bank != null && player.bank.mixer != null &&
                player.bank.mixer.GetFloat(parameter, out float db))
                value = db <= -80f ? 0f : Mathf.Clamp01(Mathf.Pow(10f, db / 20f));
            EditorGUI.BeginChangeCheck();
            float next = EditorGUILayout.Slider(label + " User Volume", value, 0f, 1f);
            if (EditorGUI.EndChangeCheck()) setter(next);
        }
    }
}
