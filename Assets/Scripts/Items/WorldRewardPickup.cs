using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace TeamProject01.Gameplay
{
    public sealed class WorldRewardPickup : MonoBehaviour // 필드 경험치/골드 픽업
    {
        private const string SpecialDropIdleVfxResourcePath = "RewardPickups/VFX_Loot_iddle"; // 다이아/선택권 대기 VFX
        private const string SpecialDropIdleVfxEditorPath = "Assets/ThirdParty/02_Monster/Casual RPG VFX/Casual RPG VFX/Prefabs/Loot/Loot_iddle.prefab";
        private static readonly List<WorldRewardPickup> ActivePickups = new List<WorldRewardPickup>(256);
        private static readonly Quaternion GoldModelUprightRotation = Quaternion.Euler(90f, 0f, 0f);
        private static readonly Color GoldOrbColor = new Color(1f, 0.78f, 0.12f, 1f);
        private static readonly Color GoldOrbEmissionColor = new Color(0.75f, 0.38f, 0.04f, 1f);
        private static readonly Color ExperienceOrbColor = new Color(0.18f, 1f, 0.36f, 1f);
        private static readonly Color ExperienceOrbEmissionColor = new Color(0.04f, 0.65f, 0.2f, 1f);
        private static readonly Color DiamondOrbColor = new Color(0.35f, 0.9f, 1f, 1f);
        private static readonly Color DiamondOrbEmissionColor = new Color(0.05f, 0.52f, 1f, 1f);
        public RewardPickupKind Kind = RewardPickupKind.Experience; // 보상 종류
        [Min(0)] public int Amount = 1; // 보상 수치
        public Transform ModelRoot; // 회전/둥둥 표시 루트
        public Transform IdleVfxRoot; // 대기 VFX 자리
        public Transform CollectVfxRoot; // 획득 VFX 자리
        public Transform MagnetTrailVfxRoot; // 자석 흡수 VFX 자리

        [Header("Special Drop VFX")]
        public GameObject SpecialDropIdleVfxPrefab; // Loot_iddle, 비어 있으면 Resources fallback
        [Min(0.01f)] public float SpecialDropIdleVfxScale = 1f; // 대기 VFX 크기
        public Vector3 SpecialDropIdleVfxLocalOffset = Vector3.zero; // VFX 루트 기준 보정

        [Header("Motion")]
        [Min(0f)] public float HoverHeight = 0.62f;
        [Min(0f)] public float HoverAmplitude = 0.12f;
        [Min(0f)] public float HoverSpeed = 2.4f;
        [Min(0f)] public float RotationSpeed = 100f;
        [Min(0f)] public float GroundHeightOffset = 0.02f;
        [Min(0f)] public float DropPopHeight = 1.15f;
        [Min(0.05f)] public float DropPopDuration = 0.42f;

        [Header("Collect")]
        [Min(0.05f)] public float CollectDistance = 0.55f;
        public bool SubmitRewardOnCollect = true;

        private int enemyId;
        private bool collected;
        private bool attractedThisFrame;
        private bool isDropping;
        private float dropTimer;
        private float hoverPhase;
        private Vector3 velocity;
        private Vector3 dropStartPosition;
        private Vector3 dropLandingPosition;
        private MaterialPropertyBlock visualPropertyBlock;
        private RewardDropService poolOwner;
        private WorldRewardPickup poolSourcePrefab;
        private GameObject specialDropIdleVfxInstance; // 다이아/선택권 대기 VFX 인스턴스
        private bool debugAttractLogged; // 시작 선택권 원인 추적 로그 중복 방지
        private static GameObject cachedSpecialDropIdleVfxPrefab; // 기본 Loot_iddle 캐시
        private static bool specialDropIdleVfxLoadAttempted; // 로드 중복 방지
        private static bool specialDropIdleVfxMissingWarningLogged; // 누락 로그 1회

        public static int ActiveCount => ActivePickups.Count; // 디버그용 활성 수

        private void OnEnable()
        {
            if (!ActivePickups.Contains(this))
            {
                ActivePickups.Add(this); // 흡수 검색 등록
            }

            hoverPhase = Random.Range(0f, Mathf.PI * 2f); // 같은 타이밍으로 흔들리지 않게 분산
            SetVfxRootActive(IdleVfxRoot, true);
            SetVfxRootActive(CollectVfxRoot, false);
            SetVfxRootActive(MagnetTrailVfxRoot, false);
            ApplyKindVisualPose();
            RefreshSpecialDropIdleVfx(); // 다이아/선택권 idle VFX
        }

        private void OnDisable()
        {
            ActivePickups.Remove(this); // 비활성 픽업 제거
        }

        private void Update()
        {
            if (collected)
            {
                return;
            }

            UpdateVisualMotion();
            SetVfxRootActive(MagnetTrailVfxRoot, attractedThisFrame);
            attractedThisFrame = false;
        }

        public void Configure(RewardPickupKind kind, int amount, int sourceEnemyId, Vector3 position)
        {
            Configure(kind, amount, sourceEnemyId, position, position);
        }

        public void Configure(RewardPickupKind kind, int amount, int sourceEnemyId, Vector3 landingPosition, Vector3 spawnPosition)
        {
            Kind = kind;
            Amount = Mathf.Max(0, amount);
            enemyId = sourceEnemyId;
            collected = false;
            attractedThisFrame = false;
            debugAttractLogged = false;
            velocity = Vector3.zero;
            hoverPhase = Random.Range(0f, Mathf.PI * 2f);
            if (StartingSegmentChoiceTicketDebug.ShouldLog && kind == RewardPickupKind.SegmentChoiceTicket)
            {
                StartingSegmentChoiceTicketDebug.Log(
                    $"WorldRewardPickup.Configure name={name}, amount={Amount}, inputSpawn={StartingSegmentChoiceTicketDebug.Format(spawnPosition)}, inputLanding={StartingSegmentChoiceTicketDebug.Format(landingPosition)}, "
                    + $"activeInHierarchy={gameObject.activeInHierarchy}, poolOwner={(poolOwner != null ? poolOwner.name : "null")}",
                    this);
            }

            BeginDropMotion(spawnPosition, landingPosition);
            SetVfxRootActive(IdleVfxRoot, true);
            SetVfxRootActive(CollectVfxRoot, false);
            SetVfxRootActive(MagnetTrailVfxRoot, false);
            ApplyKindVisualPose();
            RefreshSpecialDropIdleVfx(); // 풀링 재사용 시 종류 반영
        }

        public static bool AttractInRange(Vector3 center, float radius, float pullStrength, float maxSpeed, float collectDistance, float deltaTime)
        {
            if (radius <= 0f || deltaTime <= 0f)
            {
                return false;
            }

            bool hasCandidate = false;
            float safeRadius = Mathf.Max(0.05f, radius);
            float safePullStrength = Mathf.Max(0f, pullStrength);
            float safeMaxSpeed = Mathf.Max(0.1f, maxSpeed);
            float safeCollectDistance = Mathf.Max(0.05f, collectDistance);

            for (int i = ActivePickups.Count - 1; i >= 0; i--)
            {
                WorldRewardPickup pickup = ActivePickups[i];
                if (pickup == null)
                {
                    ActivePickups.RemoveAt(i);
                    continue;
                }

                if (pickup.TryAttract(center, safeRadius, safePullStrength, safeMaxSpeed, safeCollectDistance, deltaTime))
                {
                    hasCandidate = true;
                }
            }

            return hasCandidate;
        }

        public static void CollectActiveInRange(Vector3 center, float radius, List<WorldRewardPickup> results, System.Func<WorldRewardPickup, bool> filter = null)
        {
            if (results == null)
            {
                return;
            }

            results.Clear();

            if (radius <= 0f)
            {
                return;
            }

            float radiusSqr = radius * radius;
            for (int i = ActivePickups.Count - 1; i >= 0; i--)
            {
                WorldRewardPickup pickup = ActivePickups[i];
                if (pickup == null)
                {
                    ActivePickups.RemoveAt(i);
                    continue;
                }

                if (!pickup.isActiveAndEnabled || pickup.collected || pickup.Amount <= 0)
                {
                    continue;
                }

                if (filter != null && !filter(pickup))
                {
                    continue;
                }

                Vector3 offset = pickup.transform.position - center;
                offset.y = 0f;
                if (offset.sqrMagnitude <= radiusSqr)
                {
                    results.Add(pickup);
                }
            }
        }

        private bool TryAttract(Vector3 center, float radius, float pullStrength, float maxSpeed, float collectDistance, float deltaTime)
        {
            if (collected || Amount <= 0 || isDropping)
            {
                return false;
            }

            Vector3 target = center;
            target.y = transform.position.y; // 루트는 바닥면을 따라 이동
            Vector3 offset = target - transform.position;
            offset.y = 0f;

            float radiusSqr = radius * radius;
            float distanceSqr = offset.sqrMagnitude;
            if (distanceSqr > radiusSqr)
            {
                return false;
            }

            attractedThisFrame = true;
            if (StartingSegmentChoiceTicketDebug.ShouldLog && Kind == RewardPickupKind.SegmentChoiceTicket && !debugAttractLogged)
            {
                debugAttractLogged = true;
                StartingSegmentChoiceTicketDebug.Log(
                    $"WorldRewardPickup.AttractStart name={name}, pickup={StartingSegmentChoiceTicketDebug.Format(transform.position)}, center={StartingSegmentChoiceTicketDebug.Format(center)}, "
                    + $"distance={Mathf.Sqrt(distanceSqr):0.00}, radius={radius:0.00}, pullStrength={pullStrength:0.00}, maxSpeed={maxSpeed:0.00}, collectDistance={collectDistance:0.00}",
                    this);
            }

            float finalCollectDistance = Mathf.Max(CollectDistance, collectDistance);
            if (distanceSqr <= finalCollectDistance * finalCollectDistance)
            {
                if (StartingSegmentChoiceTicketDebug.ShouldLog && Kind == RewardPickupKind.SegmentChoiceTicket)
                {
                    StartingSegmentChoiceTicketDebug.Log(
                        $"WorldRewardPickup.AttractCollectRange name={name}, pickup={StartingSegmentChoiceTicketDebug.Format(transform.position)}, center={StartingSegmentChoiceTicketDebug.Format(center)}, "
                        + $"distance={Mathf.Sqrt(distanceSqr):0.00}, finalCollectDistance={finalCollectDistance:0.00}",
                        this);
                }

                TryCollect();
                return true;
            }

            float distance = Mathf.Sqrt(distanceSqr);
            if (distance <= 0.001f)
            {
                return true;
            }

            Vector3 direction = offset / distance;
            float rangeFactor = 1f - Mathf.Clamp01(distance / radius);
            float targetSpeed = Mathf.Min(maxSpeed, velocity.magnitude + pullStrength * (0.45f + rangeFactor) * deltaTime);
            velocity = Vector3.Lerp(velocity, direction * targetSpeed, 1f - Mathf.Exp(-12f * deltaTime));

            Vector3 nextPosition = transform.position + velocity * deltaTime;
            nextPosition.y = transform.position.y;
            transform.position = nextPosition;
            return true;
        }

        private void TryCollect()
        {
            if (collected)
            {
                return;
            }

            if (Kind == RewardPickupKind.SegmentChoiceTicket)
            {
                if (StartingSegmentChoiceTicketDebug.ShouldLog)
                {
                    StartingSegmentChoiceTicketDebug.Log(
                        $"WorldRewardPickup.TryCollectTicket name={name}, position={StartingSegmentChoiceTicketDebug.Format(transform.position)}, amount={Amount}",
                        this);
                }

                if (!TryOpenSegmentChoiceTicket())
                {
                    if (StartingSegmentChoiceTicketDebug.ShouldLog)
                    {
                        StartingSegmentChoiceTicketDebug.Log(
                            $"WorldRewardPickup.TryCollectTicketBlocked name={name}, reason=CardUIOpenFailed, position={StartingSegmentChoiceTicketDebug.Format(transform.position)}",
                            this);
                    }

                    return;
                }

                DamageFloatingSpawner.SpawnRewardGain(Kind, Amount, ResolveRewardFloatingFallbackPosition());
                CompleteCollect();
                return;
            }

            RewardData reward = ResolveRewardData(); // 픽업 종류별 보상

            if (!SubmitRewardOnCollect)
            {
                DamageFloatingSpawner.SpawnRewardGain(Kind, Amount, ResolveRewardFloatingFallbackPosition());
                CompleteCollect();
                return;
            }

            if (!RewardGateway.SubmitReward(reward))
            {
                return;
            }

            DamageFloatingSpawner.SpawnRewardGain(Kind, Amount, ResolveRewardFloatingFallbackPosition());
            CompleteCollect();
        }

        private bool TryOpenSegmentChoiceTicket()
        {
            CardUI cardUi = FindFirstObjectByType<CardUI>();
            if (cardUi == null)
            {
                Debug.LogWarning("[WorldRewardPickup] CardUI가 없어 세그먼트 선택권을 열 수 없습니다.", this);
                return false;
            }

            bool opened = cardUi.OpenSegmentChoiceTicket(Mathf.Max(1, Amount));
            if (StartingSegmentChoiceTicketDebug.ShouldLog && Kind == RewardPickupKind.SegmentChoiceTicket)
            {
                StartingSegmentChoiceTicketDebug.Log(
                    $"WorldRewardPickup.CardUIOpen name={name}, cardUi={cardUi.name}, opened={opened}, amount={Mathf.Max(1, Amount)}",
                    cardUi);
            }

            return opened;
        }

        private void CompleteCollect()
        {
            if (StartingSegmentChoiceTicketDebug.ShouldLog && Kind == RewardPickupKind.SegmentChoiceTicket)
            {
                StartingSegmentChoiceTicketDebug.Log(
                    $"WorldRewardPickup.CompleteCollect name={name}, position={StartingSegmentChoiceTicketDebug.Format(transform.position)}, poolOwner={(poolOwner != null ? poolOwner.name : "null")}",
                    this);
            }

            collected = true;
            SetVfxRootActive(IdleVfxRoot, false);
            SetSpecialDropIdleVfxActive(false); // 수집 후 idle VFX 숨김
            SetVfxRootActive(CollectVfxRoot, true);
            PlayCollectSfx();
            PlayCollectVfx(); // 획득 VFX
            if (poolOwner != null && poolOwner.ReleasePickup(this, poolSourcePrefab))
            {
                return;
            }

            Destroy(gameObject);
        }

        internal void AttachPoolOwner(RewardDropService owner, WorldRewardPickup sourcePrefab)
        {
            poolOwner = owner;
            poolSourcePrefab = sourcePrefab;
        }

        internal void ResetForPool()
        {
            collected = false;
            attractedThisFrame = false;
            isDropping = false;
            dropTimer = 0f;
            velocity = Vector3.zero;
            SetSpecialDropIdleVfxActive(false);
            SetVfxRootActive(IdleVfxRoot, false);
            SetVfxRootActive(CollectVfxRoot, false);
            SetVfxRootActive(MagnetTrailVfxRoot, false);
        }

        private void UpdateVisualMotion()
        {
            UpdateDropMotion();
            Transform visual = ModelRoot != null ? ModelRoot : transform;

            if (ModelRoot != null)
            {
                float bob = HoverHeight + Mathf.Sin(Time.time * HoverSpeed + hoverPhase) * HoverAmplitude;
                ModelRoot.localPosition = Vector3.up * Mathf.Max(0f, bob);
            }

            if (ShouldRotateVisual())
            {
                visual.Rotate(0f, RotationSpeed * Time.deltaTime, 0f, Space.Self);
            }
        }

        private bool ShouldRotateVisual()
        {
            return Kind != RewardPickupKind.Experience && RotationSpeed > 0f;
        }

        private RewardData ResolveRewardData()
        {
            if (Kind == RewardPickupKind.Experience)
            {
                return RewardData.Create(Amount, 0, enemyId, transform.position); // 경험치
            }

            if (Kind == RewardPickupKind.Diamond)
            {
                return RewardData.CreateDiamond(Amount, enemyId, transform.position); // 다이아
            }

            return RewardData.Create(0, Amount, enemyId, transform.position); // 골드
        }

        private void BeginDropMotion(Vector3 spawnPosition, Vector3 landingPosition)
        {
            if (Kind == RewardPickupKind.SegmentChoiceTicket)
            {
                dropStartPosition = GroundService.ProjectToGroundForStartSegmentTicket(spawnPosition, GroundHeightOffset, "WorldRewardPickup.DropStart", this);
                dropLandingPosition = GroundService.ProjectToGroundForStartSegmentTicket(landingPosition, GroundHeightOffset, "WorldRewardPickup.DropLanding", this);
            }
            else
            {
                dropStartPosition = GroundService.ProjectToGround(spawnPosition, GroundHeightOffset);
                dropLandingPosition = GroundService.ProjectToGround(landingPosition, GroundHeightOffset);
            }
            dropTimer = 0f;
            isDropping = DropPopHeight > 0f && DropPopDuration > 0f;
            transform.position = dropStartPosition;
            if (StartingSegmentChoiceTicketDebug.ShouldLog && Kind == RewardPickupKind.SegmentChoiceTicket)
            {
                StartingSegmentChoiceTicketDebug.Log(
                    $"WorldRewardPickup.BeginDrop name={name}, projectedSpawn={StartingSegmentChoiceTicketDebug.Format(dropStartPosition)}, projectedLanding={StartingSegmentChoiceTicketDebug.Format(dropLandingPosition)}, "
                    + $"dropDelta={StartingSegmentChoiceTicketDebug.Format(dropLandingPosition - dropStartPosition)}, dropPopHeight={DropPopHeight:0.00}, dropDuration={DropPopDuration:0.00}, isDropping={isDropping}",
                    this);
            }
        }

        private void UpdateDropMotion()
        {
            if (!isDropping)
            {
                return;
            }

            dropTimer += Time.deltaTime;
            float progress = Mathf.Clamp01(dropTimer / Mathf.Max(0.05f, DropPopDuration));
            float smoothProgress = progress * progress * (3f - 2f * progress);
            Vector3 position = Vector3.Lerp(dropStartPosition, dropLandingPosition, smoothProgress);
            position.y += Mathf.Sin(progress * Mathf.PI) * DropPopHeight;
            transform.position = position;

            if (progress >= 1f)
            {
                isDropping = false;
                velocity = Vector3.zero;
                transform.position = dropLandingPosition;
                if (StartingSegmentChoiceTicketDebug.ShouldLog && Kind == RewardPickupKind.SegmentChoiceTicket)
                {
                    StartingSegmentChoiceTicketDebug.Log(
                        $"WorldRewardPickup.DropComplete name={name}, landing={StartingSegmentChoiceTicketDebug.Format(transform.position)}, elapsed={dropTimer:0.00}",
                        this);
                }
            }
        }

        private void ApplyKindVisualPose()
        {
            if (Kind == RewardPickupKind.Experience)
            {
                ApplyExperienceVisualColor();
                return;
            }

            if (Kind == RewardPickupKind.SegmentChoiceTicket)
            {
                ApplySegmentTicketVisualColor();
                return;
            }

            if (Kind == RewardPickupKind.Diamond)
            {
                ApplyDiamondVisualColor();
                return;
            }

            if (Kind != RewardPickupKind.Gold || ModelRoot == null)
            {
                if (Kind == RewardPickupKind.Gold)
                {
                    ApplyGoldVisualColor();
                }

                return;
            }

            ApplyGoldVisualColor();
            Transform model = FindPrimaryModelTransform();
            if (model != null)
            {
                model.localRotation = GoldModelUprightRotation;
            }
        }

        private Transform FindPrimaryModelTransform()
        {
            MeshRenderer renderer = ModelRoot.GetComponentInChildren<MeshRenderer>(true);
            return renderer != null && renderer.transform != ModelRoot ? renderer.transform : ModelRoot;
        }

        private void ApplyExperienceVisualColor()
        {
            ApplyVisualColor(ExperienceOrbColor, ExperienceOrbEmissionColor);
        }

        private void ApplyGoldVisualColor()
        {
            ApplyVisualColor(GoldOrbColor, GoldOrbEmissionColor);
        }

        private void ApplyDiamondVisualColor()
        {
            ApplyVisualColor(DiamondOrbColor, DiamondOrbEmissionColor);
        }

        private void ApplySegmentTicketVisualColor()
        {
            ClearVisualColorOverride(); // 선택권은 모델 텍스처 색을 그대로 사용
        }

        private void ApplyVisualColor(Color baseColor, Color emissionColor)
        {
            MeshRenderer[] renderers = ModelRoot != null
                ? ModelRoot.GetComponentsInChildren<MeshRenderer>(true)
                : GetComponentsInChildren<MeshRenderer>(true);

            if (renderers == null || renderers.Length == 0)
            {
                return;
            }

            visualPropertyBlock ??= new MaterialPropertyBlock();
            for (int i = 0; i < renderers.Length; i++)
            {
                MeshRenderer renderer = renderers[i];
                if (renderer == null)
                {
                    continue;
                }

                renderer.GetPropertyBlock(visualPropertyBlock);
                visualPropertyBlock.SetColor("_Color", baseColor);
                visualPropertyBlock.SetColor("_BaseColor", baseColor);
                visualPropertyBlock.SetColor("_EmissionColor", emissionColor);
                renderer.SetPropertyBlock(visualPropertyBlock);
            }
        }

        private void ClearVisualColorOverride()
        {
            MeshRenderer[] renderers = ModelRoot != null
                ? ModelRoot.GetComponentsInChildren<MeshRenderer>(true)
                : GetComponentsInChildren<MeshRenderer>(true);

            if (renderers == null || renderers.Length == 0)
            {
                return;
            }

            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null)
                {
                    renderers[i].SetPropertyBlock(null); // 풀링 재사용 시 이전 색상 제거
                }
            }
        }

        private Vector3 ResolveRewardFloatingFallbackPosition()
        {
            return transform.position + Vector3.up * 1.2f;
        }

        private Vector3 ResolveCollectVfxPosition()
        {
            return CollectVfxRoot != null ? CollectVfxRoot.position : transform.position + Vector3.up * HoverHeight;
        }

        private void PlayCollectVfx() // 루팅 위치 기준 획득 VFX
        {
            if (PlayerPickupInteractor.TryResolveLootingCenter(out Transform lootingCenter))
            {
                RewardPickupCollectVfxPlayer.PlayFollowing(lootingCenter, Vector3.zero); // 재생 중 Looting 추적
                return;
            }

            RewardPickupCollectVfxPlayer.Play(ResolveCollectVfxPosition()); // fallback 월드 재생
        }

        private void PlayCollectSfx()
        {
            Transform root = ResolvePlayerSfxRoot();
            Vector3 fallbackPosition = root != null ? root.position : transform.position;
            PlayCollectCue(GameplaySfxCue.Pickup, root, fallbackPosition);
            if (ShouldUseGoodPickupSfx())
            {
                PlayCollectCue(GameplaySfxCue.GoodPickup, root, fallbackPosition);
            }
        }

        private static bool PlayCollectCue(GameplaySfxCue cue, Transform root, Vector3 fallbackPosition)
        {
            if (root != null)
            {
                if (GameplaySfxEmitter.TryPlay(root, cue))
                {
                    return true;
                }

                if (GameplaySfxEmitter.TryPlayCatalogAt(cue, root.position))
                {
                    return true;
                }
            }

            return GameplaySfxEmitter.TryPlayCatalogAt(cue, fallbackPosition);
        }

        private bool ShouldUseGoodPickupSfx()
        {
            return Kind == RewardPickupKind.Diamond || Kind == RewardPickupKind.SegmentChoiceTicket;
        }

        private static Transform ResolvePlayerSfxRoot()
        {
            if (MonsterInteractionApi.TryGetConvoyTarget(out Transform convoyTarget) && convoyTarget != null)
            {
                ConvoyController convoy = convoyTarget.GetComponent<ConvoyController>();
                if (convoy != null && convoy.HeadVisual != null && convoy.HeadVisual.gameObject.activeInHierarchy)
                {
                    return convoy.HeadVisual;
                }

                return convoyTarget;
            }

            PlayerPickupInteractor interactor = FindFirstObjectByType<PlayerPickupInteractor>();
            return interactor != null ? interactor.transform : null;
        }

        private static void SetVfxRootActive(Transform root, bool active)
        {
            if (root != null && root.gameObject.activeSelf != active)
            {
                root.gameObject.SetActive(active);
            }
        }

        private void RefreshSpecialDropIdleVfx() // 다이아/선택권 대기 VFX 갱신
        {
            if (!ShouldUseSpecialDropIdleVfx())
            {
                SetSpecialDropIdleVfxActive(false);
                return;
            }

            EnsureSpecialDropIdleVfx();
            SetSpecialDropIdleVfxActive(true);
            PlaySpecialDropIdleParticles();
        }

        private bool ShouldUseSpecialDropIdleVfx() // Loot_iddle 적용 대상
        {
            return Kind == RewardPickupKind.Diamond || Kind == RewardPickupKind.SegmentChoiceTicket;
        }

        private void EnsureSpecialDropIdleVfx() // VFX 인스턴스 보장
        {
            if (specialDropIdleVfxInstance != null)
            {
                ApplySpecialDropIdleVfxTransform();
                return;
            }

            GameObject prefab = ResolveSpecialDropIdleVfxPrefab();
            if (prefab == null)
            {
                LogMissingSpecialDropIdleVfxOnce();
                return;
            }

            Transform parent = IdleVfxRoot != null ? IdleVfxRoot : transform;
            specialDropIdleVfxInstance = Instantiate(prefab, parent);
            specialDropIdleVfxInstance.name = "Loot_iddle_DropVFX";
            ApplySpecialDropIdleVfxTransform();
            DisableRuntimeColliders(specialDropIdleVfxInstance);
        }

        private void ApplySpecialDropIdleVfxTransform() // VFX 위치/크기 보정
        {
            if (specialDropIdleVfxInstance == null)
            {
                return;
            }

            Transform vfxTransform = specialDropIdleVfxInstance.transform;
            vfxTransform.localPosition = SpecialDropIdleVfxLocalOffset;
            vfxTransform.localRotation = Quaternion.identity;
            vfxTransform.localScale = Vector3.one * Mathf.Max(0.01f, SpecialDropIdleVfxScale);
        }

        private GameObject ResolveSpecialDropIdleVfxPrefab() // Loot_iddle 프리팹 찾기
        {
            if (SpecialDropIdleVfxPrefab != null)
            {
                return SpecialDropIdleVfxPrefab; // 인스펙터 우선
            }

            if (cachedSpecialDropIdleVfxPrefab != null)
            {
                return cachedSpecialDropIdleVfxPrefab;
            }

            if (specialDropIdleVfxLoadAttempted)
            {
                return null;
            }

            specialDropIdleVfxLoadAttempted = true;
            cachedSpecialDropIdleVfxPrefab = Resources.Load<GameObject>(SpecialDropIdleVfxResourcePath); // 빌드용

#if UNITY_EDITOR
            if (cachedSpecialDropIdleVfxPrefab == null)
            {
                cachedSpecialDropIdleVfxPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SpecialDropIdleVfxEditorPath); // 에디터 fallback
            }
