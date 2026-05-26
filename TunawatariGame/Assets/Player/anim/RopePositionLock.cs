using UnityEngine;

// プレイヤーをロープ中心から外れないように固定する保険用スクリプトです。
// 他の処理で少し横にずれても、毎フレーム最後にロープ中心へ戻します。
public class RopePositionLock : MonoBehaviour
{
    // 固定先になるロープの中心線情報です。
    // InspectorでRopeに付けたRopePathを指定します。
    [SerializeField] private RopePath ropePath;
    // ロープ中心よりどれだけ上にプレイヤーを置くかです。
    // モデルの足がロープに乗る高さに合わせるために使います。
    [SerializeField] private float heightOffset = 0.9f;
    // trueにすると、プレイヤーの向きもロープ方向へ固定します。
    // 移動スクリプト側で回転する場合はfalseのままで大丈夫です。
    [SerializeField] private bool lockRotationToRope = false;
    // 回転固定を使う場合、どれくらい速くロープ方向へ向くかです。
    // Slerpでなめらかに回転させるための速度です。
    [SerializeField] private float rotationLerpSpeed = 20f;

    // Resetはコンポーネントを追加した時などに呼ばれます。
    // RopePathを自動で探して、Inspector設定の手間を減らします。
    private void Reset()
    {
        AutoFindRopePath();
    }

    // Awakeはゲーム開始時、Startより前に呼ばれます。
    // 参照が空のままだと補正できないため、ここで探します。
    private void Awake()
    {
        // ropePathがInspectorで未設定なら、自動検索します。
        if (ropePath == null)
        {
            AutoFindRopePath();
        }
    }

    // LateUpdateは通常のUpdate後に呼ばれます。
    // プレイヤー移動が終わった後に位置を補正することで、最後に必ずロープ上へ戻せます。
    private void LateUpdate()
    {
        // RopePathが無いとロープ中心を計算できないので何もしません。
        if (ropePath == null)
        {
            return;
        }

        // 現在位置をロープの中心線へ投影します。
        // ProjectPositionは「今の位置に一番近いロープ上の点」を返します。
        Vector3 center = ropePath.ProjectPosition(transform.position);
        // Transformのpositionを書き換え、ロープ中心の少し上へ固定します。
        // これにより横方向へずれても、毎フレーム中心へ戻ります。
        transform.position = center + Vector3.up * heightOffset;

        // 回転固定を使わない設定なら、ここで処理を終えます。
        if (!lockRotationToRope)
        {
            return;
        }

        // ロープの進行方向を見る回転を作ります。
        // Vector3.upを指定することで、上方向がひっくり返らないようにします。
        Quaternion targetRotation = Quaternion.LookRotation(ropePath.Forward, Vector3.up);
        // Slerpで現在の回転から目標回転へなめらかに近づけます。
        // いきなり向きを変えるより自然に見えるためです。
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationLerpSpeed * Time.deltaTime);
    }

    // シーン内のRopeオブジェクトからRopePathを探す関数です。
    // Inspector設定を忘れても最低限動きやすくするための補助です。
    private void AutoFindRopePath()
    {
        // GameObject.Findは名前でシーン内オブジェクトを探します。
        // ロープ名が「Rope」と決まっているため、それを使っています。
        GameObject ropeObject = GameObject.Find("Rope");
        // Ropeが見つからない場合は、設定できないので戻ります。
        if (ropeObject == null)
        {
            return;
        }

        // Ropeオブジェクトに付いているRopePathコンポーネントを取得します。
        ropePath = ropeObject.GetComponent<RopePath>();
    }
}
