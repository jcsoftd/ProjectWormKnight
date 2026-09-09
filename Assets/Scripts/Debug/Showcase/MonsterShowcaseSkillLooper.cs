using System;
using System.Collections;
using System.Reflection;
using UnityEngine;

namespace TeamProject01.Gameplay
{
    public sealed class MonsterShowcaseSkillLooper : MonoBehaviour
    {
        private const BindingFlags SerializedFieldFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private const float ShowcaseVfxDefaultLifeTime = 2.0f;
        private static readonly Color MutedFallbackBlue = new Color(0.38f, 0.62f, 0.72f, 1.0f);
        private static readonly Color MutedFallbackCyan = new Color(0.42f, 0.78f, 0.72f, 1.0f);
        private static readonly Color WarmFallbackImpact = new Color(0.95f, 0.68f, 0.28f, 1.0f);

        private enum ShowcaseSkillKind
        {
            Idle,
            Melee,
            Ranged,
            GroundArea,
            Jump,
            Shield,
            Buff,
            Portal,
            SegmentCut,
            Obstacle,
            Charge,
            Hatchling,
            Boss
        }

        [Header("Timing")]
        [Min(0.2f)] public float SkillInterval = 3.0f;
        [Min(0.1f)] public float FirstDelay = 0.5f;

        [Header("Target")]
        [Min(0.5f)] public float ForwardOffset = 5.0f;
        [Min(0.0f)] public float GroundHeight = 0.04f;

        [Header("Jump")]
        [Min(0.2f)] public float JumpDistance = 5.0f;
        [Min(0.1f)] public float JumpHeight = 2.0f;
        [Min(0.1f)] public float JumpDuration = 0.55f;

        [Header("Visuals")]
        public Transform EffectRoot;
        public bool EnableAudio = true;

        private ShowcaseSkillKind skillKind;
        private Vector3 anchorPosition;
        private Quaternion anchorRotation;
        private Transform cachedFirePoint;
        private Animator[] cachedAnimators;
        private EnemyHatchlingConsumePresentationBridge hatchlingPresentation;
        private float timer;
        private bool actionRunning;

        public void Configure(
            Transform effectRoot,
            float forwardOffset,
            float skillInterval,
            float jumpDistance,
            float jumpHeight,
            float jumpDuration,
            float groundHeight,
            bool enableAudio)
        {
            EffectRoot = effectRoot;
            ForwardOffset = Mathf.Max(0.5f, forwardOffset);
            SkillInterval = Mathf.Max(0.2f, skillInterval);
            JumpDistance = Mathf.Max(0.2f, jumpDistance);
            JumpHeight = Mathf.Max(0.1f, jumpHeight);
            JumpDuration = Mathf.Max(0.1f, jumpDuration);
            GroundHeight = Mathf.Max(0.0f, groundHeight);
            EnableAudio = enableAudio;
            anchorPosition = transform.position;
            anchorRotation = transform.rotation;
            cachedFirePoint = FindFirstNamedChild(transform, "FirePoint", "CastPoint", "Muzzle");
            RefreshPresentationReferencesIfNeeded();
            skillKind = ResolveSkillKind();
            timer = Mathf.Max(0.0f, FirstDelay);
        }

        private void OnEnable()
        {
            if (Mathf.Approximately(anchorPosition.sqrMagnitude, 0.0f))
            {
                anchorPosition = transform.position;
                anchorRotation = transform.rotation;
            }
        }

        private void Update()
        {
            if (actionRunning)
            {
                return;
            }

            timer -= Time.deltaTime;
            if (timer > 0.0f)
            {
                return;
            }

            timer = SkillInterval;
            PlayShowcaseSkill();
        }

        private ShowcaseSkillKind ResolveSkillKind()
        {
            EnemyController controller = GetComponent<EnemyController>();
            if (GetComponent<BossController>() != null || (controller != null && controller.Grade == EnemyGrade.Boss) || name.Contains("Boss"))
            {
                return ShowcaseSkillKind.Boss;
            }

            if (GetComponent<EnemyJump>() != null)
            {
                return ShowcaseSkillKind.Jump;
            }

            if (GetComponent<EnemySlowZoneThrower>() != null)
            {
                return ShowcaseSkillKind.GroundArea;
            }

            if (GetComponent<EnemyObstacleSummoner>() != null)
            {
                return ShowcaseSkillKind.Obstacle;
            }

            if (GetComponent<EnemySegmentCutCaster>() != null)
            {
                return ShowcaseSkillKind.SegmentCut;
            }

            if (GetComponent<EnemyPortalTotemCaster>() != null)
            {
                return ShowcaseSkillKind.Portal;
            }

            if (GetComponent<EnemyAreaShield>() != null)
            {
                return ShowcaseSkillKind.Shield;
            }

            if (GetComponent<EnemyBuffCaster>() != null)
            {
                return ShowcaseSkillKind.Buff;
            }

            if (GetComponent<EnemyHatchlingGrowth>() != null)
            {
                return ShowcaseSkillKind.Hatchling;
            }

            if (GetComponent<EnemySuicideCharger>() != null)
            {
                return ShowcaseSkillKind.Charge;
            }

            if (GetComponent<EnemyRangedAttack>() != null)
            {
                return ShowcaseSkillKind.Ranged;
            }

            if (GetComponent<EnemyMeleeAttack>() != null)
            {
                return ShowcaseSkillKind.Melee;
            }

            return ShowcaseSkillKind.Idle;
        }

        private void PlayShowcaseSkill()
        {
            switch (skillKind)
            {
                case ShowcaseSkillKind.Melee:
                    StartCoroutine(MeleeRoutine());
                    break;
                case ShowcaseSkillKind.Ranged:
                    StartCoroutine(RangedRoutine());
                    break;
                case ShowcaseSkillKind.GroundArea:
                    StartCoroutine(GroundAreaRoutine());
                    break;
                case ShowcaseSkillKind.Jump:
                    StartCoroutine(JumpRoutine());
                    break;
                case ShowcaseSkillKind.Shield:
                    StartCoroutine(ShieldRoutine());
                    break;
                case ShowcaseSkillKind.Buff:
                    StartCoroutine(BuffRoutine());
                    break;
                case ShowcaseSkillKind.Portal:
                    StartCoroutine(PortalRoutine());
                    break;
                case ShowcaseSkillKind.SegmentCut:
                    StartCoroutine(SegmentCutRoutine());
                    break;
                case ShowcaseSkillKind.Obstacle:
                    StartCoroutine(ObstacleRoutine());
                    break;
                case ShowcaseSkillKind.Charge:
                    StartCoroutine(ChargeRoutine());
                    break;
                case ShowcaseSkillKind.Hatchling:
                    StartCoroutine(HatchlingRoutine());
                    break;
                case ShowcaseSkillKind.Boss:
                    StartCoroutine(BossRoutine());
                    break;
                default:
                    SpawnPulse(transform.position + Vector3.up * 0.05f, 1.8f, MutedFallbackBlue, 0.6f);
                    PlayCue(GameplaySfxCue.Activation, transform.position);
                    break;
            }
        }

