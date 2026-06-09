using UnityEngine;
using UnityEngine.Events;

public class TightropeAutoGoalMover : MonoBehaviour
{
    [Header("Target")]
    // プレイヤーが自動で向かうゴール地点です。
    // Scene内のGoalPointを入れてください。
    [SerializeField] private Transform goalPoint;
    // RopePathを入れると、ロープに沿ってGoalPointまで進みます。
    // 未設定の場合はGoalPointへまっすぐMoveTowardsで進みます。
    [SerializeField] private RopePath ropePath;

    [Header("Move")]
    // 1秒間に進む距離です。
    [SerializeField] private float moveSpeed = 1.2f;
    // ロープ上の点からプレイヤーを少し上へ置くための高さです。
    [SerializeField] private float heightOffset = 0f;
    // GoalPointにこの距離まで近づいたら到着扱いにします。
    [SerializeField] private float goalStopDistance = 0.1f;
    // trueならStart時から自動移動します。
    [SerializeField] private bool moveOnStart = true;
    // trueなら進行方向へプレイヤーを向けます。
    [SerializeField] private bool rotateToMoveDirection = true;
    // 回転のなめらかさです。
    [SerializeField] private float rotationSpeed = 10f;
    // trueなら、下のManual Movement BehavioursをStart時にOFFにします。
    // 既存のTightropePlayerMoverなど、手動前進スクリプトとの競合を防ぐためです。
    [SerializeField] private bool disableManualMovementOnStart = true;
    // 自動前進と同時に動かしたくない手動移動スクリプトを入れます。
    [SerializeField] private Behaviour[] manualMovementBehaviours;

    [Header("Animator")]
    // 既存Animatorを使って歩きモーションを切り替えたい場合に入れます。
    [SerializeField] private Animator animator;
    // Animator側の歩きbool名です。
    [SerializeField] private string walkBoolName = "catwalk";

    [Header("Events")]
    [SerializeField] private UnityEvent onGoalReached;

    [Header("Debug")]
    [SerializeField] private bool debugLog = true;

    private float currentDistanceAlongRope;
    private float goalDistanceAlongRope;
    private bool isMoving;
    private bool hasReachedGoal;

    public bool IsMoving => isMoving;
    public bool HasReachedGoal => hasReachedGoal;

    private void Reset()
    {
        AutoAssignReferences();
    }

    private void Awake()
    {
        AutoAssignReferences();
    }

    private void Start()
    {
        if (disableManualMovementOnStart)
        {
            SetManualMovementEnabled(false);
        }

        InitializePathDistances();

        if (moveOnStart)
        {
            StartMoving();
        }
    }

    private void Update()
    {
        if (!isMoving || hasReachedGoal || goalPoint == null)
        {
            SetWalkAnimation(false);
            return;
        }

        if (ropePath != null)
        {
            MoveAlongRope();
        }
        else
        {
            MoveDirectlyToGoal();
        }
    }

    public void StartMoving()
    {
        if (hasReachedGoal)
        {
            return;
        }

        isMoving = true;
        SetWalkAnimation(true);
    }

    public void StopMoving()
    {
        isMoving = false;
        SetWalkAnimation(false);
    }

    public void SetManualMovementEnabled(bool enabled)
    {
        if (manualMovementBehaviours == null)
        {
            return;
        }

        for (int i = 0; i < manualMovementBehaviours.Length; i++)
        {
            Behaviour behaviour = manualMovementBehaviours[i];
            if (behaviour == null || behaviour == this)
            {
                continue;
            }

            behaviour.enabled = enabled;
        }
    }

    public void ResetAutoMove()
    {
        hasReachedGoal = false;
        InitializePathDistances();
    }

    private void InitializePathDistances()
    {
        if (ropePath == null || goalPoint == null)
        {
            return;
        }

        currentDistanceAlongRope = ropePath.GetDistanceAlongRope(transform.position);
        goalDistanceAlongRope = ropePath.GetDistanceAlongRope(goalPoint.position);
    }

    private void MoveAlongRope()
    {
        float previousDistance = currentDistanceAlongRope;
        currentDistanceAlongRope = Mathf.MoveTowards(
            currentDistanceAlongRope,
            goalDistanceAlongRope,
            moveSpeed * Time.deltaTime);

        Vector3 nextPosition = ropePath.GetPointAtDistance(currentDistanceAlongRope) + Vector3.up * heightOffset;
        Vector3 moveDirection = nextPosition - transform.position;
        transform.position = nextPosition;

        if (rotateToMoveDirection)
        {
            RotateTowards(moveDirection);
        }

        SetWalkAnimation(!Mathf.Approximately(previousDistance, currentDistanceAlongRope));

        if (Mathf.Abs(currentDistanceAlongRope - goalDistanceAlongRope) <= goalStopDistance)
        {
            ReachGoal();
        }
    }

    private void MoveDirectlyToGoal()
    {
        Vector3 targetPosition = goalPoint.position;
        Vector3 nextPosition = Vector3.MoveTowards(transform.position, targetPosition, moveSpeed * Time.deltaTime);
        Vector3 moveDirection = nextPosition - transform.position;
        transform.position = nextPosition;

        if (rotateToMoveDirection)
        {
            RotateTowards(moveDirection);
        }

        SetWalkAnimation(moveDirection.sqrMagnitude > 0.0001f);

        if (Vector3.Distance(transform.position, targetPosition) <= goalStopDistance)
        {
            ReachGoal();
        }
    }

    private void RotateTowards(Vector3 moveDirection)
    {
        if (moveDirection.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        Quaternion targetRotation = Quaternion.LookRotation(moveDirection.normalized, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
    }

    private void ReachGoal()
    {
        if (hasReachedGoal)
        {
            return;
        }

        hasReachedGoal = true;
        isMoving = false;
        SetWalkAnimation(false);
        DebugLog("Goal reached.");
        onGoalReached?.Invoke();
    }

    private void SetWalkAnimation(bool walking)
    {
        if (animator == null || string.IsNullOrEmpty(walkBoolName))
        {
            return;
        }

        animator.SetBool(walkBoolName, walking);
    }

    private void AutoAssignReferences()
    {
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }

        if (goalPoint == null)
        {
            GameObject goalObject = GameObject.Find("GoalPoint");
            if (goalObject != null)
            {
                goalPoint = goalObject.transform;
            }
        }

        if (ropePath == null)
        {
            GameObject ropeObject = GameObject.Find("Rope");
            if (ropeObject != null)
            {
                ropePath = ropeObject.GetComponent<RopePath>();
            }
        }
    }

    private void DebugLog(string message)
    {
        if (!debugLog)
        {
            return;
        }

        Debug.Log($"[TightropeAutoGoalMover] {message}", this);
    }
}
