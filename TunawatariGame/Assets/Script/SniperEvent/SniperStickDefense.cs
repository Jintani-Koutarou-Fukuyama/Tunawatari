using UnityEngine;
using UnityEngine.Events;

public class SniperStickDefense : MonoBehaviour
{
    [Header("References")]
    // 防御成功をイベント全体へ知らせるためのイベントマネージャーです。
    [SerializeField] private SniperEventManager eventManager;
    // 防御成功時に出すエフェクトPrefabです。
    // 火花や衝撃エフェクトを入れる想定です。
    [SerializeField] private GameObject guardEffectPrefab;
    // SEを鳴らすAudioSourceです。未設定ならこのGameObjectから探します。
    [SerializeField] private AudioSource audioSource;
    // 防御成功時に鳴らす音です。
    [SerializeField] private AudioClip guardSe;

    [Header("Effect")]
    // エフェクトを何秒後に消すかです。
    // エフェクトPrefab側で自動削除している場合は0以下でもOKです。
    [SerializeField] private float effectLifeTime = 2f;

    [Header("Events")]
    // Inspectorから追加演出をつなぐためのイベントです。
    // 例: 画面揺れ、UI表示、スコア加算など。
    [SerializeField] private UnityEvent onGuardSucceeded;

    [Header("Debug")]
    [SerializeField] private bool debugLog = true;

    private void Awake()
    {
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        TryGuard(other.gameObject, other.ClosestPoint(transform.position));
    }

    private void OnCollisionEnter(Collision collision)
    {
        Vector3 hitPosition = transform.position;

        if (collision.contactCount > 0)
        {
            hitPosition = collision.GetContact(0).point;
        }

        TryGuard(collision.gameObject, hitPosition);
    }

    private void TryGuard(GameObject hitObject, Vector3 hitPosition)
    {
        SniperBullet bullet = hitObject.GetComponent<SniperBullet>();
        if (bullet == null)
        {
            bullet = hitObject.GetComponentInParent<SniperBullet>();
        }

        if (bullet == null || bullet.IsDestroyed)
        {
            return;
        }

        SpawnGuardEffect(hitPosition);
        PlayGuardSe();

        bullet.DestroyBullet();
        onGuardSucceeded?.Invoke();

        if (eventManager != null)
        {
            eventManager.NotifySniperBulletGuarded(bullet);
        }

        Log("Bullet guarded successfully.");
    }

    private void SpawnGuardEffect(Vector3 position)
    {
        if (guardEffectPrefab == null)
        {
            return;
        }

        GameObject effect = Instantiate(guardEffectPrefab, position, Quaternion.identity);

        if (effectLifeTime > 0f)
        {
            Destroy(effect, effectLifeTime);
        }
    }

    private void PlayGuardSe()
    {
        if (audioSource == null || guardSe == null)
        {
            return;
        }

        audioSource.PlayOneShot(guardSe);
    }

    private void Log(string message)
    {
        if (!debugLog)
        {
            return;
        }

        Debug.Log($"[SniperStickDefense] {message}", this);
    }
}
