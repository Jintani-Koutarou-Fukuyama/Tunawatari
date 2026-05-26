using UnityEngine;
using UnityEngine.Events;
using System.Collections;

public class BalanceManager : MonoBehaviour
{
    // 将来的なイベント切り替え用。
    // Horizontal: 横ゲージ、Vertical: 縦ゲージとしてUIを動かします。
    public enum BalanceGaugeDirection
    {
        Horizontal,
        Vertical
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
    // イベント中にゲージ全体を画面左端へ置くための設定です。
    // Canvasの左中央に寄せたいので、anchor/pivotの初期値を左中央にしています。
    [SerializeField] private Vector2 eventVerticalAnchorMin = new Vector2(0f, 0.5f);
    [SerializeField] private Vector2 eventVerticalAnchorMax = new Vector2(0f, 0.5f);
    [SerializeField] private Vector2 eventVerticalPivot = new Vector2(0f, 0.5f);
    [SerializeField] private Vector2 eventVerticalAnchoredPosition = new Vector2(40f, 0f);
    // 縦表示時のBalanceBarの大きさです。xが太さ、yが長さです。
    [SerializeField] private Vector2 eventVerticalBarSize = new Vector2(28f, 240f);
    // 縦表示時のTargetZoneの大きさです。xが幅、yが縦方向の判定範囲です。
    [SerializeField] private Vector2 eventVerticalTargetZoneSize = new Vector2(44f, 50f);
    // 縦表示時のBalancePointの大きさです。白い球を正方形で扱います。
    [SerializeField] private Vector2 eventVerticalPointSize = new Vector2(28f, 28f);
    [SerializeField] private bool resetGaugePositionOnLayoutSwitch = true;

    [Header("Gauge Direction")]
    [SerializeField] private BalanceGaugeDirection gaugeDirection = BalanceGaugeDirection.Horizontal;

    [Header("Balance Point")]
    [SerializeField] private float pointMoveSpeed = 10f;
    [SerializeField] private KeyCode negativeKey = KeyCode.LeftArrow;
    [SerializeField] private KeyCode positiveKey = KeyCode.RightArrow;
    [SerializeField] private bool returnPointToCenter;
    [SerializeField] private float pointReturnSpeed = 80f;

    [Header("Target Zone")]
    [SerializeField] private float targetMoveSpeed = 0.5f;
    [SerializeField] private float targetMoveRangeRate = 0.85f;

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
    private float wobbleTimer;
    private Quaternion wobbleBaseRotation;
    private bool wasInsideTarget;
    private Coroutine unbalanceCoroutine;
    private float nextTimerLogTime;
    private bool hasSavedHorizontalLayout;
    private RectTransformLayout savedGaugeRootLayout;
    private RectTransformLayout savedBalanceBarLayout;
    private RectTransformLayout savedTargetZoneLayout;
    private RectTransformLayout savedBalancePointLayout;

    public bool IsInsideTarget { get; private set; }
    public float OutsideTimer => outsideTimer;
    public BalanceGaugeDirection GaugeDirection => gaugeDirection;

    private void Start()
    {
        if (gaugeRoot == null && balanceBar != null)
        {
            gaugeRoot = balanceBar.parent as RectTransform;
        }

        SaveHorizontalLayoutIfNeeded();

        // UIの初期位置を現在のInspector配置から読み取ります。
        pointAxisPosition = GetAxisAnchoredPosition(balancePoint);
        targetAxisPosition = GetAxisAnchoredPosition(targetZone);

        if (wobbleTarget != null)
        {
            wobbleBaseRotation = wobbleTarget.localRotation;
        }

        ApplyAllUiPositions();
        UpdateBalanceState();
        wasInsideTarget = IsInsideTarget;

        DebugLog($"Start: IsInsideTarget={IsInsideTarget}, animator={(animator != null ? animator.name : "null")}, failTimeLimit={failTimeLimit}");
        CheckAnimatorSetup();
    }

    private void Update()
    {
        MoveTargetZone();
        MoveBalancePoint();
        ApplyAllUiPositions();
        UpdateBalanceState();
        UpdateFailureTimer();
        UpdateWobble();
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

        SetGaugeDirection(BalanceGaugeDirection.Vertical);
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
        ApplyAllUiPositions();
        UpdateBalanceState();
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

        IsInsideTarget = pointAxisPosition >= min && pointAxisPosition <= max;

        // 状態が変わった瞬間だけイベントを呼びます。
        if (IsInsideTarget != wasInsideTarget)
        {
            DebugLog($"Balance state changed: IsInsideTarget={IsInsideTarget}, point={pointAxisPosition:F2}, targetMin={min:F2}, targetMax={max:F2}, targetCenter={targetAxisPosition:F2}");

            if (IsInsideTarget)
            {
                onBalanceSuccess?.Invoke();
            }
            else
            {
                onBalanceMiss?.Invoke();
            }

            wasInsideTarget = IsInsideTarget;
        }
    }

    private void UpdateFailureTimer()
    {
        if (IsInsideTarget)
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
            root.localRotation = Quaternion.identity;
        }

        if (balanceBar != null)
        {
            balanceBar.sizeDelta = eventVerticalBarSize;
            balanceBar.localRotation = Quaternion.identity;
        }

        if (targetZone != null)
        {
            targetZone.sizeDelta = eventVerticalTargetZoneSize;
            SetCrossAxisAnchoredPosition(targetZone, 0f);
        }

        if (balancePoint != null)
        {
            balancePoint.sizeDelta = eventVerticalPointSize;
            SetCrossAxisAnchoredPosition(balancePoint, 0f);
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
        DebugLog($"outsideTimer={outsideTimer:F2}/{failTimeLimit:F2}, point={pointAxisPosition:F2}, target={targetAxisPosition:F2}, IsInsideTarget={IsInsideTarget}");
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
