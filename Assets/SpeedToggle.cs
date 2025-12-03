using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Toggle between fast training speed and normal viewing speed.
/// Press SPACE to switch between 20x speed (training) and 1x speed (watching).
/// Attach to any GameObject in the scene.
/// </summary>
public class SpeedToggle : MonoBehaviour
{
    [Header("Speed Settings")]
    [Tooltip("Fast training speed multiplier")]
    public float fastSpeed = 20f;
    
    [Tooltip("Normal viewing speed")]
    public float normalSpeed = 1f;
    
    [Tooltip("Key to toggle speed")]
    public KeyCode toggleKey = KeyCode.Space;
    
    [Header("Display")]
    [Tooltip("Show speed indicator on screen")]
    public bool showSpeedIndicator = true;
    
    private bool isFastMode = true;
    private float currentSpeed;
    
    void Start()
    {
        // Start in fast mode (training speed)
        SetSpeed(fastSpeed);
        Debug.Log($"[SpeedToggle] Started in FAST mode ({fastSpeed}x). Press {toggleKey} to toggle.");
    }
    
    void Update()
    {
#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        if (kb != null)
        {
            // Toggle speed when key is pressed
            if (kb.spaceKey.wasPressedThisFrame)
            {
                ToggleSpeed();
            }
            
            // Alternative: Number keys for direct control
            if (kb.digit1Key.wasPressedThisFrame)
            {
                SetSpeed(normalSpeed);
            }
            if (kb.digit2Key.wasPressedThisFrame)
            {
                SetSpeed(fastSpeed);
            }
            if (kb.digit3Key.wasPressedThisFrame)
            {
                SetSpeed(50f); // Ultra fast
            }
            if (kb.digit0Key.wasPressedThisFrame)
            {
                SetSpeed(0.5f); // Slow motion
            }
        }
#else
        // Toggle speed when key is pressed
        if (Input.GetKeyDown(toggleKey))
        {
            ToggleSpeed();
        }
        
        // Alternative: Number keys for direct control
        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            SetSpeed(normalSpeed);
        }
        if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            SetSpeed(fastSpeed);
        }
        if (Input.GetKeyDown(KeyCode.Alpha3))
        {
            SetSpeed(50f); // Ultra fast
        }
        if (Input.GetKeyDown(KeyCode.Alpha0))
        {
            SetSpeed(0.5f); // Slow motion
        }
#endif
    }
    
    void ToggleSpeed()
    {
        isFastMode = !isFastMode;
        
        if (isFastMode)
        {
            SetSpeed(fastSpeed);
            Debug.Log($"<color=cyan>⚡ FAST MODE: {fastSpeed}x speed (Training)</color>");
        }
        else
        {
            SetSpeed(normalSpeed);
            Debug.Log($"<color=green>👁️ NORMAL MODE: {normalSpeed}x speed (Watching)</color>");
        }
    }
    
    void SetSpeed(float speed)
    {
        Time.timeScale = speed;
        currentSpeed = speed;
        isFastMode = speed > 1f;
    }
    
    void OnGUI()
    {
        if (!showSpeedIndicator) return;
        
        // Speed indicator in bottom-right corner
        GUIStyle style = new GUIStyle();
        style.fontSize = 24;
        style.fontStyle = FontStyle.Bold;
        style.alignment = TextAnchor.LowerRight;
        
        // Color based on mode
        if (isFastMode)
        {
            style.normal.textColor = Color.cyan;
        }
        else
        {
            style.normal.textColor = Color.green;
        }
        
        string modeText = isFastMode ? "⚡ TRAINING" : "👁️ WATCHING";
        string speedText = $"{modeText}\nSpeed: {currentSpeed}x\n\nControls:\nSPACE: Toggle\n1: Normal (1x)\n2: Fast (20x)\n3: Ultra (50x)\n0: Slow (0.5x)";
        
        // Background box
        GUI.Box(new Rect(Screen.width - 220, Screen.height - 180, 210, 170), "");
        
        // Text
        GUI.Label(new Rect(Screen.width - 215, Screen.height - 175, 200, 160), speedText, style);
    }
}
