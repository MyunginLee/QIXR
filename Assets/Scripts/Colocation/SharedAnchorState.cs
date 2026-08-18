#if FUSION_PRESENT || FUSION_WEAVER
using System;
using Fusion;
using UnityEngine;

namespace ArtsOfEntanglement.Colocation
{
    public class SharedAnchorState : NetworkBehaviour
    {
        [Networked]
        public NetworkString<_64> AnchorId { get; set; }

        public event Action<string> AnchorIdChanged;

        private string lastAnchorId;

        public override void Spawned()
        {
            if (SessionManager.Instance != null)
            {
                SessionManager.Instance.RegisterAnchorState(this);
            }

            var current = AnchorId.ToString();
            lastAnchorId = current;
            if (!string.IsNullOrEmpty(current))
            {
                HandleAnchorIdChanged(current);
            }
        }

        public override void Render()
        {
            var current = AnchorId.ToString();
            if (current != lastAnchorId)
            {
                lastAnchorId = current;
                HandleAnchorIdChanged(current);
            }
        }

        public void SetAnchorId(string id)
        {
            if (!HasStateAuthority)
            {
                return;
            }

            AnchorId = id;
            if (lastAnchorId != id)
            {
                lastAnchorId = id;
                HandleAnchorIdChanged(id);
            }
        }

        private void HandleAnchorIdChanged(string id)
        {
            AnchorIdChanged?.Invoke(id);
        }
    }
}
#else
using System;
using UnityEngine;

namespace ArtsOfEntanglement.Colocation
{
    public class SharedAnchorState : MonoBehaviour
    {
        public event Action<string> AnchorIdChanged;
        public bool HasStateAuthority => true;

        public void SetAnchorId(string id)
        {
            AnchorIdChanged?.Invoke(id);
        }
    }
}
#endif
