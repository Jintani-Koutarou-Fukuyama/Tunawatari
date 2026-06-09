using UnityEngine;
using UnityEngine.Events;
using System.Collections;
using TMPro;
using UnityEngine.UI;

public class BalanceManager : MonoBehaviour
{
    // 将来的なイベント切り替え用。
    // Horizontal: 横ゲージ、Vertical: 縦ゲージとしてUIを動かします。
    public enum BalanceGaugeDirection
    {
        Horizontal,
        Vertical
    }

    public enum BalancePlayMode
    {
        Normal,
        SniperDefense,
        MatrixAvoid
    }

    [System.Serializable]
    public class DamageEvent : UnityEvent<int>
    {
    }

    private struct RectTransformLayout
    {
        public Vector2 anchorMin;
        public Vector2 anchorMax;
        public Vector2 pivot;
        public Vector2 anchoredPosition;
        public Vector2 sizeDelta;
        public Vector3 localScale;
        public Quaternion localRotation;

        public RectTransformLayout(RectTransform rectTransform)
        {
            anchorMin = rectTransform.anchorMin;
            anchorMax = rectTransform.anchorMax;
            pivot = rectTransform.pivot;
            anchoredPosition = rectTransform.anchoredPosition;
            sizeDelta = rectTransform.sizeDelta;
            localScale = rectTransform.localScale;
            localRotation = rectTransform.localRotation;
        }

        public void ApplyTo(RectTransform rectTransform)
        {
            rectTransform.anchorMin = anchorMin;
            rectTransform.anchorMax = anchorMax;
            rectTransform.pivot = pivot;
            rectTransform.anchoredPosition = anchoredPosition;
            rectTransform.sizeDelta = sizeDelta;
            rectTransform.localScale = localScale;
            rectTransform.localRotation = localRotation;
        }
    }

    [Header("UI")]
    [SerializeField] private RectTransform gaugeRoot;
    [SerializeField] private RectTransform balanceBar;
    [SerializeField] private RectTransform targetZone;
    [SerializeField] private RectTransform balancePoint;

    [Header("Event Vertical Layout")]
    // イベント中にBalanceGauge全体を画面左側へ置くための設定です。
    // 横ゲージのサイズは変えず、親RectTransformを90度回転して縦表示にします。
    [SerializeField] private Vector2 eventVerticalAnchorMin = new Vector2(0f, 0.5f);
    [SerializeField] private Vector2 eventVerticalAnchorMax = new Vector2(0f, 0.5f);
    [SerializeField] private Vector2 eventVerticalPivot = new Vector2(0.5f, 0.5f);
    [SerializeField] private Vector2 eventVerticalAnchoredPosition = new Vector2(40f, 0f);
    [SerializeField] private float eventVerticalRotationZ = 90f;
    [SerializeField] private bool resetGaugePositionOnLayoutSwitch = true;

    [Header("Gauge Direction")]
    [SerializeField] private BalanceGaugeDirection gaugeDirection = BalanceGaugeDirection.Horizontal;

    [Header("Mode")]
    // 通常綱渡り、スナイパー防御、マトリックス回避のどれとして動かすかです。
    // State遷移はSniperEventManager側で行い、BalanceManagerは表示・操作・判定だけを担当します。
    [SerializeField] private BalancePlayMode playMode = BalancePlayMode.Normal;

    [Header("Balance Point")]
    [SerializeField] private float pointMoveSpeed = 10f;
    [SerializeField] private KeyCode negativeKey = KeyCode.LeftArrow;
    [SerializeField] private KeyCode positiveKey = KeyCode.RightArrow;
    [SerializeField] private bool returnPointToCenter;
    [SerializeField] private float pointReturnSpeed = 80f;

    [Header("Target Zone")]
    [SerializeField] private float targetMoveSpeed = 0.5f;
    [SerializeField] private float targetMoveRangeRate = 0.85f;

    [Header("Random Challenge")]
    // trueにすると、黄色い目標範囲は動き続けず、ランダムな位置へ出る方式になります。
    [SerializeField] private bool useRandomTargetChallenge = true;
    // PlayerMarkerをTargetYellowAreaへ合わせる制限時間です。
    [SerializeField] private float challengeTimeLimit = 3f;
    // 成功したら次のランダム位置へ移動します。
    [SerializeField] private bool randomizeTargetAfterSuccess = true;
    // 失敗したら次のランダム位置へ移動します。
    [SerializeField] private bool randomizeTargetAfterFailure = true;
    // ランダム位置が現在のPlayerMarkerと重なりにくいようにする最小距離です。
    [SerializeField] private float randomTargetMinDistanceFromPoint = 30f;
    // UI Textを使っている場合の残り時間表示です。
    [SerializeField] private Text balanceTimerText;
    // TextMeshProを使っている場合の残り時間表示です。
    [SerializeField] private TMP_Text balanceTimerTmpText;
    // 残り時間表示の書式です。{0:0.0} に秒数が入ります。
    [SerializeField] private string timerFormat = "{0:0.0}";