        private IEnumerator MeleeRoutine()
        {
            actionRunning = true;
            Vector3 hitPosition = GetForwardGroundPosition(1.8f);
            FaceGroundPosition(hitPosition);
            SetAnimatorBool(true, "IsAttacking");
            PlayAnimatorTrigger("Attack");
            PlayCue(GameplaySfxCue.MonsterMeleeImpact, hitPosition);
            yield return MoveTo(anchorPosition + GetFlatForward() * 0.8f, 0.16f);
            SpawnPulse(hitPosition, 1.4f, new Color(1.0f, 0.48f, 0.22f, 1.0f), 0.35f);
            yield return new WaitForSeconds(0.15f);
            yield return MoveTo(anchorPosition, 0.22f);
            SetAnimatorBool(false, "IsAttacking");
            RestoreAnchorPose();
            actionRunning = false;
        }

        private IEnumerator RangedRoutine()
        {
            actionRunning = true;
            Vector3 targetPosition = GetForwardGroundPosition(ForwardOffset);
            Vector3 startPosition = GetCastPosition();
            FaceGroundPosition(targetPosition);
            PlayAnimatorTrigger("Attack");
            PlayCue(GameplaySfxCue.MonsterRangedFire, startPosition);

            GameObject projectile = CreateRangedProjectileObject(startPosition, targetPosition);
            if (projectile != null)
            {
                yield return MoveObjectArc(projectile, startPosition, targetPosition, 1.7f, 0.55f, true);
            }
            else
            {
                yield return ProjectileArc(startPosition, targetPosition, 1.7f, 0.55f, 0.22f, new Color(0.72f, 0.18f, 1.0f, 1.0f));
            }

            SpawnRangedImpactVfx(targetPosition);
            SpawnPulse(targetPosition, 1.5f, new Color(0.72f, 0.18f, 1.0f, 1.0f), 0.45f);
            actionRunning = false;
        }

        private IEnumerator GroundAreaRoutine()
        {
            actionRunning = true;
            Vector3 targetPosition = GetForwardGroundPosition(ForwardOffset);
            Vector3 startPosition = GetCastPosition();
            FaceGroundPosition(targetPosition);
            PlayAnimatorTrigger("Throw", "Attack");
            PlayCue(GameplaySfxCue.MonsterSlowZoneCast, startPosition);

            if (TrySpawnSlowZoneProjectile(startPosition, targetPosition))
            {
                yield return new WaitForSeconds(1.05f);
            }
            else
            {
                SpawnDisc(targetPosition, 2.6f, MutedFallbackBlue, 0.55f, 0.02f);
                yield return new WaitForSeconds(0.2f);
                yield return ProjectileArc(startPosition, targetPosition, 2.1f, 0.7f, 0.24f, MutedFallbackBlue);
                SpawnDisc(targetPosition, 3.1f, MutedFallbackBlue, 1.25f, 0.04f);
            }

            PlayCue(GameplaySfxCue.MonsterSlowZoneImpact, targetPosition);
            actionRunning = false;
        }

        private IEnumerator JumpRoutine()
        {
            actionRunning = true;
            Vector3 from = anchorPosition;
            Vector3 to = GroundService.ProjectToGround(anchorPosition + GetFlatForward() * JumpDistance, GroundHeight);
            FaceGroundPosition(to);
            SetAnimatorBool(true, "IsJumping");
            PlayAnimatorTrigger("Jump");
            yield return ArcMove(from, to, JumpHeight, JumpDuration);
            SpawnJumpLandingVfx(to);
            SpawnPulse(to, 3.0f, new Color(1.0f, 0.86f, 0.22f, 1.0f), 0.55f);
            PlayCue(GameplaySfxCue.MonsterJumpLanding, to);
            RequestJumpLandingShockwave(to);
            yield return new WaitForSeconds(GetJumpLandingRecoveryDuration());
            SetAnimatorBool(false, "IsJumping");
            yield return MoveTo(anchorPosition, 0.45f);
            RestoreAnchorPose();
            actionRunning = false;
        }

        private IEnumerator ShieldRoutine()
        {
            actionRunning = true;
            GameObject shieldVisualRoot = ShowAreaShieldVisual(true);
            PlayAnimatorTrigger("Shield", "Cast");
            if (shieldVisualRoot == null)
            {
                SpawnDisc(transform.position, 4.0f, MutedFallbackCyan, 1.0f, 0.03f);
                SpawnPulse(transform.position + Vector3.up * 0.6f, 3.2f, MutedFallbackCyan, 0.8f);
            }

            PlayCue(GameplaySfxCue.MonsterShieldLoop, transform.position);
            yield return new WaitForSeconds(0.8f);
            if (shieldVisualRoot != null)
            {
                shieldVisualRoot.SetActive(false);
            }

            actionRunning = false;
        }

        private IEnumerator BuffRoutine()
        {
            actionRunning = true;
            PlayAnimatorTrigger("Cast", "Buff", "Attack");
            ApplyBuffVisual(true);
            PlayCue(GameplaySfxCue.Activation, transform.position);
            SpawnPulse(transform.position + Vector3.up * 0.2f, 3.5f, new Color(0.32f, 1.0f, 0.32f, 1.0f), 0.7f);
            for (int i = 0; i < 4; i++)
            {
                float angle = 90.0f * i;
                Vector3 offset = Quaternion.Euler(0.0f, angle, 0.0f) * Vector3.forward * 2.6f;
                StartCoroutine(OrbPulse(transform.position + offset + Vector3.up * 1.0f, new Color(0.32f, 1.0f, 0.32f, 1.0f), 0.7f));
            }

            yield return new WaitForSeconds(0.75f);
            ApplyBuffVisual(false);
            actionRunning = false;
        }

        private IEnumerator PortalRoutine()
        {
            actionRunning = true;
            Vector3 entry = GetForwardGroundPosition(2.0f);
            Vector3 exit = GetForwardGroundPosition(ForwardOffset + 3.0f);
            FaceGroundPosition(exit);
            SetAnimatorBool(true, "IsPortalChanneling", "IsChanneling");
            PlayAnimatorTrigger("Cast", "Portal");
            bool spawnedPortalVfx = SpawnPortalTotems(entry, exit);
            if (!spawnedPortalVfx)
            {
                SpawnDisc(entry, 2.0f, MutedFallbackBlue, 1.1f, 0.05f);
            }

            yield return new WaitForSeconds(0.25f);
            if (!spawnedPortalVfx)
            {
                SpawnDisc(exit, 2.0f, new Color(1.0f, 0.38f, 0.85f, 1.0f), 1.1f, 0.05f);
            }

            PlayCue(GameplaySfxCue.MonsterPortalTeleport, exit);
            if (spawnedPortalVfx)
            {
                yield return new WaitForSeconds(0.45f);
            }
            else
            {
                yield return ProjectileLine(entry + Vector3.up * 0.4f, exit + Vector3.up * 0.4f, 0.45f, MutedFallbackBlue);
            }

            SetAnimatorBool(false, "IsPortalChanneling", "IsChanneling");
            actionRunning = false;
        }

