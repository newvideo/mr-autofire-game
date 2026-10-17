using UnityEngine;
using UnityEngine.EventSystems;

namespace Polyus
{
    // Pointer ownership lets separate fingers hold movement and activate abilities.
    public sealed class PolyusControl : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        public bool Held { get; private set; }
        public bool Pressed { get; private set; }
        private int pointer = int.MinValue;

        public void OnPointerDown(PointerEventData data)
        {
            if (pointer != int.MinValue) return;
            pointer = data.pointerId;
            Held = true;
            Pressed = true;
        }

        public void OnPointerUp(PointerEventData data)
        {
            if (data.pointerId != pointer) return;
            ResetHold();
        }

        public void OnPointerExit(PointerEventData data) => OnPointerUp(data);
        public bool ConsumePress() { bool value = Pressed; Pressed = false; return value; }
        public void Clear() { ResetHold(); Pressed = false; }
        private void ResetHold() { pointer = int.MinValue; Held = false; }
        private void OnDisable() => Clear();
    }
}
