using UnityEngine;

public class SniperMatrixHitReceiver : MonoBehaviour
{
    [Header("References")]
    // マトリックス回避フェーズを管理しているスクリプトです。
    // 弾がプレイヤーに当たった時、ここへ失敗通知します。
    [SerializeField] private SniperMatrixAvoidController matrixAvoidController;

    [Header("Debug")]
    [SerializeField] private bool debugLog = true;

    private void OnTriggerEnter(Collider other)
    {
        TryNotifyHit(other.gameObject, other.ClosestPoint(transform.position));
    }

    private void OnCollisionEnter(Collision collision)
    {
        Vector3 hitPosition = transform.position;

        if (collision.contactCount > 0)
        {
            hitPosition = collision.GetContact(0).point;
        }

        TryNotifyHit(collision.gameObject, hitPosition);
    }

    private void TryNotifyHit(GameObject hitObject, Vector3 hitPosition)
    {
        SniperBullet bullet = hitObject.GetComponent<SniperBullet>();
        if (bullet == null)
        {
            bullet = hitObject.GetComponentInParent<SniperBullet>();
        }

        if (bullet == null || bullet.IsDestroyed || matrixAvoidController == null)
        {
            return;
        }

        Log("Matrix bullet hit player.");
        matrixAvoidController.NotifyBulletHit(bullet, hitPosition);
    }

    private void Log(string message)
    {
        if (!debugLog)
        {
            return;
        }

        Debug.Log($"[SniperMatrixHitReceiver] {message}", this);
    }
}
