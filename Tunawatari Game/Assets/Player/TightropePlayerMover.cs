using UnityEngine;

public class TightropePlayerMover : MonoBehaviour
{
    [Header("Rope")]
    [SerializeField] private RopePath ropePath;
    [SerializeField] private float heightOffset = 0.9f;

    [Header("Move")]
    [SerializeField] private float moveSpeed = 1.5f;
    [SerializeField] private bool faceMoveDirection = true;
    [SerializeField] private float rotationLerpSpeed = 12f;

    [Header("Input")]
    [SerializeField] private KeyCode forwardKey = KeyCode.W;
    [SerializeField] private KeyCode backwardKey = KeyCode.S;

    [Header("Animation")]
    [SerializeField] private Animator animator;
    [SerializeField] private string walkBoolName = "catwalk";
    [SerializeField] private bool updateWalkBool = true;

    private float distanceAlongRope;
    private float lastMoveInput;

    public float MoveInput => lastMoveInput;

    private void Reset()
    {
        animator = GetComponentInChildren<Animator>();
        AutoFindRopePath();
    }

    private void Awake()
    {
        if (ropePath == null)
        {
            AutoFindRopePath();
        }

        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }
    }

    private void Start()
    {
        if (ropePath == null)
        {
            Debug.LogWarning("TightropePlayerMover: RopePath が設定されていません。", this);
            return;
        }

        ropePath.RebuildPath();
        distanceAlongRope = ropePath.GetDistanceAlongRope(transform.position);
        SnapToRope();
    }

    private void Update()
    {
        if (ropePath == null)
        {
            return;
        }

        lastMoveInput = ReadMoveInput();
        distanceAlongRope += lastMoveInput * moveSpeed * Time.deltaTime;
        distanceAlongRope = Mathf.Clamp(distanceAlongRope, 0f, ropePath.Length);

        SnapToRope();
        UpdateRotation();
        UpdateAnimation();
    }

    private float ReadMoveInput()
    {
        float input = 0f;

        if (Input.GetKey(forwardKey))
        {
            input += 1f;
        }

        if (Input.GetKey(backwardKey))
        {
            input -= 1f;
        }

        return Mathf.Clamp(input, -1f, 1f);
    }

    private void SnapToRope()
    {
        transform.position = ropePath.GetPointAtDistance(distanceAlongRope) + Vector3.up * heightOffset;
    }

    private void UpdateRotation()
    {
        Vector3 forward = ropePath.Forward;
        if (faceMoveDirection && lastMoveInput < -0.01f)
        {
            forward = -forward;
        }

        if (forward.sqrMagnitude <= Mathf.Epsilon)
        {
            return;
        }

        Quaternion targetRotation = Quaternion.LookRotation(forward, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationLerpSpeed * Time.deltaTime);
    }

    private void UpdateAnimation()
    {
        if (!updateWalkBool || animator == null || string.IsNullOrEmpty(walkBoolName))
        {
            return;
        }

        animator.SetBool(walkBoolName, Mathf.Abs(lastMoveInput) > 0.01f);
    }

    private void AutoFindRopePath()
    {
        GameObject ropeObject = GameObject.Find("Rope");
        if (ropeObject == null)
        {
            return;
        }

        ropePath = ropeObject.GetComponent<RopePath>();
    }
}