    [Header("Sniper Defense Mode")]
    // スナイパー防御中、白球と連動して上下させる棒Transformです。
    [SerializeField] private Transform sniperDefenseStickTarget;
    // 白球の位置を棒のローカルY移動へ変換する幅です。
    [SerializeField] private float sniperDefenseStickMoveRange = 0.5f;
    // スナイパー防御中に白球を下へ動かすキーです。
    [SerializeField] private KeyCode sniperDefenseDownKey = KeyCode.DownArrow;
    // スナイパー防御中に白球を上へ動かすキーです。
    [SerializeField] private KeyCode sniperDefenseUpKey = KeyCode.UpArrow;

    [Header("Matrix Avoid Mode")]
    // マトリックス回避中、白球が自動で上昇する速さです。
    [SerializeField] private float matrixPointRiseSpeed = 80f;
    // マトリックス回避中、下入力で白球を押し戻す強さです。
    [SerializeField] private float matrixDownPushSpeed = 140f;
    // マトリックス回避中に白球を下へ押し戻すキーです。
    [SerializeField] private KeyCode matrixDownKey = KeyCode.DownArrow;

    [Header("Failure")]
    [SerializeField] private float failTimeLimit = 1.5f;
    [SerializeField] private int damageAmount = 1;
    [SerializeField] private bool resetPointAfterDamage = true;

    [Header("Player Wobble")]
    [SerializeField] private Transform wobbleTarget;
    [SerializeField] private float wobbleDuration = 0.6f;
    [SerializeField] private float wobbleAngle = 8f;
    [SerializeField] private float wobbleSpeed = 18f;

    [Header("Events")]
    [SerializeField] private UnityEvent onBalanceSuccess;
    [SerializeField] private UnityEvent onBalanceMiss;
    [SerializeField] private DamageEvent onDamage;

    [Header("Animator")]
    // Unbalanceアニメーションを再生するAnimatorです。
    // ここがnullだとSetBoolを呼べず、アニメーションは再生されません。
    [SerializeField] private Animator animator;
    // Animator側のboolパラメーター名です。
    // Animator Parametersにある名前と完全に同じ文字にしてください。
    [SerializeField] private string unbalanceBoolName = "Unbalance";
    // Unbalanceをtrueにしておく時間です。
    // 短すぎるとアニメーションに入る前にfalseへ戻ることがあります。
    [SerializeField] private float unbalanceDuration = 0.6f;

    [Header("Debug")]
    // trueにするとConsoleへ状態確認用ログを出します。
    // 原因調査が終わったらfalseにするとConsoleが静かになります。
    [SerializeField] private bool enableDebugLog = true;
    // outsideTimerのログを何秒ごとに出すかです。
    // 毎フレーム出すとConsoleが見づらくなるので、少し間隔を空けます。
    [SerializeField] private float timerLogInterval = 0.25f;

    private float pointAxisPosition;
    private float targetAxisPosition;
    private float targetMoveDirection = 1f;
    private float outsideTimer;
    private float challengeTimer;
    private float wobbleTimer;
    private Quaternion wobbleBaseRotation;
    private Vector3 sniperDefenseStickBaseLocalPosition;
    private bool hasSniperDefenseStickBasePosition;
    private bool isInsideTarget;
    private bool wasInsideTarget;
    private Coroutine unbalanceCoroutine;
    private float nextTimerLogTime;
    private bool hasSavedHorizontalLayout;
    private RectTransformLayout savedGaugeRootLayout;
    private RectTransformLayout savedBalanceBarLayout;
    private RectTransformLayout savedTargetZoneLayout;
    private RectTransformLayout savedBalancePointLayout;

    public float OutsideTimer => outsideTimer;
    public float ChallengeRemainingTime => Mathf.Max(0f, challengeTimeLimit - challengeTimer);
    public BalanceGaugeDirection GaugeDirection => gaugeDirection;
    public BalancePlayMode PlayMode => playMode;

