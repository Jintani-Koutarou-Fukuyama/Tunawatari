using System.Collections;
using UnityEngine;
using UnityEngine.Events;

public class SniperBulletShooter : MonoBehaviour
{
    [System.Serializable]
    public class ShotEvent : UnityEvent<int>
    {
    }

    [Header("References")]
    // 4本のレーザー位置を管理しているスクリプトです。
    // ここから発射位置と狙う位置を受け取ります。
    [SerializeField] private SniperLaserController laserController;
    // 弾として生成するPrefabです。
    // 未設定でも、Create Simple Bullet If MissingがONなら仮の球を自動生成します。
    [SerializeField] private GameObject bulletPrefab;
    // ここを設定すると、レーザーのTargetPointより優先して全弾がこのTransformを狙います。
    // プレイヤーの胸や腰あたりに空オブジェクトを置いて入れると調整しやすいです。
    [SerializeField] private Transform aimTargetOverride;
    // Defense中の黄色ゲージ位置を、これから発射する弾の高さに合わせるために使います。
    // SniperEventManagerがDefense StateでBalanceManagerをSniperDefenseModeにしている時だけ動きます。
    [SerializeField] private BalanceManager balanceManager;

    [Header("Shot Settings")]
    // 合計で何発撃つかです。今回の仕様では4発です。
    [SerializeField] private int totalShots = 4;
    // 弾の速さです。遅めに見せたいので初期値は小さめです。
    [SerializeField] private float bulletSpeed = 4f;
    // 弾がSceneに残り続けないよう、自動削除するまでの秒数です。
    [SerializeField] private float bulletLifeTime = 6f;
    // 1発撃つ前にレーザーを点滅させて予告する時間です。
    [SerializeField] private float preFireBlinkTime = 1f;
    // 1発撃ったあと、次の発射まで待つ時間です。
    [SerializeField] private float intervalBetweenShots = 0.6f;
    // trueにすると、Time.timeScaleの影響を受けずに発射間隔を進めます。
    [SerializeField] private bool useUnscaledTime;

    [Header("Cinematic Slow Motion")]
    // trueにすると、TimeScaleは変えずに弾自身の速度だけを距離で落とします。
    [SerializeField] private bool enableBulletSlowMotion = true;
    // 減速の基準にするTransformです。未設定ならAim Target OverrideやレーザーのTargetPointを使います。
    [SerializeField] private Transform slowMotionTargetOverride;
    // この距離より近くなったら弾を減速させます。
    [SerializeField] private float slowStartDistance = 4f;
    // この距離まで近づいたら最低速度になります。
    [SerializeField] private float minimumSpeedDistance = 0.6f;
    // 最低速度の倍率です。0.18なら通常速度の18%です。
    [SerializeField] private float minimumSpeedMultiplier = 0.18f;
    // 距離による速度変化カーブです。右側ほど遠く、左側ほど近い状態です。
    [SerializeField] private AnimationCurve speedMultiplierCurve = AnimationCurve.EaseInOut(0f, 0.18f, 1f, 1f);

    [Header("Fallback Bullet")]
    // bulletPrefabが未設定の時に、確認用の小さい球を自動で作るかどうかです。
    [SerializeField] private bool createSimpleBulletIfMissing = true;
    [SerializeField] private float simpleBulletSize = 0.18f;
    [SerializeField] private Color simpleBulletColor = Color.white;

    [Header("Hit Detection")]
    // trueにすると、生成した弾のColliderをTriggerにします。
    // 棒側のSniperStickDefenseがOnTriggerEnterで防御判定を受け取るためです。
    [SerializeField] private bool makeBulletColliderTrigger = true;
    // trueにすると、弾にキネマティックRigidbodyを付けます。
    // UnityではTrigger判定にRigidbodyが必要になることがあるため、判定を安定させます。
    [SerializeField] private bool addKinematicRigidbody = true;

    [Header("Laser During Shot")]
    // 発射シーケンス中にレーザーを表示するかどうかです。
    [SerializeField] private bool showLaserDuringSequence = true;
    // 全弾を撃ち終わった後にレーザーを消すかどうかです。
    [SerializeField] private bool hideLaserAfterSequence;

    [Header("Events")]
    [SerializeField] private UnityEvent onSequenceStarted;
    [SerializeField] private UnityEvent onSequenceFinished;
    [SerializeField] private ShotEvent onShotFired;