        private IEnumerator SegmentCutRoutine()
        {
            actionRunning = true;
            Vector3 targetPosition = GetForwardGroundPosition(ForwardOffset);
            Vector3 startPosition = GetCastPosition();
            FaceGroundPosition(targetPosition);
            PlayAnimatorTrigger("Cast");
            SegmentCutMagicEffect warningEffect = SpawnSegmentCutWarning(targetPosition);
            if (warningEffect == null)
            {
                SpawnDisc(targetPosition, 1.7f, new Color(1.0f, 0.08f, 0.18f, 1.0f), 0.8f, 0.03f);
            }

            PlayCue(GameplaySfxCue.MonsterSegmentCutCast, startPosition);
            yield return new WaitForSeconds(0.45f);
            PlayAnimatorTrigger("Fire", "Attack");
            PlayCue(GameplaySfxCue.MonsterSegmentCutLaunch, startPosition);
            GameObject projectile = CreateSegmentCutProjectileObject(startPosition, targetPosition);
            if (projectile != null)
            {
                yield return MoveObjectLine(projectile, startPosition, targetPosition + Vector3.up * 0.45f, 0.45f, true);
            }
            else
            {
                yield return ProjectileLine(startPosition, targetPosition + Vector3.up * 0.45f, 0.45f, new Color(1.0f, 0.08f, 0.18f, 1.0f));
            }

            SpawnSegmentCutImpact(targetPosition);
            SpawnPulse(targetPosition, 1.5f, new Color(1.0f, 0.08f, 0.18f, 1.0f), 0.45f);
            actionRunning = false;
        }

        private IEnumerator ObstacleRoutine()
        {
            actionRunning = true;
            Vector3 targetPosition = GetForwardGroundPosition(ForwardOffset);
            FaceGroundPosition(targetPosition);
            SetAnimatorBool(true, "IsSummoning");
            PlayAnimatorTrigger("Summon", "Attack", "Cast");

            if (TryGetObstacleShowcaseValues(out EnemyObstacle obstaclePrefab, out GameObject obstacleTelegraphPrefab, out float obstacleRadius, out float obstacleLifeTime, out float telegraphHeight, out float obstacleHeight))
            {
                GameObject telegraph = SpawnTelegraphPrefab(obstacleTelegraphPrefab, targetPosition, obstacleRadius, telegraphHeight, 0.07f, 0.6f);
                yield return new WaitForSeconds(0.35f);
                if (telegraph != null)
                {
                    SetPrefabAlpha(telegraph, 1.0f);
                    Destroy(telegraph);
                }

                SpawnObstaclePrefab(obstaclePrefab, targetPosition, obstacleRadius, obstacleLifeTime, obstacleHeight);
            }
            else
            {
                SpawnDisc(targetPosition, 2.2f, new Color(1.0f, 0.72f, 0.16f, 1.0f), 0.45f, 0.03f);
                yield return new WaitForSeconds(0.35f);
                SpawnBlock(targetPosition + Vector3.up * 0.8f, new Vector3(1.4f, 1.6f, 1.4f), new Color(0.46f, 0.34f, 0.24f, 1.0f), 1.2f);
            }

            PlayCue(GameplaySfxCue.MonsterObstacleSummon, targetPosition);
            yield return new WaitForSeconds(0.45f);
            SetAnimatorBool(false, "IsSummoning");
            actionRunning = false;
        }

        private IEnumerator ChargeRoutine()
        {
            actionRunning = true;
            Vector3 from = anchorPosition;
            Vector3 to = GroundService.ProjectToGround(anchorPosition + GetFlatForward() * Mathf.Max(2.5f, ForwardOffset), GroundHeight);
            FaceGroundPosition(to);
            SetAnimatorBool(true, "IsCharging");
            PlayAnimatorTrigger("Charge", "Attack");
            GameObject telegraph = SpawnSuicideTelegraph(to);
            yield return MoveTo(to, 0.32f);
            if (telegraph != null)
            {
                SetPrefabAlpha(telegraph, 1.0f);
                Destroy(telegraph);
            }

            SpawnPulse(to, 3.2f, new Color(1.0f, 0.22f, 0.1f, 1.0f), 0.55f);
            PlayCue(GameplaySfxCue.MonsterSuicideExplosion, to);
            yield return new WaitForSeconds(0.25f);
            yield return MoveTo(from, 0.5f);
            SetAnimatorBool(false, "IsCharging");
            RestoreAnchorPose();
            actionRunning = false;
        }

        private IEnumerator HatchlingRoutine()
        {
            actionRunning = true;
            Vector3 foodStart = GetForwardGroundPosition(2.4f) + Vector3.up * 0.55f;
            GameObject food = SpawnSphere(foodStart, 0.45f, new Color(0.85f, 0.45f, 0.2f, 1.0f), 1.5f);
            BeginHatchlingConsume(foodStart);
            PlayCue(GameplaySfxCue.MonsterHatchlingConsume, transform.position);
            float duration = 0.65f;
            float elapsed = 0.0f;
            Vector3 foodEnd = transform.position + Vector3.up * 0.8f;
            while (elapsed < duration && food != null)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                food.transform.position = Vector3.Lerp(foodStart, foodEnd, t);
                food.transform.localScale = Vector3.one * Mathf.Lerp(0.45f, 0.08f, t);
                yield return null;
            }

            SpawnPulse(transform.position + Vector3.up * 0.2f, 2.6f, new Color(0.85f, 0.45f, 0.2f, 1.0f), 0.6f);
            FinishHatchlingConsume(foodStart);
            yield return ScalePulse(1.18f, 0.35f);
            SetAnimatorBool(false, "IsConsuming");
            actionRunning = false;
        }

