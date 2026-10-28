using UnityEngine;
using UnityEngine.EventSystems;

namespace Qixr.Companion
{
    public sealed class QuantumTalkButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        public QuantumCompanionClient Client;
        public void OnPointerDown(PointerEventData data) { Client.BeginTalk(); }
        public void OnPointerUp(PointerEventData data) { Client.EndTalk(); }
        private void OnDisable() { if(Client!=null && Client.IsRecording) Client.Interrupt(); }
    }
}
