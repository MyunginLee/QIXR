#if META_XR_SDK || OCULUS_INTEGRATION || OVRPLUGIN_PRESENT
#define META_XR_PRESENT
#endif

using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.XR;

namespace ArtsOfEntanglement.Colocation
{
    public class SharedAnchorHost : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private SessionManager sessionManager;
        [SerializeField] private SharedSpaceAligner sharedSpaceAligner;

        [Header("Placement")]
        [SerializeField] private Transform rayOrigin;
        [SerializeField] private LayerMask placementMask = -1;
        [SerializeField] private float maxDistance = 5f;
        [SerializeField] private float triggerThreshold = 0.6f;
        [SerializeField] private float placementCooldown = 0.5f;

        [Header("Anchor")]
        [SerializeField] private float createTimeoutSeconds = 10f;

        private bool isPlacing;
        private bool wasPressed;
        private float nextAllowedTime;
        private string pendingSessionName;

        public event Action<string> StatusMessage;

        private void Awake()
        {
            if (sessionManager == null)
            {
                sessionManager = FindObjectOfType<SessionManager>();
            }

            if (sharedSpaceAligner == null)
            {
                sharedSpaceAligner = FindObjectOfType<SharedSpaceAligner>();
            }
        }

        public void BeginPlacement(string sessionName)
        {
            pendingSessionName = sessionName;
            isPlacing = true;
            ReportStatus("Point at a surface and press trigger to place anchor.");
        }

        private void Update()
        {
            if (!isPlacing)
            {
                return;
            }

            bool pressed = GetTriggerPressed();
            if (pressed && !wasPressed && Time.time >= nextAllowedTime)
            {
                nextAllowedTime = Time.time + placementCooldown;
                if (TryGetPlacementPose(out var pose))
                {
                    isPlacing = false;
                    CreateAnchorAtPose(pose);
                }
                else
                {
                    ReportStatus("No placement pose found.");
                }
            }

            wasPressed = pressed;
        }

        private bool GetTriggerPressed()
        {
            var device = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
            if (!device.isValid)
            {
                device = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
            }

            if (device.isValid)
            {
                if (device.TryGetFeatureValue(CommonUsages.triggerButton, out bool triggerButton))
                {
                    return triggerButton;
                }

                if (device.TryGetFeatureValue(CommonUsages.trigger, out float triggerValue))
                {
                    return triggerValue >= triggerThreshold;
                }
            }

            return Input.GetMouseButtonDown(0);
        }

        private bool TryGetPlacementPose(out Pose pose)
        {
            var origin = rayOrigin != null ? rayOrigin : (Camera.main != null ? Camera.main.transform : null);
            if (origin == null)
            {
                pose = default;
                return false;
            }

            var ray = new Ray(origin.position, origin.forward);
            if (Physics.Raycast(ray, out var hit, maxDistance, placementMask))
            {
                var up = hit.normal;
                var forward = Vector3.ProjectOnPlane(origin.forward, up);
                if (forward.sqrMagnitude < 0.001f)
                {
                    forward = Vector3.Cross(up, origin.right);
                }

                pose = new Pose(hit.point, Quaternion.LookRotation(forward.normalized, up));
                return true;
            }

            pose = new Pose(origin.position + origin.forward * maxDistance, origin.rotation);
            return true;
        }

        private async void CreateAnchorAtPose(Pose pose)
        {
#if META_XR_PRESENT
            ReportStatus("Creating spatial anchor...");

            var anchorGo = new GameObject("SharedAnchor_Host");
            anchorGo.transform.SetPositionAndRotation(pose.position, pose.rotation);
            var spatialAnchor = anchorGo.AddComponent<OVRSpatialAnchor>();

            bool created = await WaitForAnchorCreated(spatialAnchor, createTimeoutSeconds);
            if (!created)
            {
                ReportStatus("Anchor creation timed out.");
                Destroy(anchorGo);
                return;
            }

            bool saved = await spatialAnchor.SaveAsync();
            if (!saved)
            {
                ReportStatus("Anchor save failed.");
                Destroy(anchorGo);
                return;
            }

            var anchorId = spatialAnchor.Uuid.ToString();
            ReportStatus($"Anchor saved: {anchorId}");

            if (sharedSpaceAligner != null)
            {
                sharedSpaceAligner.AlignToAnchorPose(pose);
            }

            if (sessionManager != null)
            {
                sessionManager.StartHost(pendingSessionName);
                sessionManager.SetSharedAnchorId(anchorId);
            }
            else
            {
                ReportStatus("SessionManager missing.");
            }
#else
            ReportStatus("Meta XR SDK not found. Install Meta XR SDK and add META_XR_SDK define.");
#endif
        }

#if META_XR_PRESENT
        private static async Task<bool> WaitForAnchorCreated(OVRSpatialAnchor anchor, float timeoutSeconds)
        {
            float start = Time.realtimeSinceStartup;
            while (!anchor.Created)
            {
                if (Time.realtimeSinceStartup - start > timeoutSeconds)
                {
                    return false;
                }

                await Task.Yield();
            }

            return true;
        }
#endif

        private void ReportStatus(string message)
        {
            Debug.Log($"[SharedAnchorHost] {message}");
            StatusMessage?.Invoke(message);
        }
    }
}
