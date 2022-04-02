using UnityEngine;

[RequireComponent(typeof(Camera))]
public class OrbitCameraCustomGravity : MonoBehaviour {
    [SerializeField] private Transform focus;
    [SerializeField, Range(1f, 20f)] private float distance = 5f;
    [SerializeField, Min(0f)] private float focusRadius = 1f;
    [SerializeField, Range(0f, 1f)] private float focusCentering = 0.5f;
    [SerializeField, Range(1f, 360f)] private float rotationSpeed = 90f;
    [SerializeField, Range(-89f, 89)] private float minVerticalAngle = -30, maxVerticalAngle = 60f;
    [SerializeField, Min(0f)] private float alignDelay = 5f;
    [SerializeField, Range(0f, 90f)] private float alignSmoothRange = 45f;
    [SerializeField] private LayerMask obstructionMask = -1;
    private float lastManualRotationTime;
    private Vector2 orbitAngles = new Vector2(45f, 0f);
    private Vector3 focusPoint, previousFocusPoint;
    private Camera regularCamera;
    private Quaternion gravityAlignment = Quaternion.identity;
    private Quaternion orbitRotation;

    private void OnValidate() {
        if (maxVerticalAngle < minVerticalAngle)
            maxVerticalAngle = minVerticalAngle;
    }

    private void Awake() {
        regularCamera = GetComponent<Camera>();
        focusPoint = focus.position;
        transform.localRotation = orbitRotation = Quaternion.Euler(orbitAngles);
    }

    private void LateUpdate() {
        gravityAlignment = Quaternion.FromToRotation(gravityAlignment * Vector3.up, CustomGravity.GetUpAxis(focusPoint)) * gravityAlignment;
        UpdateFocusPoint();
        if (ManualRotation() || AutomaticRotation()) {
            ConstrainAngles();
            orbitRotation = Quaternion.Euler(orbitAngles);
        }
        Quaternion lookRotation = gravityAlignment * orbitRotation;
        Vector3 lookDirection = lookRotation * Vector3.forward;
        Vector3 lookPosition = focusPoint - lookDirection * distance;

        Vector3 rectOffset = lookDirection * regularCamera.nearClipPlane;
        Vector3 rectPosition = lookPosition + rectOffset;
        Vector3 castFrom = focus.position;
        Vector3 castLine = rectPosition - castFrom;
        float castDistance = castLine.magnitude;
        Vector3 castDirection = castLine / castDistance;

        if (Physics.BoxCast(castFrom, CameraHalfExtends, castDirection, out RaycastHit hit, lookRotation, castDistance, obstructionMask)) {
            rectPosition = castFrom + castDirection * hit.distance;
            lookPosition = rectPosition - rectOffset;
        }


        transform.SetPositionAndRotation(lookPosition, lookRotation);
    }

    private void UpdateFocusPoint() {
        previousFocusPoint = focusPoint;
        Vector3 targetPoint = focus.position;
        float t = 1f;
        if (distance > 0.01f && focusCentering > 0f)
            t = Mathf.Pow(1f - focusCentering, Time.unscaledDeltaTime);
        if (focusRadius > 0) {
            float distance = Vector3.Distance(focusPoint, targetPoint);
            if (distance > focusRadius)
                t = Mathf.Min(t, focusRadius / distance);
            focusPoint = Vector3.Lerp(targetPoint, focusPoint, t);
            return;
        }
        focusPoint = targetPoint;
    }

    private bool ManualRotation() {
        Vector2 input = new Vector2(
            Input.GetAxis("Vertical Camera"),
            Input.GetAxis("Horizontal Camera")
        );
        const float e = 0.001f;
        if (input.x < -e || input.x > e || input.y < -e || input.y > e) {
            orbitAngles += input * rotationSpeed * Time.unscaledDeltaTime;
            lastManualRotationTime = Time.unscaledTime;
            return true;
        }
        return false;
    }

    private void ConstrainAngles() {
        orbitAngles.x =
            Mathf.Clamp(orbitAngles.x, minVerticalAngle, maxVerticalAngle);

        if (orbitAngles.y >= 360f)
            orbitAngles.y -= 360f;
        else if (orbitAngles.y < 0)
            orbitAngles.y += 360f;
    }

    private bool AutomaticRotation() {
        if (Time.unscaledTime - lastManualRotationTime < alignDelay)
            return false;

        Vector3 alignedDelta = Quaternion.Inverse(gravityAlignment) * (focusPoint - previousFocusPoint);
        Vector2 movement = new Vector2(alignedDelta.x, alignedDelta.z);

        float movementDeltaSqr = movement.sqrMagnitude;
        if (movementDeltaSqr < 0.0001f)
            return false;

        float headingAngle = GetAngle(movement / Mathf.Sqrt(movementDeltaSqr));
        float rotationChange = rotationSpeed * Mathf.Min(Time.unscaledDeltaTime, movementDeltaSqr);
        float deltaAbs = Mathf.Abs(Mathf.DeltaAngle(orbitAngles.y, headingAngle));
        if (deltaAbs < alignSmoothRange)
            rotationChange *= deltaAbs / alignSmoothRange;
        else if (180f - deltaAbs < alignSmoothRange)
            rotationChange *= (180f - deltaAbs) / alignSmoothRange;
        orbitAngles.y =
            Mathf.MoveTowardsAngle(orbitAngles.y, headingAngle, rotationChange);
        return true;
    }

    private static float GetAngle(Vector2 direction) {
        float angle = Mathf.Acos(direction.y) * Mathf.Rad2Deg;
        return direction.x < 0f ? 360f - angle : angle;
    }

    private Vector3 CameraHalfExtends {
        get {
            Vector3 halfExtends;
            halfExtends.y = regularCamera.nearClipPlane * Mathf.Tan(regularCamera.fieldOfView / 2f * Mathf.Deg2Rad);
            halfExtends.x = halfExtends.y * regularCamera.aspect;
            halfExtends.z = 0f;
            return halfExtends;
        }
    }
}
