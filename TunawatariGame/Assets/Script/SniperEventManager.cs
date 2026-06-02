using UnityEngine;
using UnityEngine.Events;

public class SniperEventManager : MonoBehaviour
{
    // スナイパーイベントの進行状態です。
    // 上から順番に進めることで、イベントの流れを見失いにくくします。
    public enum SniperEventState
    {
        Warning,
        Paper,
        SideView,
        Defense,
        StickBreak,
        SlowMotion,
        MatrixAvoid,
        End
    }

    [System.Serializable]
    public class SniperStateEvent : UnityEvent<SniperEventState>
    {
    }

    [System.Serializable]
    public class SniperBulletEvent : UnityEvent<SniperBullet>
    {
    }

    [Header("Event")]
    [SerializeField] private SniperEventState startState = SniperEventState.Warning;
    [SerializeField] private bool startOnAwake;
    [SerializeField] private bool debugLog = true;

    [Header("Player Control")]
    [SerializeField] private bool disablePlayerControlDuringEvent = true;
    // イベント中に止めたい「左右移動」などの通常移動スクリプトを入れます。
    // 例: TightropePlayerMoverなど、プレイヤーを動かすMonoBehaviour。
    [SerializeField] private Behaviour[] movementBehaviours;
    // イベント中に止めたい「前進」専用処理があれば入れます。
    // 移動と前進が同じスクリプトなら、movementBehavioursだけでOKです。
    [SerializeField] private Behaviour[] forwardMoveBehaviours;
    // イベント中に止めたい通常カメラ操作を入れます。
    // 例: ThirdPersonCameraFollowや手動カメラ操作スクリプト。
    [SerializeField] private Behaviour[] normalCameraBehaviours;
    // イベント中も有効にしておきたいバランス操作を入れます。
    // 例: BalanceManager。ここに入れたものはイベント中にtrueへします。
    [SerializeField] private Behaviour[] balanceBehaviours;

    [Header("Sniper Camera")]
    // スナイパーイベント専用の横視点カメラ制御です。
    // 設定しておくと、SideView Stateに入った時に自動で横視点へ移動できます。
    [SerializeField] private SniperSideViewCameraController sideViewCameraController;
    // trueならSideView Stateに入った瞬間に横視点カメラ演出を開始します。
    [SerializeField] private bool moveCameraOnSideView = true;
    // trueならイベント終了時に通常カメラ位置へ戻します。
    [SerializeField] private bool returnCameraOnEnd = true;

    [Header("Sniper Laser")]
    // スナイパーイベント専用の赤いレーザー表示です。
    // SideView State中だけ表示し、将来的には弾の発射位置としても使えます。
    [SerializeField] private SniperLaserController sniperLaserController;
    // trueならSideView Stateに入った時にレーザーを表示します。
    [SerializeField] private bool showLaserOnSideView = true;
    // trueならSideView Stateから出た時にレーザーを消します。
    [SerializeField] private bool hideLaserWhenLeavingSideView = true;
    // trueならイベント終了時にレーザーを必ず消します。
    [SerializeField] private bool hideLaserOnEventEnd = true;

    [Header("Sniper Bullet")]
    // スナイパーイベント専用の弾発射システムです。
    // 4本のレーザーからランダムに選んで、1発ずつ発射します。
    [SerializeField] private SniperBulletShooter bulletShooter;
    // trueならDefense Stateに入った時に弾発射シーケンスを開始します。
    [SerializeField] private bool startBulletSequenceOnDefense = true;
    // trueならイベント終了時に弾発射シーケンスを止めます。
    [SerializeField] private bool stopBulletSequenceOnEventEnd = true;
    // trueならMatrixAvoid Stateでも弾発射シーケンスを開始します。
    [SerializeField] private bool startBulletSequenceOnMatrixAvoid = true;

