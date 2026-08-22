using UnityEngine;
using UnityEngine.EventSystems;

namespace RubyFrisbee.UI
{
    /// <summary>
    /// UI button that reports press-and-hold state (for move left/right).
    /// </summary>
    public class HoldButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        public System.Action<bool> OnHoldChanged;

        bool isHeld;

        public bool IsHeld => isHeld;

        public void OnPointerDown(PointerEventData eventData)
        {
            SetHeld(true);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            SetHeld(false);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            SetHeld(false);
        }

        void SetHeld(bool held)
        {
            if (isHeld == held)
            {
                return;
            }

            isHeld = held;
            OnHoldChanged?.Invoke(isHeld);
        }
    }
}
