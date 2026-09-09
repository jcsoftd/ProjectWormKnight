using System;
using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace TeamProject01.Gameplay
{
    public sealed class SegmentShowcaseDirector : MonoBehaviour
    {
        private const float DefaultLevelRowGap = 4f;
        private const float DefaultTargetGroundHeight = 0.72f;
        private const string WormholePortalSegmentId = "SG56_WormholePortal";

        [Header("Catalog")]
        public SegmentCatalogAsset SegmentCatalog;
        public string[] SegmentOrder =
        {
            "SG01_Cannon",
            "SG02_Missile",
            "SG03_Trebuchet",
            "SG04_SawLauncher",
            "SG05_Flamethrower",
            "SG06_Ballista",
            "SG20_LightningObelisk",
            "SG21_FireballTower",
            "SG22_IceCrystalOrb",
            "SG50_WarDrum",
            "SG51_Magnet",
            "SG52_FrostBell",
            "SG53_WarBanner",
            "SG55_MagicBook",
            "SG56_WormholePortal"
        };
        public string[] ExcludedSegmentIds = { "SG54_HolyWaterSprayer" };
        public string[] AttackDisabledSegmentIds = { WormholePortalSegmentId };

        [Header("Scene Roots")]
        public ConvoyController ShowcaseOwner;
        public Transform SegmentRoot;
        public Transform TargetRoot;
        public Transform LabelRoot;
        public Transform ProjectileRoot;
        public Transform RewardRoot;

        [Header("Layout")]
        [Min(1f)] public float ColumnSpacing = 10f;
        [Min(1f)] public float LevelRowGap = DefaultLevelRowGap;
        [Min(1f)] public float TargetForwardOffset = 7f;
        [Min(0f)] public float SegmentHeight = 0.03f;
        [Min(0f)] public float LabelHeight = 2.2f;
        public bool CenterColumns = true;

        [Header("Targets")]
        public GameObject TargetDummyPrefab;
        [Min(1)] public int DummyCountPerCell = 5;
        [Min(0.1f)] public float DummyClusterRadius = 1.6f;
        [Min(0f)] public float TargetGroundHeight = DefaultTargetGroundHeight;
        public bool ProjectTargetsToGround = true;
        public bool KeepTargetsGrounded = true;
        [Min(1f)] public float DummyMaxHp = 1000000f;
        public bool LockDummyTransform = true;

        [Header("Runtime")]
        public bool BuildOnStart = true;
        public bool RebuildWithR;
        public bool RenderTextLabels;
        public bool EnsureCameraAudioListener = true;
        public bool AttackOnlySelectedLevel = true;
        [Min(1)] public int AttackLevel = 3;
        public bool SpawnTargetsOnlyForAttackRows = true;
        [Min(0.05f)] public float FirstFireWarmupSeconds = 0.25f;

        [Header("Support Showcase")]
        public bool RunSupportSegments = true;
        public bool LoopSupportAbilities = true;
        public bool SpawnSupportTargetDummies = true;
        public bool ReplenishSupportTargetDummies = true;
        [Min(0.1f)] public float SupportActivationCooldown = 1.5f;
        [Min(0.1f)] public float SupportBuffActiveSeconds = 5f;
        [Min(1)] public int SupportDummyCountPerCell = 6;
        [Min(0.1f)] public float SupportDummyClusterRadius = 2.1f;
        [Min(0.1f)] public float SupportDummyReplenishInterval = 0.75f;

        [Header("Magnet Showcase")]
        public bool SpawnMagnetRewards = true;
        [Min(0.05f)] public float MagnetActivationCooldown = 0.35f;
        [Min(1)] public int MagnetRewardTargetCount = 18;
        [Min(0.1f)] public float MagnetRewardRadius = 7f;
        [Min(0.02f)] public float MagnetRewardSpawnInterval = 0.08f;
        [Min(1)] public int MagnetExperienceAmount = 3;
        [Min(1)] public int MagnetGoldAmount = 2;

        private readonly List<ConvoySegmentRuntime> runtimeSegments = new List<ConvoySegmentRuntime>(64);
        private readonly List<Vector3> showcaseColumnCenters = new List<Vector3>(32);
        private readonly List<SupportTargetSource> supportTargetSources = new List<SupportTargetSource>(16);
        private readonly List<MagnetRewardSource> magnetRewardSources = new List<MagnetRewardSource>(4);
        private readonly List<EnemyController> enemyBuffer = new List<EnemyController>(64);
        private readonly List<WorldRewardPickup> rewardPickupBuffer = new List<WorldRewardPickup>(64);
        private readonly HashSet<string> excludedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> attackDisabledIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private float warmupTimer;
        private WorldRewardPickup experiencePickupPrefab;
        private WorldRewardPickup goldPickupPrefab;

        private static readonly string[] MovementComponentTypeNames =
        {
            "EnemyMovement",
            "EnemyMeleeAttack",
            "EnemyRangedAttack",
            "EnemyJump",
            "EnemySuicideCharger",
            "EnemySlowZoneThrower",
            "EnemySegmentCutCaster",
            "EnemyPortalTotemCaster",
            "EnemyObstacleSummoner",
            "EnemyBuffCaster",
            "EnemyHatchlingGrowth",
            "EnemyCrowdBlocker",
            "EnemyAreaShield"
        };

        private void Awake()
        {
            NormalizeShowcaseSettings();
        }

        private void Start()
        {
            NormalizeShowcaseSettings();

            if (EnsureCameraAudioListener)
            {
                EnsureActiveAudioListener();
            }

            if (BuildOnStart)
            {
                BuildShowcase();
            }
        }

        private void Update()
        {
            if (RebuildWithR && WasPressedRebuild())
            {
                BuildShowcase();
            }

            float deltaTime = Time.deltaTime;
            if (deltaTime <= 0f)
            {
                return;
            }

            if (warmupTimer > 0f)
            {
                warmupTimer -= deltaTime;
                return;
            }

            for (int i = 0; i < runtimeSegments.Count; i++)
            {
                ConvoySegmentRuntime runtime = runtimeSegments[i];
                if (runtime != null)
                {
                    runtime.Tick(deltaTime);
                }
            }

            TickSupportTargetSources(deltaTime);
            TickMagnetRewardSources(deltaTime);
        }

        private static bool WasPressedRebuild()
        {
#if ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            return keyboard != null && keyboard.rKey.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.R);
#endif
        }

        public void BuildShowcase()
        {
            NormalizeShowcaseSettings();
            runtimeSegments.Clear();
            showcaseColumnCenters.Clear();
            supportTargetSources.Clear();
            magnetRewardSources.Clear();
            excludedIds.Clear();
            attackDisabledIds.Clear();
            if (ExcludedSegmentIds != null)
            {
                for (int i = 0; i < ExcludedSegmentIds.Length; i++)
                {
                    if (!string.IsNullOrWhiteSpace(ExcludedSegmentIds[i]))
                    {
                        excludedIds.Add(ExcludedSegmentIds[i].Trim());
                    }
                }
            }

            if (AttackDisabledSegmentIds != null)
            {
                for (int i = 0; i < AttackDisabledSegmentIds.Length; i++)
                {
                    if (!string.IsNullOrWhiteSpace(AttackDisabledSegmentIds[i]))
                    {
                        attackDisabledIds.Add(AttackDisabledSegmentIds[i].Trim());
                    }
                }
            }

            EnsureRoots();
            ClearRoot(SegmentRoot);
            ClearRoot(TargetRoot);
            ClearRoot(LabelRoot);
            ClearRoot(ProjectileRoot);
            ClearRoot(RewardRoot);

            if (SegmentCatalog == null || SegmentOrder == null || SegmentOrder.Length == 0)
            {
                Debug.LogWarning("SegmentShowcaseDirector has no segment catalog or order.", this);
                return;
            }

            int columnCount = CountValidColumns();
            int columnIndex = 0;
            int chainIndex = 0;
            for (int i = 0; i < SegmentOrder.Length; i++)
            {
                string segmentId = SegmentOrder[i];
                if (!TryGetShowcaseDefinition(segmentId, out SegmentDefinition definition))
                {
                    continue;
                }

                float x = ResolveColumnX(columnIndex, columnCount);
                showcaseColumnCenters.Add(new Vector3(x, SegmentHeight, 0f));
                BuildSegmentColumn(definition, x, ref chainIndex);
                columnIndex++;
            }

            warmupTimer = FirstFireWarmupSeconds;
        }

        private int CountValidColumns()
        {
            int count = 0;
            for (int i = 0; i < SegmentOrder.Length; i++)
            {
                if (TryGetShowcaseDefinition(SegmentOrder[i], out _))
                {
                    count++;
                }
            }

            return count;
        }

        private bool TryGetShowcaseDefinition(string segmentId, out SegmentDefinition definition)
        {
            definition = null;
            if (SegmentCatalog == null || string.IsNullOrWhiteSpace(segmentId))
            {
                return false;
            }

            string normalizedId = segmentId.Trim();
            if (excludedIds.Contains(normalizedId))
            {
                return false;
            }

            return SegmentCatalog.TryFind(normalizedId, out definition)
                && definition != null
                && definition.HasId
                && !definition.StarterOnly;
        }

        public int CopyShowcaseColumnCenters(List<Vector3> results)
        {
            if (results == null)
            {
                return 0;
            }

            results.Clear();
            for (int i = 0; i < showcaseColumnCenters.Count; i++)
            {
                results.Add(showcaseColumnCenters[i]);
            }

            return results.Count;
        }

        private void BuildSegmentColumn(SegmentDefinition definition, float x, ref int chainIndex)
        {
            int maxLevel = definition.UseLevels ? Mathf.Min(3, Mathf.Max(1, definition.MaxLevel)) : 1;
            bool attackDisabled = IsAttackDisabled(definition);
            for (int level = 1; level <= maxLevel; level++)
            {
                if (!definition.TryGetLevel(level, out SegmentLevelDefinition levelData) || levelData.SegmentPrefab == null)
                {
                    continue;
                }

                Vector3 segmentPosition = new Vector3(x, SegmentHeight, ResolveLevelRowZ(level, maxLevel));
                Quaternion segmentRotation = Quaternion.Euler(0f, -90f, 0f); // right side points toward +Z targets
                GameObject segmentObject = Instantiate(levelData.SegmentPrefab, segmentPosition, segmentRotation, SegmentRoot);
                segmentObject.name = $"{definition.NormalizedId}_Lv{level}_Showcase";

                ConvoySegmentRuntime runtime = segmentObject.GetComponent<ConvoySegmentRuntime>();
                if (runtime == null)
                {
                    runtime = segmentObject.AddComponent<ConvoySegmentRuntime>();
                }

                ApplyLevelAttackProfile(runtime, levelData);
                SupportSegmentAbility supportAbility = runtime.GetComponent<SupportSegmentAbility>();
                SegmentSupportAbilityProfile supportProfile = PrepareSupportAbilityForShowcase(supportAbility);
                runtime.SetSegmentLevel(level);
                bool shouldAttack = !attackDisabled && ShouldAttackLevel(level);
                bool shouldRunSupport = !attackDisabled && IsRunnableSupportProfile(supportProfile);
                bool shouldRunRuntime = shouldAttack || shouldRunSupport;
                runtime.Configure(ShowcaseOwner, chainIndex, shouldRunRuntime);
                if (shouldRunRuntime)
                {
                    runtimeSegments.Add(runtime);
                }

                if (!attackDisabled && (shouldAttack || (!shouldRunSupport && !SpawnTargetsOnlyForAttackRows)))
                {
                    BuildTargetCluster(definition, level, segmentPosition + Vector3.forward * TargetForwardOffset);
                }

                if (shouldRunSupport)
                {
                    ConfigureSupportShowcase(definition, level, supportProfile, supportAbility, segmentPosition);
                }

                chainIndex++;
            }

            if (RenderTextLabels)
            {
                BuildColumnLabel(definition, x, maxLevel);
            }
        }

        private bool ShouldAttackLevel(int level)
        {
            return !AttackOnlySelectedLevel || level == Mathf.Max(1, AttackLevel);
        }

        private bool IsAttackDisabled(SegmentDefinition definition)
        {
            if (definition == null || string.IsNullOrWhiteSpace(definition.NormalizedId))
            {
                return false;
            }

            return attackDisabledIds.Contains(definition.NormalizedId)
                || string.Equals(definition.NormalizedId, WormholePortalSegmentId, StringComparison.OrdinalIgnoreCase);
        }

        private SegmentSupportAbilityProfile PrepareSupportAbilityForShowcase(SupportSegmentAbility supportAbility)
        {
            if (!RunSupportSegments || supportAbility == null || supportAbility.Profile == null)
            {
                return supportAbility != null ? supportAbility.Profile : null;
            }

            SegmentSupportAbilityProfile profile = supportAbility.Profile;
            if (!LoopSupportAbilities || profile.AbilityKind == SegmentSupportAbilityKind.None)
            {
                return profile;
            }

            SegmentSupportAbilityProfile runtimeProfile = Instantiate(profile);
            runtimeProfile.name = $"{profile.name}_ShowcaseRuntime";
            runtimeProfile.StartsReady = true;

            switch (runtimeProfile.AbilityKind)
            {
                case SegmentSupportAbilityKind.FinalDamageBuff:
                case SegmentSupportAbilityKind.FinalAttackSpeedBuff:
                    runtimeProfile.Cooldown = SupportActivationCooldown;
                    runtimeProfile.ActiveDurationSeconds = Mathf.Max(runtimeProfile.ActiveDurationSeconds, SupportBuffActiveSeconds);
                    break;
                case SegmentSupportAbilityKind.PickupMagnet:
                    runtimeProfile.Cooldown = MagnetActivationCooldown;
                    runtimeProfile.ActiveDurationSeconds = Mathf.Max(runtimeProfile.ActiveDurationSeconds, SupportBuffActiveSeconds);
                    break;
                case SegmentSupportAbilityKind.FreezeArea:
                case SegmentSupportAbilityKind.HolyWaterVulnerabilitySpray:
                case SegmentSupportAbilityKind.WormholePortal:
                    runtimeProfile.Cooldown = SupportActivationCooldown;
                    runtimeProfile.ActiveDurationSeconds = Mathf.Max(runtimeProfile.ActiveDurationSeconds, 0.2f);
                    break;
            }

            supportAbility.Profile = runtimeProfile;
            return runtimeProfile;
        }

        private bool IsRunnableSupportProfile(SegmentSupportAbilityProfile profile)
        {
            return RunSupportSegments && profile != null && profile.AbilityKind != SegmentSupportAbilityKind.None;
        }

        private void ConfigureSupportShowcase(
            SegmentDefinition definition,
            int level,
            SegmentSupportAbilityProfile profile,
            SupportSegmentAbility supportAbility,
            Vector3 segmentPosition)
        {
            if (profile == null)
            {
                return;
            }

            if (SpawnSupportTargetDummies && NeedsSupportTargets(profile.AbilityKind))
            {
                Vector3 targetCenter = segmentPosition + Vector3.forward * TargetForwardOffset;
                bool lockTargets = profile.AbilityKind != SegmentSupportAbilityKind.WormholePortal;
                int count = Mathf.Max(1, SupportDummyCountPerCell);
                float radius = Mathf.Max(0.1f, SupportDummyClusterRadius);
                BuildTargetCluster(definition, level, targetCenter, count, radius, lockTargets, "SupportTarget");

                if (ReplenishSupportTargetDummies)
                {
                    supportTargetSources.Add(new SupportTargetSource(definition, level, targetCenter, count, radius, lockTargets));
                }
            }

            if (SpawnMagnetRewards && profile.AbilityKind == SegmentSupportAbilityKind.PickupMagnet && supportAbility != null)
            {
                MagnetRewardSource source = new MagnetRewardSource(supportAbility.transform);
                magnetRewardSources.Add(source);
                FillMagnetRewardSource(source);
            }
        }

        private static bool NeedsSupportTargets(SegmentSupportAbilityKind abilityKind)
        {
            return abilityKind == SegmentSupportAbilityKind.FreezeArea
                || abilityKind == SegmentSupportAbilityKind.HolyWaterVulnerabilitySpray
                || abilityKind == SegmentSupportAbilityKind.WormholePortal;
        }

        private float ResolveLevelRowZ(int level, int maxLevel)
        {
            int attackLevel = Mathf.Max(1, AttackLevel);
            if (maxLevel < attackLevel)
            {
                return 0f;
            }

            if (level >= attackLevel)
            {
                return 0f;
            }

            return -(attackLevel - level) * LevelRowGap;
        }

        private void NormalizeShowcaseSettings()
        {
            LevelRowGap = Mathf.Clamp(LevelRowGap, 1f, DefaultLevelRowGap);
            TargetGroundHeight = Mathf.Max(0f, TargetGroundHeight);
            SupportActivationCooldown = Mathf.Max(0.1f, SupportActivationCooldown);
            SupportBuffActiveSeconds = Mathf.Max(0.1f, SupportBuffActiveSeconds);
            SupportDummyCountPerCell = Mathf.Max(1, SupportDummyCountPerCell);
            SupportDummyClusterRadius = Mathf.Max(0.1f, SupportDummyClusterRadius);
            SupportDummyReplenishInterval = Mathf.Max(0.1f, SupportDummyReplenishInterval);
            MagnetActivationCooldown = Mathf.Max(0.05f, MagnetActivationCooldown);
            MagnetRewardTargetCount = Mathf.Max(1, MagnetRewardTargetCount);
            MagnetRewardRadius = Mathf.Max(0.1f, MagnetRewardRadius);
            MagnetRewardSpawnInterval = Mathf.Max(0.02f, MagnetRewardSpawnInterval);
            MagnetExperienceAmount = Mathf.Max(1, MagnetExperienceAmount);
            MagnetGoldAmount = Mathf.Max(1, MagnetGoldAmount);
        }

        private void EnsureActiveAudioListener()
        {
            if (HasActiveAudioListener())
            {
                return;
            }

            Camera targetCamera = Camera.main;
            if (targetCamera == null || !targetCamera.gameObject.activeInHierarchy)
            {
                targetCamera = FindActiveCamera();
            }

            if (targetCamera == null)
            {
                return;
            }

            AudioListener listener = targetCamera.GetComponent<AudioListener>();
            if (listener == null)
            {
                listener = targetCamera.gameObject.AddComponent<AudioListener>();
            }

            listener.enabled = true;
        }

        private static bool HasActiveAudioListener()
        {
            AudioListener[] listeners = FindObjectsByType<AudioListener>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < listeners.Length; i++)
            {
                AudioListener listener = listeners[i];
                if (listener != null && listener.enabled && listener.gameObject.activeInHierarchy)
                {
                    return true;
                }
            }

            return false;
        }

        private static Camera FindActiveCamera()
        {
            Camera[] cameras = FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < cameras.Length; i++)
            {
                Camera camera = cameras[i];
                if (camera != null && camera.enabled && camera.gameObject.activeInHierarchy)
                {
                    return camera;
                }
            }

            return cameras.Length > 0 ? cameras[0] : null;
        }

        private void ApplyLevelAttackProfile(ConvoySegmentRuntime runtime, SegmentLevelDefinition levelData)
        {
            if (runtime == null || levelData.AttackProfile == null)
            {
                return;
            }

            GenericSegmentWeapon weapon = runtime.GetComponent<GenericSegmentWeapon>();
            if (weapon != null && weapon.AttackProfile == null)
            {
                weapon.AttackProfile = levelData.AttackProfile;
            }
        }

        private void BuildTargetCluster(SegmentDefinition definition, int level, Vector3 center)
        {
            BuildTargetCluster(definition, level, center, Mathf.Max(1, DummyCountPerCell), DummyClusterRadius, LockDummyTransform, "Target");
        }

        private void BuildTargetCluster(
            SegmentDefinition definition,
            int level,
            Vector3 center,
            int count,
            float radius,
            bool lockTransform,
            string nameSuffix)
        {
            count = Mathf.Max(1, count);
            radius = Mathf.Max(0.1f, radius);
            for (int i = 0; i < count; i++)
            {
                float angle = count == 1 ? 0f : (360f / count) * i;
                Vector3 offset = Quaternion.Euler(0f, angle, 0f) * Vector3.forward * radius;
                GameObject dummy = CreateDummy(center + offset, lockTransform);
                dummy.name = $"{definition.NormalizedId}_Lv{level}_{nameSuffix}_{i + 1:00}";
            }
        }

        private GameObject CreateDummy(Vector3 position)
        {
            return CreateDummy(position, LockDummyTransform);
        }

        private GameObject CreateDummy(Vector3 position, bool lockTransform)
        {
            Vector3 spawnPosition = ResolveTargetGroundPosition(position);
            GameObject dummy = TargetDummyPrefab != null
                ? Instantiate(TargetDummyPrefab, spawnPosition, Quaternion.identity, TargetRoot)
                : CreatePrimitiveDummy(spawnPosition);

            DisableDummyMovement(dummy);
            Rigidbody body = dummy.GetComponent<Rigidbody>();
            if (body != null)
            {
                body.isKinematic = true;
                body.useGravity = false;
            }

            ShowcaseTargetDummy lockDummy = dummy.GetComponent<ShowcaseTargetDummy>();
            if (lockDummy == null)
            {
                lockDummy = dummy.AddComponent<ShowcaseTargetDummy>();
            }

            lockDummy.Configure(DummyMaxHp, lockTransform, ProjectTargetsToGround && KeepTargetsGrounded, TargetGroundHeight);
            return dummy;
        }

        private Vector3 ResolveTargetGroundPosition(Vector3 position)
        {
            return ProjectTargetsToGround
                ? GroundService.ProjectToGround(position, TargetGroundHeight)
                : position;
        }

        private GameObject CreatePrimitiveDummy(Vector3 position)
        {
            GameObject dummy = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            dummy.transform.SetParent(TargetRoot, true);
            dummy.transform.position = position;
            dummy.transform.localScale = new Vector3(0.85f, 1.2f, 0.85f);
            dummy.AddComponent<EnemyHealth>();
            dummy.AddComponent<EnemyController>();
            return dummy;
        }

        private void DisableDummyMovement(GameObject dummy)
        {
            if (dummy == null)
            {
                return;
            }

            MonoBehaviour[] behaviours = dummy.GetComponentsInChildren<MonoBehaviour>(true);
            for (int i = 0; i < behaviours.Length; i++)
            {
                MonoBehaviour behaviour = behaviours[i];
                if (behaviour == null || behaviour is EnemyController || behaviour is EnemyHealth || behaviour is EnemySupportDebuffState)
                {
                    continue;
                }

                string typeName = behaviour.GetType().Name;
                for (int j = 0; j < MovementComponentTypeNames.Length; j++)
                {
                    if (string.Equals(typeName, MovementComponentTypeNames[j], StringComparison.Ordinal))
                    {
                        behaviour.enabled = false;
                        break;
                    }
                }
            }
        }

        private void TickSupportTargetSources(float deltaTime)
        {
            if (!ReplenishSupportTargetDummies || supportTargetSources.Count == 0)
            {
                return;
            }

            for (int i = 0; i < supportTargetSources.Count; i++)
            {
                SupportTargetSource source = supportTargetSources[i];
                source.Timer -= deltaTime;
                if (source.Timer > 0f)
                {
                    continue;
                }

                source.Timer = SupportDummyReplenishInterval;
                EnemyController.CollectActiveInRange(
                    source.Center,
                    source.Radius + 0.75f,
                    enemyBuffer,
                    SegmentTargetQuery.IsEnemyUsable);

                int missing = source.DesiredCount - enemyBuffer.Count;
                for (int j = 0; j < missing; j++)
                {
                    SpawnSupportTarget(source);
                }
            }
        }

        private void SpawnSupportTarget(SupportTargetSource source)
        {
            float angle = UnityEngine.Random.Range(0f, 360f);
            float distance = UnityEngine.Random.Range(0.15f, Mathf.Max(0.15f, source.Radius));
            Vector3 offset = Quaternion.Euler(0f, angle, 0f) * Vector3.forward * distance;
            GameObject dummy = CreateDummy(source.Center + offset, source.LockTransform);
            source.Serial++;
            dummy.name = $"{source.SegmentId}_Lv{source.Level}_SupportReplenish_{source.Serial:00}";
        }

        private void TickMagnetRewardSources(float deltaTime)
        {
            if (!SpawnMagnetRewards || magnetRewardSources.Count == 0)
            {
                return;
            }

            for (int i = 0; i < magnetRewardSources.Count; i++)
            {
                MagnetRewardSource source = magnetRewardSources[i];
                if (source.Source == null)
                {
                    continue;
                }

                source.Timer -= deltaTime;
                if (source.Timer > 0f)
                {
                    continue;
                }

                source.Timer = MagnetRewardSpawnInterval;
                if (CountMagnetRewards(source) < MagnetRewardTargetCount)
                {
                    SpawnMagnetReward(source);
                }
            }
        }

        private void FillMagnetRewardSource(MagnetRewardSource source)
        {
            if (source == null || source.Source == null)
            {
                return;
            }

            int count = Mathf.Max(1, MagnetRewardTargetCount);
            for (int i = 0; i < count; i++)
            {
                SpawnMagnetReward(source);
            }

            source.Timer = MagnetRewardSpawnInterval;
        }

        private int CountMagnetRewards(MagnetRewardSource source)
        {
            if (source == null || source.Source == null)
            {
                return 0;
            }

            WorldRewardPickup.CollectActiveInRange(
                source.Source.position,
                MagnetRewardRadius + 1f,
                rewardPickupBuffer,
                IsShowcaseRewardPickup);
            return rewardPickupBuffer.Count;
        }

        private bool IsShowcaseRewardPickup(WorldRewardPickup pickup)
        {
            return pickup != null
                && RewardRoot != null
                && pickup.transform.IsChildOf(RewardRoot);
        }

        private void SpawnMagnetReward(MagnetRewardSource source)
        {
            if (source == null || source.Source == null)
            {
                return;
            }

            RewardPickupKind kind = source.Serial % 2 == 0 ? RewardPickupKind.Experience : RewardPickupKind.Gold;
            WorldRewardPickup prefab = ResolveRewardPickupPrefab(kind);
            float angle = UnityEngine.Random.Range(0f, 360f);
            float minDistance = Mathf.Min(1.5f, MagnetRewardRadius);
            float distance = UnityEngine.Random.Range(minDistance, Mathf.Max(minDistance, MagnetRewardRadius));
            Vector3 offset = Quaternion.Euler(0f, angle, 0f) * Vector3.forward * distance;
            Vector3 landingPosition = GroundService.ProjectToGround(source.Source.position + offset, 0.02f);
            WorldRewardPickup pickup = prefab != null
                ? Instantiate(prefab, landingPosition, Quaternion.identity, RewardRoot)
                : CreatePrimitiveRewardPickup(landingPosition);

            source.Serial++;
            pickup.name = $"ShowcaseMagnet_{kind}_{source.Serial:00}";
            pickup.SubmitRewardOnCollect = false;
            pickup.Configure(kind, ResolveMagnetRewardAmount(kind), 0, landingPosition, landingPosition);
        }

        private int ResolveMagnetRewardAmount(RewardPickupKind kind)
        {
            return kind == RewardPickupKind.Gold
                ? Mathf.Max(1, MagnetGoldAmount)
                : Mathf.Max(1, MagnetExperienceAmount);
        }

        private WorldRewardPickup ResolveRewardPickupPrefab(RewardPickupKind kind)
        {
            if (kind == RewardPickupKind.Gold)
            {
                goldPickupPrefab ??= LoadRewardPickupPrefab("RewardPickups/PF_RewardPickup_Gold");
                return goldPickupPrefab;
            }

            experiencePickupPrefab ??= LoadRewardPickupPrefab("RewardPickups/PF_RewardPickup_Exp");
            return experiencePickupPrefab;
        }

        private static WorldRewardPickup LoadRewardPickupPrefab(string resourcePath)
        {
            GameObject prefab = Resources.Load<GameObject>(resourcePath);
            return prefab != null ? prefab.GetComponent<WorldRewardPickup>() : null;
        }

        private WorldRewardPickup CreatePrimitiveRewardPickup(Vector3 position)
        {
            GameObject reward = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            reward.transform.SetParent(RewardRoot, true);
            reward.transform.position = position;
            reward.transform.localScale = Vector3.one * 0.35f;
            Collider collider = reward.GetComponent<Collider>();
            if (collider != null)
            {
                collider.enabled = false;
            }

            return reward.AddComponent<WorldRewardPickup>();
        }

        private void BuildColumnLabel(SegmentDefinition definition, float x, int maxLevel)
        {
            Vector3 columnPosition = new Vector3(x, LabelHeight, TargetForwardOffset + 2.5f);
            CreateTextLabel(columnPosition, definition.NormalizedId, 0.45f);

            for (int level = 1; level <= maxLevel; level++)
            {
                Vector3 levelPosition = new Vector3(x - 2.7f, LabelHeight * 0.55f, ResolveLevelRowZ(level, maxLevel));
                CreateTextLabel(levelPosition, $"Lv{level}", 0.36f);
            }
        }

        private void CreateTextLabel(Vector3 position, string text, float size)
        {
            GameObject label = new GameObject($"Label_{text}");
            label.transform.SetParent(LabelRoot, false);
            label.transform.position = position;
            label.transform.rotation = Quaternion.Euler(65f, 0f, 0f);

            TextMesh mesh = label.AddComponent<TextMesh>();
            mesh.text = text;
            mesh.anchor = TextAnchor.MiddleCenter;
            mesh.alignment = TextAlignment.Center;
            mesh.characterSize = size;
            mesh.fontSize = 42;
            mesh.color = new Color(0.08f, 0.16f, 0.27f, 1f);
        }

        private float ResolveColumnX(int columnIndex, int columnCount)
        {
            if (!CenterColumns)
            {
                return columnIndex * ColumnSpacing;
            }

            float centerOffset = Mathf.Max(0, columnCount - 1) * ColumnSpacing * 0.5f;
            return columnIndex * ColumnSpacing - centerOffset;
        }

        private void EnsureRoots()
        {
            SegmentRoot = EnsureRoot(SegmentRoot, "ShowcaseSegments");
            TargetRoot = EnsureRoot(TargetRoot, "ShowcaseTargets");
            LabelRoot = EnsureRoot(LabelRoot, "ShowcaseLabels");
            ProjectileRoot = EnsureRoot(ProjectileRoot, "Projectiles");
            RewardRoot = EnsureRoot(RewardRoot, "ShowcaseRewards");
        }

        private Transform EnsureRoot(Transform root, string rootName)
        {
            if (root != null)
            {
                return root;
            }

            GameObject existing = GameObject.Find(rootName);
            if (existing != null)
            {
                return existing.transform;
            }

            GameObject created = new GameObject(rootName);
            return created.transform;
        }

        private sealed class SupportTargetSource
        {
            public readonly string SegmentId;
            public readonly int Level;
            public readonly Vector3 Center;
            public readonly int DesiredCount;
            public readonly float Radius;
            public readonly bool LockTransform;
            public float Timer;
            public int Serial;

            public SupportTargetSource(
                SegmentDefinition definition,
                int level,
                Vector3 center,
                int desiredCount,
                float radius,
                bool lockTransform)
            {
                SegmentId = definition != null ? definition.NormalizedId : "Support";
                Level = level;
                Center = center;
                DesiredCount = Mathf.Max(1, desiredCount);
                Radius = Mathf.Max(0.1f, radius);
                LockTransform = lockTransform;
            }
        }

        private sealed class MagnetRewardSource
        {
            public readonly Transform Source;
            public float Timer;
            public int Serial;

            public MagnetRewardSource(Transform source)
            {
                Source = source;
            }
        }

        private static void ClearRoot(Transform root)
        {
            if (root == null)
            {
                return;
            }

            for (int i = root.childCount - 1; i >= 0; i--)
            {
                Transform child = root.GetChild(i);
                if (Application.isPlaying)
                {
                    Destroy(child.gameObject);
                }
                else
                {
                    DestroyImmediate(child.gameObject);
                }
            }
        }
    }
}
