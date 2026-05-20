using UnityEngine;

// ロープの「中心線」を管理するスクリプトです。
// プレイヤー移動や位置固定は、この中心線を基準にして行います。
public class RopePath : MonoBehaviour
{
    // ロープがどの方向に長いかを指定するための種類です。
    // Autoなら子オブジェクトの大きさから自動で一番長い方向を選びます。
    public enum RopeAxis
    {
        Auto,
        LocalX,
        LocalY,
        LocalZ
    }

    [Header("Rope Ends")]
    // ロープのスタート地点です。
    // Inspectorで空オブジェクトを入れると、ロープの開始位置を明確に指定できます。
    [SerializeField] private Transform startPoint;
    // ロープのゴール地点です。
    // startPointとendPointを設定すると、自動推定より確実な中心線になります。
    [SerializeField] private Transform endPoint;

    [Header("Auto Setup")]
    // startPoint/endPointを設定しない場合、どの軸をロープ方向として使うかです。
    // Autoにすると、ロープ全体のBoundsから長い方向を推定します。
    [SerializeField] private RopeAxis autoAxis = RopeAxis.Auto;
    // 非表示の子オブジェクトも自動計算に含めるかです。
    // 通常はfalseで十分ですが、非表示パーツもロープ長に含めたい時にtrueにします。
    [SerializeField] private bool includeInactiveChildren;

    // 計算済みのロープ開始位置です。
    // 毎回計算すると手間なので、RebuildPathで一度計算して保存します。
    private Vector3 cachedStart;
    // 計算済みのロープ終了位置です。
    private Vector3 cachedEnd;
    // スタートからゴールへ向かう方向です。
    // normalizedされた、長さ1の方向ベクトルとして保存します。
    private Vector3 cachedForward;
    // ロープの長さです。
    // プレイヤーがロープ端から出ないようClampする時に使います。
    private float cachedLength;

    // 外部スクリプトがロープ開始位置を読めるようにするプロパティです。
    public Vector3 StartPosition => cachedStart;
    // 外部スクリプトがロープ終了位置を読めるようにするプロパティです。
    public Vector3 EndPosition => cachedEnd;
    // 外部スクリプトがロープ方向を読めるようにするプロパティです。
    public Vector3 Forward => cachedForward;
    // 外部スクリプトがロープ長を読めるようにするプロパティです。
    public float Length => cachedLength;

    // Awakeはゲーム開始時、Startより前に呼ばれます。
    // 他のスクリプトが使う前にロープ情報を作っておくためです。
    private void Awake()
    {
        RebuildPath();
    }

    // OnValidateはInspectorで値を変えた時など、Editor上で呼ばれます。
    // 設定変更をすぐ計算に反映し、確認しやすくするためです。
    private void OnValidate()
    {
        RebuildPath();
    }

    // ロープの開始位置、終了位置、方向、長さを作り直す関数です。
    // ロープを動かしたり設定を変えたりした時に呼ぶと、情報が最新になります。
    public void RebuildPath()
    {
        // startPointとendPointが両方ある場合は、それを最優先で使います。
        // 手動指定は自動推定より正確なので、意図したロープ中心を作れます。
        if (startPoint != null && endPoint != null)
        {
            // Transform.positionは、そのオブジェクトのワールド座標です。
            // ワールド座標とは、シーン全体で見た位置のことです。
            SetPath(startPoint.position, endPoint.position);
            return;
        }

        // 手動の端点が無い場合は、子オブジェクトの大きさから自動でロープ範囲を作ります。
        BuildPathFromChildren();
    }

    // スタート地点から指定距離だけ進んだロープ上の点を返す関数です。
    // プレイヤーをロープ上へ配置する時に使います。
    public Vector3 GetPointAtDistance(float distance)
    {
        // 長さがほぼ0だと計算できないので、ロープ本体の位置を返します。
        // Mathf.Epsilonは「ほとんど0」と考えるためのとても小さい値です。
        if (cachedLength <= Mathf.Epsilon)
        {
            return transform.position;
        }

        // Clampは値を範囲内に収める関数です。
        // 0未満やロープ長より先へ行かないようにして、ロープ外へ出るのを防ぎます。
        float clampedDistance = Mathf.Clamp(distance, 0f, cachedLength);
        // スタート位置から、ロープ方向へclampedDistance分だけ進んだ位置を返します。
        // cachedForwardは長さ1の方向なので、距離を掛けると実際の移動量になります。
        return cachedStart + cachedForward * clampedDistance;
    }

    // 任意のワールド座標を、最も近いロープ上の点へ変換する関数です。
    // プレイヤーが横にずれても、中心線へ戻すために使います。
    public Vector3 ProjectPosition(Vector3 worldPosition)
    {
        // まずロープ上での距離を求め、その距離に対応するロープ上の点を返します。
        return GetPointAtDistance(GetDistanceAlongRope(worldPosition));
    }

