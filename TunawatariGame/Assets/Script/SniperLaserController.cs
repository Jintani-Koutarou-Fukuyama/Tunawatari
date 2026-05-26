using UnityEngine;

public class SniperLaserController : MonoBehaviour
{
    [System.Serializable]
    public class LaserSlot
    {
        [Header("References")]
        // 赤い線を描画するLineRendererです。
        // 未設定の場合は、Auto Create LasersがONなら自動で作ります。
        [SerializeField] private LineRenderer lineRenderer;
        // 将来的に弾の発射位置として使うTransformです。
        // ここからtargetPoint方向へ弾を飛ばす設計にできます。
        [SerializeField] private Transform firePoint;
        // レーザーが狙う先です。基本はプレイヤー付近のTransformを入れます。
        [SerializeField] private Transform targetPoint;

        [Header("Fallback Positions")]
        // firePointが未設定の時に使う開始位置です。
        [SerializeField] private Vector3 localStartPosition;
        // targetPointが未設定の時に使う終了位置です。
        [SerializeField] private Vector3 localEndPosition = new Vector3(8f, 0f, 0f);

        public LineRenderer LineRenderer => lineRenderer;
        public Transform FirePoint => firePoint;
        public Transform TargetPoint => targetPoint;

        public void SetLineRenderer(LineRenderer renderer)
        {
            lineRenderer = renderer;
        }

        public Vector3 GetStartPosition(Transform owner)
        {
            return firePoint != null ? firePoint.position : owner.TransformPoint(localStartPosition);
        }

        public Vector3 GetEndPosition(Transform owner)
        {
            return targetPoint != null ? targetPoint.position : owner.TransformPoint(localEndPosition);
        }
    }

    [Header("Laser Setup")]
    [SerializeField] private LaserSlot[] lasers = new LaserSlot[4];
    [SerializeField] private bool autoCreateLasers = true;
    [SerializeField] private bool hideOnAwake = true;

    [Header("Visual")]
    [SerializeField] private Color laserColor = new Color(1f, 0f, 0f, 0.9f);
    [SerializeField] private float laserWidth = 0.035f;
    [SerializeField] private Material laserMaterial;

    [Header("Blink")]
    [SerializeField] private bool blinkEnabled = true;
    [SerializeField] private float blinkSpeed = 8f;
    [SerializeField] private float minAlpha = 0.2f;
    [SerializeField] private float maxAlpha = 1f;
    // trueにすると、Time.timeScaleを下げたスローモーション中でも点滅速度が落ちません。
    // スナイパーイベントではスローモーション演出を使う予定なので、初期値はtrueにしています。
    [SerializeField] private bool useUnscaledTime = true;

    [Header("Debug")]
    [SerializeField] private bool debugLog = true;

    private bool lasersVisible;

    public bool LasersVisible => lasersVisible;

    private void Awake()
    {
        EnsureLaserArray();

        if (autoCreateLasers)
        {
            CreateMissingLineRenderers();
        }

        ApplyLineSettings();
        SetLasersVisible(!hideOnAwake);
    }

    private void Update()
    {
        if (!lasersVisible)
        {
            return;
        }

        UpdateLaserPositions();
        UpdateBlink();
    }

    public void ShowLasers()
    {
        SetLasersVisible(true);
        Log("Lasers shown.");
    }

    public void HideLasers()
    {
        SetLasersVisible(false);
        Log("Lasers hidden.");
    }

    public void SetLasersVisible(bool visible)
    {
        lasersVisible = visible;

        EnsureLaserArray();

        for (int i = 0; i < lasers.Length; i++)
        {
            LineRenderer renderer = lasers[i]?.LineRenderer;
            if (renderer == null)
            {
                continue;
            }

            renderer.enabled = visible;
        }

        if (visible)
        {
            UpdateLaserPositions();
            UpdateBlink();
        }
    }

