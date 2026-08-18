#if META_XR_SDK || OCULUS_INTEGRATION || OVRPLUGIN_PRESENT
#define META_XR_PRESENT
#endif

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace ArtsOfEntanglement.Colocation
{
    public class SharedAnchorClient : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private SessionManager sessionManager;
        [SerializeField] private SharedSpaceAligner sharedSpaceAligner;

        [Header("Anchor")]
        [SerializeField] private float localizationTimeoutSeconds = 15f;

        private bool isLocalizing;
        private Pose? lastAnchorPose;
        private string currentAnchorId;

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

        private void OnEnable()
        {
            if (sessionManager != null)
            {
                sessionManager.SharedAnchorIdChanged += OnAnchorIdChanged;
            }
        }

        private void OnDisable()
        {
            if (sessionManager != null)
            {
                sessionManager.SharedAnchorIdChanged -= OnAnchorIdChanged;
            }
        }

        private void OnAnchorIdChanged(string anchorId)
        {
            if (string.IsNullOrWhiteSpace(anchorId))
            {
                return;
            }

            if (anchorId == currentAnchorId && isLocalizing)
            {
                return;
            }

            currentAnchorId = anchorId;
            StartLocalization(anchorId);
        }

        public void ReAlign()
        {
            if (lastAnchorPose.HasValue && sharedSpaceAligner != null)
            {
                sharedSpaceAligner.AlignToAnchorPose(lastAnchorPose.Value);
            }
        }

        private async void StartLocalization(string anchorId)
        {
#if META_XR_PRESENT
            if (isLocalizing)
            {
                return;
            }

            isLocalizing = true;
            ReportStatus("Localizing shared anchor...");

            if (!Guid.TryParse(anchorId, out var guid))
            {
                ReportStatus("Invalid anchor ID format.");
                isLocalizing = false;
                return;
            }

            var unboundAnchors = new List<OVRSpatialAnchor.UnboundAnchor>();
            var loadResult = await OVRSpatialAnchor.LoadUnboundSharedAnchorsAsync(new[] { guid }, unboundAnchors);
            if (!loadResult.Success || unboundAnchors.Count == 0)
            {
                ReportStatus($"Failed to load shared anchor: {loadResult.Status}.");
                isLocalizing = false;
                return;
            }

            var unbound = unboundAnchors[0];
            bool localized = unbound.Localized;
            if (!localized)
            {
                var localizeTask = unbound.LocalizeAsync();
                float start = Time.realtimeSinceStartup;

                while (!localizeTask.IsCompleted)
                {
                    if (localizationTimeoutSeconds > 0f &&
                        Time.realtimeSinceStartup - start > localizationTimeoutSeconds)
                    {
                        ReportStatus("Anchor localization timed out.");
                        isLocalizing = false;
                        return;
                    }

                    await Task.Yield();
                }

                localized = localizeTask.GetResult();
            }

            if (!localized)
            {
                ReportStatus("Anchor localization failed.");
                isLocalizing = false;
                return;
            }

            var anchorGo = new GameObject("SharedAnchor_Client");
            var spatialAnchor = anchorGo.AddComponent<OVRSpatialAnchor>();
            unbound.BindTo(spatialAnchor);

            var pose = new Pose(anchorGo.transform.position, anchorGo.transform.rotation);
            lastAnchorPose = pose;

            if (sharedSpaceAligner != null)
            {
                sharedSpaceAligner.AlignToAnchorPose(pose);
            }

            ReportStatus("Anchor localized.");
            isLocalizing = false;
#else
            ReportStatus("Meta XR SDK not found. Install Meta XR SDK and add META_XR_SDK define.");
#endif
        }

        private void ReportStatus(string message)
        {
            Debug.Log($"[SharedAnchorClient] {message}");
            StatusMessage?.Invoke(message);
        }
    }
}
