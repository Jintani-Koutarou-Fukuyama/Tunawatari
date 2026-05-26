using UnityEngine;

// プレイヤーをロープ上だけで前後に動かすためのスクリプトです。
// Rigidbodyを使わず、Transformの位置を直接変えてシンプルに制御します。
public class TightropePlayerMover : MonoBehaviour
{
    [Header("Rope")]
    // プレイヤーが乗るロープの中心線情報です。
    // InspectorでRopeオブジェクトに付けたRopePathを入れると、そのロープ上だけを移動できます。
    [SerializeField] private RopePath ropePath;
    // ロープ中心からプレイヤーをどれだけ上に置くかの高さです。
    // モデルの足元がロープに合うようにInspectorで調整します。
    [SerializeField] private float heightOffset = 0.9f;

    [Header("Move")]
    // プレイヤーが1秒間にロープ上をどれくらい進むかです。
    // 数字を大きくすると速く、小さくするとゆっくり動きます。
    [SerializeField] private float moveSpeed = 1.5f;
    // trueにすると、進む向きに合わせてプレイヤーの体の向きを変えます。
    // 後ろへ進む時に背中向きのまま滑るのを防ぐために使います。
    [SerializeField] private bool faceMoveDirection = true;
    // 回転が目的の向きへ近づく速さです。
    // Lerp系の値は「一気に変える」のではなく「なめらかに近づける」ために使います。
    [SerializeField] private float rotationLerpSpeed = 12f;

    [Header("Input")]
    // 前へ進むキーです。初期値はWキーです。
    // Inspectorで別のキーに変えると、コードを書き換えずに操作を変更できます。
    [SerializeField] private KeyCode forwardKey = KeyCode.W;
    // 後ろへ進むキーです。初期値はSキーです。
    // 今回は前後移動だけなので、A/Dはこのスクリプトでは使いません。
    [SerializeField] private KeyCode backwardKey = KeyCode.S;

    [Header("Animation")]
    // プレイヤーのAnimatorです。
    // 既存のIdle/Walk切り替えに使うだけで、Animator Controller自体は変更しません。
    [SerializeField] private Animator animator;
    // 歩きアニメーションをON/OFFするAnimatorのBool名です。
    // 既存設定に合わせて、初期値はcatwalkにしています。
    [SerializeField] private string walkBoolName = "catwalk";
    // trueなら移動入力に合わせてAnimatorのBoolを更新します。
    // 既存の別スクリプトでアニメ管理したい時はfalseにできます。
    [SerializeField] private bool updateWalkBool = true;

    // プレイヤーが現在どれくらいロープを進んでいるかを保存します。
    // 0 = ロープのスタート地点、ropePath.Length = ロープの終点です。
    private float distanceAlongRope;
    // 直前フレームの入力値を保存します。
    // 1 = 前進、-1 = 後退、0 = 入力なし、という意味です。
    private float lastMoveInput;

    // 他のスクリプトから現在の入力状態を読みたい時のための公開プロパティです。
    // 値を外から勝手に変更されないよう、読み取り専用にしています。
    public float MoveInput => lastMoveInput;

    // ResetはUnityでコンポーネントを追加した時やResetした時に呼ばれます。
    // Inspector設定の手間を減らすため、自動でAnimatorとRopePathを探します。
    private void Reset()
    {
        // 子オブジェクトも含めてAnimatorを探します。
        // プレイヤーモデルのAnimatorが子階層にある場合でも拾えるようにしています。
        animator = GetComponentInChildren<Animator>();
        // シーン内の「Rope」という名前のオブジェクトからRopePathを探します。
        // Inspectorで入れ忘れても動かしやすくするための補助です。
        AutoFindRopePath();
    }