        private IEnumerator BossRoutine()
        {
            actionRunning = true;
            Vector3 targetPosition = GetForwardGroundPosition(ForwardOffset);
            FaceGroundPosition(targetPosition);
            PlayAnimatorTrigger("Diamond", "EnhancedDiamond", "Attack", "Cast");
            PlayCue(GameplaySfxCue.BossDiamondCharge, transform.position);
            yield return new WaitForSeconds(0.35f);
            for (int i = -1; i <= 1; i++)
            {
                Vector3 offset = transform.right * (i * 1.2f);
                Vector3 projectileStart = GetCastPosition() + offset;
                Vector3 projectileEnd = targetPosition + offset;
                GameObject projectile = CreateBossDiamondProjectileObject(projectileStart, projectileEnd);
                if (projectile != null)
                {
                    StartCoroutine(MoveObjectArc(projectile, projectileStart, projectileEnd, 2.4f, 0.65f, true));
                }
                else
                {
                    StartCoroutine(ProjectileArc(projectileStart, projectileEnd, 2.4f, 0.65f, 0.28f, WarmFallbackImpact));
                }
            }

            PlayCue(GameplaySfxCue.BossDiamondLaunch, transform.position);
            yield return new WaitForSeconds(0.7f);
            SpawnPulse(targetPosition, 3.4f, WarmFallbackImpact, 0.65f);
            PlayCue(GameplaySfxCue.BossDiamondBurstLarge, targetPosition);
            actionRunning = false;
        }

        private GameObject CreateRangedProjectileObject(Vector3 startPosition, Vector3 targetPosition)
        {
            EnemyRangedAttack rangedAttack = GetComponent<EnemyRangedAttack>();
            EnemyProjectile projectilePrefab = ReadSerializedField<EnemyProjectile>(rangedAttack, "projectilePrefab");
            if (projectilePrefab == null)
            {
                return null;
            }

            Quaternion rotation = BuildFlatLookRotation(startPosition, targetPosition, transform.rotation);
            GameObject projectile = SpawnPrefabObject(projectilePrefab.gameObject, startPosition, rotation, 1.0f, ShowcaseVfxDefaultLifeTime, true);
            DisableBehaviour<EnemyProjectile>(projectile);
            return projectile;
        }

        private void SpawnRangedImpactVfx(Vector3 targetPosition)
        {
            EnemyRangedAttack rangedAttack = GetComponent<EnemyRangedAttack>();
            GameObject impactPrefab = ReadSerializedField<GameObject>(rangedAttack, "impactPrefab");
            if (impactPrefab == null)
            {
                return;
            }

            SpawnPrefabObject(impactPrefab, targetPosition, transform.rotation, 1.0f, ShowcaseVfxDefaultLifeTime, false);
        }

        private bool TrySpawnSlowZoneProjectile(Vector3 startPosition, Vector3 targetPosition)
        {
            EnemySlowZoneThrower slowZoneThrower = GetComponent<EnemySlowZoneThrower>();
            EnemySlowZoneProjectile projectilePrefab = ReadSerializedField<EnemySlowZoneProjectile>(slowZoneThrower, "projectilePrefab");
            EnemySlowZone slowZonePrefab = ReadSerializedField<EnemySlowZone>(slowZoneThrower, "slowZonePrefab");
            GameObject telegraphPrefab = ReadSerializedField<GameObject>(slowZoneThrower, "areaTelegraphPrefab");
            if (projectilePrefab == null || slowZonePrefab == null || telegraphPrefab == null)
            {
                return false;
            }

            float slowZoneRadius = ReadSerializedFloat(slowZoneThrower, "slowZoneRadius", 3.0f);
            float slowZoneLifeTime = ReadSerializedFloat(slowZoneThrower, "slowZoneLifeTime", 4.0f);
            float speedMultiplier = ReadSerializedFloat(slowZoneThrower, "speedMultiplier", 0.5f);
            float telegraphGroundHeight = ReadSerializedFloat(slowZoneThrower, "telegraphGroundHeight", 0.03f);
            float slowZoneGroundHeight = ReadSerializedFloat(slowZoneThrower, "slowZoneGroundHeight", 0.04f);

            Quaternion rotation = BuildFlatLookRotation(startPosition, targetPosition, transform.rotation);
            Transform parent = GetEffectParent();
            EnemySlowZoneProjectile projectile = Instantiate(projectilePrefab, startPosition, rotation, parent);
            RestartVfx(projectile.gameObject);
            projectile.Configure(
                targetPosition,
                slowZonePrefab,
                telegraphPrefab,
                parent,
                parent,
                slowZoneRadius,
                slowZoneLifeTime,
                speedMultiplier,
                telegraphGroundHeight,
                slowZoneGroundHeight);

            return true;
        }

        private void SpawnJumpLandingVfx(Vector3 landingPosition)
        {
            EnemyJump enemyJump = GetComponent<EnemyJump>();
            GameObject landingVfxPrefab = ReadSerializedField<GameObject>(enemyJump, "landingCrackVfxPrefab");
            if (landingVfxPrefab == null)
            {
                return;
            }

            float groundHeight = ReadSerializedFloat(enemyJump, "landingCrackGroundHeight", 0.03f);
            float scale = ReadSerializedFloat(enemyJump, "landingCrackScale", 0.65f);
            float lifeTime = ReadSerializedFloat(enemyJump, "landingCrackLifeTime", 2.0f);
            Vector3 position = GroundService.ProjectToGround(landingPosition, groundHeight);
            SpawnPrefabObject(landingVfxPrefab, position, Quaternion.identity, scale, lifeTime, false);
        }

        private float GetJumpLandingRecoveryDuration()
        {
            EnemyJump enemyJump = GetComponent<EnemyJump>();
            return Mathf.Max(0.05f, ReadSerializedFloat(enemyJump, "landingRecoveryDuration", 0.35f));
        }

        private void RequestJumpLandingShockwave(Vector3 landingPosition)
        {
            EnemyJump enemyJump = GetComponent<EnemyJump>();
            float radius = ReadSerializedFloat(enemyJump, "shockwaveRadius", 7.0f);
            float pushDistance = ReadSerializedFloat(enemyJump, "shockwavePushDistance", 3.0f);
            float recoveryDuration = ReadSerializedFloat(enemyJump, "shockwaveRecoveryDuration", 1.5f);
            MonsterInteractionApi.RequestSegmentShockwave(landingPosition, radius, pushDistance, recoveryDuration);
        }

        private GameObject ShowAreaShieldVisual(bool active)
        {
            EnemyAreaShield areaShield = GetComponent<EnemyAreaShield>();
            GameObject shieldVisualRoot = ReadSerializedField<GameObject>(areaShield, "shieldVisualRoot");
            if (shieldVisualRoot == null)
            {
                return null;
            }

            shieldVisualRoot.SetActive(active);
            if (active)
            {
                RestartVfx(shieldVisualRoot);
            }

            return shieldVisualRoot;
        }

        private void ApplyBuffVisual(bool active)
        {
            EnemyBuffVisual buffVisual = GetComponentInChildren<EnemyBuffVisual>(true);
            if (buffVisual == null)
            {
                return;
            }

            if (!active)
            {
                buffVisual.ClearVisual();
                return;
            }

            EnemyBuffType buffType = UnityEngine.Random.Range(0, 3) switch
            {
                0 => EnemyBuffType.AttackPower,
                1 => EnemyBuffType.MoveSpeed,
                _ => EnemyBuffType.AttackSpeed
            };
            buffVisual.ApplyVisual(buffType);
        }