#endif

            return cachedSpecialDropIdleVfxPrefab;
        }

        private void SetSpecialDropIdleVfxActive(bool active) // VFX 표시 전환
        {
            if (specialDropIdleVfxInstance != null && specialDropIdleVfxInstance.activeSelf != active)
            {
                specialDropIdleVfxInstance.SetActive(active);
            }
        }

        private void PlaySpecialDropIdleParticles() // 루프 파티클 재시작
        {
            if (specialDropIdleVfxInstance == null)
            {
                return;
            }

            ParticleSystem[] particles = specialDropIdleVfxInstance.GetComponentsInChildren<ParticleSystem>(true);
            for (int i = 0; i < particles.Length; i++)
            {
                particles[i].Play(true);
            }
        }

        private static void DisableRuntimeColliders(GameObject root) // VFX 충돌 방지
        {
            if (root == null)
            {
                return;
            }

            Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                colliders[i].enabled = false;
            }
        }

        private void LogMissingSpecialDropIdleVfxOnce() // 누락 로그
        {
            if (specialDropIdleVfxMissingWarningLogged)
            {
                return;
            }

            specialDropIdleVfxMissingWarningLogged = true;
            Debug.LogWarning("[WorldRewardPickup] RewardPickups/VFX_Loot_iddle prefab을 찾지 못했습니다.", this);
        }
    }

    internal static class StartingSegmentChoiceTicketDebug
    {
        public const string Prefix = "[StartSegmentTicketDebug]";

        // Player Settings > Scripting Define Symbols에 START_SEGMENT_TICKET_DEBUG 추가 시 활성화됩니다.
        public static bool ShouldLog
        {
            get
            {
#if START_SEGMENT_TICKET_DEBUG
                return Application.isEditor || Debug.isDebugBuild;
#else
                return false;
#endif
            }
        }

        [System.Diagnostics.Conditional("START_SEGMENT_TICKET_DEBUG")]
        public static void Log(string message, Object context = null)
        {
            if (!ShouldLog)
            {
                return;
            }

            Debug.Log($"{Prefix} {message}", context);
        }

        public static string SceneName => UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;

        public static string Format(Vector3 value)
        {
            return $"({value.x:0.00}, {value.y:0.00}, {value.z:0.00})";
        }
    }
}