    // Awakeはゲーム開始時、Startより前に呼ばれます。
    // 参照が空のままだと後の処理で動けないので、ここで自動補完します。
    private void Awake()
    {
        // ropePathがInspectorで未設定なら、自動で探します。
        if (ropePath == null)
        {
            AutoFindRopePath();
        }

        // animatorがInspectorで未設定なら、自動で探します。
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }
    }

    // Startは最初のUpdateの前に1回だけ呼ばれます。
    // 初期位置をロープ上に合わせる準備をします。
    private void Start()
    {
        // RopePathが無いとロープの場所が分からないので、警告を出して処理を止めます。
        if (ropePath == null)
        {
            Debug.LogWarning("TightropePlayerMover: RopePath が設定されていません。", this);
            return;
        }

        // ロープのスタート、終点、長さを最新状態に作り直します。
        // Unity上でロープを動かした後でも正しい位置を使うためです。
        ropePath.RebuildPath();
        // 現在のプレイヤー位置がロープ上のどの距離に近いかを計算します。
        // これにより、ゲーム開始時に急にスタート地点へ飛ばず、近い位置から始められます。
        distanceAlongRope = ropePath.GetDistanceAlongRope(transform.position);
        // 最初からロープ中心に乗せます。
        // ロープ外に少しずれて配置されていても、開始時に補正できます。
        SnapToRope();
    }

    // Updateは毎フレーム呼ばれます。
    // 入力、移動距離、位置補正、向き、アニメーションを更新します。
    private void Update()
    {
        // RopePathが無いと移動先を計算できないので何もしません。
        if (ropePath == null)
        {
            return;
        }

        // W/Sなどの入力を読み取り、-1から1の値として保存します。
        lastMoveInput = ReadMoveInput();
        // 入力値、移動速度、Time.deltaTimeを使って進む距離を増減します。
        // Time.deltaTimeを掛けることで、PCの処理速度が違っても移動速度が安定します。
        distanceAlongRope += lastMoveInput * moveSpeed * Time.deltaTime;
        // Clampは値を指定範囲内に収める処理です。
        // 0より小さくならず、ロープの長さより大きくならないので、ロープ端から落ちません。
        distanceAlongRope = Mathf.Clamp(distanceAlongRope, 0f, ropePath.Length);

        // 距離情報から実際のワールド座標を計算し、プレイヤーをロープ中心に置きます。
        SnapToRope();
        // プレイヤーの向きをロープ方向へ合わせます。
        UpdateRotation();
        // 移動しているかどうかをAnimatorへ伝えます。
        UpdateAnimation();
    }

    // 前進/後退キーの入力を、移動に使いやすい数値へ変換する関数です。
    private float ReadMoveInput()
    {
        // まず入力なしを0として用意します。
        float input = 0f;

        // 前進キーが押されていれば、前方向として+1します。
        if (Input.GetKey(forwardKey))
        {
            input += 1f;
        }

        // 後退キーが押されていれば、後ろ方向として-1します。
        if (Input.GetKey(backwardKey))
        {
            input -= 1f;
        }

        // 前進と後退を同時押ししても、値が-1から1の範囲に収まるようにします。
        // Clampは「これ以上大きくしない・小さくしない」という安全装置です。
        return Mathf.Clamp(input, -1f, 1f);
    }

    // プレイヤーの位置を、現在のdistanceAlongRopeに対応したロープ上の点へ移動します。
    private void SnapToRope()
    {
        // Transformは「位置・回転・大きさ」を持つUnityの基本コンポーネントです。
        // transform.positionを変えると、Rigidbodyなしでオブジェクトの場所を直接変えられます。
        // Vector3.up * heightOffsetを足すことで、ロープ中心より少し上にプレイヤーを置きます。
        transform.position = ropePath.GetPointAtDistance(distanceAlongRope) + Vector3.up * heightOffset;
    }

    // プレイヤーの体の向きを、進行方向へなめらかに合わせる関数です。
    private void UpdateRotation()
    {
        // RopePath.Forwardは、ロープのスタートから終点へ向かう方向です。
        // Vector3は3D空間の方向や位置を表す型です。
        Vector3 forward = ropePath.Forward;
        // 後退中で、進行方向を向く設定なら、向きを反対にします。
        // これで後ろへ進む時もキャラクターが進む方向を向きます。
        if (faceMoveDirection && lastMoveInput < -0.01f)
        {
            forward = -forward;
        }

        // sqrMagnitudeはベクトルの長さの二乗です。
        // 方向がほぼ0だと回転を作れないため、ここで処理を止めます。
        if (forward.sqrMagnitude <= Mathf.Epsilon)
        {
            return;
        }

        // LookRotationは「この方向を向く回転」を作る関数です。
        // 第2引数のVector3.upは、キャラクターの上方向を世界の上に合わせるためです。
        Quaternion targetRotation = Quaternion.LookRotation(forward, Vector3.up);
        // Slerpは回転をなめらかに近づける関数です。
        // 急に向きが変わると不自然なので、少しずつ目標回転へ寄せます。
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationLerpSpeed * Time.deltaTime);
    }

    // 移動入力に合わせて歩きアニメーションのBoolを更新する関数です。
    private void UpdateAnimation()
    {
        // アニメ更新を使わない設定、Animator未設定、Bool名未設定のどれかなら何もしません。
        // 参照ミスでエラーが出るのを防ぐための安全確認です。
        if (!updateWalkBool || animator == null || string.IsNullOrEmpty(walkBoolName))
        {
            return;
        }

        // 入力の絶対値が少しでもあれば歩き中とみなします。
        // Absはマイナスをプラスにするので、前進でも後退でも歩き扱いにできます。
        animator.SetBool(walkBoolName, Mathf.Abs(lastMoveInput) > 0.01f);
    }

    // シーン内のRopeオブジェクトからRopePathを自動で探す関数です。
    // Inspector設定を忘れた時の保険として使います。
    private void AutoFindRopePath()
    {
        // GameObject.Findは、シーン内から名前でオブジェクトを探す関数です。
        // 今回はロープ名が「Rope」と決まっているため、それを探します。
        GameObject ropeObject = GameObject.Find("Rope");
        // 見つからなければ何も設定できないので戻ります。
        if (ropeObject == null)
        {
            return;
        }

        // 見つけたRopeオブジェクトからRopePathコンポーネントを取得します。
        // GetComponentは同じオブジェクトに付いたスクリプトや部品を取る関数です。
        ropePath = ropeObject.GetComponent<RopePath>();
    }
}
