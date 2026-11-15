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
        static bool touchDashQueued;

        static float lastLeftTapTime = -10f;
        static float lastRightTapTime = -10f;
        const float DoubleTapWindow = 0.24f;

        public static float Horizontal { get; private set; }
        public static bool JumpPressed { get; private set; }
        public static bool JumpHeld { get; private set; }
        public static bool DashPressed { get; private set; }

        public static void SetTouchHorizontal(float value)
        {
            float clamped = Mathf.Clamp(value, -1f, 1f);
            if (clamped < -0.5f && touchHorizontal >= -0.5f)
            {
                if (Time.unscaledTime - lastLeftTapTime <= DoubleTapWindow)
                {
                    touchDashQueued = true;
                }
                lastLeftTapTime = Time.unscaledTime;
            }
            else if (clamped > 0.5f && touchHorizontal <= 0.5f)
            {
                if (Time.unscaledTime - lastRightTapTime <= DoubleTapWindow)
                {
                    touchDashQueued = true;
                }
                lastRightTapTime = Time.unscaledTime;
            }

            touchHorizontal = clamped;
        }

        public static void QueueTouchJump()
        {
            touchJumpQueued = true;
        }

        public static void QueueTouchDash()
        {
            touchDashQueued = true;
        }

        public static void Refresh()
        {
            JumpPressed = false;
            DashPressed = false;

            float keyboardHorizontal = UnityEngine.Input.GetAxisRaw("Horizontal");
            Horizontal = Mathf.Abs(keyboardHorizontal) > 0.01f ? keyboardHorizontal : touchHorizontal;

            if (UnityEngine.Input.GetButtonDown("Jump")
                || UnityEngine.Input.GetKeyDown(KeyCode.W)
                || UnityEngine.Input.GetKeyDown(KeyCode.UpArrow))
            {
                JumpPressed = true;
            }

            JumpHeld = UnityEngine.Input.GetButton("Jump")
                || UnityEngine.Input.GetKey(KeyCode.W)
                || UnityEngine.Input.GetKey(KeyCode.UpArrow)
                || touchJumpQueued;

            if (UnityEngine.Input.GetKeyDown(KeyCode.LeftShift)
                || UnityEngine.Input.GetKeyDown(KeyCode.RightShift)
                || UnityEngine.Input.GetKeyDown(KeyCode.S)
                || UnityEngine.Input.GetKeyDown(KeyCode.DownArrow))
            {
                DashPressed = true;
            }

            if (touchJumpQueued)
            {
                JumpPressed = true;
                touchJumpQueued = false;
            }

            if (touchDashQueued)
            {
                DashPressed = true;
                touchDashQueued = false;
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
