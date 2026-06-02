using System.Collections;
using UnityEngine;
using UnityEngine.Events;

public class SniperMatrixAvoidController : MonoBehaviour
{
    [Header("References")]
    // 成功/失敗をスナイパーイベント全体へ知らせるためのイベントマネージャーです。
    [SerializeField] private SniperEventManager eventManager;
    // 動かすカメラです。未設定ならMainCameraを自動で探します。
    [SerializeField] private Transform cameraTransform;
    [SerializeField] private Camera targetCamera;
    // カメラが見る対象です。プレイヤーの胸あたりに置いた空オブジェクトが扱いやすいです。
    [SerializeField] private Transform lookTarget;
    // 重心を表すTransformです。プレイヤーの上半身や腰の補助オブジェクトを入れます。
    // 未設定でも判定は動きますが、設定すると「重心が上がる」見た目を作れます。
    [SerializeField] private Transform centerOfGravityTransform;

    [Header("Camera")]
    // マトリックス風の斜め横カメラへ移動する時間です。
    [SerializeField] private float moveCameraDuration = 1.2f;
    // プレイヤー横方向への距離です。
    [SerializeField] private float sideDistance = 5.5f;
    // プレイヤー後ろ方向への距離です。斜め横にするため少し後ろへ置きます。
    [SerializeField] private float backDistance = 2f;
    // カメラの高さです。
    [SerializeField] private float cameraHeight = 1.8f;
    // 見る位置の高さです。
    [SerializeField] private float lookHeight = 1.2f;
    // カメラを少し傾ける角度です。
    [SerializeField] private float dutchAngle = -8f;
    // 演出中の画角です。
    [SerializeField] private float matrixFieldOfView = 38f;
    [SerializeField] private AnimationCurve cameraMoveCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Phase")]
    // 回避する必要がある弾数です。今回は4発想定です。
    [SerializeField] private int requiredAvoidCount = 4;
    // 弾がこの距離以内に来た時、下入力を維持できていれば自動回避します。
    [SerializeField] private float autoAvoidDistance = 1.2f;
    // 下入力がこの値以下なら「下を押している」と判定します。
    // Input.GetAxisRaw("Vertical")は下入力で-1になります。
    [SerializeField] private float downInputThreshold = -0.5f;

    [Header("Center Of Gravity")]
    // 重心値が1へ向かって自動上昇する速さです。
    [SerializeField] private float gravityRiseSpeed = 0.35f;
    // 下入力で重心値を下げる速さです。
    [SerializeField] private float downHoldPower = 0.75f;
    // この値以下なら自動回避できる安定状態です。
    [SerializeField] private float avoidableGravityValue = 0.45f;
    // 重心Transformをどれくらい上へ動かすかです。
    [SerializeField] private float gravityVisualUpOffset = 0.45f;
    // 重心がこの値を超えると、下維持が足りないため失敗扱いにします。
    [SerializeField] private float gravityFailValue = 0.95f;

    [Header("Slow Motion Feeling")]
    // trueならTime.timeScaleの影響を受けずに演出を進めます。
    [SerializeField] private bool useUnscaledTime = true;

    [Header("Effects")]
    [SerializeField] private GameObject avoidEffectPrefab;
    [SerializeField] private GameObject failEffectPrefab;
    [SerializeField] private float effectLifeTime = 2f;
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip avoidSe;
    [SerializeField] private AudioClip failSe;

    [Header("Events")]
    [SerializeField] private UnityEvent onPhaseStarted;
    [SerializeField] private UnityEvent onBulletAvoided;
    [SerializeField] private UnityEvent onPhaseSucceeded;
    [SerializeField] private UnityEvent onPhaseFailed;

    [Header("Debug")]
    [SerializeField] private bool debugLog = true;

    private Vector3 originalCameraPosition;
    private Quaternion originalCameraRotation;
    private float originalFieldOfView;
    private Vector3 originalGravityLocalPosition;
    private Coroutine cameraRoutine;
    private bool hasOriginalCameraState;
    private bool isPhaseActive;
    private bool hasFailed;
    private int avoidedCount;
    private float centerOfGravityValue;

    public bool IsPhaseActive => isPhaseActive;
    public int AvoidedCount => avoidedCount;

    private void Awake()
    {
        AutoAssignCamera();

        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }

