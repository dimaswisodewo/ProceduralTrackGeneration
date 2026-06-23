using UnityEngine;
using UnityEngine.EventSystems;

public class PedalUI : MonoBehaviour, IPointerDownHandler, IPointerUpHandler {
    public enum PedalType {
        Throttle,
        Brake,
        Handbrake
    }

    [Header("Settings")]
    public PedalType pedalType;
    public float rampSpeed = 2.5f; // Speed at which input value reaches 1 or 0 (simulates foot movement)

    public float InputValue { get; private set; }
    private bool isPressed;

    public void OnPointerDown(PointerEventData eventData) {
        isPressed = true;
    }

    public void OnPointerUp(PointerEventData eventData) {
        isPressed = false;
    }

    private void Update() {
        // Ramp value up or down for smoother analog feel
        float targetValue = isPressed ? 1f : 0f;
        InputValue = Mathf.MoveTowards(InputValue, targetValue, Time.deltaTime * rampSpeed);

        if (CarInputManager.Instance != null) {
            switch (pedalType) {
                case PedalType.Throttle:
                    CarInputManager.Instance.SetMobileThrottle(InputValue);
                    break;
                case PedalType.Brake:
                    CarInputManager.Instance.SetMobileBrake(InputValue);
                    break;
                case PedalType.Handbrake:
                    CarInputManager.Instance.SetMobileHandbrake(isPressed);
                    break;
            }
        }
    }

    private void OnDisable() {
        // Reset when UI is disabled
        isPressed = false;
        InputValue = 0f;
        if (CarInputManager.Instance != null) {
            switch (pedalType) {
                case PedalType.Throttle:
                    CarInputManager.Instance.SetMobileThrottle(0f);
                    break;
                case PedalType.Brake:
                    CarInputManager.Instance.SetMobileBrake(0f);
                    break;
                case PedalType.Handbrake:
                    CarInputManager.Instance.SetMobileHandbrake(false);
                    break;
            }
        }
    }
}
