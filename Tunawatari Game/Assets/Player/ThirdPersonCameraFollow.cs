using UnityEngine;

public class ThirdPersonCameraFollow : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private Vector3 offset = new Vector3(0f, 3f, -5f);
    [SerializeField] private float followLerpSpeed = 8f;
    [SerializeField] private float lookHeight = 1.4f;
    [SerializeField] private float rotationLerpSpeed = 12f;
    [SerializeField] private bool useTargetRotation = true;

    private void Reset()
    {
        AutoFindTarget();
    }

    private void Awake()
    {
        if (target == null)
        {
            AutoFindTarget();
        }
    }

    private void LateUpdate()
    {
        if (target == null)
        {
            return;
        }

        Vector3 desiredOffset = useTargetRotation ? target.TransformDirection(offset) : offset;
        Vector3 desiredPosition = target.position + desiredOffset;
        transform.position = Vector3.Lerp(transform.position, desiredPosition, followLerpSpeed * Time.deltaTime);

        Vector3 lookTarget = target.position + Vector3.up * lookHeight;
        Vector3 lookDirection = lookTarget - transform.position;
        if (lookDirection.sqrMagnitude <= Mathf.Epsilon)
        {
            return;
        }

        Quaternion targetRotation = Quaternion.LookRotation(lookDirection.normalized, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationLerpSpeed * Time.deltaTime);
    }

    private void AutoFindTarget()
    {
        GameObject player = GameObject.Find("Suit");
        if (player == null)
        {
            player = GameObject.Find("SuitMan");
        }

        if (player != null)
        {
            target = player.transform;
        }
    }
}