    [Header("Debug")]
    [SerializeField] private bool debugLog = true;

    private Coroutine fireCoroutine;
    private Material simpleBulletMaterial;

    public bool IsFiring => fireCoroutine != null;

    public void StartFireSequence()
    {
        if (fireCoroutine != null)
        {
            Log("StartFireSequence was ignored because bullets are already firing.");
            return;
        }

        if (laserController == null)
        {
            Log("StartFireSequence was ignored because laserController is not assigned.");
            return;
        }

        fireCoroutine = StartCoroutine(FireSequence());
    }

    public void StopFireSequence()
    {
        if (fireCoroutine != null)
        {
            StopCoroutine(fireCoroutine);
            fireCoroutine = null;
        }

        if (hideLaserAfterSequence && laserController != null)
        {
            laserController.HideLasers();
        }
    }

    private IEnumerator FireSequence()
    {
        Log("Bullet sequence started.");
        onSequenceStarted?.Invoke();

        if (showLaserDuringSequence)
        {
            laserController.ShowLasers();
        }

        for (int shotIndex = 0; shotIndex < totalShots; shotIndex++)
        {
            // Random.Range(0, 4)は0,1,2,3のどれかを選びます。
            // これで4本のレーザーからランダムに1本を選べます。
            int laserIndex = Random.Range(0, 4);

            Log($"Shot {shotIndex + 1}/{totalShots}. Laser = {laserIndex}");
            yield return Wait(preFireBlinkTime);

            SetDefenseBalanceTargetForShot(laserIndex);
            FireBullet(laserIndex);
            onShotFired?.Invoke(laserIndex);

            yield return Wait(intervalBetweenShots);
        }

        if (hideLaserAfterSequence)
        {
            laserController.HideLasers();
        }

        Log("Bullet sequence finished.");
        onSequenceFinished?.Invoke();
        fireCoroutine = null;
    }

    private void SetDefenseBalanceTargetForShot(int laserIndex)
    {
        if (balanceManager == null)
        {
            Log("Balance target sync skipped because balanceManager is not assigned.");
            return;
        }

        if (balanceManager.PlayMode != BalanceManager.BalancePlayMode.SniperDefense)
        {
            Log($"Balance target sync skipped because BalanceManager mode is {balanceManager.PlayMode}.");
            return;
        }

        float normalizedPosition = GetBalanceTargetPositionFromLaserHeight(laserIndex);
        balanceManager.SetSniperTargetPosition(normalizedPosition);
        Log($"Defense balance target set. laser={laserIndex}, normalized={normalizedPosition:F2}");
    }

    private float GetBalanceTargetPositionFromLaserHeight(int laserIndex)
    {
        int[] sortedLaserIndices = { 0, 1, 2, 3 };
        float[] laserHeights = new float[4];

        for (int i = 0; i < 4; i++)
        {
            laserHeights[i] = GetLaserShotHeight(i);
        }

        for (int i = 0; i < sortedLaserIndices.Length - 1; i++)
        {
            for (int j = i + 1; j < sortedLaserIndices.Length; j++)
            {
                int currentLaserIndex = sortedLaserIndices[i];
                int nextLaserIndex = sortedLaserIndices[j];

                if (laserHeights[currentLaserIndex] >= laserHeights[nextLaserIndex])
                {
                    continue;
                }

                sortedLaserIndices[i] = nextLaserIndex;
                sortedLaserIndices[j] = currentLaserIndex;
            }
        }

        float[] normalizedPositionsByRank = { 1f, 0.66f, 0.33f, 0f };

        for (int rank = 0; rank < sortedLaserIndices.Length; rank++)
        {
            if (sortedLaserIndices[rank] != laserIndex)
            {
                continue;
            }

            return normalizedPositionsByRank[rank];
        }

        return 0.5f;
    }

    private float GetLaserShotHeight(int laserIndex)
    {
        Vector3 firePosition = laserController.GetFirePosition(laserIndex);
        Vector3 targetPosition = aimTargetOverride != null
            ? aimTargetOverride.position
            : laserController.GetTargetPosition(laserIndex);

        return (firePosition.y + targetPosition.y) * 0.5f;
    }