    [Header("Stick Break")]
    // 4発防御後の棒破壊演出を管理するスクリプトです。
    [SerializeField] private SniperStickBreakController stickBreakController;
    // 何発防御したら棒破壊へ進むかです。今回の仕様では4発です。
    [SerializeField] private int requiredGuardCountToBreakStick = 4;
    // trueなら必要数を防御した時点でStickBreak Stateへ進みます。
    [SerializeField] private bool moveToStickBreakAfterRequiredGuards = true;
    // trueならStickBreak Stateに入った時に棒破壊演出を自動再生します。
    [SerializeField] private bool playStickBreakOnStickBreakState = true;

    [Header("Matrix Avoid")]
    // 棒破壊後のマトリックス風回避フェーズを管理するスクリプトです。
    [SerializeField] private SniperMatrixAvoidController matrixAvoidController;
    // trueならMatrixAvoid Stateに入った時に回避フェーズを開始します。
    [SerializeField] private bool startMatrixAvoidOnMatrixAvoidState = true;
    // trueなら全弾回避成功時にイベントを終了します。
    [SerializeField] private bool endEventOnMatrixAvoidSuccess = true;
    // trueなら1発でも当たって失敗した時にイベントを終了します。
    [SerializeField] private bool endEventOnMatrixAvoidFail = true;

    [Header("Screen Fade")]
    // イベント用の暗転演出です。
    // 黒Imageを少しだけ表示して、完全暗転ではない映画っぽい暗さにします。
    [SerializeField] private EventScreenFadeController screenFadeController;
    // trueならイベント開始時に少し暗くします。
    [SerializeField] private bool fadeInOnEventStart = true;
    // trueならイベント終了時に明るく戻します。
    [SerializeField] private bool fadeOutOnEventEnd = true;

    [Header("Balance Gauge")]
    // イベント中だけ縦表示へ切り替えるBalanceManagerです。
    [SerializeField] private BalanceManager balanceManager;
    // trueならStartEventで縦表示、EndEventで横表示へ戻します。
    [SerializeField] private bool switchBalanceGaugeDuringEvent = true;

    [Header("State Events")]
    [SerializeField] private UnityEvent onEventStarted;
    [SerializeField] private UnityEvent onWarning;
    [SerializeField] private UnityEvent onPaper;
    [SerializeField] private UnityEvent onSideView;
    [SerializeField] private UnityEvent onDefense;
    [SerializeField] private UnityEvent onStickBreak;
    [SerializeField] private UnityEvent onSlowMotion;
    [SerializeField] private UnityEvent onMatrixAvoid;
    [SerializeField] private UnityEvent onMatrixAvoidSucceeded;
    [SerializeField] private UnityEvent onMatrixAvoidFailed;
    [SerializeField] private UnityEvent onBulletGuarded;
    [SerializeField] private SniperBulletEvent onBulletGuardedWithBullet;
    [SerializeField] private UnityEvent onRequiredBulletsGuarded;
    [SerializeField] private UnityEvent onEventEnded;
    [SerializeField] private SniperStateEvent onStateChanged;

    private bool isEventRunning;
    private SniperEventState currentState;
    private int guardedBulletCount;
    private bool stickBreakStarted;
    private bool[] movementOriginalEnabled;
    private bool[] forwardMoveOriginalEnabled;
    private bool[] normalCameraOriginalEnabled;
    private bool[] balanceOriginalEnabled;

    public bool IsEventRunning => isEventRunning;
    public SniperEventState CurrentState => currentState;
    public int GuardedBulletCount => guardedBulletCount;

    private void Awake()
    {
        currentState = startState;
        CacheControlStates();

        if (startOnAwake)
        {
            StartEvent();
        }
    }

    private void Update()
    {
        if (!isEventRunning)
        {
            return;
        }

        switch (currentState)
        {
            case SniperEventState.Warning:
                UpdateWarning();
                break;
            case SniperEventState.Paper:
                UpdatePaper();
                break;
            case SniperEventState.SideView:
                UpdateSideView();
                break;
            case SniperEventState.Defense:
                UpdateDefense();
                break;
            case SniperEventState.StickBreak:
                UpdateStickBreak();
                break;
            case SniperEventState.SlowMotion:
                UpdateSlowMotion();
                break;
            case SniperEventState.MatrixAvoid:
                UpdateMatrixAvoid();
                break;
            case SniperEventState.End:
                UpdateEnd();
                break;
        }
    }