    // 任意のワールド座標が、ロープ開始地点からどれくらい進んだ場所かを求める関数です。
    // 「今のプレイヤー位置はロープ上の何メートル地点に近いか」を知るために使います。
    public float GetDistanceAlongRope(Vector3 worldPosition)
    {
        // ロープ長がほぼ0なら距離を出せないため、スタート地点扱いにします。
        if (cachedLength <= Mathf.Epsilon)
        {
            return 0f;
        }

        // Dotは2つの方向ベクトルがどれくらい同じ方向を向いているかを調べる計算です。
        // ここでは「スタートから現在位置へのベクトル」を「ロープ方向」に写し、
        // ロープ方向に沿った距離だけを取り出しています。
        float distance = Vector3.Dot(worldPosition - cachedStart, cachedForward);
        // 距離を0からロープ長の範囲に収めます。
        // これにより、ロープの手前や奥にいても、最寄りの端点に補正できます。
        return Mathf.Clamp(distance, 0f, cachedLength);
    }

    // 開始位置と終了位置から、ロープ情報を保存する関数です。
    // RebuildPathの中から呼ばれます。
    private void SetPath(Vector3 start, Vector3 end)
    {
        // 開始位置を保存します。
        cachedStart = start;
        // 終了位置を保存します。
        cachedEnd = end;

        // 終了位置から開始位置を引くと、スタートからゴールへの方向と距離が分かります。
        Vector3 delta = cachedEnd - cachedStart;
        // magnitudeはベクトルの長さです。
        // ここではロープの実際の長さとして使います。
        cachedLength = delta.magnitude;
        // normalizedはベクトルの長さを1にする考え方です。
        // ここではdelta / cachedLengthで長さ1の方向を作っています。
        // 長さが0に近い場合は割り算できないので、代わりにtransform.forwardを使います。
        cachedForward = cachedLength > Mathf.Epsilon ? delta / cachedLength : transform.forward;
    }

    // 子オブジェクトのRendererやColliderから、ロープ全体の長さを自動計算する関数です。
    // startPoint/endPointを置かなくても動くようにするための補助です。
    private void BuildPathFromChildren()
    {
        // ロープ方向として使う軸を決めます。
        Vector3 axis = GetAutoAxis();
        // axis方向の最小位置を保存します。
        float min = 0f;
        // axis方向の最大位置を保存します。
        float max = 0f;
        // まだ有効なBoundsを見つけていないかどうかの印です。
        bool hasPoint = false;

        // RendererはMeshなど「見えているもの」の範囲を持っています。
        // 見た目の大きさからロープ範囲を推定するために集めます。
        Renderer[] renderers = GetComponentsInChildren<Renderer>(includeInactiveChildren);
        // 見つかったRendererを1つずつ確認します。
        for (int i = 0; i < renderers.Length; i++)
        {
            // RendererのBoundsをロープ軸方向へ投影して、min/maxを広げます。
            EncapsulateBounds(renderers[i].bounds, axis, ref min, ref max, ref hasPoint);
        }

        // Colliderは当たり判定の範囲を持っています。
        // 見た目が無い部品でもColliderがあればロープ範囲に含められます。
        Collider[] colliders = GetComponentsInChildren<Collider>(includeInactiveChildren);
        // 見つかったColliderを1つずつ確認します。
        for (int i = 0; i < colliders.Length; i++)
        {
            // ColliderのBoundsをロープ軸方向へ投影して、min/maxを広げます。
            EncapsulateBounds(colliders[i].bounds, axis, ref min, ref max, ref hasPoint);
        }

        // RendererもColliderも見つからなかった場合の保険です。
        if (!hasPoint)
        {
            // ロープ本体の位置を中心に、仮で1メートル分の短い線を作ります。
            // 完全に計算不能になるより、最低限動く状態にするためです。
            SetPath(transform.position - axis * 0.5f, transform.position + axis * 0.5f);
            return;
        }

        // transform.positionを基準点にします。
        // min/maxはこの基準点から見たaxis方向の距離として保存されています。
        Vector3 origin = transform.position;
        // axis方向のmin地点を開始、max地点を終了としてロープ中心線を作ります。
        SetPath(origin + axis * min, origin + axis * max);
    }

