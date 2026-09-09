using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace TeamProject01.Gameplay
{
    [RequireComponent(typeof(Camera))]
    public sealed class ShowcaseFreeCamera : MonoBehaviour
    {
        [Header("Move")]
        [Min(0.1f)] public float MoveSpeed = 14f;
        [Min(1f)] public float SprintMultiplier = 3f;
        [Min(0.1f)] public float VerticalSpeed = 10f;
        [Min(0.01f)] public float MouseSensitivity = 0.12f;
        public bool HoldRightMouseToLook = true;

        [Header("Auto Sweep")]
        public bool AutoSweepOnPlay;
        public Transform SweepStart;
        public Transform SweepEnd;
        public Transform SweepLookAt;
        [Min(0.1f)] public float SweepDuration = 12f;
        public bool LoopAutoSweep = true;

        [Header("Segment Sweep")]
        public bool UseSegmentSweepPath = true;
        public SegmentShowcaseDirector ShowcaseDirector;
        public MonsterShowcaseDirector MonsterShowcaseDirector;
        [Min(0.1f)] public float SegmentMoveSeconds = 1.35f;
        [Min(0f)] public float SegmentHoldSeconds = 0.55f;
        [Min(0f)] public float SegmentLookHeight = 1.6f;
        [Min(0.5f)] public float SegmentSweepCameraDistance = 3.5f;
        [Min(0f)] public float SegmentSweepExtraHeight = 5.5f;
        public bool UseFixedSegmentSweepViewDirection;
        public Vector3 SegmentSweepViewDirection = new Vector3(-1f, 0f, 1f);
        public Vector3 MonsterSweepViewDirection = new Vector3(-1f, 0f, 1f);
        public Vector3 SegmentSweepCameraOffset = new Vector3(0f, 10f, -12f);
        public bool LoopSegmentSweep;

        private Vector3 startPosition;
        private Quaternion startRotation;
        private float yaw;
        private float pitch;
        private bool autoSweepActive;
        private float sweepTimer;
        private readonly List<Vector3> segmentSweepFocusPoints = new List<Vector3>(32);
        private readonly List<Vector3> segmentSweepCameraPoints = new List<Vector3>(32);
        private bool segmentSweepActive;
        private bool segmentSweepHolding;
        private int segmentSweepIndex;
        private float segmentSweepTimer;
        private Vector3 segmentSweepFromPosition;
        private Quaternion segmentSweepFromRotation;
        private bool segmentSweepUsesMonsterDirector;

        private void Awake()
        {
            startPosition = transform.position;
            startRotation = transform.rotation;
            Vector3 euler = transform.rotation.eulerAngles;
            yaw = euler.y;
            pitch = NormalizePitch(euler.x);
            autoSweepActive = AutoSweepOnPlay;
        }

        private void Update()
        {
            if (WasPressedReset())
            {
                ResetPose();
            }

            if (WasPressedAutoSweepToggle())
            {
                if (autoSweepActive)
                {
                    StopAutoSweep();
                }
                else
                {
                    StartAutoSweep();
                }
            }

            if (autoSweepActive)
            {
                if (segmentSweepActive || (UseSegmentSweepPath && TryBeginSegmentSweep()))
                {
                    UpdateSegmentSweep();
                    return;
                }

                if (SweepStart != null && SweepEnd != null)
                {
                    UpdateAutoSweep();
                    return;
                }

                StopAutoSweep();
                return;
            }

            UpdateLook();
            UpdateMove();
        }

        private void UpdateLook()
        {
            if (HoldRightMouseToLook && !IsLookHeld())
            {
                return;
            }

            Vector2 delta = ReadMouseDelta();
            yaw += delta.x * MouseSensitivity;
            pitch = Mathf.Clamp(pitch - delta.y * MouseSensitivity, -85f, 85f);
            transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
        }

        private void UpdateMove()
        {
            Vector3 input = ReadMoveInput();
            float speed = MoveSpeed * (IsSprintHeld() ? SprintMultiplier : 1f);
            Vector3 worldMove = transform.TransformDirection(new Vector3(input.x, 0f, input.z)) * speed;
            worldMove += Vector3.up * input.y * VerticalSpeed;
            transform.position += worldMove * Time.deltaTime;
        }

        private void StartAutoSweep()
        {
            sweepTimer = 0f;
            autoSweepActive = true;
            segmentSweepActive = false;
            segmentSweepFocusPoints.Clear();
            segmentSweepCameraPoints.Clear();
            segmentSweepUsesMonsterDirector = false;

            if (UseSegmentSweepPath && TryBeginSegmentSweep())
            {
                return;
            }

            if (SweepStart == null || SweepEnd == null)
            {
                autoSweepActive = false;
            }
        }

        private void StopAutoSweep()
        {
            autoSweepActive = false;
            segmentSweepActive = false;
            segmentSweepHolding = false;
            segmentSweepUsesMonsterDirector = false;
            sweepTimer = 0f;
            segmentSweepTimer = 0f;
        }

        private bool TryBeginSegmentSweep()
        {
            if (!TryRefreshSegmentSweepPoints())
            {
                return false;
            }

            segmentSweepActive = true;
            BeginSegmentTransition(0);
            return true;
        }

        private bool TryRefreshSegmentSweepPoints()
        {
            segmentSweepFocusPoints.Clear();
            segmentSweepCameraPoints.Clear();

            if (!TryCopyShowcaseColumnCenters(segmentSweepFocusPoints))
            {
                return false;
            }

            float minX = segmentSweepFocusPoints[0].x;
            float maxX = minX;
            for (int i = 1; i < segmentSweepFocusPoints.Count; i++)
            {
                float x = segmentSweepFocusPoints[i].x;
                minX = Mathf.Min(minX, x);
                maxX = Mathf.Max(maxX, x);
            }

            for (int i = 0; i < segmentSweepFocusPoints.Count; i++)
            {
                Vector3 focus = segmentSweepFocusPoints[i] + Vector3.up * SegmentLookHeight;
                segmentSweepFocusPoints[i] = focus;
                segmentSweepCameraPoints.Add(ResolveSegmentSweepCameraPosition(focus, minX, maxX));
            }

            return segmentSweepCameraPoints.Count > 0;
        }

        private bool TryCopyShowcaseColumnCenters(List<Vector3> results)
        {
            SegmentShowcaseDirector segmentDirector = ResolveShowcaseDirector();
            if (segmentDirector != null && segmentDirector.CopyShowcaseColumnCenters(results) > 0)
            {
                segmentSweepUsesMonsterDirector = false;
                return true;
            }

            MonsterShowcaseDirector monsterDirector = ResolveMonsterShowcaseDirector();
            if (monsterDirector != null && monsterDirector.CopyShowcaseColumnCenters(results) > 0)
            {
                segmentSweepUsesMonsterDirector = true;
                return true;
            }

            segmentSweepUsesMonsterDirector = false;
            return false;
        }

        private SegmentShowcaseDirector ResolveShowcaseDirector()
        {
            if (ShowcaseDirector != null)
            {
                return ShowcaseDirector;
            }

            ShowcaseDirector = FindFirstObjectByType<SegmentShowcaseDirector>();
            return ShowcaseDirector;
        }

        private MonsterShowcaseDirector ResolveMonsterShowcaseDirector()
        {
            if (MonsterShowcaseDirector != null)
            {
                return MonsterShowcaseDirector;
            }

            MonsterShowcaseDirector = FindFirstObjectByType<MonsterShowcaseDirector>();
            return MonsterShowcaseDirector;
        }

        private Vector3 ResolveSegmentSweepCameraPosition(Vector3 focus, float minX, float maxX)
        {
            if (ShouldUseFixedSweepViewDirection())
            {
                return ApplySegmentSweepFraming(focus, focus + ResolveFixedSweepViewDirection());
            }

            if (SweepStart != null && SweepEnd != null)
            {
                float t = Mathf.Approximately(minX, maxX) ? 0.5f : Mathf.InverseLerp(minX, maxX, focus.x);
                return ApplySegmentSweepFraming(focus, Vector3.Lerp(SweepStart.position, SweepEnd.position, t));
            }

            return ApplySegmentSweepFraming(focus, focus + SegmentSweepCameraOffset);
        }

        private Vector3 ApplySegmentSweepFraming(Vector3 focus, Vector3 cameraPosition)
        {
            Vector3 viewDirection = cameraPosition - focus;
            viewDirection.y = 0f;
            if (viewDirection.sqrMagnitude <= 0.001f)
            {
                viewDirection = SegmentSweepCameraOffset;
                viewDirection.y = 0f;
            }

            viewDirection = viewDirection.sqrMagnitude > 0.001f ? viewDirection.normalized : Vector3.back;
            Vector3 framedPosition = focus + viewDirection * Mathf.Max(0.5f, SegmentSweepCameraDistance);
            framedPosition.y = focus.y + SegmentSweepExtraHeight;
            return framedPosition;
        }

        private void BeginSegmentTransition(int index)
        {
            segmentSweepIndex = Mathf.Clamp(index, 0, segmentSweepCameraPoints.Count - 1);
            segmentSweepTimer = 0f;
            segmentSweepHolding = false;
            segmentSweepFromPosition = transform.position;
            segmentSweepFromRotation = transform.rotation;

            if (ShouldLockSegmentSweepRotation())
            {
                Quaternion targetRotation = ResolveSegmentSweepRotation(segmentSweepIndex, segmentSweepCameraPoints[segmentSweepIndex]);
                segmentSweepFromRotation = targetRotation;
                transform.rotation = targetRotation;
                SyncYawPitchFromCurrentRotation();
            }
        }

        private void UpdateSegmentSweep()
        {
            if (segmentSweepCameraPoints.Count == 0 || segmentSweepFocusPoints.Count != segmentSweepCameraPoints.Count)
            {
                StopAutoSweep();
                return;
            }

            if (segmentSweepHolding)
            {
                segmentSweepTimer += Time.deltaTime;
                ApplySegmentSweepHoldLook();
                if (segmentSweepTimer >= SegmentHoldSeconds)
                {
                    AdvanceSegmentSweepTarget();
                }

                return;
            }

            segmentSweepTimer += Time.deltaTime;
            float duration = Mathf.Max(0.1f, SegmentMoveSeconds);
            float t = Mathf.Clamp01(segmentSweepTimer / duration);
            float smooth = SmootherStep(t);
            Vector3 targetPosition = segmentSweepCameraPoints[segmentSweepIndex];
            Quaternion targetRotation = ResolveSegmentSweepRotation(segmentSweepIndex, targetPosition);
            transform.position = Vector3.Lerp(segmentSweepFromPosition, targetPosition, smooth);
            transform.rotation = ShouldLockSegmentSweepRotation()
                ? targetRotation
                : Quaternion.Slerp(segmentSweepFromRotation, targetRotation, smooth);
            SyncYawPitchFromCurrentRotation();

            if (t >= 1f)
            {
                transform.SetPositionAndRotation(targetPosition, targetRotation);
                SyncYawPitchFromCurrentRotation();
                segmentSweepHolding = true;
                segmentSweepTimer = 0f;
            }
        }

        private void ApplySegmentSweepHoldLook()
        {
            Quaternion targetRotation = ResolveSegmentSweepRotation(segmentSweepIndex, transform.position);
            if (ShouldLockSegmentSweepRotation())
            {
                transform.rotation = targetRotation;
                SyncYawPitchFromCurrentRotation();
                return;
            }

            float lerp = 1f - Mathf.Exp(-10f * Time.deltaTime);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, lerp);
            SyncYawPitchFromCurrentRotation();
        }

        private void AdvanceSegmentSweepTarget()
        {
            int nextIndex = segmentSweepIndex + 1;
            if (nextIndex >= segmentSweepCameraPoints.Count)
            {
                if (LoopSegmentSweep)
                {
                    BeginSegmentTransition(0);
                }
                else
                {
                    StopAutoSweep();
                }

                return;
            }

            BeginSegmentTransition(nextIndex);
        }

        private Quaternion ResolveSegmentSweepRotation(int index, Vector3 cameraPosition)
        {
            Vector3 direction = segmentSweepFocusPoints[index] - cameraPosition;
            if (direction.sqrMagnitude <= 0.001f)
            {
                return transform.rotation;
            }

            return Quaternion.LookRotation(direction.normalized, Vector3.up);
        }

        private bool ShouldUseFixedSweepViewDirection()
        {
            return UseFixedSegmentSweepViewDirection || segmentSweepUsesMonsterDirector;
        }

        private bool ShouldLockSegmentSweepRotation()
        {
            return ShouldUseFixedSweepViewDirection() && ResolveFixedSweepViewDirection().sqrMagnitude > 0.001f;
        }

        private Vector3 ResolveFixedSweepViewDirection()
        {
            Vector3 direction = segmentSweepUsesMonsterDirector && !UseFixedSegmentSweepViewDirection
                ? MonsterSweepViewDirection
                : SegmentSweepViewDirection;

            direction.y = 0f;
            if (direction.sqrMagnitude > 0.001f)
            {
                return direction;
            }

            Vector3 fallback = SegmentSweepCameraOffset;
            fallback.y = 0f;
            return fallback.sqrMagnitude > 0.001f ? fallback : Vector3.back;
        }

        private void UpdateAutoSweep()
        {
            sweepTimer += Time.deltaTime;
            float duration = Mathf.Max(0.1f, SweepDuration);
            float t = Mathf.Clamp01(sweepTimer / duration);
            transform.position = Vector3.Lerp(SweepStart.position, SweepEnd.position, SmoothStep(t));

            if (SweepLookAt != null)
            {
                Vector3 direction = SweepLookAt.position - transform.position;
                if (direction.sqrMagnitude > 0.001f)
                {
                    transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
                    SyncYawPitchFromCurrentRotation();
                }
            }

            if (t >= 1f)
            {
                if (LoopAutoSweep)
                {
                    sweepTimer = 0f;
                }
                else
                {
                    autoSweepActive = false;
                }
            }
        }

        private void ResetPose()
        {
            transform.SetPositionAndRotation(startPosition, startRotation);
            SyncYawPitchFromCurrentRotation();
            StopAutoSweep();
        }

        private void SyncYawPitchFromCurrentRotation()
        {
            Vector3 euler = transform.rotation.eulerAngles;
            yaw = euler.y;
            pitch = NormalizePitch(euler.x);
        }

        private static float SmoothStep(float t)
        {
            return t * t * (3f - 2f * t);
        }

        private static float SmootherStep(float t)
        {
            return t * t * t * (t * (t * 6f - 15f) + 10f);
        }

        private static float NormalizePitch(float value)
        {
            return value > 180f ? value - 360f : value;
        }

        private Vector3 ReadMoveInput()
        {
            Vector3 input = Vector3.zero;
#if ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.aKey.isPressed) input.x -= 1f;
                if (keyboard.dKey.isPressed) input.x += 1f;
                if (keyboard.sKey.isPressed) input.z -= 1f;
                if (keyboard.wKey.isPressed) input.z += 1f;
                if (keyboard.qKey.isPressed) input.y -= 1f;
                if (keyboard.eKey.isPressed) input.y += 1f;
            }
#else
            input.x = Input.GetAxisRaw("Horizontal");
            input.z = Input.GetAxisRaw("Vertical");
            if (Input.GetKey(KeyCode.Q)) input.y -= 1f;
            if (Input.GetKey(KeyCode.E)) input.y += 1f;
#endif
            return input.sqrMagnitude > 1f ? input.normalized : input;
        }

        private Vector2 ReadMouseDelta()
        {
#if ENABLE_INPUT_SYSTEM
            Mouse mouse = Mouse.current;
            return mouse != null ? mouse.delta.ReadValue() : Vector2.zero;
#else
            return new Vector2(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y")) * 8f;
#endif
        }

        private bool IsLookHeld()
        {
#if ENABLE_INPUT_SYSTEM
            Mouse mouse = Mouse.current;
            return mouse != null && mouse.rightButton.isPressed;
#else
            return Input.GetMouseButton(1);
#endif
        }

        private bool IsSprintHeld()
        {
#if ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            return keyboard != null && (keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed);
#else
            return Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
#endif
        }

        private bool WasPressedReset()
        {
#if ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            return keyboard != null && keyboard.rKey.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.R);
#endif
        }

        private bool WasPressedAutoSweepToggle()
        {
#if ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            return keyboard != null && keyboard.fKey.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.F);
#endif
        }
    }
}
