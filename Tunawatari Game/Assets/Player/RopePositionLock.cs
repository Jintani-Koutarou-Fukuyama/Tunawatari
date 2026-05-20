using UnityEngine;

public class RopePositionLock : MonoBehaviour
{
    [SerializeField] private RopePath ropePath;
    [SerializeField] private float heightOffset = 0.9f;
    [SerializeField] private bool lockRotationToRope = false;
    [SerializeField] private float rotationLerpSpeed = 20f;

    private void Reset()
    {
        AutoFindRopePath();
    }

    private void Awake()
    {
        if (ropePath == null)
        {
            AutoFindRopePath();
        }
    }

    private void LateUpdate()
    {
        if (ropePath == null)
        {
            return;
        }

        Vector3 center = ropePath.ProjectPosition(transform.position);
        transform.position = center + Vector3.up * heightOffset;

        if (!lockRotationToRope)
        {
            return;
        }

        Quaternion targetRotation = Quaternion.LookRotation(ropePath.Forward, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationLerpSpeed * Time.deltaTime);
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
