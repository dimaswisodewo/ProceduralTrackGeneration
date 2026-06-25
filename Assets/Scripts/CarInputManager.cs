using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class CarInputManager : MonoBehaviour {
    public static CarInputManager Instance { get; private set; }

    [Header("Input States")]
    public float Throttle { get; private set; }
    public float Steering { get; private set; }
    public float Brake { get; private set; }
    public bool Handbrake { get; private set; }
    public bool Drift { get; private set; }
    public bool ResetPressed { get; private set; }
    public bool RepositionPressed { get; private set; }

    [Header("Keyboard Settings")]
    public float keyboardSteerSpeed = 4f;
    public float keyboardCenteringSpeed = 6f;
    private float currentKeyboardSteering = 0f;

    // Internal mobile input states
    private float mobileThrottle;
    private float mobileBrake;
    private float mobileSteering;
    private bool mobileHandbrake;
    private bool mobileDrift;
    private bool mobileReset;
    private bool mobileReposition;

    private void Awake() {
        if (Instance != null && Instance != this) {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Update() {
        // 1. Gather Keyboard Inputs
        float keyboardThrottle = 0f;
        float keyboardBrake = 0f;
        float keyboardSteering = 0f;
        bool keyboardHandbrake = false;
        bool keyboardDrift = false;
        bool keyboardReset = false;
        bool keyboardReposition = false;
        
        bool hasVerticalInput = false;
        bool hasHorizontalInput = false;

#if ENABLE_INPUT_SYSTEM
        var keyboard = Keyboard.current;
        if (keyboard != null) {
            float kVert = 0f;
            if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) kVert += 1f;
            if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) kVert -= 1f;

            if (kVert > 0.01f) {
                keyboardThrottle = kVert;
                hasVerticalInput = true;
            } else if (kVert < -0.01f) {
                keyboardBrake = -kVert;
                hasVerticalInput = true;
            }

            float kHoriz = 0f;
            if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) kHoriz += 1f;
            if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) kHoriz -= 1f;
            keyboardSteering = kHoriz;
            if (Mathf.Abs(kHoriz) > 0.01f) {
                hasHorizontalInput = true;
            }

            keyboardHandbrake = keyboard.spaceKey.isPressed;
            keyboardDrift = keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed;
            keyboardReset = keyboard.rKey.wasPressedThisFrame;
            keyboardReposition = keyboard.tKey.wasPressedThisFrame;

            if (keyboard.escapeKey.wasPressedThisFrame) {
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#else
                Application.Quit();
#endif
            }
        }
#else
        float kVert = Input.GetAxisRaw("Vertical"); // W/S or Up/Down
        if (Mathf.Abs(kVert) > 0.01f) {
            hasVerticalInput = true;
            if (kVert > 0.01f) {
                keyboardThrottle = kVert;
            } else {
                keyboardBrake = -kVert;
            }
        }

        float kHoriz = Input.GetAxisRaw("Horizontal"); // A/D or Left/Right
        if (Mathf.Abs(kHoriz) > 0.01f) {
            hasHorizontalInput = true;
            keyboardSteering = kHoriz;
        }

        keyboardHandbrake = Input.GetKey(KeyCode.Space);
        keyboardDrift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        keyboardReset = Input.GetKeyDown(KeyCode.R);
        keyboardReposition = Input.GetKeyDown(KeyCode.T);

        if (Input.GetKeyDown(KeyCode.Escape)) {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
#endif

        // Smooth the keyboard steering input to prevent twitchiness
        if (hasHorizontalInput) {
            currentKeyboardSteering = Mathf.MoveTowards(currentKeyboardSteering, keyboardSteering, Time.deltaTime * keyboardSteerSpeed);
        } else {
            currentKeyboardSteering = Mathf.MoveTowards(currentKeyboardSteering, 0f, Time.deltaTime * keyboardCenteringSpeed);
        }

        // 2. Blend/Override: Keyboard inputs override mobile controls when active
        float finalThrottle = mobileThrottle;
        float finalBrake = mobileBrake;
        float finalSteering = mobileSteering;
        bool finalHandbrake = mobileHandbrake;
        bool finalDrift = mobileDrift;

        if (hasVerticalInput) {
            finalThrottle = keyboardThrottle;
            finalBrake = keyboardBrake;
        }

        // If keyboard steering is active or returning to center, override mobile steering
        if (hasHorizontalInput || Mathf.Abs(currentKeyboardSteering) > 0.001f) {
            finalSteering = currentKeyboardSteering;
        }

        if (keyboardHandbrake) {
            finalHandbrake = true;
        }

        if (keyboardDrift) {
            finalDrift = true;
        }

        // Clamp values to safe ranges
        Throttle = Mathf.Clamp01(finalThrottle);
        Brake = Mathf.Clamp01(finalBrake);
        Steering = Mathf.Clamp(finalSteering, -1f, 1f);
        Handbrake = finalHandbrake;
        Drift = finalDrift;

        // Reset inputs: keyboard or mobile trigger
        ResetPressed = keyboardReset || mobileReset;
        mobileReset = false; // Reset mobile trigger to ensure it acts as a single-frame pulse

        RepositionPressed = keyboardReposition || mobileReposition;
        mobileReposition = false;
    }

    // Methods for Mobile UI (Steering Wheel, Pedals, Buttons) to update states
    public void SetMobileThrottle(float value) {
        mobileThrottle = value;
    }

    public void SetMobileBrake(float value) {
        mobileBrake = value;
    }

    public void SetMobileSteering(float value) {
        mobileSteering = value;
    }

    public void SetMobileHandbrake(bool value) {
        mobileHandbrake = value;
    }

    public void SetMobileDrift(bool value) {
        mobileDrift = value;
    }

    public void SetMobileReset(bool value) {
        mobileReset = value;
    }

    public void SetMobileReposition(bool value) {
        mobileReposition = value;
    }
}
