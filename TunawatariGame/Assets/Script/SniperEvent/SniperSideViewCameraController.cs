using System.Collections;
using UnityEngine;

public class SniperSideViewCameraController : MonoBehaviour
{
    [Header("Camera")]
    // 実際に動かすカメラのTransformです。
    // Main Cameraを入れると、そのカメラを横視点へ移動できます。
    [SerializeField] private Transform cameraTransform;
    // 画角を演出用に変えたい場合に使います。
    // 未設定でもTransform移動だけで動きます。
    [SerializeField] private Camera targetCamera;

    [Header("Target")]
    // 横視点で見る対象です。
    // 基本はプレイヤーのSuit、またはプレイヤーの中心に置いた空オブジェクトを入れます。
    [SerializeField] private Transform lookTarget;
    // 視線を少し上げるための高さです。
    // 足元ではなく上半身を見ると、映画っぽい構図にしやすいです。
    [SerializeField] private float lookHeight = 1.4f;

    [Header("Side View Position")]
    // プレイヤーから見て横へどれだけ離れるかです。
    // 大きくすると引きの横カメラ、小さくすると近い横カメラになります。
    [SerializeField] private float sideDistance = 6f;
    // プレイヤーよりどれだけ上から見るかです。
    [SerializeField] private float height = 2.2f;
    // プレイヤーよりどれだけ後ろへずらすかです。
    // 0なら真横、少し負の値にすると斜め後ろ寄りになります。
    [SerializeField] private float backwardOffset = -0.8f;
    // trueならプレイヤーの右側、falseなら左側から見ます。
    [SerializeField] private bool useRightSide = true;

    [Header("Move Timing")]
    // 通常カメラから横視点へ移動する秒数です。
    [SerializeField] private float moveToSideDuration = 2.5f;
    // 横視点から元のカメラ位置へ戻る秒数です。
    [SerializeField] private float returnDuration = 1.5f;
    // 補間カーブです。
    // EaseInOutにすると、動き始めと止まり際がなめらかになって映画っぽく見えます。
    [SerializeField] private AnimationCurve moveCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Cinematic")]
    // 横視点中のカメラ画角です。
    // 小さめにすると望遠っぽく、映画的な圧縮感が出ます。
    [SerializeField] private float sideViewFieldOfView = 45f;
    // 横視点中に少しカメラを傾ける角度です。
    // 使いすぎると見づらいので、0〜3くらいが扱いやすいです。
    [SerializeField] private float dutchAngle = 2f;
    // trueならTime.timeScaleの影響を受けずにカメラが動きます。
    // スローモーション中でも演出時間を安定させたい時に使います。
    [SerializeField] private bool useUnscaledTime = true;

    [Header("Debug")]
    [SerializeField] private bool debugLog = true;

    private Vector3 originalPosition;
    private Quaternion originalRotation;
    private float originalFieldOfView;
    private Coroutine cameraRoutine;
    private bool hasOriginalCameraState;

    private void Reset()
    {
        AutoAssignCamera();
    }

    private void Awake()
    {
        if (cameraTransform == null || targetCamera == null)
        {
            AutoAssignCamera();
        }
    }

    public void MoveToSideView()
    {
        if (!CanMoveCamera())
        {
            return;
        }

        SaveOriginalCameraState();

        Vector3 targetPosition = CalculateSideViewPosition();
        Quaternion targetRotation = CalculateLookRotation(targetPosition, dutchAngle);

        StartCameraRoutine(targetPosition, targetRotation, sideViewFieldOfView, moveToSideDuration, "MoveToSideView", false);
    }

    public void ReturnToOriginalView()
    {
        if (!CanMoveCamera())
        {
            return;
        }

        if (!hasOriginalCameraState)
        {
            Log("ReturnToOriginalView skipped because original camera state was not saved.");
            return;
        }

        StartCameraRoutine(originalPosition, originalRotation, originalFieldOfView, returnDuration, "ReturnToOriginalView", true);
    }

    public void SnapToSideView()
    {
        if (!CanMoveCamera())
        {
            return;
        }

        SaveOriginalCameraState();

        Vector3 targetPosition = CalculateSideViewPosition();
        cameraTransform.position = targetPosition;
        cameraTransform.rotation = CalculateLookRotation(targetPosition, dutchAngle);

        if (targetCamera != null)
        {
            targetCamera.fieldOfView = sideViewFieldOfView;
        }
    }

    public void SnapBackToOriginalView()
    {
        if (!CanMoveCamera() || !hasOriginalCameraState)
        {
            return;
        }

        cameraTransform.position = originalPosition;
        cameraTransform.rotation = originalRotation;

        if (targetCamera != null)
        {
            targetCamera.fieldOfView = originalFieldOfView;
        }

        hasOriginalCameraState = false;
    }

