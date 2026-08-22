using UnityEngine;

namespace RubyFrisbee.Gameplay
{
    /// <summary>
    /// Subtle camera shake when Ruby catches a frisbee.
    /// </summary>
    public class CameraJuice : MonoBehaviour
    {
        [SerializeField] float shakeDuration = 0.15f;
        [SerializeField] float shakeMagnitude = 0.2f;

        Vector3 originalPosition;
        float shakeTimer;

        void Awake()
        {
            originalPosition = transform.localPosition;
        }

        void LateUpdate()
        {
            if (shakeTimer <= 0f)
            {
                transform.localPosition = originalPosition;
                return;
            }

            shakeTimer -= Time.deltaTime;
            Vector2 offset = Random.insideUnitCircle * shakeMagnitude;
            transform.localPosition = originalPosition + new Vector3(offset.x, offset.y, 0f);
        }

        public void Shake()
        {
            shakeTimer = shakeDuration;
        }
    }
}
