using UnityEngine;

namespace ArtsOfEntanglement.Colocation
{
    public class SharedSpaceAligner : MonoBehaviour
    {
        public enum AlignMode
        {
            MoveSharedSpaceRoot,
            MoveRigRoot
        }

        [SerializeField] private Transform sharedSpaceRoot;
        [SerializeField] private Transform rigRoot;
        [SerializeField] private Transform experienceRoot;
        [SerializeField] private AlignMode alignMode = AlignMode.MoveSharedSpaceRoot;

        private bool hasAnchorPose;
        private Pose lastAnchorPose;

        public Transform SharedSpaceRoot => sharedSpaceRoot;

        private void Awake()
        {
            EnsureRoots();
        }

        public void EnsureRoots()
        {
            if (sharedSpaceRoot == null)
            {
                var existing = GameObject.Find("SharedSpaceRoot");
                if (existing == null)
                {
                    existing = new GameObject("SharedSpaceRoot");
                }

                sharedSpaceRoot = existing.transform;
            }

            if (experienceRoot == null)
            {
                var exp = GameObject.Find("ExperienceRoot");
                if (exp != null)
                {
                    experienceRoot = exp.transform;
                }
            }

            if (experienceRoot != null && experienceRoot.parent != sharedSpaceRoot)
            {
                experienceRoot.SetParent(sharedSpaceRoot, true);
            }
        }

        public void AlignToAnchorPose(Pose anchorPose)
        {
            EnsureRoots();

            hasAnchorPose = true;
            lastAnchorPose = anchorPose;

            var sharedPose = new Pose(sharedSpaceRoot.position, sharedSpaceRoot.rotation);
            var delta = ComputeDelta(sharedPose, anchorPose);

            switch (alignMode)
            {
                case AlignMode.MoveSharedSpaceRoot:
                    ApplyDelta(sharedSpaceRoot, delta);
                    break;
                case AlignMode.MoveRigRoot:
                    if (rigRoot != null)
                    {
                        ApplyDelta(rigRoot, delta);
                    }
                    else
                    {
                        ApplyDelta(sharedSpaceRoot, delta);
                    }
                    break;
            }
        }

        public void ReAlign()
        {
            if (hasAnchorPose)
            {
                AlignToAnchorPose(lastAnchorPose);
            }
        }

        private static Pose ComputeDelta(Pose fromPose, Pose toPose)
        {
            var deltaRot = toPose.rotation * Quaternion.Inverse(fromPose.rotation);
            var deltaPos = toPose.position - (deltaRot * fromPose.position);
            return new Pose(deltaPos, deltaRot);
        }

        private static void ApplyDelta(Transform target, Pose delta)
        {
            var newRot = delta.rotation * target.rotation;
            var newPos = (delta.rotation * target.position) + delta.position;
            target.SetPositionAndRotation(newPos, newRot);
        }
    }
}
