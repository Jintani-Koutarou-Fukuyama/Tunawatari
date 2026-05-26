using UnityEngine;

public class SniperBullet : MonoBehaviour
{
    [Header("Move")]
    // 弾が1秒間に進む距離です。
    // 数字を小さくすると、マトリックス風に見える遅い弾になります。
    [SerializeField] private float speed = 4f;
    // 弾を自動で消すまでの秒数です。
    // 画面外へ飛んだ弾がSceneに残り続けないようにします。
    [SerializeField] private float lifeTime = 6f;
    // trueにすると、Time.timeScaleを下げた時でも弾の速度が変わりません。
    // スローモーション演出で弾も遅くしたい場合はfalseにします。
    [SerializeField] private bool useUnscaledTime;

    [Header("Cinematic Slow Motion")]
    // trueにすると、Time.timeScaleを変えずに「弾の速度だけ」を距離で変えます。
    // プレイヤーに近づくほど弾が遅くなり、演出として見せ場を作れます。
    [SerializeField] private bool slowDownNearTarget = true;
    // 減速の基準にする相手です。基本はプレイヤー付近のTransformを入れます。
    [SerializeField] private Transform slowTarget;
    // この距離より近くなったら減速を始めます。
    [SerializeField] private float slowStartDistance = 4f;
    // この距離まで近づくと、ほぼ最低速度になります。
    [SerializeField] private float minimumSpeedDistance = 0.6f;
    // 一番遅い時の速度倍率です。0.2なら通常速度の20%です。
    [SerializeField] private float minimumSpeedMultiplier = 0.18f;
    // 距離に応じた減速カーブです。
    // 左が近い時、右が遠い時で、値が小さいほど遅くなります。
    [SerializeField] private AnimationCurve speedMultiplierCurve = AnimationCurve.EaseInOut(0f, 0.18f, 1f, 1f);

    private Vector3 moveDirection = Vector3.forward;
    private float lifeTimer;
    private bool isDestroyed;

    public bool IsDestroyed => isDestroyed;

    public void Initialize(Vector3 direction, float newSpeed, float newLifeTime, bool newUseUnscaledTime)
    {
        Initialize(direction, newSpeed, newLifeTime, newUseUnscaledTime, null, slowDownNearTarget);
    }

    public void Initialize(
        Vector3 direction,
        float newSpeed,
        float newLifeTime,
        bool newUseUnscaledTime,
        Transform newSlowTarget,
        bool newSlowDownNearTarget)
    {
        Initialize(
            direction,
            newSpeed,
            newLifeTime,
            newUseUnscaledTime,
            newSlowTarget,
            newSlowDownNearTarget,
            slowStartDistance,
            minimumSpeedDistance,
            minimumSpeedMultiplier,
            speedMultiplierCurve);
    }

    public void Initialize(
        Vector3 direction,
        float newSpeed,
        float newLifeTime,
        bool newUseUnscaledTime,
        Transform newSlowTarget,
        bool newSlowDownNearTarget,
        float newSlowStartDistance,
        float newMinimumSpeedDistance,
        float newMinimumSpeedMultiplier,
        AnimationCurve newSpeedMultiplierCurve)
    {
        // normalizedは「向きだけ」を残して長さを1にする処理です。
        // これをしないと、距離が遠いほど弾が速くなる可能性があります。
        moveDirection = direction.normalized;
        speed = newSpeed;
        lifeTime = newLifeTime;
        useUnscaledTime = newUseUnscaledTime;
        slowTarget = newSlowTarget;
        slowDownNearTarget = newSlowDownNearTarget;
        slowStartDistance = Mathf.Max(0.001f, newSlowStartDistance);
        minimumSpeedDistance = Mathf.Max(0f, newMinimumSpeedDistance);
        minimumSpeedMultiplier = Mathf.Clamp01(newMinimumSpeedMultiplier);
        speedMultiplierCurve = newSpeedMultiplierCurve;
        lifeTimer = 0f;
        isDestroyed = false;

        if (moveDirection.sqrMagnitude > 0.0001f)
        {
            // 弾の見た目を進行方向へ向けます。
            // 弾モデルを使う時に、先端が進行方向を向くようにするためです。
            transform.rotation = Quaternion.LookRotation(moveDirection);
        }
    }

    private void Update()
    {
        if (isDestroyed)
        {
            return;
        }

        // deltaTimeは「前のフレームから何秒経ったか」です。
        // これを掛けることで、PCの性能差があっても同じ速度で動きます。
        float deltaTime = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        float currentSpeed = speed * GetSpeedMultiplier();

        transform.position += moveDirection * currentSpeed * deltaTime;
        lifeTimer += deltaTime;

        if (lifeTimer >= lifeTime)
        {
            DestroyBullet();
        }
    }

    public void DestroyBullet()
    {
        if (isDestroyed)
        {
            return;
        }

        isDestroyed = true;
        Destroy(gameObject);
    }

    private float GetSpeedMultiplier()
    {
        if (!slowDownNearTarget || slowTarget == null)
        {
            return 1f;
        }

        // Distanceは2つの位置の距離を調べます。
        // ここでは「弾がプレイヤーへどれくらい近づいたか」を見ています。
        float distance = Vector3.Distance(transform.position, slowTarget.position);

        if (distance >= slowStartDistance)
        {
            return 1f;
        }

        // InverseLerpは、値が範囲内のどの割合にいるかを0〜1で返します。
        // minimumSpeedDistanceに近いほど0、slowStartDistanceに近いほど1になります。
        float normalizedDistance = Mathf.InverseLerp(minimumSpeedDistance, slowStartDistance, distance);
        float curveMultiplier = speedMultiplierCurve != null
            ? speedMultiplierCurve.Evaluate(normalizedDistance)
            : normalizedDistance;

        // Clampは値を範囲内に収める処理です。
        // カーブが0以下になって弾が止まりすぎる事故を防ぎます。
        float multiplier = Mathf.Clamp(curveMultiplier, minimumSpeedMultiplier, 1f);

        if (distance <= minimumSpeedDistance)
        {
            multiplier = minimumSpeedMultiplier;
        }

        return multiplier;
    }
}
