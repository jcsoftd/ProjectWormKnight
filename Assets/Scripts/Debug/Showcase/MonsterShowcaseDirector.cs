using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace TeamProject01.Gameplay
{
    public sealed class MonsterShowcaseDirector : MonoBehaviour
    {
        private const float DefaultMonsterGroundHeight = 0.72f;

        [Header("Catalog")]
        public GameObject[] MonsterPrefabs;
        public string[] MonsterPrefabPaths =
        {
            "Assets/Prefabs/Monster/EnemyPrefab/Enemy_Melee_Normal.prefab",
            "Assets/Prefabs/Monster/EnemyPrefab/Enemy_Melee_SkeletonDagger.prefab",
            "Assets/Prefabs/Monster/EnemyPrefab/Enemy_Ranged_Normal.prefab",
            "Assets/Prefabs/Monster/EnemyPrefab/Enemy_Ranged_SkeletonCrossbow.prefab",
            "Assets/Prefabs/Monster/EnemyPrefab/Enemy_Elite_AreaShield.prefab",
            "Assets/Prefabs/Monster/EnemyPrefab/Enemy_Elite_BuffCaster.prefab",
            "Assets/Prefabs/Monster/EnemyPrefab/Enemy_Elite_DragonHatchling.prefab",
            "Assets/Prefabs/Monster/EnemyPrefab/Enemy_Elite_ObstacleSingle.prefab",
            "Assets/Prefabs/Monster/EnemyPrefab/Enemy_Elite_PortalTotemCaster.prefab",
            "Assets/Prefabs/Monster/EnemyPrefab/Enemy_Elite_SegmentCutCaster.prefab",
            "Assets/Prefabs/Monster/EnemyPrefab/Enemy_Elite_SkeletonGolemJumper.prefab",
            "Assets/Prefabs/Monster/EnemyPrefab/Enemy_Elite_SlowThrower.prefab",
            "Assets/Prefabs/Monster/EnemyPrefab/Enemy_Elite_SuicideCharger.prefab",
            "Assets/Prefabs/Monster/EnemyPrefab/Boss/Boss01.prefab"
        };

        [Header("Scene Roots")]
        public Transform MonsterRoot;
        public Transform EffectRoot;
        public Transform TargetRoot;
        public Transform LabelRoot;

        [Header("Layout")]
        [Min(1f)] public float ColumnSpacing = 7.0f;
        [Min(1f)] public float EliteColumnSpacing = 10.0f;
        [Min(1f)] public float BossColumnSpacing = 16.0f;
        [Min(0f)] public float BossBackwardOffset = 5.0f;
        [Min(0f)] public float MonsterGroundHeight = DefaultMonsterGroundHeight;
        public bool ProjectMonstersToGround = true;
        public bool CenterColumns = true;
        public float MonsterYaw = 180.0f;
        public float JumpMonsterYawOffset = -90.0f;

        [Header("Skill Showcase")]
        public bool UseShowcaseSkillLoop = true;
        public bool EnableShowcaseAudio = true;
        [Min(0.5f)] public float SkillTargetForwardOffset = 5.5f;
        [Min(0.2f)] public float SkillInterval = 3.0f;
        [Min(0.2f)] public float JumpDistance = 5.0f;
        [Min(0.1f)] public float JumpHeight = 2.0f;
        [Min(0.1f)] public float JumpDuration = 0.55f;
        [Min(0.0f)] public float SkillGroundHeight = 0.04f;

        [Header("Runtime")]
        public bool BuildOnStart = true;
        public bool RebuildWithR;
        public bool RenderTextLabels;
        public bool SpawnTargetPads = true;
        public bool SpawnBossNexusTarget = true;
        public bool FreezeOriginalMonsterAi = true;
        public bool EnsureCameraAudioListener = true;
        [Min(0.5f)] public float BossNexusForwardDistance = 7.0f;
        [Min(0.2f)] public float BossNexusVisualRadius = 1.2f;

        private readonly List<Vector3> showcaseColumnCenters = new List<Vector3>(32);
        private readonly List<GameObject> resolvedPrefabs = new List<GameObject>(32);
        private readonly List<string> resolvedLabels = new List<string>(32);
        private readonly List<float> resolvedColumnXs = new List<float>(32);

        private static readonly string[] FrozenBehaviourTypeNames =
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
            "EnemyAreaShield",
            "EnemyJumpAnimatorBridge",
            "EnemyObstacleSummonerAnimatorBridge",
            "EnemyPortalTotemCasterAnimatorBridge",
            "EnemySegmentCutCasterAnimatorBridge",
            "EnemySlowZoneThrowerAnimatorBridge",
            "BossController",
            "BossTeleportMovement",
            "BossJumpShockwaveAttack",
            "BossDiamondProjectileAttack",
            "BossDiamondSiegeAttack",
            "BossSummonAttack",
            "BossLineWallAttack",
            "BossChargeStunAttack"
        };

        private void Awake()
        {
            NormalizeSettings();
        }

        private void Start()
        {
            NormalizeSettings();

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
            NormalizeSettings();
            showcaseColumnCenters.Clear();
            ResolveMonsterPrefabs();
            EnsureRoots();
            ClearRoot(MonsterRoot);
            ClearRoot(EffectRoot);
            ClearRoot(TargetRoot);
            ClearRoot(LabelRoot);

            int count = resolvedPrefabs.Count;
            if (count == 0)
            {
                Debug.LogWarning("MonsterShowcaseDirector has no monster prefabs.", this);
                return;
            }

            BuildColumnLayout(count);
            for (int i = 0; i < count; i++)
            {
                float x = i < resolvedColumnXs.Count ? resolvedColumnXs[i] : ResolveColumnX(i, count);
                BuildMonsterColumn(resolvedPrefabs[i], resolvedLabels[i], x);
            }
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

        private void BuildMonsterColumn(GameObject prefab, string label, float x)
        {
            if (prefab == null)
            {
                return;
            }

            Vector3 spawnPosition = ResolveMonsterGroundPosition(new Vector3(x, 0.0f, 0.0f));
            Quaternion spawnRotation = ResolveMonsterRotation(prefab);
            bool isBoss = IsBossPrefab(prefab);
            if (isBoss && BossBackwardOffset > 0.0f)
            {
                Vector3 backOffset = -(spawnRotation * Vector3.forward) * BossBackwardOffset;
                spawnPosition = ResolveMonsterGroundPosition(spawnPosition + backOffset);
            }

            GameObject monster = Instantiate(prefab, spawnPosition, spawnRotation, MonsterRoot);
            monster.name = $"{label}_Showcase";
            monster.SetActive(true);

            PrepareMonsterForShowcase(monster);
            showcaseColumnCenters.Add(spawnPosition);

            if (SpawnTargetPads)
            {
                BuildTargetPad(monster.transform);
            }

            if (isBoss && SpawnBossNexusTarget)
            {
                BuildBossNexusTarget(monster.transform);
            }

            if (RenderTextLabels)
            {
                BuildColumnLabel(label, x);
            }
        }

        private void PrepareMonsterForShowcase(GameObject monster)
        {
            if (monster == null)
            {
                return;
            }

            FreezePhysics(monster);

            if (FreezeOriginalMonsterAi)
            {
                FreezeOriginalBehaviours(monster);
            }

            MonsterShowcaseSkillLooper looper = monster.GetComponent<MonsterShowcaseSkillLooper>();
            if (looper == null)
            {
                looper = monster.AddComponent<MonsterShowcaseSkillLooper>();
            }

            looper.enabled = UseShowcaseSkillLoop;
            looper.Configure(
                EffectRoot,
                SkillTargetForwardOffset,
                SkillInterval,
                JumpDistance,
                JumpHeight,
                JumpDuration,
                SkillGroundHeight,
                EnableShowcaseAudio);
        }

        private void FreezePhysics(GameObject monster)
        {
            Rigidbody[] bodies = monster.GetComponentsInChildren<Rigidbody>(true);
            for (int i = 0; i < bodies.Length; i++)
            {
                Rigidbody body = bodies[i];
                if (body == null)
                {
                    continue;
                }

                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                body.useGravity = false;
                body.isKinematic = true;
            }

            NavMeshAgent[] agents = monster.GetComponentsInChildren<NavMeshAgent>(true);
            for (int i = 0; i < agents.Length; i++)
            {
                NavMeshAgent agent = agents[i];
                if (agent != null)
                {
                    agent.enabled = false;
                }
            }
        }

        private void FreezeOriginalBehaviours(GameObject monster)
        {
            MonoBehaviour[] behaviours = monster.GetComponentsInChildren<MonoBehaviour>(true);
            for (int i = 0; i < behaviours.Length; i++)
            {
                MonoBehaviour behaviour = behaviours[i];
                if (behaviour == null || behaviour is MonsterShowcaseSkillLooper || behaviour is GameplaySfxEmitter)
                {
                    continue;
                }

                if (ShouldFreezeBehaviour(behaviour.GetType().Name))
                {
                    behaviour.enabled = false;
                }
            }
        }

        private static bool ShouldFreezeBehaviour(string typeName)
        {
            for (int i = 0; i < FrozenBehaviourTypeNames.Length; i++)
            {
                if (string.Equals(typeName, FrozenBehaviourTypeNames[i], StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private void BuildBossNexusTarget(Transform boss)
        {
            if (boss == null || TargetRoot == null)
            {
                return;
            }

            Vector3 forward = boss.forward;
            forward.y = 0.0f;
            if (forward.sqrMagnitude <= 0.0001f)
            {
                forward = Vector3.forward;
            }

            Vector3 targetPosition = GroundService.ProjectToGround(
                boss.position + forward.normalized * BossNexusForwardDistance,
                SkillGroundHeight);

            GameObject nexus = new GameObject("Nexus_Core");
            nexus.transform.SetParent(TargetRoot, true);
            nexus.transform.position = targetPosition;

            float radius = Mathf.Max(0.2f, BossNexusVisualRadius);
            GameObject baseRing = CreateTargetPrimitive(
                PrimitiveType.Cylinder,
                "ShowcaseBossNexusBase",
                nexus.transform,
                Vector3.zero,
                Quaternion.identity,
                new Vector3(radius * 1.8f, 0.04f, radius * 1.8f),
                new Color(0.1f, 0.55f, 1.0f, 0.92f));

            GameObject core = CreateTargetPrimitive(
                PrimitiveType.Sphere,
                "ShowcaseBossNexusCore",
                nexus.transform,
                Vector3.up * (radius * 0.75f),
                Quaternion.identity,
                Vector3.one * radius,
                new Color(0.25f, 0.82f, 1.0f, 0.95f));

            if (baseRing != null)
            {
                baseRing.transform.localRotation = Quaternion.identity;
            }

            if (core != null)
            {
                core.transform.localRotation = Quaternion.identity;
            }
        }

        private void BuildTargetPad(Transform monster)
        {
            if (monster == null || TargetRoot == null)
            {
                return;
            }

            Vector3 forward = monster.forward;
            forward.y = 0.0f;
            if (forward.sqrMagnitude <= 0.0001f)
            {
                forward = Vector3.forward;
            }

            Vector3 targetPosition = GroundService.ProjectToGround(monster.position + forward.normalized * SkillTargetForwardOffset, SkillGroundHeight);
            GameObject pad = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pad.name = monster.name + "_TargetPad";
            pad.transform.SetParent(TargetRoot, true);
            pad.transform.position = targetPosition;
            pad.transform.localScale = new Vector3(1.25f, 0.015f, 1.25f);

            Collider collider = pad.GetComponent<Collider>();
            if (collider != null)
            {
                collider.enabled = false;
            }

            Renderer renderer = pad.GetComponent<Renderer>();
            if (renderer != null)
            {
                Material material = new Material(ResolveShowcaseShader());
                SetMaterialColor(material, new Color(0.16f, 0.18f, 0.22f, 1.0f));
                renderer.material = material;
            }
        }

        private GameObject CreateTargetPrimitive(
            PrimitiveType primitiveType,
            string objectName,
            Transform parent,
            Vector3 localPosition,
            Quaternion localRotation,
            Vector3 localScale,
            Color color)
        {
            GameObject primitive = GameObject.CreatePrimitive(primitiveType);
            primitive.name = objectName;
            primitive.transform.SetParent(parent, false);
            primitive.transform.localPosition = localPosition;
            primitive.transform.localRotation = localRotation;
            primitive.transform.localScale = localScale;

            Collider collider = primitive.GetComponent<Collider>();
            if (collider != null)
            {
                collider.enabled = false;
            }

            Renderer renderer = primitive.GetComponent<Renderer>();
            if (renderer != null)
            {
                Material material = new Material(ResolveShowcaseShader());
                SetMaterialColor(material, color);
                renderer.material = material;
            }

            return primitive;
        }

        private void BuildColumnLabel(string text, float x)
        {
            if (LabelRoot == null || string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            GameObject label = new GameObject("Label_" + text);
            label.transform.SetParent(LabelRoot, false);
            label.transform.position = new Vector3(x, 2.4f, SkillTargetForwardOffset + 1.6f);
            label.transform.rotation = Quaternion.Euler(65.0f, 0.0f, 0.0f);

            TextMesh mesh = label.AddComponent<TextMesh>();
            mesh.text = text;
            mesh.anchor = TextAnchor.MiddleCenter;
            mesh.alignment = TextAlignment.Center;
            mesh.characterSize = 0.4f;
            mesh.fontSize = 36;
            mesh.color = new Color(0.08f, 0.16f, 0.27f, 1.0f);
        }

        private void ResolveMonsterPrefabs()
        {
            resolvedPrefabs.Clear();
            resolvedLabels.Clear();

            if (MonsterPrefabs != null && MonsterPrefabs.Length > 0)
            {
                for (int i = 0; i < MonsterPrefabs.Length; i++)
                {
                    AddResolvedPrefab(MonsterPrefabs[i], MonsterPrefabs[i] != null ? MonsterPrefabs[i].name : string.Empty);
                }
            }

            if (resolvedPrefabs.Count > 0)
            {
                return;
            }

#if UNITY_EDITOR
            if (MonsterPrefabPaths == null)
            {
                return;
            }

            for (int i = 0; i < MonsterPrefabPaths.Length; i++)
            {
                string path = MonsterPrefabPaths[i];
                if (string.IsNullOrWhiteSpace(path))
                {
                    continue;
                }

                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                AddResolvedPrefab(prefab, prefab != null ? prefab.name : path);
            }
#endif
        }

        private void AddResolvedPrefab(GameObject prefab, string label)
        {
            if (prefab == null)
            {
                return;
            }

            resolvedPrefabs.Add(prefab);
            resolvedLabels.Add(SanitizeLabel(label));
        }

        private static string SanitizeLabel(string label)
        {
            if (string.IsNullOrWhiteSpace(label))
            {
                return "Monster";
            }

            return label.Trim().Replace(' ', '_');
        }

        private Vector3 ResolveMonsterGroundPosition(Vector3 position)
        {
            return ProjectMonstersToGround
                ? GroundService.ProjectToGround(position, MonsterGroundHeight)
                : new Vector3(position.x, MonsterGroundHeight, position.z);
        }

        private Quaternion ResolveMonsterRotation(GameObject prefab)
        {
            float yaw = MonsterYaw;
            if (IsJumpMonsterPrefab(prefab))
            {
                yaw += JumpMonsterYawOffset;
            }

            return Quaternion.Euler(0.0f, yaw, 0.0f);
        }

        private void BuildColumnLayout(int columnCount)
        {
            resolvedColumnXs.Clear();
            if (columnCount <= 0)
            {
                return;
            }

            float x = 0.0f;
            resolvedColumnXs.Add(x);
            for (int i = 1; i < columnCount; i++)
            {
                x += ResolveColumnGap(resolvedPrefabs[i - 1], resolvedPrefabs[i]);
                resolvedColumnXs.Add(x);
            }

            if (!CenterColumns || resolvedColumnXs.Count <= 0)
            {
                return;
            }

            float centerOffset = (resolvedColumnXs[0] + resolvedColumnXs[resolvedColumnXs.Count - 1]) * 0.5f;
            for (int i = 0; i < resolvedColumnXs.Count; i++)
            {
                resolvedColumnXs[i] -= centerOffset;
            }
        }

        private float ResolveColumnGap(GameObject previousPrefab, GameObject currentPrefab)
        {
            if (IsBossPrefab(previousPrefab) || IsBossPrefab(currentPrefab))
            {
                return BossColumnSpacing;
            }

            if (IsElitePrefab(previousPrefab) || IsElitePrefab(currentPrefab))
            {
                return EliteColumnSpacing;
            }

            return ColumnSpacing;
        }

        private static bool IsElitePrefab(GameObject prefab)
        {
            if (prefab == null)
            {
                return false;
            }

            EnemyController controller = prefab.GetComponent<EnemyController>();
            if (controller != null && controller.Grade == EnemyGrade.Elite)
            {
                return true;
            }

            return prefab.name.Contains("_Elite_");
        }

        private static bool IsBossPrefab(GameObject prefab)
        {
            if (prefab == null)
            {
                return false;
            }

            EnemyController controller = prefab.GetComponent<EnemyController>();
            if (controller != null && controller.Grade == EnemyGrade.Boss)
            {
                return true;
            }

            return prefab.GetComponentInChildren<BossController>(true) != null || prefab.name.Contains("Boss");
        }

        private static bool IsJumpMonsterPrefab(GameObject prefab)
        {
            if (prefab == null)
            {
                return false;
            }

            return prefab.GetComponentInChildren<EnemyJump>(true) != null || prefab.name.Contains("SkeletonGolemJumper");
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

        private void NormalizeSettings()
        {
            ColumnSpacing = Mathf.Max(1.0f, ColumnSpacing);
            EliteColumnSpacing = Mathf.Max(1.0f, EliteColumnSpacing);
            BossColumnSpacing = Mathf.Max(1.0f, BossColumnSpacing);
            BossBackwardOffset = Mathf.Max(0.0f, BossBackwardOffset);
            MonsterGroundHeight = Mathf.Max(0.0f, MonsterGroundHeight);
            SkillTargetForwardOffset = Mathf.Max(0.5f, SkillTargetForwardOffset);
            SkillInterval = Mathf.Max(0.2f, SkillInterval);
            JumpDistance = Mathf.Max(0.2f, JumpDistance);
            JumpHeight = Mathf.Max(0.1f, JumpHeight);
            JumpDuration = Mathf.Max(0.1f, JumpDuration);
            SkillGroundHeight = Mathf.Max(0.0f, SkillGroundHeight);
            BossNexusForwardDistance = Mathf.Max(0.5f, BossNexusForwardDistance);
            BossNexusVisualRadius = Mathf.Max(0.2f, BossNexusVisualRadius);
        }

        private void EnsureRoots()
        {
            MonsterRoot = EnsureRoot(MonsterRoot, "ShowcaseMonsters");
            EffectRoot = EnsureRoot(EffectRoot, "ShowcaseMonsterEffects");
            TargetRoot = EnsureRoot(TargetRoot, "ShowcaseMonsterTargets");
            LabelRoot = EnsureRoot(LabelRoot, "ShowcaseMonsterLabels");
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