    public Transform GetFirePoint(int index)
    {
        if (lasers == null || index < 0 || index >= lasers.Length || lasers[index] == null)
        {
            return null;
        }

        return lasers[index].FirePoint;
    }

    public Transform GetTargetPoint(int index)
    {
        if (lasers == null || index < 0 || index >= lasers.Length || lasers[index] == null)
        {
            return null;
        }

        return lasers[index].TargetPoint;
    }

    public Vector3 GetFirePosition(int index)
    {
        if (lasers == null || index < 0 || index >= lasers.Length || lasers[index] == null)
        {
            return transform.position;
        }

        return lasers[index].GetStartPosition(transform);
    }

    public Vector3 GetTargetPosition(int index)
    {
        if (lasers == null || index < 0 || index >= lasers.Length || lasers[index] == null)
        {
            return transform.position + transform.forward;
        }

        return lasers[index].GetEndPosition(transform);
    }

    private void EnsureLaserArray()
    {
        if (lasers == null || lasers.Length != 4)
        {
            LaserSlot[] newLasers = new LaserSlot[4];

            for (int i = 0; i < newLasers.Length; i++)
            {
                if (lasers != null && i < lasers.Length)
                {
                    newLasers[i] = lasers[i];
                }

                if (newLasers[i] == null)
                {
                    newLasers[i] = new LaserSlot();
                }
            }

            lasers = newLasers;
        }

        for (int i = 0; i < lasers.Length; i++)
        {
            if (lasers[i] == null)
            {
                lasers[i] = new LaserSlot();
            }
        }
    }

    private void CreateMissingLineRenderers()
    {
        for (int i = 0; i < lasers.Length; i++)
        {
            if (lasers[i].LineRenderer != null)
            {
                continue;
            }

            GameObject laserObject = new GameObject($"SniperLaser_{i + 1}");
            laserObject.transform.SetParent(transform, false);

            LineRenderer renderer = laserObject.AddComponent<LineRenderer>();
            lasers[i].SetLineRenderer(renderer);
        }
    }

    private void ApplyLineSettings()
    {
        for (int i = 0; i < lasers.Length; i++)
        {
            LineRenderer renderer = lasers[i]?.LineRenderer;
            if (renderer == null)
            {
                continue;
            }

            renderer.positionCount = 2;
            renderer.useWorldSpace = true;
            renderer.startWidth = laserWidth;
            renderer.endWidth = laserWidth;
            renderer.numCapVertices = 4;

            if (laserMaterial != null)
            {
                renderer.material = laserMaterial;
            }
            else if (renderer.sharedMaterial == null)
            {
                renderer.material = new Material(Shader.Find("Sprites/Default"));
            }

            renderer.startColor = laserColor;
            renderer.endColor = laserColor;
        }
    }

    private void UpdateLaserPositions()
    {
        for (int i = 0; i < lasers.Length; i++)
        {
            LaserSlot laser = lasers[i];
            LineRenderer renderer = laser?.LineRenderer;
            if (laser == null || renderer == null)
            {
                continue;
            }

            renderer.SetPosition(0, laser.GetStartPosition(transform));
            renderer.SetPosition(1, laser.GetEndPosition(transform));
        }
    }

    private void UpdateBlink()
    {
        float alpha = maxAlpha;

        if (blinkEnabled)
        {
            float time = useUnscaledTime ? Time.unscaledTime : Time.time;
            float wave = Mathf.PingPong(time * blinkSpeed, 1f);
            alpha = Mathf.Lerp(minAlpha, maxAlpha, wave);
        }

        Color currentColor = laserColor;
        currentColor.a = alpha;

        for (int i = 0; i < lasers.Length; i++)
        {
            LineRenderer renderer = lasers[i]?.LineRenderer;
            if (renderer == null)
            {
                continue;
            }

            renderer.startColor = currentColor;
            renderer.endColor = currentColor;
        }
    }

    private void Log(string message)
    {
        if (!debugLog)
        {
            return;
        }

        Debug.Log($"[SniperLaserController] {message}", this);
    }
}
