using UnityEngine;

/// <summary>
/// Düğme ile aktive olduğunda kapalı konumdan açık konuma pürüzsüzce kayan köprü veya kapı.
/// </summary>
public class ActivatedBridge : MonoBehaviour
{
    public Vector3 activeOffset = new Vector3(0f, 6.0f, 0f);
    public float moveSpeed = 6f;

    public bool IsActivated { get; private set; }

    private Vector3 closedPos;
    private Vector3 openPos;

    void Awake()
    {
        closedPos = transform.position;
        openPos = closedPos + activeOffset;
    }

    public void Activate()
    {
        IsActivated = true;
    }

    void Update()
    {
        if (!IsActivated) return;
        if ((transform.position - openPos).sqrMagnitude < 0.0001f)
        {
            transform.position = openPos;
            return;
        }

        transform.position = Vector3.MoveTowards(transform.position, openPos, moveSpeed * Time.deltaTime);
        Physics.SyncTransforms();
    }

    void OnDrawGizmosSelected()
    {
        Vector3 start = Application.isPlaying ? closedPos : transform.position;
        Vector3 end = start + activeOffset;
        Gizmos.color = Color.green;
        Gizmos.DrawLine(start, end);
        Gizmos.DrawWireCube(end, Vector3.one * 0.8f);
    }
}