    private void Start()
    {
        AutoAssignBalanceUi();

        if (gaugeRoot == null && balanceBar != null)
        {
            gaugeRoot = balanceBar.parent as RectTransform;
        }

        AutoAssignTimerText();
        SaveHorizontalLayoutIfNeeded();

        // UIの初期位置を現在のInspector配置から読み取ります。
        pointAxisPosition = GetAxisAnchoredPosition(balancePoint);
        targetAxisPosition = GetAxisAnchoredPosition(targetZone);

        if (wobbleTarget != null)
        {
            wobbleBaseRotation = wobbleTarget.localRotation;
        }

        if (useRandomTargetChallenge)
        {
            RandomizeTargetZone();
            challengeTimer = 0f;
        }

        ApplyAllUiPositions();
        UpdateBalanceState();
        wasInsideTarget = isInsideTarget;
        UpdateTimerText();

        DebugLog($"Start: IsInsideTarget={isInsideTarget}, animator={(animator != null ? animator.name : "null")}, failTimeLimit={failTimeLimit}");
        CheckAnimatorSetup();
    }

    private void Update()
    {
        switch (playMode)
        {
            case BalancePlayMode.SniperDefense:
                UpdateSniperDefenseMode();
                break;
            case BalancePlayMode.MatrixAvoid:
                UpdateMatrixAvoidMode();
                break;
            default:
                UpdateNormalMode();
                break;
        }

        UpdateWobble();
    }

    private void UpdateNormalMode()
    {
        if (!useRandomTargetChallenge)
        {
            MoveTargetZone();
        }

        MoveBalancePoint();
        ApplyAllUiPositions();
        UpdateBalanceState();

        if (useRandomTargetChallenge)
        {
            UpdateRandomChallenge();
        }
        else
        {
            UpdateFailureTimer();
        }
    }

    // 外部イベントから横/縦を切り替えたい時に呼びます。
    public void SetGaugeDirection(BalanceGaugeDirection direction)
    {
        if (gaugeDirection == direction)
        {
            return;
        }

        gaugeDirection = direction;

        // 切り替え時に位置がゲージ外へ飛ばないよう、現在値を新しい長さで丸めます。
        pointAxisPosition = Mathf.Clamp(pointAxisPosition, GetMinPointPosition(), GetMaxPointPosition());
        targetAxisPosition = Mathf.Clamp(targetAxisPosition, GetMinTargetPosition(), GetMaxTargetPosition());
        ApplyAllUiPositions();
        UpdateBalanceState();
    }

    // ボタンやTimelineなどから横ゲージへ戻したい時用。
    public void SetHorizontalGauge()
    {
        SetGaugeDirection(BalanceGaugeDirection.Horizontal);
    }

    // イベント中だけ縦ゲージにしたい時用。
    public void SetVerticalGauge()
    {
        SetGaugeDirection(BalanceGaugeDirection.Vertical);
    }

    public void SwitchToEventVerticalLayout()
    {
        SaveHorizontalLayoutIfNeeded();

        SetGaugeDirection(BalanceGaugeDirection.Horizontal);
        ApplyEventVerticalLayout();

        if (resetGaugePositionOnLayoutSwitch)
        {
            ResetBalance();
        }

        DebugLog("Balance gauge switched to event vertical layout.");
    }

    public void SwitchToNormalHorizontalLayout()
    {
        RestoreHorizontalLayout();
        SetGaugeDirection(BalanceGaugeDirection.Horizontal);

        if (resetGaugePositionOnLayoutSwitch)
        {
            ResetBalance();
        }

        DebugLog("Balance gauge restored to normal horizontal layout.");
    }

    public void SetEventVerticalLayoutActive(bool eventActive)
    {
        if (eventActive)
        {
            SwitchToEventVerticalLayout();
        }
        else
        {
            SwitchToNormalHorizontalLayout();
        }
    }

    // ダメージ後やリトライ時にゲージを中央へ戻したい時用。
    public void ResetBalance()
    {
        pointAxisPosition = 0f;
        targetAxisPosition = 0f;
        outsideTimer = 0f;
        targetMoveDirection = 1f;
        challengeTimer = 0f;

        if (useRandomTargetChallenge)
        {
            RandomizeTargetZone();
        }

        ApplyAllUiPositions();
        UpdateBalanceState();
        UpdateTimerText();
    }

    public void SetSniperTargetPosition(float normalizedPosition)
    {
        float t = Mathf.Clamp01(normalizedPosition);
        targetAxisPosition = Mathf.Lerp(GetMinTargetPosition(), GetMaxTargetPosition(), t);
        ApplyAllUiPositions();
        UpdateBalanceState();
        DebugLog($"Sniper target set. normalized={t:F2}, target={targetAxisPosition:F2}");
    }

