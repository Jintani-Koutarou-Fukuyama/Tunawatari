using System.Collections;
using UnityEngine;
using UnityEngine.Events;

public class SniperStickBreakController : MonoBehaviour
{
    [Header("Stick Models")]
    // 通常の棒モデルです。イベント開始時やリセット時に表示します。
    [SerializeField] private GameObject normalStickModel;
    // ヒビが入った棒モデルです。4発防御後、破壊前の予兆として表示します。
    [SerializeField] private GameObject crackedStickModel;
    // 折れた後の棒モデルです。破壊完了後に表示します。
    [SerializeField] private GameObject brokenStickModel;
    // trueならヒビ表示中に通常モデルを消します。
    // ヒビモデルが「通常モデルの差し替え」ならtrue、ヒビだけの追加表示ならfalseにします。
    [SerializeField] private bool hideNormalModelWhenCracked = true;

    [Header("Timing")]
    // ヒビが入ってから完全に壊れるまでの待ち時間です。
    [SerializeField] private float crackDuration = 0.8f;
    // trueならTime.timeScaleの影響を受けずに破壊演出を進めます。
    // スローモーション中でも演出時間を一定にしたい時に使います。
    [SerializeField] private bool useUnscaledTime = true;

    [Header("Effects")]
    // ヒビが入った瞬間に出すエフェクトです。
    [SerializeField] private GameObject crackEffectPrefab;
    // 完全に壊れた瞬間に出すエフェクトです。
    [SerializeField] private GameObject breakEffectPrefab;
    // エフェクトを出す位置です。未設定ならこのGameObjectの位置に出します。
    [SerializeField] private Transform effectSpawnPoint;
    // 生成したエフェクトを何秒後に消すかです。
    [SerializeField] private float effectLifeTime = 2f;

    [Header("Sound")]
    // SEを鳴らすAudioSourceです。未設定ならこのGameObjectから探します。
    [SerializeField] private AudioSource audioSource;
    // ヒビが入った瞬間のSEです。
    [SerializeField] private AudioClip crackSe;
    // 棒が壊れた瞬間のSEです。
    [SerializeField] private AudioClip breakSe;

    [Header("Events")]
    [SerializeField] private UnityEvent onCracked;
    [SerializeField] private UnityEvent onBroken;

    [Header("Debug")]
    [SerializeField] private bool debugLog = true;

    private Coroutine breakCoroutine;
    private bool isBroken;

    public bool IsBroken => isBroken;

    private void Awake()
    {
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }

        ResetStickVisual();
    }

    public void PlayBreakSequence()
    {
        if (breakCoroutine != null)
        {
            return;
        }

        breakCoroutine = StartCoroutine(BreakSequence());
    }

    public void ResetStickVisual()
    {
        if (breakCoroutine != null)
        {
            StopCoroutine(breakCoroutine);
            breakCoroutine = null;
        }

        isBroken = false;
        SetActive(normalStickModel, true);
        SetActive(crackedStickModel, false);
        SetActive(brokenStickModel, false);
    }

    private IEnumerator BreakSequence()
    {
        Log("Stick crack started.");
        ShowCrackedModel();
        SpawnEffect(crackEffectPrefab);
        PlaySe(crackSe);
        onCracked?.Invoke();

        yield return Wait(crackDuration);

        Log("Stick broken.");
        ShowBrokenModel();
        SpawnEffect(breakEffectPrefab);
        PlaySe(breakSe);
        onBroken?.Invoke();

        isBroken = true;
        breakCoroutine = null;
    }

    private void ShowCrackedModel()
    {
        if (hideNormalModelWhenCracked)
        {
            SetActive(normalStickModel, false);
        }
        else
        {
            SetActive(normalStickModel, true);
        }

        SetActive(crackedStickModel, true);
        SetActive(brokenStickModel, false);
    }

    private void ShowBrokenModel()
    {
        SetActive(normalStickModel, false);
        SetActive(crackedStickModel, false);
        SetActive(brokenStickModel, true);
    }

    private void SpawnEffect(GameObject effectPrefab)
    {
        if (effectPrefab == null)
        {
            return;
        }

        Vector3 spawnPosition = effectSpawnPoint != null ? effectSpawnPoint.position : transform.position;
        Quaternion spawnRotation = effectSpawnPoint != null ? effectSpawnPoint.rotation : transform.rotation;
        GameObject effect = Instantiate(effectPrefab, spawnPosition, spawnRotation);

        if (effectLifeTime > 0f)
        {
            Destroy(effect, effectLifeTime);
        }
    }

    private void PlaySe(AudioClip clip)
    {
        if (audioSource == null || clip == null)
        {
            return;
        }

        audioSource.PlayOneShot(clip);
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

    private void SetActive(GameObject target, bool active)
    {
        if (target == null)
        {
            return;
        }

        target.SetActive(active);
    }

    private void Log(string message)
    {
        if (!debugLog)
        {
            return;
        }

        Debug.Log($"[SniperStickBreakController] {message}", this);
    }
}
