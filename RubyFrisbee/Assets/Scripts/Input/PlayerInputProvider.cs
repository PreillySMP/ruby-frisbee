using UnityEngine;

namespace RubyFrisbee.Input
{
    /// <summary>
    /// Aggregates keyboard and on-screen touch input for Ruby.
    /// </summary>
    public static class PlayerInputProvider
    {
        static float touchHorizontal;
        static bool touchJumpQueued;

        public static float Horizontal { get; private set; }
        public static bool JumpPressed { get; private set; }

        public static void SetTouchHorizontal(float value)
        {
            touchHorizontal = Mathf.Clamp(value, -1f, 1f);
        }

        public static void QueueTouchJump()
        {
            touchJumpQueued = true;
        }

        public static void Refresh()
        {
            JumpPressed = false;

            float keyboardHorizontal = UnityEngine.Input.GetAxisRaw("Horizontal");
            Horizontal = Mathf.Abs(keyboardHorizontal) > 0.01f ? keyboardHorizontal : touchHorizontal;

            if (UnityEngine.Input.GetButtonDown("Jump")
                || UnityEngine.Input.GetKeyDown(KeyCode.W)
                || UnityEngine.Input.GetKeyDown(KeyCode.UpArrow))
            {
                JumpPressed = true;
            }

            if (touchJumpQueued)
            {
                JumpPressed = true;
                touchJumpQueued = false;
            }
        }

        public static bool ShouldShowTouchControls()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return UnityEngine.Input.touchSupported || Application.isMobilePlatform;
#else
            return Application.isMobilePlatform || UnityEngine.Input.touchSupported;
#endif
        }
    }
}