    public void EnableSniperDefenseMode()
    {
        playMode = BalancePlayMode.SniperDefense;
        outsideTimer = 0f;
        challengeTimer = 0f;
        SetGaugeDirection(BalanceGaugeDirection.Horizontal);
        SaveSniperDefenseStickBasePosition();
        ApplyAllUiPositions();
        UpdateBalanceState();
        UpdateSniperDefenseStickPosition();
        DebugLog("SniperDefenseMode enabled.");
    }

    public void DisableSniperDefenseMode()
    {
        RestoreSniperDefenseStickPosition();

        if (playMode == BalancePlayMode.SniperDefense)
        {
            playMode = BalancePlayMode.Normal;
        }

        outsideTimer = 0f;
        DebugLog("SniperDefenseMode disabled.");
    }

    public void EnableMatrixAvoidMode()
    {
        playMode = BalancePlayMode.MatrixAvoid;
        outsideTimer = 0f;
        challengeTimer = 0f;
        SetGaugeDirection(BalanceGaugeDirection.Horizontal);
        targetAxisPosition = GetMinTargetPosition();
        pointAxisPosition = targetAxisPosition;
        ApplyAllUiPositions();
        UpdateBalanceState();
        DebugLog("MatrixAvoidMode enabled.");
    }

    public void DisableMatrixAvoidMode()
    {
        if (playMode == BalancePlayMode.MatrixAvoid)
        {
            playMode = BalancePlayMode.Normal;
        }

        outsideTimer = 0f;
        DebugLog("MatrixAvoidMode disabled.");
    }

    public bool IsInsideTarget()
    {
        return isInsideTarget;
    }

    public bool ResolveSniperDefenseShot()
    {
        UpdateBalanceState();
        bool success = isInsideTarget;

        if (success)
        {
            onBalanceSuccess?.Invoke();
        }
        else
        {
            onBalanceMiss?.Invoke();
            StartWobble();
            PlayUnbalance();
        }

        DebugLog($"Sniper defense shot resolved. success={success}");
        return success;
    }

    private void MoveBalancePoint()
    {
        float input = 0f;

        // negativeKeyは左/下方向、positiveKeyは右/上方向として扱います。
        if (Input.GetKey(negativeKey))
        {
            input -= 1f;
        }

        if (Input.GetKey(positiveKey))
        {
            input += 1f;
        }

        pointAxisPosition += input * pointMoveSpeed * Time.deltaTime;

        // 入力していない時に中央へ少し戻したい場合の設定です。
        if (returnPointToCenter && Mathf.Approximately(input, 0f))
        {
            pointAxisPosition = Mathf.MoveTowards(pointAxisPosition, 0f, pointReturnSpeed * Time.deltaTime);
        }

        pointAxisPosition = Mathf.Clamp(pointAxisPosition, GetMinPointPosition(), GetMaxPointPosition());
    }

    private void UpdateSniperDefenseMode()
    {
        float input = 0f;

        if (Input.GetKey(sniperDefenseDownKey))
        {
            input -= 1f;
        }

        if (Input.GetKey(sniperDefenseUpKey))
        {
            input += 1f;
        }

        pointAxisPosition += input * pointMoveSpeed * Time.deltaTime;
        pointAxisPosition = Mathf.Clamp(pointAxisPosition, GetMinPointPosition(), GetMaxPointPosition());

        ApplyAllUiPositions();
        UpdateBalanceState();
        UpdateSniperDefenseStickPosition();
    }

    private void UpdateMatrixAvoidMode()
    {
        pointAxisPosition += matrixPointRiseSpeed * Time.deltaTime;

        if (Input.GetKey(matrixDownKey))
        {
            pointAxisPosition -= matrixDownPushSpeed * Time.deltaTime;
        }

        pointAxisPosition = Mathf.Clamp(pointAxisPosition, GetMinPointPosition(), GetMaxPointPosition());

        ApplyAllUiPositions();
        UpdateBalanceState();

        if (isInsideTarget)
        {
            outsideTimer = 0f;
            return;
        }

        outsideTimer += Time.deltaTime;
        LogOutsideTimer();

        if (outsideTimer >= failTimeLimit)
        {
            DebugLog($"MatrixAvoidMode failed. outsideTimer={outsideTimer:F2}, limit={failTimeLimit:F2}");
            outsideTimer = 0f;
            StartWobble();
            onBalanceMiss?.Invoke();
            onDamage?.Invoke(damageAmount);
            PlayUnbalance();
        }
    }