    // ロープの長い方向を決める関数です。
    // Inspectorで指定されていればその方向を使い、AutoならBoundsから推定します。
    private Vector3 GetAutoAxis()
    {
        // LocalXは、このオブジェクトの右方向をロープ方向として使う設定です。
        // normalizedは長さを1にそろえ、方向だけを使いやすくします。
        if (autoAxis == RopeAxis.LocalX)
        {
            return transform.right.normalized;
        }

        // LocalYは、このオブジェクトの上方向をロープ方向として使う設定です。
        if (autoAxis == RopeAxis.LocalY)
        {
            return transform.up.normalized;
        }

        // LocalZは、このオブジェクトの前方向をロープ方向として使う設定です。
        if (autoAxis == RopeAxis.LocalZ)
        {
            return transform.forward.normalized;
        }

        // Autoの場合、子オブジェクト全体のBoundsを取得します。
        // Boundsは「その物体を包む箱」のような範囲情報です。
        Bounds bounds;
        if (TryGetChildBounds(out bounds))
        {
            // Boundsのサイズを見ます。
            // x/y/zのうち一番大きい方向を、ロープが長い方向とみなします。
            Vector3 size = bounds.size;
            // x方向が一番長ければ、世界のX方向を使います。
            if (size.x >= size.y && size.x >= size.z)
            {
                return Vector3.right;
            }

            // y方向が一番長ければ、世界のY方向を使います。
            if (size.y >= size.x && size.y >= size.z)
            {
                return Vector3.up;
            }

            // それ以外ならz方向が一番長いので、世界のZ方向を使います。
            return Vector3.forward;
        }

        // Boundsも取れなかった場合は、このオブジェクトの前方向を使います。
        // 何も方向が無いと後続の計算ができないための保険です。
        return transform.forward.normalized;
    }

    // 子オブジェクト全体を包むBoundsを作る関数です。
    // Auto軸判定やロープ範囲の推定に使います。
    private bool TryGetChildBounds(out Bounds result)
    {
        // まずは仮のBoundsを用意します。
        // out引数は、関数の外へ計算結果を返すために使います。
        result = new Bounds(transform.position, Vector3.zero);
        // まだBoundsが1つも見つかっていない状態です。
        bool hasBounds = false;

        // 子オブジェクトからRendererを集めます。
        Renderer[] renderers = GetComponentsInChildren<Renderer>(includeInactiveChildren);
        // Rendererを1つずつ見て、全体Boundsに含めます。
        for (int i = 0; i < renderers.Length; i++)
        {
            // 最初のBoundsはそのままresultに入れます。
            if (!hasBounds)
            {
                result = renderers[i].bounds;
                hasBounds = true;
            }
            else
            {
                // Encapsulateは、今のBoundsに別のBoundsを含めて広げる処理です。
                // 複数パーツ全体を包む大きな箱を作るために使います。
                result.Encapsulate(renderers[i].bounds);
            }
        }

        // 子オブジェクトからColliderを集めます。
        Collider[] colliders = GetComponentsInChildren<Collider>(includeInactiveChildren);
        // ColliderもRendererと同じように全体Boundsへ含めます。
        for (int i = 0; i < colliders.Length; i++)
        {
            // まだBoundsが無ければ、最初のColliderを基準にします。
            if (!hasBounds)
            {
                result = colliders[i].bounds;
                hasBounds = true;
            }
            else
            {
                // すでにBoundsがある場合は、Colliderの範囲も含めて広げます。
                result.Encapsulate(colliders[i].bounds);
            }
        }

        // 1つでもRendererかColliderが見つかっていればtrueを返します。
        return hasBounds;
    }

    // Boundsをロープ軸方向に見た時の最小・最大距離へ変換する関数です。
    // 複数パーツからロープ全体の開始/終了を決めるために使います。
    private void EncapsulateBounds(Bounds bounds, Vector3 axis, ref float min, ref float max, ref bool hasPoint)
    {
        // Bounds中心が、RopePath本体からどれだけずれているかを計算します。
        Vector3 centerOffset = bounds.center - transform.position;
        // Dotで中心位置をaxis方向へ写し、軸上の距離に変換します。
        // 3Dの位置から「ロープ方向だけの距離」を取り出すイメージです。
        float projectedCenter = Vector3.Dot(centerOffset, axis);
        // Boundsの広がりをaxis方向へ投影します。
        // extentsはBoundsの半分サイズで、x/y/zの広がりを合計して軸方向の半径を求めます。
        float projectedExtent =
            Mathf.Abs(Vector3.Dot(Vector3.right * bounds.extents.x, axis)) +
            Mathf.Abs(Vector3.Dot(Vector3.up * bounds.extents.y, axis)) +
            Mathf.Abs(Vector3.Dot(Vector3.forward * bounds.extents.z, axis));

        // Boundsがaxis方向で始まる位置です。
        float boundsMin = projectedCenter - projectedExtent;
        // Boundsがaxis方向で終わる位置です。
        float boundsMax = projectedCenter + projectedExtent;

        // 最初のBoundsなら、その値をmin/maxの初期値にします。
        if (!hasPoint)
        {
            min = boundsMin;
            max = boundsMax;
            hasPoint = true;
            return;
        }

        // 既存のminより小さければ更新します。
        // これで全パーツの一番手前を覚えられます。
        min = Mathf.Min(min, boundsMin);
        // 既存のmaxより大きければ更新します。
        // これで全パーツの一番奥を覚えられます。
        max = Mathf.Max(max, boundsMax);
    }
}
