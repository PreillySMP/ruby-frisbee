using System;
using UnityEngine;
using UnityEngine.Events;

namespace RubysFrisbee.Assets
{
    /// <summary>Restartable/pool-friendly single-state FX. Disables its GameObject, never destroys it.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(Animator))]
    public sealed class RubyOneShotEffect : MonoBehaviour
    {
        [Min(0.001f)] public float duration = 1f;
        public bool autoDisable = true;
        [Serializable] public sealed class CueEvent : UnityEvent<string> { }
        public CueEvent animationCue = new CueEvent();
        Animator target;
        float elapsed;

        void OnEnable()
        {
            target = GetComponent<Animator>();
            elapsed = 0f;
            if (target.runtimeAnimatorController)
                target.Play("Base Layer.Play", 0, 0f);
        }

        void LateUpdate()
        {
            if (!autoDisable || !target || !target.enabled) return;
            // Scaled time deliberately pauses along with the normal Animator update mode.
            elapsed += Time.deltaTime * Mathf.Abs(target.speed);
            if (elapsed >= Mathf.Max(0.001f, duration)) gameObject.SetActive(false);
        }

        public void Restart()
        {
            if (!gameObject.activeSelf) gameObject.SetActive(true);
            else OnEnable();
        }
        public void OnAnimationCue(string cue) => animationCue.Invoke(cue);
    }
}
