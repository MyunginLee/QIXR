#if FUSION_PRESENT || FUSION_WEAVER
using Fusion;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

namespace ArtsOfEntanglement.Colocation
{
    public class NetworkObjectSpawner : NetworkBehaviour
    {
        [SerializeField] private SharedSpaceAligner sharedSpaceAligner;
        [SerializeField] private NetworkObject[] sharedPrefabs;
        [SerializeField] private Vector3[] initialLocalOffsets;
        [SerializeField] private bool spawnOnStart = true;

        public override void Spawned()
        {
            if (sharedSpaceAligner == null)
            {
                sharedSpaceAligner = FindObjectOfType<SharedSpaceAligner>();
            }

            if (HasStateAuthority && spawnOnStart)
            {
                SpawnInitialObjects();
            }
        }

        public void SpawnInitialObjects()
        {
            if (sharedPrefabs == null || sharedPrefabs.Length == 0)
            {
                return;
            }

            for (int i = 0; i < sharedPrefabs.Length; i++)
            {
                var localOffset = Vector3.zero;
                if (initialLocalOffsets != null && i < initialLocalOffsets.Length)
                {
                    localOffset = initialLocalOffsets[i];
                }

                SpawnSharedObject(i, localOffset, Quaternion.identity);
            }
        }

        public NetworkObject SpawnSharedObject(int prefabIndex, Vector3 localPosition, Quaternion localRotation)
        {
            if (!HasStateAuthority)
            {
                return null;
            }

            if (sharedPrefabs == null || prefabIndex < 0 || prefabIndex >= sharedPrefabs.Length)
            {
                return null;
            }

            var root = sharedSpaceAligner != null ? sharedSpaceAligner.SharedSpaceRoot : null;
            var worldPos = root != null ? root.TransformPoint(localPosition) : localPosition;
            var worldRot = root != null ? root.rotation * localRotation : localRotation;

            var spawned = Runner.Spawn(sharedPrefabs[prefabIndex], worldPos, worldRot, Object.InputAuthority);
            if (spawned != null && root != null)
            {
                spawned.transform.SetParent(root, true);
            }

            return spawned;
        }
    }

    public class NetworkGrabAuthority : MonoBehaviour
    {
        [SerializeField] private NetworkObject networkObject;
        [SerializeField] private bool returnAuthorityToHostOnRelease = false;

        private UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable grabInteractable;

        private void Awake()
        {
            if (networkObject == null)
            {
                networkObject = GetComponentInParent<NetworkObject>();
            }

            grabInteractable = GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
        }

        private void OnEnable()
        {
            if (grabInteractable != null)
            {
                grabInteractable.selectEntered.AddListener(OnSelectEntered);
                grabInteractable.selectExited.AddListener(OnSelectExited);
            }
        }

        private void OnDisable()
        {
            if (grabInteractable != null)
            {
                grabInteractable.selectEntered.RemoveListener(OnSelectEntered);
                grabInteractable.selectExited.RemoveListener(OnSelectExited);
            }
        }

        private void OnSelectEntered(SelectEnterEventArgs args)
        {
            if (networkObject != null && !networkObject.HasStateAuthority)
            {
                networkObject.RequestStateAuthority();
            }
        }

        private void OnSelectExited(SelectExitEventArgs args)
        {
            if (returnAuthorityToHostOnRelease && networkObject != null && networkObject.HasStateAuthority)
            {
                networkObject.ReleaseStateAuthority();
            }
        }
    }
}
#else
using UnityEngine;

namespace ArtsOfEntanglement.Colocation
{
    public class NetworkObjectSpawner : MonoBehaviour
    {
        public void SpawnInitialObjects() { }
    }

    public class NetworkGrabAuthority : MonoBehaviour
    {
        private void Awake()
        {
            Debug.LogWarning("[NetworkGrabAuthority] Fusion not installed. Component is inactive.");
        }
    }
}
#endif
