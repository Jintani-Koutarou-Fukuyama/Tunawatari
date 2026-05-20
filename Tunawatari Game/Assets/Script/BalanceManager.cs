using UnityEngine;
using UnityEngine.Events;

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

    [Header("UI")]
    [SerializeField] private RectTransform balanceBar;
    [SerializeField] private RectTransform targetZone;
    [SerializeField] private RectTransform balancePoint;

    [Header("Gauge Direction")]
    [SerializeField] private BalanceGaugeDirection gaugeDirection = BalanceGaugeDirection.Horizontal;

    [Header("Balance Point")]
    [SerializeField] private float pointMoveSpeed = 260f;
    [SerializeField] private KeyCode negativeKey = KeyCode.LeftArrow;
    [SerializeField] private KeyCode positiveKey = KeyCode.RightArrow;
    [SerializeField] private bool returnPointToCenter;
    [SerializeField] private float pointReturnSpeed = 80f;

    [Header("Target Zone")]
    [SerializeField] private float targetMoveSpeed = 80f;
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

    private float pointAxisPosition;
    private float targetAxisPosition;
    private float targetMoveDirection = 1f;
    private float outsideTimer;
    private float wobbleTimer;
    private Quaternion wobbleBaseRotation;
    private bool wasInsideTarget;

    public bool IsInsideTarget { get; private set; }
    public float OutsideTimer => outsideTimer;
    public BalanceGaugeDirection GaugeDirection => gaugeDirection;

    private void Start()
    {
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
            outsideTimer = 0f;
            return;
        }

        outsideTimer += Time.deltaTime;

        // 一定時間外れたら、ふらつきとダメージ通知を発生させます。
        if (outsideTimer >= failTimeLimit)
        {
            outsideTimer = 0f;
            StartWobble();
            onDamage?.Invoke(damageAmount);

            if (resetPointAfterDamage)
            {
                pointAxisPosition = 0f;
            }
        }
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
}
