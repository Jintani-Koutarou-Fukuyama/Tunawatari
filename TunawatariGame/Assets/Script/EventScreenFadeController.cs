using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class EventScreenFadeController : MonoBehaviour
{
    [Header("References")]
    // 暗転用の黒いImageです。
    // Canvasの一番手前に置いた、画面全体を覆うImageを設定します。
    [SerializeField] private Image fadeImage;

    [Header("Fade")]
    // 完全暗転にしないため、最終的な透明度は低めにします。
    // 0.25なら「少し暗くなる」くらいの見た目です。
    [SerializeField] private float darkAlpha = 0.25f;
    // 明るい状態の透明度です。基本は0で、暗転Imageが見えない状態です。
    [SerializeField] private float clearAlpha = 0f;
    // 暗くなるまでの秒数です。
    [SerializeField] private float fadeInDuration = 0.5f;
    // 明るく戻るまでの秒数です。
    [SerializeField] private float fadeOutDuration = 0.5f;
    // trueならTime.timeScaleの影響を受けずにフェードします。
    // イベント演出中でもUIフェード速度を一定にしたいので初期値はtrueです。
    [SerializeField] private bool useUnscaledTime = true;
    // trueなら開始時に透明へ戻します。
    // Scene再生時に前回の暗さが残らないようにするためです。
    [SerializeField] private bool clearOnAwake = true;

    [Header("Events")]
    [SerializeField] private UnityEvent onFadeInFinished;
    [SerializeField] private UnityEvent onFadeOutFinished;

    [Header("Debug")]
    [SerializeField] private bool debugLog = true;

    private Coroutine fadeCoroutine;

    public bool IsFading => fadeCoroutine != null;

    private void Awake()
    {
        if (clearOnAwake)
        {
            SetAlpha(clearAlpha);
        }
    }

    public void FadeIn()
    {
        FadeTo(darkAlpha, fadeInDuration, onFadeInFinished);
    }

    public void FadeOut()
    {
        FadeTo(clearAlpha, fadeOutDuration, onFadeOutFinished);
    }

    public void SetDarkImmediately()
    {
        StopFadeIfNeeded();
        SetAlpha(darkAlpha);
    }

    public void SetClearImmediately()
    {
        StopFadeIfNeeded();
        SetAlpha(clearAlpha);
    }

    public void FadeTo(float targetAlpha, float duration)
    {
        FadeTo(targetAlpha, duration, null);
    }

    private void FadeTo(float targetAlpha, float duration, UnityEvent finishedEvent)
    {
        if (fadeImage == null)
        {
            Log("Fade was ignored because fadeImage is not assigned.");
            return;
        }

        StopFadeIfNeeded();
        fadeCoroutine = StartCoroutine(FadeRoutine(targetAlpha, duration, finishedEvent));
    }

    private IEnumerator FadeRoutine(float targetAlpha, float duration, UnityEvent finishedEvent)
    {
        Color startColor = fadeImage.color;
        float startAlpha = startColor.a;
        float clampedTargetAlpha = Mathf.Clamp01(targetAlpha);

        if (duration <= 0f)
        {
            SetAlpha(clampedTargetAlpha);
            fadeCoroutine = null;
            finishedEvent?.Invoke();
            yield break;
        }

        float timer = 0f;

        while (timer < duration)
        {
            float deltaTime = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            timer += deltaTime;

            // Clamp01は0〜1の範囲に収める処理です。
            // フェードの進み具合が1を超えないようにします。
            float rate = Mathf.Clamp01(timer / duration);
            // Lerpは開始値から目標値へ少しずつ近づける処理です。
            // ここでは透明度をなめらかに変えています。
            float alpha = Mathf.Lerp(startAlpha, clampedTargetAlpha, rate);
            SetAlpha(alpha);

            yield return null;
        }

        SetAlpha(clampedTargetAlpha);
        fadeCoroutine = null;
        finishedEvent?.Invoke();
    }

    private void SetAlpha(float alpha)
    {
        if (fadeImage == null)
        {
            return;
        }

        Color color = fadeImage.color;
        color.r = 0f;
        color.g = 0f;
        color.b = 0f;
        color.a = Mathf.Clamp01(alpha);
        fadeImage.color = color;
        fadeImage.raycastTarget = false;
    }

    private void StopFadeIfNeeded()
    {
        if (fadeCoroutine == null)
        {
            return;
        }

        StopCoroutine(fadeCoroutine);
        fadeCoroutine = null;
    }

    private void Log(string message)
    {
        if (!debugLog)
        {
            return;
        }

        Debug.Log($"[EventScreenFadeController] {message}", this);
    }
}
