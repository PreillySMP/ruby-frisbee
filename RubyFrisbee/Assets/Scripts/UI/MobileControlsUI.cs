using RubyFrisbee.Input;
using UnityEngine;
using UnityEngine.UI;

namespace RubyFrisbee.UI
{
    /// <summary>
    /// On-screen left/right/jump controls for phone and WebGL browsers.
    /// </summary>
    public class MobileControlsUI : MonoBehaviour
    {
        [SerializeField] GameObject controlsRoot;
        [SerializeField] HoldButton leftButton;
        [SerializeField] HoldButton rightButton;
        [SerializeField] Button jumpButton;
        [SerializeField] bool forceVisibleInEditor;

        bool leftHeld;
        bool rightHeld;

        void Awake()
        {
            if (controlsRoot == null)
            {
                controlsRoot = gameObject;
            }

            if (leftButton != null)
            {
                leftButton.OnHoldChanged += held => leftHeld = held;
            }

            if (rightButton != null)
            {
                rightButton.OnHoldChanged += held => rightHeld = held;
            }

            if (jumpButton != null)
            {
                jumpButton.onClick.AddListener(PlayerInputProvider.QueueTouchJump);
            }

            var shouldShow = forceVisibleInEditor || PlayerInputProvider.ShouldShowTouchControls();
            controlsRoot.SetActive(shouldShow);
        }

        void Update()
        {
            float horizontal = 0f;
            if (leftHeld)
            {
                horizontal -= 1f;
            }

            if (rightHeld)
            {
                horizontal += 1f;
            }

            PlayerInputProvider.SetTouchHorizontal(horizontal);
        }

        public void SetGameplayVisible(bool visible)
        {
            if (controlsRoot == null)
            {
                return;
            }

            var deviceSupportsTouch = forceVisibleInEditor || PlayerInputProvider.ShouldShowTouchControls();
            controlsRoot.SetActive(visible && deviceSupportsTouch);
        }

        public void WireReferences(GameObject root, HoldButton left, HoldButton right, Button jump)
        {
            controlsRoot = root;
            leftButton = left;
            rightButton = right;
            jumpButton = jump;
        }
    }
}