    public void StartEvent()
    {
        if (isEventRunning)
        {
            Log("StartEvent was ignored because the event is already running.");
            return;
        }

        isEventRunning = true;
        currentState = startState;
        guardedBulletCount = 0;
        stickBreakStarted = false;

        CacheControlStates();
        SetEventControlLock(true);

        if (switchBalanceGaugeDuringEvent && balanceManager != null)
        {
            balanceManager.SwitchToEventVerticalLayout();
        }

        if (sniperLaserController != null)
        {
            // イベント開始直後に前回のレーザー表示が残っていると混乱するため、
            // SideView Stateに入るまでは一度消しておきます。
            sniperLaserController.HideLasers();
        }

        if (stickBreakController != null)
        {
            stickBreakController.ResetStickVisual();
        }

        if (fadeInOnEventStart)
        {
            FadeInEventScreen();
        }

        Log($"Event started. State = {currentState}");
        onEventStarted?.Invoke();
        EnterState(currentState);
    }

    public void AdvanceState()
    {
        if (!isEventRunning)
        {
            Log("AdvanceState was ignored because the event is not running.");
            return;
        }

        if (currentState == SniperEventState.End)
        {
            EndEvent();
            return;
        }

        SniperEventState nextState = (SniperEventState)((int)currentState + 1);
        SetState(nextState);
    }

    public void SetState(SniperEventState nextState)
    {
        if (!isEventRunning)
        {
            Log($"SetState({nextState}) was ignored because the event is not running.");
            return;
        }

        if (currentState == nextState)
        {
            return;
        }

        ExitState(currentState, nextState);
        currentState = nextState;
        Log($"State changed. State = {currentState}");
        EnterState(currentState);
    }

    public void EndEvent()
    {
        if (!isEventRunning)
        {
            return;
        }

        isEventRunning = false;
        SetEventControlLock(false);

        if (returnCameraOnEnd && sideViewCameraController != null)
        {
            sideViewCameraController.ReturnToOriginalView();
        }

        if (hideLaserOnEventEnd)
        {
            HideSniperLasers();
        }

        if (stopBulletSequenceOnEventEnd)
        {
            StopSniperBulletSequence();
        }

        StopMatrixAvoidPhase();

        if (switchBalanceGaugeDuringEvent && balanceManager != null)
        {
            balanceManager.SwitchToNormalHorizontalLayout();
        }

        if (fadeOutOnEventEnd)
        {
            FadeOutEventScreen();
        }

        Log("Event ended.");
        onEventEnded?.Invoke();
    }

    private void EnterState(SniperEventState state)
    {
        onStateChanged?.Invoke(state);

        switch (state)
        {
            case SniperEventState.Warning:
                onWarning?.Invoke();
                break;
            case SniperEventState.Paper:
                onPaper?.Invoke();
                break;
            case SniperEventState.SideView:
                if (moveCameraOnSideView && sideViewCameraController != null)
                {
                    sideViewCameraController.MoveToSideView();
                }

                if (showLaserOnSideView)
                {
                    ShowSniperLasers();
                }

                onSideView?.Invoke();
                break;
            case SniperEventState.Defense:
                if (startBulletSequenceOnDefense)
                {
                    StartSniperBulletSequence();
                }

                onDefense?.Invoke();
                break;
            case SniperEventState.StickBreak:
                if (playStickBreakOnStickBreakState)
                {
                    StartStickBreakSequence();
                }

                onStickBreak?.Invoke();
                break;
            case SniperEventState.SlowMotion:
                onSlowMotion?.Invoke();
                break;
            case SniperEventState.MatrixAvoid:
                if (startBulletSequenceOnMatrixAvoid)
                {
                    StartSniperBulletSequence();
                }

                if (startMatrixAvoidOnMatrixAvoidState)
                {
                    StartMatrixAvoidPhase();
                }

                onMatrixAvoid?.Invoke();
                break;
            case SniperEventState.End:
                EndEvent();
                break;
        }
    }

