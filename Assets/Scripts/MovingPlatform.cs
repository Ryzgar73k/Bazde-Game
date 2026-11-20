using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Belirlenen iki veya daha fazla nokta (waypoints) arasında PingPong (ileri-geri)
/// veya Loop (döngüsel) hareket eden platform bileşeni.
/// PlayerController FrameDelta üzerinden yolcuyu pürüzsüzce taşır.
/// </summary>
[SelectionBase]
[DefaultExecutionOrder(-100)]
public class MovingPlatform : MonoBehaviour
{
    public enum MovementMode
    {
        PingPong,
        Loop
    }

    [Header("Hareket Noktaları")]
    public List<Transform> waypoints = new List<Transform>();
    public Vector3 moveOffset = new Vector3(0f, 0f, 6f);
    public MovementMode movementMode = MovementMode.PingPong;

    [Header("Hız ve Zamanlama")]
    [Range(0.1f, 25f)]
    public float speed = 3.5f;

    [Range(0f, 10f)]
    public float waitTimeAtEnds = 0.6f;

    public bool smoothDamping = true;

    [Header("Yolcu (CharacterController) Taşıma")]
    public bool autoCreatePassengerTrigger = true;
    public float triggerHeight = 2.2f;
    public float triggerPadding = 0.15f;

    private readonly List<Vector3> pointPositions = new List<Vector3>();
    private readonly HashSet<CharacterController> passengers = new HashSet<CharacterController>();

    private int currentPointIndex = 0;
    private int targetPointIndex = 1;
    private int direction = 1;
    private float journeyProgress = 0f;
    private float journeyLength = 1f;
    private float waitTimer = 0f;
    private bool isWaiting = false;

    private Vector3 segmentStart;
    private Vector3 segmentEnd;
    public Vector3 FrameDelta { get; private set; }

    void Start()
    {
        InitializePoints();
        SetupPassengerDetector();
    }

    private void InitializePoints()
    {
        pointPositions.Clear();

        if (waypoints != null && waypoints.Count > 1)
        {
            for (int i = 0; i < waypoints.Count; i++)
            {
                if (waypoints[i] != null)
                    pointPositions.Add(waypoints[i].position);
            }
        }

        if (pointPositions.Count < 2)
        {
            pointPositions.Clear();
            pointPositions.Add(transform.position);
            pointPositions.Add(transform.position + moveOffset);
        }

        currentPointIndex = 0;
        targetPointIndex = 1;
        direction = 1;

        segmentStart = pointPositions[currentPointIndex];
        segmentEnd = pointPositions[targetPointIndex];
        journeyLength = Vector3.Distance(segmentStart, segmentEnd);
        journeyProgress = 0f;
    }

    private void SetupPassengerDetector()
    {
        if (!autoCreatePassengerTrigger) return;
        if (GetComponentInChildren<PassengerTriggerRelay>() != null) return;

        var detectorGO = new GameObject("_PassengerDetector");
        detectorGO.transform.SetParent(transform, false);

        var triggerCol = detectorGO.AddComponent<BoxCollider>();
        triggerCol.isTrigger = true;

        Vector3 size = Vector3.one * 2f;
        Vector3 center = Vector3.zero;

        var col = GetComponent<Collider>();
        if (col is BoxCollider box)
        {
            size = box.size;
            center = box.center;
        }
        else if (col != null)
        {
            size = col.bounds.size;
            center = transform.InverseTransformPoint(col.bounds.center);
        }
        else
        {
            var rend = GetComponentInChildren<Renderer>();
            if (rend != null)
            {
                size = rend.bounds.size;
                center = transform.InverseTransformPoint(rend.bounds.center);
            }
        }

        triggerCol.size = new Vector3(
            Mathf.Max(0.5f, size.x + triggerPadding * 2f),
            triggerHeight,
            Mathf.Max(0.5f, size.z + triggerPadding * 2f)
        );

        triggerCol.center = center + new Vector3(0f, (size.y * 0.5f) + (triggerHeight * 0.5f), 0f);

        var relay = detectorGO.AddComponent<PassengerTriggerRelay>();
        relay.platform = this;
    }