        private bool SpawnPortalTotems(Vector3 entryPosition, Vector3 exitPosition)
        {
            EnemyPortalTotemCaster portalCaster = GetComponent<EnemyPortalTotemCaster>();
            EnemyPortalTotem entryPrefab = ReadSerializedField<EnemyPortalTotem>(portalCaster, "entryTotemPrefab");
            EnemyPortalTotem exitPrefab = ReadSerializedField<EnemyPortalTotem>(portalCaster, "exitTotemPrefab");
            if (entryPrefab == null || exitPrefab == null)
            {
                return false;
            }

            float totemGroundHeight = ReadSerializedFloat(portalCaster, "totemGroundHeight", 0.03f);
            float attractRadius = ReadSerializedFloat(portalCaster, "attractRadius", 12.0f);
            float entryRadius = ReadSerializedFloat(portalCaster, "entryRadius", 6.0f);
            Transform parent = GetEffectParent();
            EnemyPortalTotem entry = Instantiate(entryPrefab, GroundService.ProjectToGround(entryPosition, totemGroundHeight), Quaternion.identity, parent);
            EnemyPortalTotem exit = Instantiate(exitPrefab, GroundService.ProjectToGround(exitPosition, totemGroundHeight), Quaternion.identity, parent);
            entry.Configure(EnemyPortalTotemType.Entry, attractRadius, entryRadius);
            exit.Configure(EnemyPortalTotemType.Exit, 0.1f, 0.1f);
            RestartVfx(entry.gameObject);
            RestartVfx(exit.gameObject);
            Destroy(entry.gameObject, 1.2f);
            Destroy(exit.gameObject, 1.2f);
            return true;
        }

        private SegmentCutMagicEffect SpawnSegmentCutWarning(Vector3 targetPosition)
        {
            EnemySegmentCutCaster segmentCutCaster = GetComponent<EnemySegmentCutCaster>();
            SegmentCutMagicEffect magicEffectPrefab = ReadSerializedField<SegmentCutMagicEffect>(segmentCutCaster, "magicEffectPrefab");
            if (magicEffectPrefab == null)
            {
                return null;
            }

            SegmentCutMagicEffect magicEffect = Instantiate(magicEffectPrefab, targetPosition + Vector3.up * 0.05f, Quaternion.identity, GetEffectParent());
            magicEffect.ShowWarning();
            RestartVfx(magicEffect.gameObject);
            Destroy(magicEffect.gameObject, 1.4f);
            return magicEffect;
        }

        private GameObject CreateSegmentCutProjectileObject(Vector3 startPosition, Vector3 targetPosition)
        {
            EnemySegmentCutCaster segmentCutCaster = GetComponent<EnemySegmentCutCaster>();
            SegmentCutProjectile projectilePrefab = ReadSerializedField<SegmentCutProjectile>(segmentCutCaster, "projectilePrefab");
            if (projectilePrefab == null)
            {
                return null;
            }

            Quaternion rotation = BuildFlatLookRotation(startPosition, targetPosition, transform.rotation);
            GameObject projectile = SpawnPrefabObject(projectilePrefab.gameObject, startPosition, rotation, 1.0f, ShowcaseVfxDefaultLifeTime, true);
            DisableBehaviour<SegmentCutProjectile>(projectile);
            return projectile;
        }

        private void SpawnSegmentCutImpact(Vector3 targetPosition)
        {
            EnemySegmentCutCaster segmentCutCaster = GetComponent<EnemySegmentCutCaster>();
            SegmentCutProjectile projectilePrefab = ReadSerializedField<SegmentCutProjectile>(segmentCutCaster, "projectilePrefab");
            GameObject impactPrefab = ReadSerializedField<GameObject>(projectilePrefab, "impactEffectPrefab");
            if (impactPrefab == null)
            {
                return;
            }

            SpawnPrefabObject(impactPrefab, targetPosition + Vector3.up * 0.2f, Quaternion.identity, 1.0f, ShowcaseVfxDefaultLifeTime, false);
        }

        private bool TryGetObstacleShowcaseValues(
            out EnemyObstacle obstaclePrefab,
            out GameObject telegraphPrefab,
            out float obstacleRadius,
            out float obstacleLifeTime,
            out float telegraphGroundHeight,
            out float obstacleGroundHeight)
        {
            EnemyObstacleSummoner obstacleSummoner = GetComponent<EnemyObstacleSummoner>();
            obstaclePrefab = ReadSerializedField<EnemyObstacle>(obstacleSummoner, "obstaclePrefab");
            telegraphPrefab = ReadSerializedField<GameObject>(obstacleSummoner, "telegraphPrefab");
            obstacleRadius = ReadSerializedFloat(obstacleSummoner, "obstacleRadius", 1.2f);
            obstacleLifeTime = ReadSerializedFloat(obstacleSummoner, "obstacleLifeTime", 8.0f);
            telegraphGroundHeight = ReadSerializedFloat(obstacleSummoner, "telegraphGroundHeight", 0.03f);
            obstacleGroundHeight = ReadSerializedFloat(obstacleSummoner, "obstacleGroundHeight", 1.0f);
            return obstaclePrefab != null || telegraphPrefab != null;
        }

        private GameObject SpawnTelegraphPrefab(GameObject prefab, Vector3 position, float radius, float groundHeight, float alpha, float lifeTime)
        {
            if (prefab == null)
            {
                return null;
            }

            Vector3 spawnPosition = GroundService.ProjectToGround(position, groundHeight);
            GameObject telegraph = SpawnPrefabObject(prefab, spawnPosition, Quaternion.identity, 1.0f, lifeTime, false);
            float diameter = Mathf.Max(0.1f, radius) * 2.0f;
            telegraph.transform.localScale = new Vector3(diameter, telegraph.transform.localScale.y, diameter);
            SetPrefabAlpha(telegraph, alpha);
            return telegraph;
        }

        private void SpawnObstaclePrefab(EnemyObstacle obstaclePrefab, Vector3 position, float radius, float lifeTime, float groundHeight)
        {
            if (obstaclePrefab == null)
            {
                return;
            }

            Vector3 spawnPosition = GroundService.ProjectToGround(position, groundHeight);
            EnemyObstacle obstacle = Instantiate(obstaclePrefab, spawnPosition, Quaternion.identity, GetEffectParent());
            obstacle.Configure(radius, lifeTime);
            RestartVfx(obstacle.gameObject);
        }

        private GameObject SpawnSuicideTelegraph(Vector3 position)
        {
            EnemySuicideCharger suicideCharger = GetComponent<EnemySuicideCharger>();
            GameObject telegraphPrefab = ReadSerializedField<GameObject>(suicideCharger, "areaTelegraphPrefab");
            if (telegraphPrefab == null)
            {
                return null;
            }

            float explosionRadius = ReadSerializedFloat(suicideCharger, "explosionRadius", 3.0f);
            float telegraphGroundHeight = ReadSerializedFloat(suicideCharger, "telegraphGroundHeight", 0.03f);
            return SpawnTelegraphPrefab(telegraphPrefab, position, explosionRadius, telegraphGroundHeight, 0.5f, 1.0f);
        }