    private void SaveOriginalCameraState()
    {
        if (hasOriginalCameraState)
        {
            return;
        }

        originalPosition = cameraTransform.position;
        originalRotation = cameraTransform.rotation;
        originalFieldOfView = targetCamera != null ? targetCamera.fieldOfView : 60f;
        hasOriginalCameraState = true;
    }

    private Vector3 CalculateSideViewPosition()
    {
        Vector3 targetPosition = GetLookTargetPosition();

        Vector3 targetRight = lookTarget != null ? lookTarget.right : Vector3.right;
        Vector3 targetBack = lookTarget != null ? -lookTarget.forward : Vector3.back;

        float sideSign = useRightSide ? 1f : -1f;
        return targetPosition +
               targetRight.normalized * sideDistance * sideSign +
               Vector3.up * height +
               targetBack.normalized * backwardOffset;
    }

    private Quaternion CalculateLookRotation(Vector3 fromPosition, float rollAngle)
    {
        Vector3 lookDirection = GetLookTargetPosition() - fromPosition;
        if (lookDirection.sqrMagnitude <= Mathf.Epsilon)
        {
            return cameraTransform.rotation;
        }

        Quaternion lookRotation = Quaternion.LookRotation(lookDirection.normalized, Vector3.up);
        return lookRotation * Quaternion.Euler(0f, 0f, rollAngle);
    }

    private Vector3 GetLookTargetPosition()
    {
        if (lookTarget == null)
        {
            return cameraTransform.position + cameraTransform.forward;
        }

        return lookTarget.position + Vector3.up * lookHeight;
    }

    private void StartCameraRoutine(Vector3 targetPosition, Quaternion targetRotation, float targetFieldOfView, float duration, string routineName, bool clearOriginalStateOnComplete)
    {
        if (cameraRoutine != null)
        {
            StopCoroutine(cameraRoutine);
        }

        cameraRoutine = StartCoroutine(MoveCameraRoutine(targetPosition, targetRotation, targetFieldOfView, duration, routineName, clearOriginalStateOnComplete));
    }

    private IEnumerator MoveCameraRoutine(Vector3 targetPosition, Quaternion targetRotation, float targetFieldOfView, float duration, string routineName, bool clearOriginalStateOnComplete)
    {
        Log($"{routineName} started.");

        Vector3 startPosition = cameraTransform.position;
        Quaternion startRotation = cameraTransform.rotation;
        float startFieldOfView = targetCamera != null ? targetCamera.fieldOfView : targetFieldOfView;
        float elapsed = 0f;

        if (duration <= 0f)
        {
            cameraTransform.position = targetPosition;
            cameraTransform.rotation = targetRotation;

            if (targetCamera != null)
            {
                targetCamera.fieldOfView = targetFieldOfView;
            }

            if (clearOriginalStateOnComplete)
            {
                hasOriginalCameraState = false;
            }

            cameraRoutine = null;
            yield break;
        }

        while (elapsed < duration)
        {
            elapsed += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;

            float t = Mathf.Clamp01(elapsed / duration);
            float curvedT = moveCurve != null ? moveCurve.Evaluate(t) : t;

            cameraTransform.position = Vector3.Lerp(startPosition, targetPosition, curvedT);
            cameraTransform.rotation = Quaternion.Slerp(startRotation, targetRotation, curvedT);

            if (targetCamera != null)
            {
                targetCamera.fieldOfView = Mathf.Lerp(startFieldOfView, targetFieldOfView, curvedT);
            }

            yield return null;
        }

        cameraTransform.position = targetPosition;
        cameraTransform.rotation = targetRotation;

        if (targetCamera != null)
        {
            targetCamera.fieldOfView = targetFieldOfView;
        }

        if (clearOriginalStateOnComplete)
        {
            hasOriginalCameraState = false;
        }

        cameraRoutine = null;
        Log($"{routineName} finished.");
    }

    private bool CanMoveCamera()
    {
        if (cameraTransform == null)
        {
            Log("Camera Transform is not assigned.");
            return false;
        }

        return true;
    }

    private void AutoAssignCamera()
    {
        Camera mainCamera = Camera.main;
        if (mainCamera == null)
        {
            return;
        }

        cameraTransform = mainCamera.transform;
        targetCamera = mainCamera;
    }

    private void Log(string message)
    {
        if (!debugLog)
        {
            return;
        }

        Debug.Log($"[SniperSideViewCameraController] {message}", this);
    }
}