    void Update()
    {
        FrameDelta = Vector3.zero;
        if (pointPositions.Count < 2) return;

        UpdateActiveSegmentPositions();
        Vector3 currentPos = transform.position;

        if (isWaiting)
        {
            waitTimer -= Time.deltaTime;
            if (waitTimer <= 0f)
            {
                isWaiting = false;
                AdvanceToNextWaypoint();
            }
        }
        else
        {
            float safeLength = Mathf.Max(0.001f, journeyLength);
            float step = (speed / safeLength) * Time.deltaTime;
            journeyProgress = Mathf.Clamp01(journeyProgress + step);

            float t = smoothDamping ? Mathf.SmoothStep(0f, 1f, journeyProgress) : journeyProgress;
            Vector3 newPosition = Vector3.Lerp(segmentStart, segmentEnd, t);

            transform.position = newPosition;

            FrameDelta = newPosition - currentPos;
            Physics.SyncTransforms();
            MovePassengers(FrameDelta);

            if (journeyProgress >= 1f)
            {
                isWaiting = true;
                waitTimer = waitTimeAtEnds;
            }
        }
    }

    private void UpdateActiveSegmentPositions()
    {
        if (waypoints != null && waypoints.Count == pointPositions.Count)
        {
            if (waypoints[currentPointIndex] != null)
                segmentStart = waypoints[currentPointIndex].position;

            if (waypoints[targetPointIndex] != null)
                segmentEnd = waypoints[targetPointIndex].position;

            journeyLength = Vector3.Distance(segmentStart, segmentEnd);
        }
    }

    private void AdvanceToNextWaypoint()
    {
        currentPointIndex = targetPointIndex;

        if (movementMode == MovementMode.PingPong)
        {
            int next = targetPointIndex + direction;
            if (next >= pointPositions.Count)
            {
                direction = -1;
                targetPointIndex = pointPositions.Count - 2;
            }
            else if (next < 0)
            {
                direction = 1;
                targetPointIndex = 1;
            }
            else
            {
                targetPointIndex = next;
            }
        }
        else
        {
            targetPointIndex = (targetPointIndex + 1) % pointPositions.Count;
        }

        segmentStart = pointPositions[currentPointIndex];
        segmentEnd = pointPositions[targetPointIndex];
        journeyLength = Vector3.Distance(segmentStart, segmentEnd);
        journeyProgress = 0f;
    }

    private void MovePassengers(Vector3 delta)
    {
        if (delta.sqrMagnitude < 0.0000001f || passengers.Count == 0) return;

        passengers.RemoveWhere(p => p == null);

        foreach (var cc in passengers)
        {
            if (cc != null && cc.enabled && cc.gameObject.activeInHierarchy && cc.GetComponent<PlayerController>() == null)
            {
                cc.Move(delta);
            }
        }
    }

    public void AddPassenger(CharacterController cc)
    {
        if (cc != null && !passengers.Contains(cc))
            passengers.Add(cc);
    }

    public void RemovePassenger(CharacterController cc)
    {
        if (cc != null && passengers.Contains(cc))
            passengers.Remove(cc);
    }

    void OnTriggerEnter(Collider other)
    {
        var cc = other.GetComponentInParent<CharacterController>();
        if (cc != null) AddPassenger(cc);
    }

    void OnTriggerExit(Collider other)
    {
        var cc = other.GetComponentInParent<CharacterController>();
        if (cc != null) RemovePassenger(cc);
    }

    void OnDisable()
    {
        FrameDelta = Vector3.zero;
        passengers.Clear();
    }

    void OnDrawGizmos()
    {
        List<Vector3> pts = new List<Vector3>();

        if (waypoints != null && waypoints.Count > 1)
        {
            foreach (var wp in waypoints)
                if (wp != null) pts.Add(wp.position);
        }
        else
        {
            pts.Add(transform.position);
            pts.Add(transform.position + moveOffset);
        }

        if (pts.Count < 2) return;

        Gizmos.color = Color.yellow;
        for (int i = 0; i < pts.Count - 1; i++)
        {
            Gizmos.DrawLine(pts[i], pts[i + 1]);
            Gizmos.DrawWireSphere(pts[i], 0.35f);
        }
        Gizmos.DrawWireSphere(pts[pts.Count - 1], 0.35f);

        if (movementMode == MovementMode.Loop && pts.Count > 2)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(pts[pts.Count - 1], pts[0]);
        }
    }
}

public class PassengerTriggerRelay : MonoBehaviour
{
    public MovingPlatform platform;

    void OnTriggerEnter(Collider other)
    {
        if (platform == null) return;
        var cc = other.GetComponentInParent<CharacterController>();
        if (cc != null) platform.AddPassenger(cc);
    }

    void OnTriggerExit(Collider other)
    {
        if (platform == null) return;
        var cc = other.GetComponentInParent<CharacterController>();
        if (cc != null) platform.RemovePassenger(cc);
    }
}
