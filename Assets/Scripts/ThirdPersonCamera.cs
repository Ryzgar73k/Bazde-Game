using UnityEngine;

/// <summary>
/// Basit üçüncü şahıs takip kamerası. Ekranın sağ tarafını sürükleyerek (veya fare sağ tık) döner.
/// </summary>
public class ThirdPersonCamera : MonoBehaviour
{
    public Transform target;
    [Header("Kamera Mesafesi")]
    public float distance = 3.2f;      // Daha yakın ve dinamik takip (önceki 5f idi)
    public float height = 1.6f;        // Karakterin göğüs/omuz hizası
    public float minPitch = -20f;
    public float maxPitch = 60f;
    [Tooltip("Takip yumuşatma hızı")]
    public float followSmoothing = 30f;
    [Tooltip("Zıplama ve ani hareketlerde kameranın geride kalmaması için anında takip")]
    public bool lockInstant = true;

    float yaw;
    float pitch = 20f;
    Vector3 smoothedTarget;

    void Start()
    {
        if (target != null) smoothedTarget = target.position;
        yaw = transform.eulerAngles.y;

        // PC editöründe fareyi ekranda kilitle (istediğin gibi serbestçe dönmek için)
        if (!Application.isMobilePlatform)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    void Update()
    {
        // ESC tuşuyla fareyi serbest bırak / tıkla geri kilitle
        if (!Application.isMobilePlatform)
        {
            if (UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            if (UnityEngine.InputSystem.Mouse.current != null && UnityEngine.InputSystem.Mouse.current.leftButton.wasPressedThisFrame && Cursor.lockState != CursorLockMode.Locked)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }
    }

    void LateUpdate()
    {
        if (target == null) return;

        Vector2 look = PlayerInputState.Look;
        yaw += look.x;
        pitch = Mathf.Clamp(pitch - look.y, minPitch, maxPitch);

        if (lockInstant)
        {
            // Kamera oyuncunun fiziksel pozisyonuna anında kilitlenir; zıplarken veya koşarken asla geride kalmaz!
            smoothedTarget = target.position;
        }
        else
        {
            float dt = Time.deltaTime;
            float factor = 1f - Mathf.Exp(-followSmoothing * dt);
            smoothedTarget = Vector3.Lerp(smoothedTarget, target.position, factor);
        }

        Vector3 pivot = smoothedTarget + Vector3.up * height;
        Quaternion rot = Quaternion.Euler(pitch, yaw, 0f);
        transform.SetPositionAndRotation(pivot - rot * Vector3.forward * distance, rot);
    }
}