        private GameObject CreateBossDiamondProjectileObject(Vector3 startPosition, Vector3 targetPosition)
        {
            BossDiamondSiegeAttack diamondAttack = GetComponent<BossDiamondSiegeAttack>();
            GameObject projectilePrefab = ReadPrefabGameObject(diamondAttack, "projectilePrefab");
            if (projectilePrefab == null)
            {
                return null;
            }

            Quaternion rotation = BuildFlatLookRotation(startPosition, targetPosition, transform.rotation);
            return SpawnPrefabObject(projectilePrefab, startPosition, rotation, 1.0f, ShowcaseVfxDefaultLifeTime, true);
        }

        private void BeginHatchlingConsume(Vector3 targetPosition)
        {
            RefreshPresentationReferencesIfNeeded();
            SetAnimatorBool(true, "IsConsuming");

            if (hatchlingPresentation != null && hatchlingPresentation.isActiveAndEnabled)
            {
                hatchlingPresentation.SetConsuming(true);
                hatchlingPresentation.PlayBite();
                hatchlingPresentation.SpawnConsumeVfx(transform.position, targetPosition);
                return;
            }

            PlayAnimatorTrigger("Bite", "Attack");
        }

        private void FinishHatchlingConsume(Vector3 targetPosition)
        {
            RefreshPresentationReferencesIfNeeded();
            if (hatchlingPresentation != null && hatchlingPresentation.isActiveAndEnabled)
            {
                hatchlingPresentation.SpawnGrowthVfx(transform);
                hatchlingPresentation.SetConsuming(false);
                return;
            }

            PlayAnimatorTrigger("Grow", "Attack");
        }

        private void PlayAnimatorTrigger(params string[] parameterNames)
        {
            RefreshPresentationReferencesIfNeeded();

            if (cachedAnimators == null || cachedAnimators.Length == 0 || parameterNames == null)
            {
                return;
            }

            for (int i = 0; i < cachedAnimators.Length; i++)
            {
                Animator animator = cachedAnimators[i];
                if (!CanUseAnimator(animator))
                {
                    continue;
                }

                for (int j = 0; j < parameterNames.Length; j++)
                {
                    string parameterName = parameterNames[j];
                    if (!TryGetAnimatorParameterHash(animator, parameterName, AnimatorControllerParameterType.Trigger, out int parameterHash))
                    {
                        continue;
                    }

                    animator.ResetTrigger(parameterHash);
                    animator.SetTrigger(parameterHash);
                    animator.Update(0.0f);
                    break;
                }
            }
        }

        private void SetAnimatorBool(bool value, params string[] parameterNames)
        {
            RefreshPresentationReferencesIfNeeded();

            if (cachedAnimators == null || cachedAnimators.Length == 0 || parameterNames == null)
            {
                return;
            }

            for (int i = 0; i < cachedAnimators.Length; i++)
            {
                Animator animator = cachedAnimators[i];
                if (!CanUseAnimator(animator))
                {
                    continue;
                }

                for (int j = 0; j < parameterNames.Length; j++)
                {
                    string parameterName = parameterNames[j];
                    if (!TryGetAnimatorParameterHash(animator, parameterName, AnimatorControllerParameterType.Bool, out int parameterHash))
                    {
                        continue;
                    }

                    animator.SetBool(parameterHash, value);
                    animator.Update(0.0f);
                }
            }
        }

        private void RefreshPresentationReferencesIfNeeded()
        {
            if (cachedAnimators == null || cachedAnimators.Length == 0)
            {
                cachedAnimators = GetComponentsInChildren<Animator>(true);
            }

            if (hatchlingPresentation == null)
            {
                hatchlingPresentation = GetComponentInChildren<EnemyHatchlingConsumePresentationBridge>(true);
            }
        }

