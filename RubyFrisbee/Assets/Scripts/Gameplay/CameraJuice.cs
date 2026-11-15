using UnityEngine;

namespace RubyFrisbee.Gameplay
{
    /// <summary>
    /// Subtle camera shake when Ruby catches or misses a frisbee.
    /// </summary>
    public class CameraJuice : MonoBehaviour
    {
        public static CameraJuice Instance { get; private set; }

        [SerializeField] float shakeDuration = 0.14f;
        [SerializeField] float shakeMagnitude = 0.14f;

        Vector3 originalPosition;
        float shakeTimer;
        float activeMagnitude;

        void Awake()
        {
            Instance = this;
            originalPosition = transform.localPosition;
        }

        void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        void LateUpdate()
        {
            if (shakeTimer <= 0f)
            {
                transform.localPosition = originalPosition;
                return;
            }

            shakeTimer -= Time.deltaTime;
            float decay = Mathf.Clamp01(shakeTimer / Mathf.Max(0.01f, shakeDuration));
            Vector2 offset = Random.insideUnitCircle * (activeMagnitude * decay);
            transform.localPosition = originalPosition + new Vector3(offset.x, offset.y, 0f);
        }

        public void Shake()
        {
            Shake(1f);
        }

        public void Shake(float intensityMultiplier)
        {
            shakeTimer = shakeDuration;
            activeMagnitude = shakeMagnitude * Mathf.Clamp(intensityMultiplier, 0.25f, 2.5f);
        }
    }
}