    private void FireBullet(int laserIndex)
    {
        Vector3 startPosition = laserController.GetFirePosition(laserIndex);
        Vector3 targetPosition = aimTargetOverride != null
            ? aimTargetOverride.position
            : laserController.GetTargetPosition(laserIndex);

        Vector3 direction = targetPosition - startPosition;

        if (direction.sqrMagnitude < 0.0001f)
        {
            // 発射位置と狙い位置が同じだと向きが作れないため、
            // 念のためこのGameObjectの前方向へ撃ちます。
            direction = transform.forward;
        }

        GameObject bulletObject = CreateBulletObject(startPosition, direction.normalized);
        SetupBulletHitDetection(bulletObject);
        SniperBullet bullet = bulletObject.GetComponent<SniperBullet>();

        if (bullet == null)
        {
            bullet = bulletObject.AddComponent<SniperBullet>();
        }

        Transform slowTarget = GetSlowMotionTarget(laserIndex);
        bullet.Initialize(
            direction,
            bulletSpeed,
            bulletLifeTime,
            useUnscaledTime,
            slowTarget,
            enableBulletSlowMotion,
            slowStartDistance,
            minimumSpeedDistance,
            minimumSpeedMultiplier,
            speedMultiplierCurve);

        bullet.SetDefenseArrivalCheck(balanceManager, GetDefenseArrivalTarget(laserIndex));
    }

    private Transform GetSlowMotionTarget(int laserIndex)
    {
        if (slowMotionTargetOverride != null)
        {
            return slowMotionTargetOverride;
        }

        if (aimTargetOverride != null)
        {
            return aimTargetOverride;
        }

        return laserController.GetTargetPoint(laserIndex);
    }

    private Transform GetDefenseArrivalTarget(int laserIndex)
    {
        if (aimTargetOverride != null)
        {
            return aimTargetOverride;
        }

        return laserController.GetTargetPoint(laserIndex);
    }

    private GameObject CreateBulletObject(Vector3 startPosition, Vector3 direction)
    {
        if (bulletPrefab != null)
        {
            return Instantiate(bulletPrefab, startPosition, Quaternion.LookRotation(direction));
        }

        if (!createSimpleBulletIfMissing)
        {
            GameObject emptyBullet = new GameObject("SniperBullet");
            emptyBullet.transform.position = startPosition;
            emptyBullet.transform.rotation = Quaternion.LookRotation(direction);
            return emptyBullet;
        }

        GameObject simpleBullet = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        simpleBullet.name = "SniperBullet";
        simpleBullet.transform.position = startPosition;
        simpleBullet.transform.rotation = Quaternion.LookRotation(direction);
        simpleBullet.transform.localScale = Vector3.one * simpleBulletSize;

        Renderer renderer = simpleBullet.GetComponent<Renderer>();
        if (renderer != null)
        {
            if (simpleBulletMaterial == null)
            {
                simpleBulletMaterial = new Material(Shader.Find("Sprites/Default"));
                simpleBulletMaterial.color = simpleBulletColor;
            }

            renderer.material = simpleBulletMaterial;
        }

        return simpleBullet;
    }

    private void SetupBulletHitDetection(GameObject bulletObject)
    {
        Collider collider = bulletObject.GetComponent<Collider>();
        if (collider == null)
        {
            // PrefabにColliderが無い場合でも棒との当たり判定を取れるよう、
            // 小さなSphereColliderを補助的に付けます。
            collider = bulletObject.AddComponent<SphereCollider>();
        }

        collider.isTrigger = makeBulletColliderTrigger;

        if (!addKinematicRigidbody)
        {
            return;
        }

        Rigidbody rigidbody = bulletObject.GetComponent<Rigidbody>();
        if (rigidbody == null)
        {
            rigidbody = bulletObject.AddComponent<Rigidbody>();
        }

        // isKinematicをtrueにすると物理エンジンに押されず、
        // Transform移動のままTrigger判定だけを使えます。
        rigidbody.isKinematic = true;
        rigidbody.useGravity = false;
    }

    private IEnumerator Wait(float seconds)
    {
        if (seconds <= 0f)
        {
            yield break;
        }

        if (useUnscaledTime)
        {
            yield return new WaitForSecondsRealtime(seconds);
        }
        else
        {
            yield return new WaitForSeconds(seconds);
        }
    }

    private void Log(string message)
    {
        if (!debugLog)
        {
            return;
        }

        Debug.Log($"[SniperBulletShooter] {message}", this);
    }
}