        private IEnumerator MoveObjectArc(GameObject targetObject, Vector3 start, Vector3 end, float height, float duration, bool destroyOnComplete)
        {
            if (targetObject == null)
            {
                yield break;
            }

            float elapsed = 0.0f;
            while (elapsed < duration && targetObject != null)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, duration));
                Vector3 position = Vector3.Lerp(start, end, t);
                position.y += Mathf.Sin(t * Mathf.PI) * height;
                Vector3 direction = position - targetObject.transform.position;
                targetObject.transform.position = position;
                if (direction.sqrMagnitude > 0.0001f)
                {
                    targetObject.transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
                }

                yield return null;
            }

            if (targetObject != null)
            {
                targetObject.transform.position = end;
                if (destroyOnComplete)
                {
                    Destroy(targetObject);
                }
            }
        }

        private IEnumerator MoveObjectLine(GameObject targetObject, Vector3 start, Vector3 end, float duration, bool destroyOnComplete)
        {
            if (targetObject == null)
            {
                yield break;
            }

            float elapsed = 0.0f;
            while (elapsed < duration && targetObject != null)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, duration));
                Vector3 position = Vector3.Lerp(start, end, t);
                Vector3 direction = position - targetObject.transform.position;
                targetObject.transform.position = position;
                if (direction.sqrMagnitude > 0.0001f)
                {
                    targetObject.transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
                }

                yield return null;
            }

            if (targetObject != null)
            {
                targetObject.transform.position = end;
                if (destroyOnComplete)
                {
                    Destroy(targetObject);
                }
            }
        }

        private GameObject SpawnPrefabObject(GameObject prefab, Vector3 position, Quaternion rotation, float scaleMultiplier, float lifeTime, bool disableScripts)
        {
            if (prefab == null)
            {
                return null;
            }

            GameObject instance = Instantiate(prefab, position, rotation, GetEffectParent());
            if (!Mathf.Approximately(scaleMultiplier, 1.0f))
            {
                instance.transform.localScale = instance.transform.localScale * Mathf.Max(0.01f, scaleMultiplier);
            }

            if (disableScripts)
            {
                DisableAllMonoBehaviours(instance);
            }

            RestartVfx(instance);

            if (lifeTime > 0.0f)
            {
                Destroy(instance, Mathf.Max(0.05f, lifeTime));
            }

            return instance;
        }

        private Transform GetEffectParent()
        {
            if (EffectRoot != null)
            {
                return EffectRoot;
            }

            return MonsterRuntimeRoot.GetRootOrFallback(transform.parent);
        }

        private static Quaternion BuildFlatLookRotation(Vector3 from, Vector3 to, Quaternion fallback)
        {
            Vector3 direction = to - from;
            direction.y = 0.0f;
            return direction.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(direction.normalized, Vector3.up) : fallback;
        }

        private static void DisableBehaviour<T>(GameObject root) where T : MonoBehaviour
        {
            if (root == null)
            {
                return;
            }

            T[] behaviours = root.GetComponentsInChildren<T>(true);
            for (int i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] != null)
                {
                    behaviours[i].enabled = false;
                }
            }
        }

        private static void DisableAllMonoBehaviours(GameObject root)
        {
            if (root == null)
            {
                return;
            }

            MonoBehaviour[] behaviours = root.GetComponentsInChildren<MonoBehaviour>(true);
            for (int i = 0; i < behaviours.Length; i++)
            {
                MonoBehaviour behaviour = behaviours[i];
                if (behaviour != null)
                {
                    behaviour.enabled = false;
                }
            }
        }

        private static void RestartVfx(GameObject root)
        {
            if (root == null)
            {
                return;
            }

            root.SetActive(true);
            HideInvalidVfxRenderers(root);

            ParticleSystem[] particleSystems = root.GetComponentsInChildren<ParticleSystem>(true);
            for (int i = 0; i < particleSystems.Length; i++)
            {
                ParticleSystem particleSystem = particleSystems[i];
                if (particleSystem == null)
                {
                    continue;
                }

                particleSystem.gameObject.SetActive(true);
                ParticleSystem.MainModule main = particleSystem.main;
                main.stopAction = ParticleSystemStopAction.None;
                particleSystem.Clear(true);
                particleSystem.Play(true);
            }

            Component[] components = root.GetComponentsInChildren<Component>(true);
            for (int i = 0; i < components.Length; i++)
            {
                Component component = components[i];
                if (component == null || component.GetType().FullName != "UnityEngine.VFX.VisualEffect")
                {
                    continue;
                }

                component.gameObject.SetActive(true);
                MethodInfo reinitMethod = component.GetType().GetMethod("Reinit", BindingFlags.Instance | BindingFlags.Public);
                MethodInfo playMethod = component.GetType().GetMethod("Play", BindingFlags.Instance | BindingFlags.Public, null, Type.EmptyTypes, null);
                reinitMethod?.Invoke(component, null);
                playMethod?.Invoke(component, null);
            }
        }

        private static void HideInvalidVfxRenderers(GameObject root)
        {
            if (root == null)
            {
                return;
            }

            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null || HasUsableShowcaseMaterial(renderer))
                {
                    continue;
                }

                renderer.enabled = false;
            }
        }

        private static bool HasUsableShowcaseMaterial(Renderer renderer)
        {
            Material[] materials = renderer.sharedMaterials;
            if (materials == null || materials.Length == 0)
            {
                return false;
            }

            for (int i = 0; i < materials.Length; i++)
            {
                Material material = materials[i];
                if (IsUsableShowcaseMaterial(material))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsUsableShowcaseMaterial(Material material)
        {
            if (material == null || material.shader == null)
            {
                return false;
            }

            string shaderName = material.shader.name;
            return !string.IsNullOrEmpty(shaderName) && !shaderName.Contains("InternalErrorShader");
        }

        private static void SetPrefabAlpha(GameObject root, float alpha)
        {
            if (root == null)
            {
                return;
            }

            alpha = Mathf.Clamp01(alpha);
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null)
                {
                    continue;
                }

                Material[] materials = renderer.materials;
                for (int j = 0; j < materials.Length; j++)
                {
                    Material material = materials[j];
                    if (material == null)
                    {
                        continue;
                    }

                    if (material.HasProperty("_BaseColor"))
                    {
                        Color color = material.GetColor("_BaseColor");
                        color.a = alpha;
                        material.SetColor("_BaseColor", color);
                    }

                    if (material.HasProperty("_Color"))
                    {
                        Color color = material.GetColor("_Color");
                        color.a = alpha;
                        material.SetColor("_Color", color);
                    }
                }
            }
        }

        private static T ReadSerializedField<T>(object owner, string fieldName)
        {
            if (owner == null || string.IsNullOrEmpty(fieldName))
            {
                return default;
            }

            FieldInfo field = owner.GetType().GetField(fieldName, SerializedFieldFlags);
            if (field == null)
            {
                return default;
            }

            object value = field.GetValue(owner);
            if (value is T typedValue)
            {
                return typedValue;
            }

            return default;
        }

        private static float ReadSerializedFloat(object owner, string fieldName, float fallback)
        {
            if (owner == null || string.IsNullOrEmpty(fieldName))
            {
                return fallback;
            }

            FieldInfo field = owner.GetType().GetField(fieldName, SerializedFieldFlags);
            if (field == null)
            {
                return fallback;
            }

            object value = field.GetValue(owner);
            return value is float floatValue ? floatValue : fallback;
        }

        private static GameObject ReadPrefabGameObject(object owner, string fieldName)
        {
            if (owner == null || string.IsNullOrEmpty(fieldName))
            {
                return null;
            }

            FieldInfo field = owner.GetType().GetField(fieldName, SerializedFieldFlags);
            if (field == null)
            {
                return null;
            }

            object value = field.GetValue(owner);
            if (value is GameObject gameObject)
            {
                return gameObject;
            }

            if (value is Component component)
            {
                return component.gameObject;
            }

            return null;
        }

        private static bool CanUseAnimator(Animator animator)
        {
            return animator != null && animator.isActiveAndEnabled && animator.runtimeAnimatorController != null;
        }

        private static bool TryGetAnimatorParameterHash(
            Animator animator,
            string parameterName,
            AnimatorControllerParameterType parameterType,
            out int parameterHash)
        {
            parameterHash = 0;
            if (animator == null || string.IsNullOrEmpty(parameterName))
            {
                return false;
            }

            int targetHash = Animator.StringToHash(parameterName);
            AnimatorControllerParameter[] parameters = animator.parameters;
            for (int i = 0; i < parameters.Length; i++)
            {
                AnimatorControllerParameter parameter = parameters[i];
                if (parameter.type == parameterType && parameter.nameHash == targetHash)
                {
                    parameterHash = targetHash;
                    return true;
                }
            }

            return false;
        }

        private IEnumerator ArcMove(Vector3 from, Vector3 to, float height, float duration)
        {
            float elapsed = 0.0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                Vector3 position = Vector3.Lerp(from, to, t);
                position.y += Mathf.Sin(t * Mathf.PI) * height;
                transform.position = position;
                yield return null;
            }

            transform.position = to;
        }

        private IEnumerator MoveTo(Vector3 targetPosition, float duration)
        {
            Vector3 start = transform.position;
            float elapsed = 0.0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, duration));
                transform.position = Vector3.Lerp(start, targetPosition, t * t * (3.0f - 2.0f * t));
                yield return null;
            }

            transform.position = targetPosition;
        }

        private IEnumerator ProjectileArc(Vector3 start, Vector3 end, float height, float duration, float radius, Color color)
        {
            GameObject projectile = SpawnSphere(start, radius, color, duration + 0.2f);
            float elapsed = 0.0f;
            while (elapsed < duration && projectile != null)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                Vector3 position = Vector3.Lerp(start, end, t);
                position.y += Mathf.Sin(t * Mathf.PI) * height;
                projectile.transform.position = position;
                yield return null;
            }

            if (projectile != null)
            {
                Destroy(projectile);
            }
        }

        private IEnumerator ProjectileLine(Vector3 start, Vector3 end, float duration, Color color)
        {
            GameObject projectile = SpawnSphere(start, 0.2f, color, duration + 0.2f);
            float elapsed = 0.0f;
            while (elapsed < duration && projectile != null)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                projectile.transform.position = Vector3.Lerp(start, end, t);
                yield return null;
            }

            if (projectile != null)
            {
                Destroy(projectile);
            }
        }

        private IEnumerator OrbPulse(Vector3 position, Color color, float duration)
        {
            GameObject orb = SpawnSphere(position, 0.22f, color, duration + 0.1f);
            Vector3 start = position;
            Vector3 end = transform.position + Vector3.up * 1.1f;
            float elapsed = 0.0f;
            while (elapsed < duration && orb != null)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                orb.transform.position = Vector3.Lerp(start, end, t);
                orb.transform.localScale = Vector3.one * Mathf.Lerp(0.22f, 0.08f, t);
                yield return null;
            }
        }

        private IEnumerator ScalePulse(float multiplier, float duration)
        {
            Vector3 startScale = transform.localScale;
            Vector3 peakScale = startScale * Mathf.Max(1.0f, multiplier);
            float half = Mathf.Max(0.01f, duration * 0.5f);
            float elapsed = 0.0f;
            while (elapsed < half)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / half);
                transform.localScale = Vector3.Lerp(startScale, peakScale, t);
                yield return null;
            }

            elapsed = 0.0f;
            while (elapsed < half)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / half);
                transform.localScale = Vector3.Lerp(peakScale, startScale, t);
                yield return null;
            }

            transform.localScale = startScale;
        }

        private void SpawnPulse(Vector3 position, float radius, Color color, float duration)
        {
            StartCoroutine(PulseRoutine(position, radius, color, duration));
        }

        private IEnumerator PulseRoutine(Vector3 position, float radius, Color color, float duration)
        {
            GameObject disc = CreatePrimitiveVisual(PrimitiveType.Cylinder, "ShowcasePulse", position, Quaternion.identity, Vector3.one, color, duration + 0.1f);
            if (disc == null)
            {
                yield break;
            }

            float elapsed = 0.0f;
            while (elapsed < duration && disc != null)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float size = Mathf.Lerp(0.15f, radius, t);
                disc.transform.localScale = new Vector3(size, 0.025f, size);
                yield return null;
            }
        }

        private GameObject SpawnDisc(Vector3 position, float radius, Color color, float lifetime, float height)
        {
            return CreatePrimitiveVisual(
                PrimitiveType.Cylinder,
                "ShowcaseDisc",
                position,
                Quaternion.identity,
                new Vector3(radius, Mathf.Max(0.01f, height), radius),
                color,
                lifetime);
        }

        private GameObject SpawnBlock(Vector3 position, Vector3 scale, Color color, float lifetime)
        {
            return CreatePrimitiveVisual(PrimitiveType.Cube, "ShowcaseBlock", position, Quaternion.identity, scale, color, lifetime);
        }

        private GameObject SpawnSphere(Vector3 position, float radius, Color color, float lifetime)
        {
            return CreatePrimitiveVisual(PrimitiveType.Sphere, "ShowcaseOrb", position, Quaternion.identity, Vector3.one * radius, color, lifetime);
        }

        private GameObject CreatePrimitiveVisual(PrimitiveType primitive, string objectName, Vector3 position, Quaternion rotation, Vector3 scale, Color color, float lifetime)
        {
            GameObject visual = GameObject.CreatePrimitive(primitive);
            visual.name = objectName;
            if (EffectRoot != null)
            {
                visual.transform.SetParent(EffectRoot, true);
            }

            visual.transform.SetPositionAndRotation(position, rotation);
            visual.transform.localScale = scale;
            Collider collider = visual.GetComponent<Collider>();
            if (collider != null)
            {
                collider.enabled = false;
            }

            Renderer renderer = visual.GetComponent<Renderer>();
            if (renderer != null)
            {
                Material material = new Material(ResolveShowcaseShader());
                SetMaterialColor(material, color);
                renderer.material = material;
            }

            Destroy(visual, Mathf.Max(0.05f, lifetime));
            return visual;
        }

        private static void SetMaterialColor(Material material, Color color)
        {
            if (material == null)
            {
                return;
            }

            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }

            if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", color);
            }
        }

        private static Shader ResolveShowcaseShader()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader != null)
            {
                return shader;
            }

            shader = Shader.Find("Unlit/Color");
            return shader != null ? shader : Shader.Find("Standard");
        }

        private Vector3 GetCastPosition()
        {
            if (cachedFirePoint != null)
            {
                return cachedFirePoint.position;
            }

            return transform.position + Vector3.up * 1.15f + GetFlatForward() * 0.45f;
        }

        private Vector3 GetForwardGroundPosition(float distance)
        {
            return GroundService.ProjectToGround(anchorPosition + GetFlatForward() * Mathf.Max(0.0f, distance), GroundHeight);
        }

        private Vector3 GetFlatForward()
        {
            Vector3 forward = transform.forward;
            forward.y = 0.0f;
            if (forward.sqrMagnitude <= 0.0001f)
            {
                forward = anchorRotation * Vector3.forward;
                forward.y = 0.0f;
            }

            return forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector3.forward;
        }

        private void FaceGroundPosition(Vector3 targetPosition)
        {
            Vector3 direction = targetPosition - transform.position;
            direction.y = 0.0f;
            if (direction.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        }

        private void RestoreAnchorPose()
        {
            transform.SetPositionAndRotation(anchorPosition, anchorRotation);
        }

        private void PlayCue(GameplaySfxCue cue, Vector3 position)
        {
            if (!EnableAudio || cue == GameplaySfxCue.None)
            {
                return;
            }

            if (GameplaySfxEmitter.TryPlayAt(transform, cue, position, true))
            {
                return;
            }

            GameplaySfxEmitter.TryPlayCatalogAt(cue, position);
        }

        private static Transform FindFirstNamedChild(Transform root, params string[] nameParts)
        {
            if (root == null || nameParts == null)
            {
                return null;
            }

            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = root.GetChild(i);
                for (int j = 0; j < nameParts.Length; j++)
                {
                    if (!string.IsNullOrEmpty(nameParts[j]) && child.name.Contains(nameParts[j]))
                    {
                        return child;
                    }
                }

                Transform nested = FindFirstNamedChild(child, nameParts);
                if (nested != null)
                {
                    return nested;
                }
            }

            return null;
        }
    }
}