    private void SaveSniperDefenseStickBasePosition()
    {
        if (sniperDefenseStickTarget == null || hasSniperDefenseStickBasePosition)
        {
            return;
        }

        sniperDefenseStickBaseLocalPosition = sniperDefenseStickTarget.localPosition;
        hasSniperDefenseStickBasePosition = true;
    }

    private void UpdateSniperDefenseStickPosition()
    {
        if (sniperDefenseStickTarget == null)
        {
            return;
        }

        SaveSniperDefenseStickBasePosition();

        float normalizedPoint = GetNormalizedPointPositionSigned();
        Vector3 localPosition = sniperDefenseStickBaseLocalPosition;
        localPosition.y += normalizedPoint * sniperDefenseStickMoveRange;
        sniperDefenseStickTarget.localPosition = localPosition;
    }

    private void RestoreSniperDefenseStickPosition()
    {
        if (sniperDefenseStickTarget == null || !hasSniperDefenseStickBasePosition)
        {
            return;
        }

        sniperDefenseStickTarget.localPosition = sniperDefenseStickBaseLocalPosition;
    }

    private float GetNormalizedPointPositionSigned()
    {
        float min = GetMinPointPosition();
        float max = GetMaxPointPosition();

        if (Mathf.Approximately(min, max))
        {
            return 0f;
        }

        float normalized = Mathf.InverseLerp(min, max, pointAxisPosition);
        return normalized * 2f - 1f;
    }

    private void MoveTargetZone()
    {
        targetAxisPosition += targetMoveDirection * targetMoveSpeed * Time.deltaTime;

        float min = GetMinTargetPosition();
        float max = GetMaxTargetPosition();

        // 端まで来たら反転します。PingPongよりInspector調整時の挙動が読みやすい形にしています。
        if (targetAxisPosition > max)
        {
            targetAxisPosition = max;
            targetMoveDirection = -1f;
        }
        else if (targetAxisPosition < min)
        {
            targetAxisPosition = min;
            targetMoveDirection = 1f;
        }
    }

    private void UpdateBalanceState()
    {
        float targetHalfSize = GetAxisSize(targetZone) * 0.5f;
        float min = targetAxisPosition - targetHalfSize;
        float max = targetAxisPosition + targetHalfSize;

        isInsideTarget = pointAxisPosition >= min && pointAxisPosition <= max;

        // 状態が変わった瞬間だけイベントを呼びます。
        if (isInsideTarget != wasInsideTarget)
        {
            DebugLog($"Balance state changed: IsInsideTarget={isInsideTarget}, point={pointAxisPosition:F2}, targetMin={min:F2}, targetMax={max:F2}, targetCenter={targetAxisPosition:F2}");

            if (playMode == BalancePlayMode.Normal && !useRandomTargetChallenge && isInsideTarget)
            {
                onBalanceSuccess?.Invoke();
            }
            else if (playMode == BalancePlayMode.Normal && !useRandomTargetChallenge)
            {
                onBalanceMiss?.Invoke();
            }

            wasInsideTarget = isInsideTarget;
        }
    }

    private void UpdateFailureTimer()
    {
        if (isInsideTarget)
        {
            if (outsideTimer > 0f)
            {
                DebugLog($"outsideTimer reset: point returned inside target. outsideTimer was {outsideTimer:F2}");
            }

            outsideTimer = 0f;
            return;
        }

        outsideTimer += Time.deltaTime;
        LogOutsideTimer();

        // 一定時間外れたら、ふらつきとダメージ通知を発生させます。
        if (outsideTimer >= failTimeLimit)
        {
            DebugLog($"Damage triggered: outsideTimer={outsideTimer:F2}, failTimeLimit={failTimeLimit:F2}, damage={damageAmount}");

            outsideTimer = 0f;
            StartWobble();
            onDamage?.Invoke(damageAmount);
            PlayUnbalance();

            if (resetPointAfterDamage)
            {
                DebugLog("BalancePoint reset to center after damage.");
                pointAxisPosition = 0f;
            }
        }
    }

    private void UpdateRandomChallenge()
    {
        if (isInsideTarget)
        {
            HandleChallengeSuccess();
            return;
        }

        challengeTimer += Time.deltaTime;
        UpdateTimerText();

        if (challengeTimer >= challengeTimeLimit)
        {
            HandleChallengeFailure();
        }
    }

