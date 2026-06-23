using UnityEngine;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class SteeringWheelUI : MonoBehaviour, IDragHandler, IPointerDownHandler, IPointerUpHandler {
    [Header("Settings")]
    public float maxSteerAngleLimit = 360f; // Maximum rotation in degrees (e.g. 360 degrees = 1 full turn)
    public float springSpeed = 2.5f;         // Speed at which the wheel returns to center when released
    public float keyboardTurnSpeed = 2.0f;   // Speed at which the wheel turns visually under keyboard input

    private RectTransform rectTransform;
    private float currentAngle = 0f;
    private float lastPointerAngle = 0f;
    private bool isDragging = false;
    private bool isReturningFromDrag = false;

    private void Awake() {
        rectTransform = GetComponent<RectTransform>();
    }

    public void OnPointerDown(PointerEventData eventData) {
        isDragging = true;
        isReturningFromDrag = false;
        Vector2 localPos;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, eventData.position, eventData.pressEventCamera, out localPos);
        lastPointerAngle = Mathf.Atan2(localPos.y, localPos.x) * Mathf.Rad2Deg;
    }

    public void OnDrag(PointerEventData eventData) {
        Vector2 localPos;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, eventData.position, eventData.pressEventCamera, out localPos);
        float currentPointerAngle = Mathf.Atan2(localPos.y, localPos.x) * Mathf.Rad2Deg;

        float deltaAngle = currentPointerAngle - lastPointerAngle;
        
        // Handle angle wrap-around
        if (deltaAngle > 180f) deltaAngle -= 360f;
        else if (deltaAngle < -180f) deltaAngle += 360f;

        // Subtracting deltaAngle because clock-wise drag decreases pointer angle but increases wheel rotation
        currentAngle -= deltaAngle;
        currentAngle = Mathf.Clamp(currentAngle, -maxSteerAngleLimit, maxSteerAngleLimit);

        lastPointerAngle = currentPointerAngle;
    }

    public void OnPointerUp(PointerEventData eventData) {
        isDragging = false;
        if (Mathf.Abs(currentAngle) > 0.01f) {
            isReturningFromDrag = true;
        }
    }

    private void Update() {
        if (!isDragging) {
            float kHoriz = 0f;
#if ENABLE_INPUT_SYSTEM
            var keyboard = Keyboard.current;
            if (keyboard != null) {
                if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) kHoriz += 1f;
                if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) kHoriz -= 1f;
            }
#else
            kHoriz = Input.GetAxis("Horizontal");
#endif

            if (Mathf.Abs(kHoriz) > 0.01f) {
                // Animate wheel visually to match keyboard input
                float targetAngle = kHoriz * maxSteerAngleLimit;
                currentAngle = Mathf.MoveTowards(currentAngle, targetAngle, Time.deltaTime * keyboardTurnSpeed * maxSteerAngleLimit);
                isReturningFromDrag = false; // Keyboard override cancels drag return behavior
            } else {
                // Spring back to center
                currentAngle = Mathf.MoveTowards(currentAngle, 0f, Time.deltaTime * springSpeed * maxSteerAngleLimit);
                if (Mathf.Abs(currentAngle) < 0.01f) {
                    currentAngle = 0f;
                    isReturningFromDrag = false;
                }
            }
        }

        // Apply rotation to UI Element (positive Z in Unity UI is counter-clockwise, so negate currentAngle for clockwise rotation)
        rectTransform.localEulerAngles = new Vector3(0f, 0f, -currentAngle);

        // Feed normalized value (-1 to 1) to the Input Manager ONLY if this is active mobile input (dragging or returning)
        if (CarInputManager.Instance != null) {
            if (isDragging || isReturningFromDrag) {
                CarInputManager.Instance.SetMobileSteering(currentAngle / maxSteerAngleLimit);
            } else {
                CarInputManager.Instance.SetMobileSteering(0f);
            }
        }
    }
}
