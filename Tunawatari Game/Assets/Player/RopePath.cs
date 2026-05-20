using UnityEngine;

public class RopePath : MonoBehaviour
{
    public enum RopeAxis
    {
        Auto,
        LocalX,
        LocalY,
        LocalZ
    }

    [Header("Rope Ends")]
    [SerializeField] private Transform startPoint;
    [SerializeField] private Transform endPoint;

    [Header("Auto Setup")]
    [SerializeField] private RopeAxis autoAxis = RopeAxis.Auto;
    [SerializeField] private bool includeInactiveChildren;

    private Vector3 cachedStart;
    private Vector3 cachedEnd;
    private Vector3 cachedForward;
    private float cachedLength;

    public Vector3 StartPosition => cachedStart;
    public Vector3 EndPosition => cachedEnd;
    public Vector3 Forward => cachedForward;
    public float Length => cachedLength;

    private void Awake()
    {
        RebuildPath();
    }

    private void OnValidate()
    {
        RebuildPath();
    }

    public void RebuildPath()
    {
        if (startPoint != null && endPoint != null)
        {
            SetPath(startPoint.position, endPoint.position);
            return;
        }

        BuildPathFromChildren();
    }

    public Vector3 GetPointAtDistance(float distance)
    {
        if (cachedLength <= Mathf.Epsilon)
        {
            return transform.position;
        }

        float clampedDistance = Mathf.Clamp(distance, 0f, cachedLength);
        return cachedStart + cachedForward * clampedDistance;
    }

    public Vector3 ProjectPosition(Vector3 worldPosition)
    {
        return GetPointAtDistance(GetDistanceAlongRope(worldPosition));
    }

    public float GetDistanceAlongRope(Vector3 worldPosition)
    {
        if (cachedLength <= Mathf.Epsilon)
        {
            return 0f;
        }

        float distance = Vector3.Dot(worldPosition - cachedStart, cachedForward);
        return Mathf.Clamp(distance, 0f, cachedLength);
    }

    private void SetPath(Vector3 start, Vector3 end)
    {
        cachedStart = start;
        cachedEnd = end;

        Vector3 delta = cachedEnd - cachedStart;
        cachedLength = delta.magnitude;
        cachedForward = cachedLength > Mathf.Epsilon ? delta / cachedLength : transform.forward;
    }

    private void BuildPathFromChildren()
    {
        Vector3 axis = GetAutoAxis();
        float min = 0f;
        float max = 0f;
        bool hasPoint = false;

        Renderer[] renderers = GetComponentsInChildren<Renderer>(includeInactiveChildren);
        for (int i = 0; i < renderers.Length; i++)
        {
            EncapsulateBounds(renderers[i].bounds, axis, ref min, ref max, ref hasPoint);
        }

        Collider[] colliders = GetComponentsInChildren<Collider>(includeInactiveChildren);
        for (int i = 0; i < colliders.Length; i++)
        {
            EncapsulateBounds(colliders[i].bounds, axis, ref min, ref max, ref hasPoint);
        }

        if (!hasPoint)
        {
            SetPath(transform.position - axis * 0.5f, transform.position + axis * 0.5f);
            return;
        }

        Vector3 origin = transform.position;
        SetPath(origin + axis * min, origin + axis * max);
    }

    private Vector3 GetAutoAxis()
    {
        if (autoAxis == RopeAxis.LocalX)
        {
            return transform.right.normalized;
        }

        if (autoAxis == RopeAxis.LocalY)
        {
            return transform.up.normalized;
        }

        if (autoAxis == RopeAxis.LocalZ)
        {
            return transform.forward.normalized;
        }

        Bounds bounds;
        if (TryGetChildBounds(out bounds))
        {
            Vector3 size = bounds.size;
            if (size.x >= size.y && size.x >= size.z)
            {
                return Vector3.right;
            }

            if (size.y >= size.x && size.y >= size.z)
            {
                return Vector3.up;
            }

            return Vector3.forward;
        }

        return transform.forward.normalized;
    }

    private bool TryGetChildBounds(out Bounds result)
    {
        result = new Bounds(transform.position, Vector3.zero);
        bool hasBounds = false;

        Renderer[] renderers = GetComponentsInChildren<Renderer>(includeInactiveChildren);
        for (int i = 0; i < renderers.Length; i++)
        {
            if (!hasBounds)
            {
                result = renderers[i].bounds;
                hasBounds = true;
            }
            else
            {
                result.Encapsulate(renderers[i].bounds);
            }
        }

        Collider[] colliders = GetComponentsInChildren<Collider>(includeInactiveChildren);
        for (int i = 0; i < colliders.Length; i++)
        {
            if (!hasBounds)
            {
                result = colliders[i].bounds;
                hasBounds = true;
            }
            else
            {
                result.Encapsulate(colliders[i].bounds);
            }
        }

        return hasBounds;
    }

    private void EncapsulateBounds(Bounds bounds, Vector3 axis, ref float min, ref float max, ref bool hasPoint)
    {
        Vector3 centerOffset = bounds.center - transform.position;
        float projectedCenter = Vector3.Dot(centerOffset, axis);
        float projectedExtent =
            Mathf.Abs(Vector3.Dot(Vector3.right * bounds.extents.x, axis)) +
            Mathf.Abs(Vector3.Dot(Vector3.up * bounds.extents.y, axis)) +
            Mathf.Abs(Vector3.Dot(Vector3.forward * bounds.extents.z, axis));

        float boundsMin = projectedCenter - projectedExtent;
        float boundsMax = projectedCenter + projectedExtent;

        if (!hasPoint)
        {
            min = boundsMin;
            max = boundsMax;
            hasPoint = true;
            return;
        }

        min = Mathf.Min(min, boundsMin);
        max = Mathf.Max(max, boundsMax);
    }
}