    private void ExitState(SniperEventState state, SniperEventState nextState)
    {
        if (state == SniperEventState.SideView &&
            nextState != SniperEventState.SideView &&
            hideLaserWhenLeavingSideView)
        {
            // レーザーは横視点専用の見た目なので、
            // SideViewから次のStateへ進むタイミングで消します。
            HideSniperLasers();
        }
    }

    public void ShowSniperLasers()
    {
        if (sniperLaserController == null)
        {
            Log("ShowSniperLasers was ignored because sniperLaserController is not assigned.");
            return;
        }

        sniperLaserController.ShowLasers();
    }

    public void HideSniperLasers()
    {
        if (sniperLaserController == null)
        {
            return;
        }

        sniperLaserController.HideLasers();
    }

    public void StartSniperBulletSequence()
    {
        if (bulletShooter == null)
        {
            Log("StartSniperBulletSequence was ignored because bulletShooter is not assigned.");
            return;
        }

        bulletShooter.StartFireSequence();
    }

    public void StopSniperBulletSequence()
    {
        if (bulletShooter == null)
        {
            return;
        }

        bulletShooter.StopFireSequence();
    }

    public void StartMatrixAvoidPhase()
    {
        if (matrixAvoidController == null)
        {
            Log("StartMatrixAvoidPhase was ignored because matrixAvoidController is not assigned.");
            return;
        }

        matrixAvoidController.StartPhase();
    }

    public void StopMatrixAvoidPhase()
    {
        if (matrixAvoidController == null)
        {
            return;
        }

        matrixAvoidController.StopPhase();
    }

    public void NotifySniperBulletGuarded(SniperBullet bullet)
    {
        guardedBulletCount++;

        Log($"Bullet guarded. Count = {guardedBulletCount}");
        onBulletGuarded?.Invoke();
        onBulletGuardedWithBullet?.Invoke(bullet);

        if (!stickBreakStarted && guardedBulletCount >= requiredGuardCountToBreakStick)
        {
            onRequiredBulletsGuarded?.Invoke();

            if (moveToStickBreakAfterRequiredGuards &&
                isEventRunning &&
                currentState != SniperEventState.StickBreak)
            {
                SetState(SniperEventState.StickBreak);
            }
            else
            {
                StartStickBreakSequence();
            }
        }
    }

    public void StartStickBreakSequence()
    {
        if (stickBreakStarted)
        {
            return;
        }

        stickBreakStarted = true;
        StopSniperBulletSequence();

        if (stickBreakController == null)
        {
            Log("StartStickBreakSequence was ignored because stickBreakController is not assigned.");
            return;
        }

        stickBreakController.PlayBreakSequence();
    }

    public void NotifyMatrixAvoidSucceeded()
    {
        Log("Matrix avoid succeeded.");
        onMatrixAvoidSucceeded?.Invoke();

        if (endEventOnMatrixAvoidSuccess)
        {
            SetState(SniperEventState.End);
        }
    }

    public void NotifyMatrixAvoidFailed()
    {
        Log("Matrix avoid failed.");
        onMatrixAvoidFailed?.Invoke();

        if (endEventOnMatrixAvoidFail)
        {
            SetState(SniperEventState.End);
        }
    }

    public void GoToMatrixAvoidState()
    {
        SetState(SniperEventState.MatrixAvoid);
    }

    public void FadeInEventScreen()
    {
        if (screenFadeController == null)
        {
            Log("FadeInEventScreen was ignored because screenFadeController is not assigned.");
            return;
        }

        screenFadeController.FadeIn();
    }

    public void FadeOutEventScreen()
    {
        if (screenFadeController == null)
        {
            return;
        }

        screenFadeController.FadeOut();
    }

    private void UpdateWarning()
    {
        // 警告表示や赤いレーザー予告を動かす場所です。
    }

    private void UpdatePaper()
    {
        // 紙が飛んできて画面へ貼り付く処理を動かす場所です。
    }

    private void UpdateSideView()
    {
        // 横視点カメラへ切り替えた後の待機や演出を動かす場所です。
    }