    private void HandleChallengeSuccess()
    {
        DebugLog($"Random challenge success. point={pointAxisPosition:F2}, target={targetAxisPosition:F2}");
        onBalanceSuccess?.Invoke();
        challengeTimer = 0f;

        if (randomizeTargetAfterSuccess)
        {
            RandomizeTargetZone();
        }

        ApplyAllUiPositions();
        UpdateBalanceState();
        wasInsideTarget = isInsideTarget;
        UpdateTimerText();
    }

    private void HandleChallengeFailure()
    {
        DebugLog($"Random challenge failed. challengeTimer={challengeTimer:F2}, limit={challengeTimeLimit:F2}");

        challengeTimer = 0f;
        outsideTimer = 0f;
        StartWobble();
        onBalanceMiss?.Invoke();
        onDamage?.Invoke(damageAmount);
        PlayUnbalance();

        if (resetPointAfterDamage)
        {
            pointAxisPosition = 0f;
        }

        if (randomizeTargetAfterFailure)
        {
            RandomizeTargetZone();
        }

        ApplyAllUiPositions();
        UpdateBalanceState();
        wasInsideTarget = isInsideTarget;
        UpdateTimerText();
    }

    private void RandomizeTargetZone()
    {
        float min = GetMinTargetPosition();
        float max = GetMaxTargetPosition();

        if (max < min)
        {
            targetAxisPosition = 0f;
            return;
        }

        float newPosition = Random.Range(min, max);

        for (int i = 0; i < 10; i++)
        {
            if (Mathf.Abs(newPosition - pointAxisPosition) >= randomTargetMinDistanceFromPoint)
            {
                break;
            }

            newPosition = Random.Range(min, max);
        }

        targetAxisPosition = newPosition;
        DebugLog($"Target randomized. target={targetAxisPosition:F2}");
    }

    private void UpdateTimerText()
    {
        if (!useRandomTargetChallenge)
        {
            SetTimerText(string.Empty);
            return;
        }

        string text = string.Format(timerFormat, ChallengeRemainingTime);
        SetTimerText(text);
    }

    private void SetTimerText(string text)
    {
        if (balanceTimerText != null)
        {
            balanceTimerText.text = text;
        }

        if (balanceTimerTmpText != null)
        {
            balanceTimerTmpText.text = text;
        }
    }

    private void AutoAssignTimerText()
    {
        if (balanceTimerText != null || balanceTimerTmpText != null)
        {
            return;
        }

        GameObject timerObject = GameObject.Find("BalanceTimerText");
        if (timerObject == null)
        {
            return;
        }

        balanceTimerText = timerObject.GetComponent<Text>();
        balanceTimerTmpText = timerObject.GetComponent<TMP_Text>();
    }

    private void AutoAssignBalanceUi()
    {
        if (targetZone == null)
        {
            GameObject targetObject = GameObject.Find("TargetYellowArea");
            if (targetObject != null)
            {
                targetZone = targetObject.GetComponent<RectTransform>();
            }
        }

        if (balancePoint == null)
        {
            GameObject markerObject = GameObject.Find("PlayerMarker");
            if (markerObject != null)
            {
                balancePoint = markerObject.GetComponent<RectTransform>();
            }
        }

        if (gaugeRoot == null)
        {
            GameObject gaugeObject = GameObject.Find("BalanceGauge");
            if (gaugeObject != null)
            {
                gaugeRoot = gaugeObject.GetComponent<RectTransform>();
            }
        }
    }

    private void PlayUnbalance()
    {
        // Animator参照がない場合、SetBoolできないのでログを出して止めます。
        // ここが出たらInspectorのanimator欄にSuitのAnimatorを入れてください。
        if (animator == null)
        {
            DebugLog("Animator is null. Unbalance bool was not set.");
            return;
        }

        // パラメーター名が空だとAnimatorへ正しく命令できないので止めます。
        if (string.IsNullOrEmpty(unbalanceBoolName))
        {
            DebugLog("Unbalance bool name is empty. Animator.SetBool was skipped.");
            return;
        }

        // すでに戻すCoroutineが動いている場合は一度止めます。
        // 複数のCoroutineが同時にfalseを入れると、意図しないタイミングで戻ることがあるためです。
        if (unbalanceCoroutine != null)
        {
            StopCoroutine(unbalanceCoroutine);
            DebugLog("Previous StopUnbalance coroutine stopped before starting a new one.");
        }

        DebugLog($"Animator.SetBool({unbalanceBoolName}, true)");
        animator.SetBool(unbalanceBoolName, true);
        unbalanceCoroutine = StartCoroutine(StopUnbalance());
        DebugLog("StopUnbalance coroutine started.");
    }

