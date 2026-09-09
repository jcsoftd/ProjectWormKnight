using UnityEngine;

namespace TeamProject01.Gameplay
{
    public sealed class ShowcaseTargetDummy : MonoBehaviour
    {
        [Min(1f)] public float MaxHp = 1000000f;
        public bool LockTransform = true;
        public bool ProjectAnchorToGround;
        [Min(0f)] public float GroundHeight = 0.72f;

        private EnemyHealth health;
        private Rigidbody body;
        private Vector3 anchorPosition;
        private Quaternion anchorRotation;

        private void Awake()
        {
            CacheReferences();
            CaptureAnchor();
        }

        private void OnEnable()
        {
            CacheReferences();
            CaptureAnchor();
            RefillHealth();
        }

        private void LateUpdate()
        {
            if (LockTransform)
            {
                if (ProjectAnchorToGround)
                {
                    anchorPosition = GroundService.ProjectToGround(anchorPosition, GroundHeight);
                }

                transform.SetPositionAndRotation(anchorPosition, anchorRotation);
            }

            if (body != null)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }

            if (health != null && health.CurrentHp < MaxHp * 0.25f)
            {
                RefillHealth();
            }
        }

        public void Configure(float maxHp, bool lockTransform)
        {
            Configure(maxHp, lockTransform, ProjectAnchorToGround, GroundHeight);
        }

        public void Configure(float maxHp, bool lockTransform, bool projectAnchorToGround, float groundHeight)
        {
            MaxHp = Mathf.Max(1f, maxHp);
            LockTransform = lockTransform;
            ProjectAnchorToGround = projectAnchorToGround;
            GroundHeight = Mathf.Max(0f, groundHeight);
            CacheReferences();
            CaptureAnchor();
            RefillHealth();
        }

        private void CacheReferences()
        {
            if (health == null)
            {
                health = GetComponent<EnemyHealth>();
            }

            if (body == null)
            {
                body = GetComponent<Rigidbody>();
            }
        }

        private void CaptureAnchor()
        {
            anchorPosition = transform.position;
            if (ProjectAnchorToGround)
            {
                anchorPosition = GroundService.ProjectToGround(anchorPosition, GroundHeight);
            }

            anchorRotation = transform.rotation;
        }

        private void RefillHealth()
        {
            if (health != null)
            {
                health.SetMaxHp(MaxHp, true);
            }
        }
    }
}
