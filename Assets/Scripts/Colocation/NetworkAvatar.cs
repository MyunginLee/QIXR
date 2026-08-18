#if FUSION_PRESENT || FUSION_WEAVER
#if META_XR_SDK || OCULUS_INTEGRATION || OVRPLUGIN_PRESENT
#define META_XR_PRESENT
#endif

using Fusion;
using Unity.XR.CoreUtils;
using UnityEngine;

namespace ArtsOfEntanglement.Colocation
{
    public class NetworkAvatar : NetworkBehaviour
    {
        [Header("Sources (local player)")]
        [SerializeField] private Transform headSource;
        [SerializeField] private Transform leftHandSource;
        [SerializeField] private Transform rightHandSource;

        [Header("Visuals (remote players)")]
        [SerializeField] private Transform headVisual;
        [SerializeField] private Transform leftHandVisual;
        [SerializeField] private Transform rightHandVisual;
        [SerializeField] private bool hideLocalAvatar = true;

        [Header("Networking")]
        [SerializeField] private float sendRateHz = 20f;
        [SerializeField] private float smoothing = 12f;
        [SerializeField] private SharedSpaceAligner sharedSpaceAligner;

        [Networked] private Vector3 HeadPos { get; set; }
        [Networked] private Quaternion HeadRot { get; set; }
        [Networked] private Vector3 LeftPos { get; set; }
        [Networked] private Quaternion LeftRot { get; set; }
        [Networked] private Vector3 RightPos { get; set; }
        [Networked] private Quaternion RightRot { get; set; }

        private int sendEveryNTicks = 1;

        public override void Spawned()
        {
            if (Runner != null)
            {
                if (Runner.TickRate > 0f)
                {
                    sendEveryNTicks = Mathf.Max(1, Mathf.RoundToInt(Runner.TickRate / sendRateHz));
                }
            }

            if (sharedSpaceAligner == null)
            {
                sharedSpaceAligner = FindObjectOfType<SharedSpaceAligner>();
            }

            if (HasInputAuthority)
            {
                AutoBindSources();
                if (hideLocalAvatar)
                {
                    SetVisualsActive(false);
                }
            }
        }

        public override void FixedUpdateNetwork()
        {
            if (!HasInputAuthority)
            {
                return;
            }

            if (Runner != null && Runner.Tick % sendEveryNTicks != 0)
            {
                return;
            }

            var root = sharedSpaceAligner != null ? sharedSpaceAligner.SharedSpaceRoot : null;

            if (headSource != null)
            {
                if (root != null)
                {
                    HeadPos = root.InverseTransformPoint(headSource.position);
                    HeadRot = Quaternion.Inverse(root.rotation) * headSource.rotation;
                }
                else
                {
                    HeadPos = headSource.position;
                    HeadRot = headSource.rotation;
                }
            }

            if (leftHandSource != null)
            {
                if (root != null)
                {
                    LeftPos = root.InverseTransformPoint(leftHandSource.position);
                    LeftRot = Quaternion.Inverse(root.rotation) * leftHandSource.rotation;
                }
                else
                {
                    LeftPos = leftHandSource.position;
                    LeftRot = leftHandSource.rotation;
                }
            }

            if (rightHandSource != null)
            {
                if (root != null)
                {
                    RightPos = root.InverseTransformPoint(rightHandSource.position);
                    RightRot = Quaternion.Inverse(root.rotation) * rightHandSource.rotation;
                }
                else
                {
                    RightPos = rightHandSource.position;
                    RightRot = rightHandSource.rotation;
                }
            }
        }

        public override void Render()
        {
            if (HasInputAuthority)
            {
                return;
            }

            float lerp = smoothing * Time.deltaTime;
            var root = sharedSpaceAligner != null ? sharedSpaceAligner.SharedSpaceRoot : null;

            if (headVisual != null)
            {
                var targetPos = root != null ? root.TransformPoint(HeadPos) : HeadPos;
                var targetRot = root != null ? root.rotation * HeadRot : HeadRot;
                headVisual.position = Vector3.Lerp(headVisual.position, targetPos, lerp);
                headVisual.rotation = Quaternion.Slerp(headVisual.rotation, targetRot, lerp);
            }

            if (leftHandVisual != null)
            {
                var targetPos = root != null ? root.TransformPoint(LeftPos) : LeftPos;
                var targetRot = root != null ? root.rotation * LeftRot : LeftRot;
                leftHandVisual.position = Vector3.Lerp(leftHandVisual.position, targetPos, lerp);
                leftHandVisual.rotation = Quaternion.Slerp(leftHandVisual.rotation, targetRot, lerp);
            }

            if (rightHandVisual != null)
            {
                var targetPos = root != null ? root.TransformPoint(RightPos) : RightPos;
                var targetRot = root != null ? root.rotation * RightRot : RightRot;
                rightHandVisual.position = Vector3.Lerp(rightHandVisual.position, targetPos, lerp);
                rightHandVisual.rotation = Quaternion.Slerp(rightHandVisual.rotation, targetRot, lerp);
            }
        }

        private void AutoBindSources()
        {
#if META_XR_PRESENT
            var ovrRig = FindObjectOfType<OVRCameraRig>();
            if (ovrRig != null)
            {
                if (headSource == null) headSource = ovrRig.centerEyeAnchor;
                if (leftHandSource == null) leftHandSource = ovrRig.leftHandAnchor;
                if (rightHandSource == null) rightHandSource = ovrRig.rightHandAnchor;
            }
#endif

            if (headSource == null)
            {
                var xrOrigin = FindObjectOfType<XROrigin>();
                if (xrOrigin != null && xrOrigin.Camera != null)
                {
                    headSource = xrOrigin.Camera.transform;
                }
            }
        }

        private void SetVisualsActive(bool active)
        {
            if (headVisual != null) headVisual.gameObject.SetActive(active);
            if (leftHandVisual != null) leftHandVisual.gameObject.SetActive(active);
            if (rightHandVisual != null) rightHandVisual.gameObject.SetActive(active);
        }
    }
}
#else
using UnityEngine;

namespace ArtsOfEntanglement.Colocation
{
    public class NetworkAvatar : MonoBehaviour
    {
        private void Awake()
        {
            Debug.LogWarning("[NetworkAvatar] Fusion not installed. Component is inactive.");
        }
    }
}
#endif
