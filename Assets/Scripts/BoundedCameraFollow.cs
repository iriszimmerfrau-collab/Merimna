using UnityEngine;

/// <summary>
/// Orthographic camera that follows a target but is hard-clamped so the view
/// rectangle never leaves the world bounds (nothing outside the world is ever visible).
/// If the window is so large/wide that the view would exceed the world, the zoom is reduced.
/// </summary>
[RequireComponent(typeof(Camera))]
public class BoundedCameraFollow : MonoBehaviour
{
    public Transform target;
    public Rect worldBounds;
    public float smoothTime = 0.12f;
    [Tooltip("Camera tremble amount in world units (set by PlayerFear). Still clamped inside the world.")]
    public float shake;
    [Tooltip("Preferred zoom. Reduced automatically only if the world/room is smaller than the view.")]
    public float desiredSize;

    Camera cam;
    Vector3 velocity;
    Vector3 smoothed;

    void Awake() { cam = GetComponent<Camera>(); }

    public void Snap(Vector3 position)
    {
        if (cam == null) cam = GetComponent<Camera>();
        FitZoomToWorld();
        position.z = -10f;
        smoothed = Clamp(position);
        transform.position = smoothed;
        velocity = Vector3.zero;
    }

    void LateUpdate()
    {
        if (cam == null || worldBounds.width <= 0f) return;

        FitZoomToWorld();

        Vector3 desired = target != null ? target.position : smoothed;
        desired.z = -10f;

        smoothed = Clamp(Vector3.SmoothDamp(smoothed, Clamp(desired), ref velocity, smoothTime));

        Vector3 pos = smoothed;
        if (shake > 0f) pos += (Vector3)(Random.insideUnitCircle * shake);
        transform.position = Clamp(pos); // hard clamp every frame, even with shake or a resized window
    }

    void FitZoomToWorld()
    {
        if (desiredSize <= 0f) desiredSize = cam.orthographicSize;
        float maxSize = Mathf.Min(worldBounds.height * 0.5f, worldBounds.width * 0.5f / cam.aspect);
        cam.orthographicSize = Mathf.Min(desiredSize, maxSize);   // zooms back out automatically when the bounds grow
    }

    Vector3 Clamp(Vector3 p)
    {
        float halfH = cam.orthographicSize;
        float halfW = halfH * cam.aspect;
        p.x = Mathf.Clamp(p.x, worldBounds.xMin + halfW, worldBounds.xMax - halfW);
        p.y = Mathf.Clamp(p.y, worldBounds.yMin + halfH, worldBounds.yMax - halfH);
        return p;
    }
}