    private void UpdateDefense()
    {
        // 棒で弾を防ぐ入力や判定を動かす場所です。
    }

    private void UpdateStickBreak()
    {
        // 棒が壊れる演出や、次の回避フェーズへの準備を動かす場所です。
    }

    private void UpdateSlowMotion()
    {
        // Time.timeScaleを下げるなど、スローモーション演出を動かす場所です。
    }

    private void UpdateMatrixAvoid()
    {
        // マトリックス風に弾を避ける入力や判定を動かす場所です。
    }

    private void UpdateEnd()
    {
        // Endに入ったらEnterState側でEndEventを呼ぶため、基本的には何もしません。
    }

    public void StopNormalControlsForEvent()
    {
        CacheControlStates();
        SetEventControlLock(true);
    }

    public void RestoreNormalControlsAfterEvent()
    {
        SetEventControlLock(false);
    }

    public void SetEventControlLock(bool eventActive)
    {
        if (!disablePlayerControlDuringEvent)
        {
            return;
        }

        // eventActiveがtrueの間は通常操作をfalseにします。
        // これで移動、前進、通常カメラ操作をイベント中だけ停止できます。
        SetBehavioursEnabled(movementBehaviours, movementOriginalEnabled, !eventActive);
        SetBehavioursEnabled(forwardMoveBehaviours, forwardMoveOriginalEnabled, !eventActive);
        SetBehavioursEnabled(normalCameraBehaviours, normalCameraOriginalEnabled, !eventActive);

        if (eventActive)
        {
            // バランス操作はイベント中も必要なので強制的に有効化します。
            // 「通常操作は止まるが、バランスだけは動く」状態にするためです。
            ForceBehavioursEnabled(balanceBehaviours, true);
        }
        else
        {
            // イベント終了時は、イベント開始前の有効/無効状態へ戻します。
            SetBehavioursEnabled(balanceBehaviours, balanceOriginalEnabled, true);
        }

        Log(eventActive
            ? "Normal controls stopped. Balance controls kept enabled."
            : "Normal controls restored.");
    }

    private void CacheControlStates()
    {
        movementOriginalEnabled = CacheOriginalEnabled(movementBehaviours);
        forwardMoveOriginalEnabled = CacheOriginalEnabled(forwardMoveBehaviours);
        normalCameraOriginalEnabled = CacheOriginalEnabled(normalCameraBehaviours);
        balanceOriginalEnabled = CacheOriginalEnabled(balanceBehaviours);
    }

    private bool[] CacheOriginalEnabled(Behaviour[] behaviours)
    {
        if (behaviours == null)
        {
            return null;
        }

        bool[] originalEnabled = new bool[behaviours.Length];

        for (int i = 0; i < behaviours.Length; i++)
        {
            originalEnabled[i] = behaviours[i] != null && behaviours[i].enabled;
        }

        return originalEnabled;
    }

    private void SetBehavioursEnabled(Behaviour[] behaviours, bool[] originalEnabled, bool enabled)
    {
        if (behaviours == null)
        {
            return;
        }

        for (int i = 0; i < behaviours.Length; i++)
        {
            Behaviour behaviour = behaviours[i];
            if (behaviour == null)
            {
                continue;
            }

            if (enabled)
            {
                bool shouldRestore = originalEnabled == null ||
                                     i >= originalEnabled.Length ||
                                     originalEnabled[i];

                behaviour.enabled = shouldRestore;
                continue;
            }

            behaviour.enabled = false;
        }
    }

    private void ForceBehavioursEnabled(Behaviour[] behaviours, bool enabled)
    {
        if (behaviours == null)
        {
            return;
        }

        for (int i = 0; i < behaviours.Length; i++)
        {
            Behaviour behaviour = behaviours[i];
            if (behaviour == null)
            {
                continue;
            }

            behaviour.enabled = enabled;
        }
    }

    private void Log(string message)
    {
        if (!debugLog)
        {
            return;
        }

        Debug.Log($"[SniperEventManager] {message}", this);
    }
}
