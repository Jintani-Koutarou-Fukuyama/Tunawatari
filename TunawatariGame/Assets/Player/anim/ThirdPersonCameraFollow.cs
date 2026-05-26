using UnityEngine;

// 三人称視点のカメラを作るためのスクリプトです。
// プレイヤーの少し後ろ・少し上にカメラを置き、毎フレーム追いかけます。
public class ThirdPersonCameraFollow : MonoBehaviour
{
    // カメラが追いかける対象です。
    // InspectorでSuitまたはSuitManのTransformを入れます。
    // Transformは「位置・回転・大きさ」を持つUnityの基本情報です。
    [SerializeField] private Transform target;
    // プレイヤーから見て、カメラをどの位置に置くかを表すずらし量です。
    // x=左右、y=高さ、z=前後で、初期値は少し上の後ろです。
    [SerializeField] private Vector3 offset = new Vector3(0f, 3f, -5f);
    // カメラ位置が目標位置へ近づく速さです。
    // Lerpでなめらかに追従するため、数字が大きいほど素早く追いつきます。
    [SerializeField] private float followLerpSpeed = 8f;
    // カメラがプレイヤーのどの高さを見るかです。
    // 足元ではなく胸や頭あたりを見るために少し上へずらします。
    [SerializeField] private float lookHeight = 1.4f;
    // カメラの向きが目標方向へ近づく速さです。
    // 位置とは別に、向きもなめらかに変えるための値です。
    [SerializeField] private float rotationLerpSpeed = 12f;
    // trueなら、offsetをプレイヤーの向きに合わせて回転させます。
    // プレイヤーが向きを変えた時も、カメラが背後に回り込むようにするためです。
    [SerializeField] private bool useTargetRotation = true;

    // Resetはコンポーネントを追加した時などに呼ばれます。
    // 追従対象を自動で探して、設定忘れを減らします。
    private void Reset()
    {
        AutoFindTarget();
    }

    // Awakeはゲーム開始時、Startより前に呼ばれます。
    // targetが空ならここで自動検索します。
    private void Awake()
    {
        // Inspectorでtargetが入っていない時だけ自動で探します。
        if (target == null)
        {
            AutoFindTarget();
        }
    }

    // LateUpdateは通常のUpdateが終わった後に呼ばれます。
    // プレイヤー移動が終わった後にカメラを動かすと、追従がガタつきにくくなります。
    private void LateUpdate()
    {
        // targetが無いと追いかける相手が分からないので何もしません。
        if (target == null)
        {
            return;
        }

        // TransformDirectionは、ローカル方向をワールド方向へ変換する関数です。
        // useTargetRotationがtrueなら「プレイヤーから見た後ろ上」を世界座標に変換します。
        // falseならoffsetをそのまま世界座標のずらし量として使います。
        Vector3 desiredOffset = useTargetRotation ? target.TransformDirection(offset) : offset;
        // 目標カメラ位置は、プレイヤー位置にoffsetを足した場所です。
        Vector3 desiredPosition = target.position + desiredOffset;
        // Lerpは「現在位置」と「目標位置」の間を少しずつ移動する関数です。
        // いきなりワープせず、なめらかにカメラが追いつく見た目になります。
        transform.position = Vector3.Lerp(transform.position, desiredPosition, followLerpSpeed * Time.deltaTime);

        // カメラが見る目標点です。
        // target.positionだけだと足元を見るので、Vector3.up * lookHeightで視線を上げます。
        Vector3 lookTarget = target.position + Vector3.up * lookHeight;
        // カメラから目標点へ向かう方向を計算します。
        // 「見たい場所 - 今のカメラ位置」で、その方向ベクトルが作れます。
        Vector3 lookDirection = lookTarget - transform.position;
        // 方向の長さがほぼ0だと、どちらを向けばいいか分からないため処理を止めます。
        if (lookDirection.sqrMagnitude <= Mathf.Epsilon)
        {
            return;
        }

        // normalizedはベクトルの長さを1にそろえる処理です。
        // 向きだけが欲しい時に使い、距離の大きさに影響されないようにします。
        // LookRotationで「lookDirectionの方向を見る回転」を作ります。
        Quaternion targetRotation = Quaternion.LookRotation(lookDirection.normalized, Vector3.up);
        // Slerpは回転をなめらかに近づける関数です。
        // カメラの向きが急に変わらないようにしています。
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationLerpSpeed * Time.deltaTime);
    }

    // シーン内からプレイヤーを自動で探す関数です。
    // Inspectorでtargetを入れ忘れた時の補助として使います。
    private void AutoFindTarget()
    {
        // まず指定名の「Suit」を探します。
        GameObject player = GameObject.Find("Suit");
        // 見つからなかった場合、このプロジェクトで使われている「SuitMan」も探します。
        if (player == null)
        {
            player = GameObject.Find("SuitMan");
        }

        // プレイヤーが見つかったら、そのTransformを追従対象にします。
        if (player != null)
        {
            target = player.transform;
        }
    }
}