    private void CheckAnimatorSetup()
    {
        // Animatorが未設定なら、ここで分かるようにします。
        // Unbalanceが再生されない時は、まずこのログを確認してください。
        if (animator == null)
        {
            DebugLog("Animator setup check: animator is null. Assign the player's Animator in Inspector.");
            return;
        }

        // Animator Controllerに指定したbool名が存在するか確認します。
        // 名前が1文字でも違うとSetBoolしても意図した遷移が起きません。
        if (!HasAnimatorBoolParameter(unbalanceBoolName))
        {
            DebugLog($"Animator setup check: bool parameter '{unbalanceBoolName}' was not found.");
            return;
        }

        DebugLog($"Animator setup check: bool parameter '{unbalanceBoolName}' was found.");
    }

    private bool HasAnimatorBoolParameter(string parameterName)
    {
        // Animatorが無い、または名前が空なら確認できません。
        if (animator == null || string.IsNullOrEmpty(parameterName))
        {
            return false;
        }

        // animator.parametersにはAnimator Controllerに登録されているParameter一覧が入っています。
        // ここを調べることで、Inspector側の名前ミスを見つけやすくします。
        foreach (AnimatorControllerParameter parameter in animator.parameters)
        {
            if (parameter.type == AnimatorControllerParameterType.Bool && parameter.name == parameterName)
            {
                return true;
            }
        }

        return false;
    }

    private void StartWobble()
    {
        if (wobbleTarget == null)
        {
            return;
        }

        wobbleBaseRotation = wobbleTarget.localRotation;
        wobbleTimer = wobbleDuration;
    }

    private void UpdateWobble()
    {
        if (wobbleTarget == null || wobbleTimer <= 0f)
        {
            return;
        }

        wobbleTimer -= Time.deltaTime;

        float rate = wobbleDuration > 0f ? wobbleTimer / wobbleDuration : 0f;
        float angle = Mathf.Sin(Time.time * wobbleSpeed) * wobbleAngle * rate;

        // Z回転だけを足して、左右にふらついて見えるようにします。
        wobbleTarget.localRotation = wobbleBaseRotation * Quaternion.Euler(0f, 0f, angle);

        if (wobbleTimer <= 0f)
        {
            wobbleTarget.localRotation = wobbleBaseRotation;
        }
    }

    private void ApplyAllUiPositions()
    {
        SetAxisAnchoredPosition(balancePoint, pointAxisPosition);
        SetAxisAnchoredPosition(targetZone, targetAxisPosition);
    }

    private void SaveHorizontalLayoutIfNeeded()
    {
        if (hasSavedHorizontalLayout)
        {
            return;
        }

        if (gaugeRoot != null)
        {
            savedGaugeRootLayout = new RectTransformLayout(gaugeRoot);
        }

        if (balanceBar != null)
        {
            savedBalanceBarLayout = new RectTransformLayout(balanceBar);
        }

        if (targetZone != null)
        {
            savedTargetZoneLayout = new RectTransformLayout(targetZone);
        }

        if (balancePoint != null)
        {
            savedBalancePointLayout = new RectTransformLayout(balancePoint);
        }

        hasSavedHorizontalLayout = true;
    }

    private void ApplyEventVerticalLayout()
    {
        RectTransform root = gaugeRoot != null ? gaugeRoot : balanceBar;
        if (root != null)
        {
            root.anchorMin = eventVerticalAnchorMin;
            root.anchorMax = eventVerticalAnchorMax;
            root.pivot = eventVerticalPivot;
            root.anchoredPosition = eventVerticalAnchoredPosition;
            root.localRotation = Quaternion.Euler(0f, 0f, eventVerticalRotationZ);
        }
    }

    private void RestoreHorizontalLayout()
    {
        if (!hasSavedHorizontalLayout)
        {
            return;
        }

        if (gaugeRoot != null)
        {
            savedGaugeRootLayout.ApplyTo(gaugeRoot);
        }

        if (balanceBar != null)
        {
            savedBalanceBarLayout.ApplyTo(balanceBar);
        }

        if (targetZone != null)
        {
            savedTargetZoneLayout.ApplyTo(targetZone);
        }

        if (balancePoint != null)
        {
            savedBalancePointLayout.ApplyTo(balancePoint);
        }
    }