        if (centerOfGravityTransform != null)
        {
            originalGravityLocalPosition = centerOfGravityTransform.localPosition;
        }
    }

    private void Update()
    {
        if (!isPhaseActive || hasFailed)
        {
            return;
        }

        UpdateCenterOfGravity();
        TryAutoAvoidNearBullets();

        if (centerOfGravityValue >= gravityFailValue)
        {
            FailPhase(null, centerOfGravityTransform != null ? centerOfGravityTransform.position : transform.position);
        }
    }

    public void StartPhase()
    {
        if (isPhaseActive)
        {
            return;
        }

        isPhaseActive = true;
        hasFailed = false;
        avoidedCount = 0;
        centerOfGravityValue = 0f;

        SaveOriginalCameraState();
        MoveCameraToMatrixView();
        UpdateCenterOfGravityVisual();

        Log("Matrix avoid phase started.");
        onPhaseStarted?.Invoke();
    }

    public void StopPhase()
    {
        isPhaseActive = false;
        ReturnCameraToOriginalView();
        RestoreCenterOfGravityVisual();
    }

    public void NotifyBulletHit(SniperBullet bullet, Vector3 hitPosition)
    {
        if (!isPhaseActive || hasFailed)
        {
            return;
        }

        FailPhase(bullet, hitPosition);
    }

    private void UpdateCenterOfGravity()
    {
        float deltaTime = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        float verticalInput = Input.GetAxisRaw("Vertical");

        // 下入力を維持していないと、重心は自動で上へ上がっていきます。
        centerOfGravityValue += gravityRiseSpeed * deltaTime;

        if (verticalInput <= downInputThreshold)
        {
            // 下を押している時だけ重心を下げます。
            // 「綱渡りでバランスを押さえ込む」操作感を残すためです。
            centerOfGravityValue -= downHoldPower * deltaTime;
        }

        centerOfGravityValue = Mathf.Clamp01(centerOfGravityValue);
        UpdateCenterOfGravityVisual();
    }

    private void UpdateCenterOfGravityVisual()
    {
        if (centerOfGravityTransform == null)
        {
            return;
        }

        Vector3 offset = Vector3.up * gravityVisualUpOffset * centerOfGravityValue;
        centerOfGravityTransform.localPosition = originalGravityLocalPosition + offset;
    }

    private void RestoreCenterOfGravityVisual()
    {
        if (centerOfGravityTransform == null)
        {
            return;
        }

        centerOfGravityTransform.localPosition = originalGravityLocalPosition;
    }

    private void TryAutoAvoidNearBullets()
    {
        SniperBullet[] bullets = FindObjectsByType<SniperBullet>(FindObjectsSortMode.None);

        for (int i = 0; i < bullets.Length; i++)
        {
            SniperBullet bullet = bullets[i];
            if (bullet == null || bullet.IsDestroyed)
            {
                continue;
            }

            float distance = Vector3.Distance(bullet.transform.position, GetLookTargetPosition());
            if (distance > autoAvoidDistance)
            {
                continue;
            }

            if (CanAutoAvoid())
            {
                AvoidBullet(bullet);
            }
        }
    }

    private bool CanAutoAvoid()
    {
        float verticalInput = Input.GetAxisRaw("Vertical");
        return verticalInput <= downInputThreshold && centerOfGravityValue <= avoidableGravityValue;
    }

    private void AvoidBullet(SniperBullet bullet)
    {
        Vector3 position = bullet.transform.position;
        SpawnEffect(avoidEffectPrefab, position);
        PlaySe(avoidSe);
        onBulletAvoided?.Invoke();

        avoidedCount++;
        Log($"Bullet avoided. Count = {avoidedCount}/{requiredAvoidCount}");

        bullet.DestroyBullet();

        if (avoidedCount >= requiredAvoidCount)
        {
            SucceedPhase();
        }
    }

    private void SucceedPhase()
    {
        if (!isPhaseActive || hasFailed)
        {
            return;
        }

        Log("Matrix avoid phase succeeded.");
        onPhaseSucceeded?.Invoke();
        StopPhase();

        if (eventManager != null)
        {
            eventManager.NotifyMatrixAvoidSucceeded();
        }
    }

    private void FailPhase(SniperBullet bullet, Vector3 hitPosition)
    {
        if (hasFailed)
        {
            return;
        }

        hasFailed = true;
        SpawnEffect(failEffectPrefab, hitPosition);
        PlaySe(failSe);

        if (bullet != null && !bullet.IsDestroyed)
        {
            bullet.DestroyBullet();
        }

        Log("Matrix avoid phase failed.");
        onPhaseFailed?.Invoke();
        StopPhase();

        if (eventManager != null)
        {
            eventManager.NotifyMatrixAvoidFailed();
        }
    }

    private void MoveCameraToMatrixView()
    {
        if (cameraTransform == null)
        {
            return;
        }

        Vector3 targetPosition = CalculateMatrixCameraPosition();
        Quaternion targetRotation = CalculateLookRotation(targetPosition, dutchAngle);
        StartCameraRoutine(targetPosition, targetRotation, matrixFieldOfView, moveCameraDuration, false);
    }

    private void ReturnCameraToOriginalView()
    {
        if (!hasOriginalCameraState || cameraTransform == null)
        {
            return;
        }

        StartCameraRoutine(originalCameraPosition, originalCameraRotation, originalFieldOfView, moveCameraDuration, true);
    }

    private void SaveOriginalCameraState()
    {
        if (cameraTransform == null || hasOriginalCameraState)
        {
            return;
        }

        originalCameraPosition = cameraTransform.position;
        originalCameraRotation = cameraTransform.rotation;
        originalFieldOfView = targetCamera != null ? targetCamera.fieldOfView : 60f;
        hasOriginalCameraState = true;
    }

    private Vector3 CalculateMatrixCameraPosition()
    {
        Vector3 targetPosition = GetLookTargetPosition();
        Transform reference = lookTarget != null ? lookTarget : transform;

        return targetPosition +
               reference.right.normalized * sideDistance +
               -reference.forward.normalized * backDistance +
               Vector3.up * cameraHeight;
    }

    private Quaternion CalculateLookRotation(Vector3 fromPosition, float rollAngle)
    {
        Vector3 lookDirection = GetLookTargetPosition() - fromPosition;
        if (lookDirection.sqrMagnitude <= Mathf.Epsilon)
        {
            return cameraTransform.rotation;
        }

        return Quaternion.LookRotation(lookDirection.normalized, Vector3.up) *
               Quaternion.Euler(0f, 0f, rollAngle);
    }

    private Vector3 GetLookTargetPosition()
    {
        if (lookTarget == null)
        {
            return transform.position + Vector3.up * lookHeight;
        }

        return lookTarget.position + Vector3.up * lookHeight;
    }

    private void StartCameraRoutine(Vector3 targetPosition, Quaternion targetRotation, float targetFieldOfView, float duration, bool clearOriginalStateOnComplete)
    {
        if (cameraRoutine != null)
        {
            StopCoroutine(cameraRoutine);
        }

        cameraRoutine = StartCoroutine(CameraRoutine(targetPosition, targetRotation, targetFieldOfView, duration, clearOriginalStateOnComplete));
    }

    private IEnumerator CameraRoutine(Vector3 targetPosition, Quaternion targetRotation, float targetFieldOfView, float duration, bool clearOriginalStateOnComplete)
    {
        Vector3 startPosition = cameraTransform.position;
        Quaternion startRotation = cameraTransform.rotation;
        float startFieldOfView = targetCamera != null ? targetCamera.fieldOfView : targetFieldOfView;
        float timer = 0f;

        while (timer < duration)
        {
            float deltaTime = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            timer += deltaTime;

            float rate = Mathf.Clamp01(timer / Mathf.Max(0.001f, duration));
            float curvedRate = cameraMoveCurve != null ? cameraMoveCurve.Evaluate(rate) : rate;

            cameraTransform.position = Vector3.Lerp(startPosition, targetPosition, curvedRate);
            cameraTransform.rotation = Quaternion.Slerp(startRotation, targetRotation, curvedRate);

            if (targetCamera != null)
            {
                targetCamera.fieldOfView = Mathf.Lerp(startFieldOfView, targetFieldOfView, curvedRate);
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
    }

    private void SpawnEffect(GameObject effectPrefab, Vector3 position)
    {
        if (effectPrefab == null)
        {
            return;
        }

        GameObject effect = Instantiate(effectPrefab, position, Quaternion.identity);
        if (effectLifeTime > 0f)
        {
            Destroy(effect, effectLifeTime);
        }
    }

    private void PlaySe(AudioClip clip)
    {
        if (audioSource == null || clip == null)
        {
            return;
        }

        audioSource.PlayOneShot(clip);
    }

    private void AutoAssignCamera()
    {
        if (cameraTransform != null && targetCamera != null)
        {
            return;
        }

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

        Debug.Log($"[SniperMatrixAvoidController] {message}", this);
    }
}
