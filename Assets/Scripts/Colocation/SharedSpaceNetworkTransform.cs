#if FUSION_PRESENT || FUSION_WEAVER
using Fusion;
using UnityEngine;

namespace ArtsOfEntanglement.Colocation
{
    public class SharedSpaceNetworkTransform : NetworkBehaviour
    {
        [SerializeField] private SharedSpaceAligner sharedSpaceAligner;
        [SerializeField] private float smoothing = 12f;

        [Networked] private Vector3 LocalPos { get; set; }
        [Networked] private Quaternion LocalRot { get; set; }

        public override void Spawned()
        {
            if (sharedSpaceAligner == null)
            {
                sharedSpaceAligner = FindObjectOfType<SharedSpaceAligner>();
            }
        }

        public override void FixedUpdateNetwork()
        {
            if (!HasStateAuthority)
            {
                return;
            }

            var root = sharedSpaceAligner != null ? sharedSpaceAligner.SharedSpaceRoot : null;
            if (root != null)
            {
                LocalPos = root.InverseTransformPoint(transform.position);
                LocalRot = Quaternion.Inverse(root.rotation) * transform.rotation;
            }
            else
            {
                LocalPos = transform.position;
                LocalRot = transform.rotation;
            }
        }

        public override void Render()
        {
            if (HasStateAuthority)
            {
                return;
            }

            var root = sharedSpaceAligner != null ? sharedSpaceAligner.SharedSpaceRoot : null;
            var targetPos = root != null ? root.TransformPoint(LocalPos) : LocalPos;
            var targetRot = root != null ? root.rotation * LocalRot : LocalRot;

            float lerp = smoothing * Time.deltaTime;
            transform.position = Vector3.Lerp(transform.position, targetPos, lerp);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, lerp);
        }
    }
}
#else
using UnityEngine;

namespace ArtsOfEntanglement.Colocation
{
    public class SharedSpaceNetworkTransform : MonoBehaviour
    {
        private void Awake()
        {
            Debug.LogWarning("[SharedSpaceNetworkTransform] Fusion not installed. Component is inactive.");
        }
    }
}
#endif