    private float GetMinPointPosition()
    {
        return -GetGaugeHalfLength() + GetAxisSize(balancePoint) * 0.5f;
    }

    private float GetMaxPointPosition()
    {
        return GetGaugeHalfLength() - GetAxisSize(balancePoint) * 0.5f;
    }

    private float GetMinTargetPosition()
    {
        float usableHalfLength = GetGaugeHalfLength() * Mathf.Clamp01(targetMoveRangeRate);
        return -usableHalfLength + GetAxisSize(targetZone) * 0.5f;
    }

    private float GetMaxTargetPosition()
    {
        float usableHalfLength = GetGaugeHalfLength() * Mathf.Clamp01(targetMoveRangeRate);
        return usableHalfLength - GetAxisSize(targetZone) * 0.5f;
    }

    private float GetGaugeHalfLength()
    {
        if (balanceBar == null)
        {
            return 0f;
        }

        return GetAxisSize(balanceBar) * 0.5f;
    }

    private float GetAxisSize(RectTransform rectTransform)
    {
        if (rectTransform == null)
        {
            return 0f;
        }

        return gaugeDirection == BalanceGaugeDirection.Horizontal
            ? rectTransform.rect.width
            : rectTransform.rect.height;
    }

    private float GetAxisAnchoredPosition(RectTransform rectTransform)
    {
        if (rectTransform == null)
        {
            return 0f;
        }

        return gaugeDirection == BalanceGaugeDirection.Horizontal
            ? rectTransform.anchoredPosition.x
            : rectTransform.anchoredPosition.y;
    }

    private void SetAxisAnchoredPosition(RectTransform rectTransform, float axisPosition)
    {
        if (rectTransform == null)
        {
            return;
        }

        Vector2 anchoredPosition = rectTransform.anchoredPosition;

        if (gaugeDirection == BalanceGaugeDirection.Horizontal)
        {
            anchoredPosition.x = axisPosition;
        }
        else
        {
            anchoredPosition.y = axisPosition;
        }

        rectTransform.anchoredPosition = anchoredPosition;
    }

    private void SetCrossAxisAnchoredPosition(RectTransform rectTransform, float crossAxisPosition)
    {
        if (rectTransform == null)
        {
            return;
        }

        Vector2 anchoredPosition = rectTransform.anchoredPosition;

        if (gaugeDirection == BalanceGaugeDirection.Horizontal)
        {
            anchoredPosition.y = crossAxisPosition;
        }
        else
        {
            anchoredPosition.x = crossAxisPosition;
        }

        rectTransform.anchoredPosition = anchoredPosition;
    }

    private IEnumerator StopUnbalance()
    {
        // 指定秒数だけ待ちます。
        // この間はUnbalance boolをtrueのままにして、AnimatorがUnbalanceへ遷移できる時間を作ります。
        DebugLog($"StopUnbalance coroutine waiting {unbalanceDuration:F2} seconds.");
        yield return new WaitForSeconds(unbalanceDuration);

        // 待っている間にAnimator参照が外れた場合に備えて確認します。
        if (animator == null)
        {
            DebugLog("Animator became null before setting Unbalance false.");
            unbalanceCoroutine = null;
            yield break;
        }

        // 通常状態へ戻すためにUnbalanceをfalseへ戻します。
        // ここ以外で毎フレームfalseにしないことで、アニメーションに入る前に戻される事故を防ぎます。
        DebugLog($"Animator.SetBool({unbalanceBoolName}, false)");
        animator.SetBool(unbalanceBoolName, false);
        unbalanceCoroutine = null;
        DebugLog("StopUnbalance coroutine finished.");
    }

    private void LogOutsideTimer()
    {
        // enableDebugLogがfalseなら何も出しません。
        if (!enableDebugLog)
        {
            return;
        }

        // Time.timeはゲーム開始からの経過秒数です。
        // nextTimerLogTimeより前なら、ログを出すタイミングではないので戻ります。
        if (Time.time < nextTimerLogTime)
        {
            return;
        }

        nextTimerLogTime = Time.time + timerLogInterval;
        DebugLog($"outsideTimer={outsideTimer:F2}/{failTimeLimit:F2}, point={pointAxisPosition:F2}, target={targetAxisPosition:F2}, IsInsideTarget={isInsideTarget}");
    }

    private void DebugLog(string message)
    {
        // 調査用ログのON/OFFをInspectorで切り替えられるようにしています。
        if (!enableDebugLog)
        {
            return;
        }

        Debug.Log($"[BalanceManager] {message}", this);
    }
}
