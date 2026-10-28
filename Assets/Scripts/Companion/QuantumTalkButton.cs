using UnityEngine;
using UnityEngine.EventSystems;

namespace Qixr.Companion
{
    public sealed class QuantumTalkButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler, ICancelHandler
    {
        public QuantumCompanionClient Client;
        public void OnPointerDown(PointerEventData data) { if(Client!=null) Client.BeginTalk(); }
        public void OnPointerUp(PointerEventData data) { if(Client!=null) Client.EndTalk(); }
        public void OnPointerExit(PointerEventData data) { Cancel(); }
        public void OnCancel(BaseEventData data) { Cancel(); }
        private void Cancel() { if(Client!=null && Client.IsRecording) Client.Interrupt(); }
        private void OnDisable() { if(Client!=null && Client.IsRecording) Client.Interrupt(); }
    }
}
