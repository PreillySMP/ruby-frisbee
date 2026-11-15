using UnityEngine;

namespace RubysFrisbee.Assets
{
    /// <summary>Optional IMGUI asset tester. Not player input, physics, scoring, or game integration.</summary>
    public sealed class RubyAssetPreview : MonoBehaviour
    {
        public RubyAnimationDriver ruby;
        public RubyAudioPlayer audioPlayer;
        public RubyOneShotEffect[] effects;
        public float jumpHeight = 2f;
        Vector3 startPosition;
        bool moving, sitting, facingRight = true;
        float jumpTime = -1f;
        string cue = "";
        Vector2 scroll;
        bool showControls = true;

        void Start()
        {
            if (ruby) startPosition = ruby.transform.localPosition;
            // Only provide a listener when this tester runs standalone; do not disable any user's listener.
            bool hasListener = false;
            foreach (var listener in FindObjectsByType<AudioListener>(FindObjectsSortMode.None))
                if (listener.isActiveAndEnabled) { hasListener = true; break; }
            if (audioPlayer && !hasListener) gameObject.AddComponent<AudioListener>();
        }
        void Update()
        {
            if (!ruby) return;
            float y = 0f, vy = 0f;
            bool grounded = jumpTime < 0f;
            if (!grounded)
            {
                jumpTime += Time.deltaTime;
                if (jumpTime >= 1f) { jumpTime = -1f; grounded = true; }
                else { y = 4f * jumpHeight * jumpTime * (1f - jumpTime); vy = 4f * jumpHeight * (1f - 2f * jumpTime); }
            }
            ruby.transform.localPosition = startPosition + Vector3.up * y;
            ruby.SetLocomotion(moving ? 1f : 0f, vy, grounded);
        }

        void OnGUI()
        {
            if (GUI.Button(new Rect(12, 12, 150, 28), showControls ? "Hide preview controls" : "Show preview controls"))
                showControls = !showControls;
            if (!showControls) return;
            GUILayout.BeginArea(new Rect(12, 48, 310, Mathf.Max(120, Mathf.Min(760, Screen.height - 60))), GUI.skin.box);
            scroll = GUILayout.BeginScrollView(scroll);
            GUILayout.Label("Ruby asset preview (visual test only)");
            GUILayout.Label("No score, collision, or gameplay integration.");
            if (ruby)
            {
                bool nextMoving = GUILayout.Toggle(moving, "Move / run");
                if (nextMoving != moving)
                {
                    moving = nextMoving;
                    if (moving) { sitting = false; ruby.SetSitting(false); }
                }
                bool nextSitting = GUILayout.Toggle(sitting, "Sit");
                if (nextSitting != sitting)
                {
                    sitting = nextSitting;
                    if (sitting) moving = false;
                    ruby.SetSitting(sitting);
                }
                bool nextFacing = GUILayout.Toggle(facingRight, "Face right");
                if (nextFacing != facingRight) { facingRight = nextFacing; ruby.SetFacing(facingRight); }
                if (GUILayout.Button("Jump (visual position only)") && jumpTime < 0f && !ruby.GameOverActive)
                { jumpTime = 0f; sitting = false; ruby.SetSitting(false); }
                if (GUILayout.Button("Catch")) ruby.TriggerCatch();
                if (GUILayout.Button("Celebrate")) ruby.TriggerCelebrate();
                bool over = GUILayout.Toggle(ruby.GameOverActive, "Game over");
                if (over && !ruby.GameOverActive) { moving = false; sitting = false; jumpTime = -1f; }
                ruby.SetGameOver(over); // Exercises the true-edge guard even when called every GUI event.
                if (GUILayout.Button("Recover")) { moving = false; sitting = false; ruby.Recover(); }
            }
            if (audioPlayer)
            {
                GUILayout.Label("Sound cue key (see audio bank):");
                cue = GUILayout.TextField(cue);
                if (GUILayout.Button("Play cue") && !string.IsNullOrWhiteSpace(cue)) audioPlayer.PlayCue(cue);
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Music")) audioPlayer.PlayMusic();
                if (GUILayout.Button("Stop music")) audioPlayer.StopMusic();
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Gameplay mix")) audioPlayer.ApplySnapshot("Gameplay", 0.2f);
                if (GUILayout.Button("Menu mix")) audioPlayer.ApplySnapshot("Menu", 0.2f);
                if (GUILayout.Button("Paused mix")) audioPlayer.ApplySnapshot("Paused", 0.2f);
                GUILayout.EndHorizontal();
            }
            if (effects != null)
                foreach (var effect in effects)
                    if (effect && GUILayout.Button("FX: " + effect.name)) effect.Restart();
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }
    }
}
