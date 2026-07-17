using UnityEngine;
using TMPro;

/// <summary>
/// Attach to Telemetry_Panel (child of HUD_Canvas).
///
/// This script does NOT read from your robot scripts directly.
/// Instead, your other scripts PUSH data into it by calling the public methods below.
///
/// Minimum wiring from your existing scripts:
///
///   Robot Twin       --> call SetJointAngles(float[] angles6) in its Update
///   Robot Twin       --> call SetTCPPosition(Vector3 pos) in its Update
///   ROS Connection   --> call SetConnectionStatus("CONNECTED", true) on connect
///                        and SetConnectionStatus("OFFLINE", false) on disconnect
///   VR Joysticks     --> call SetJoystickInput(Vector2 left, Vector2 right) in its Update (optional)
///
/// All text fields are assigned in the Inspector. Nothing breaks if a field is left unassigned.
/// </summary>
public class TelemetryDisplay : MonoBehaviour
{
    // ------------------------------------------------------------------
    // INSPECTOR -- drag the TMP_Text GameObjects from Telemetry_Panel here
    // ------------------------------------------------------------------

    [Header("Joint Angle Display")]
    [Tooltip("Single text field showing all 6 joints. Name it Joints_Text in the hierarchy.")]
    public TMP_Text jointsText;

    [Header("TCP Position")]
    [Tooltip("Shows live XYZ world position of the TCP.")]
    public TMP_Text tcpPositionText;

    [Header("Joystick Input (optional debug display)")]
    [Tooltip("Shows the raw joystick axes being sent. Useful during calibration.")]
    public TMP_Text joystickText;

    [Header("Status Row")]
    [Tooltip("GRAB or mode indicator. Set by calling SetModeStatus().")]
    public TMP_Text modeStatusText;
    [Tooltip("ROS connection status. Set by calling SetConnectionStatus().")]
    public TMP_Text connectionStatusText;

    // ------------------------------------------------------------------
    // PRIVATE STATE
    // ------------------------------------------------------------------

    private static readonly Color ColorGood    = new Color(0.2f,  1.0f,  0.3f);   // green
    private static readonly Color ColorNeutral = new Color(0.75f, 0.75f, 0.75f);  // light gray
    private static readonly Color ColorBad     = new Color(1.0f,  0.35f, 0.35f);  // red
    private static readonly Color ColorWarn    = new Color(1.0f,  0.85f, 0.2f);   // amber

    // Cached last values so we only update TMP when data actually changes
    private float[] lastAngles   = new float[6];
    private Vector3  lastTCP     = Vector3.zero;
    private Vector2  lastLeft    = Vector2.zero;
    private Vector2  lastRight   = Vector2.zero;

    // ------------------------------------------------------------------
    // UNITY LIFECYCLE
    // ------------------------------------------------------------------

    void Start()
    {
        // Set safe defaults on startup
        SetConnectionStatus("OFFLINE", false);
        SetModeStatus("IDLE", false);

        if (jointsText != null)
            jointsText.text = BuildJointsString(new float[6]);

        if (tcpPositionText != null)
            tcpPositionText.text = "X +0.000  Y +0.000  Z +0.000";

        if (joystickText != null)
            joystickText.text = "L (0.00, 0.00)   R (0.00, 0.00)";
    }

    // ------------------------------------------------------------------
    // PUBLIC PUSH API
    // Call these from Robot Twin, VR Joysticks, ROS Connection, etc.
    // ------------------------------------------------------------------

    /// <summary>
    /// Push the current 6 joint angles (in degrees) from Robot Twin.
    /// Call this in Robot Twin's Update() every frame.
    ///
    /// Example inside Robot Twin:
    ///     telemetryDisplay.SetJointAngles(new float[]
    ///         { joint1Deg, joint2Deg, joint3Deg, joint4Deg, joint5Deg, joint6Deg });
    /// </summary>
    public void SetJointAngles(float[] angles6)
    {
        if (angles6 == null || angles6.Length < 6) return;
        if (jointsText == null) return;

        // Only rebuild the string if something changed (saves GC alloc each frame)
        bool changed = false;
        for (int i = 0; i < 6; i++)
        {
            if (!Mathf.Approximately(angles6[i], lastAngles[i]))
            {
                changed = true;
                lastAngles[i] = angles6[i];
            }
        }

        if (changed)
            jointsText.text = BuildJointsString(angles6);
    }

    /// <summary>
    /// Push the TCP world position from Robot Twin or from your forward-kinematics calculation.
    /// Call this in Robot Twin's Update() every frame.
    ///
    /// Example:
    ///     telemetryDisplay.SetTCPPosition(wrist3Link.position);
    /// </summary>
    public void SetTCPPosition(Vector3 worldPosition)
    {
        if (tcpPositionText == null) return;
        if (worldPosition == lastTCP) return;

        lastTCP = worldPosition;
        Vector3 p = worldPosition;
        tcpPositionText.text = $"X {p.x:+0.000;-0.000}  Y {p.y:+0.000;-0.000}  Z {p.z:+0.000;-0.000}";
    }

    /// <summary>
    /// Push joystick axes from VR Joysticks script.
    /// Call this in VR Joysticks Update() every frame.
    ///
    /// Example:
    ///     telemetryDisplay.SetJoystickInput(leftStickAxis, rightStickAxis);
    /// </summary>
    public void SetJoystickInput(Vector2 leftStick, Vector2 rightStick)
    {
        if (joystickText == null) return;
        if (leftStick == lastLeft && rightStick == lastRight) return;

        lastLeft  = leftStick;
        lastRight = rightStick;
        joystickText.text = $"L ({leftStick.x:+0.00;-0.00}, {leftStick.y:+0.00;-0.00})" +
                            $"   R ({rightStick.x:+0.00;-0.00}, {rightStick.y:+0.00;-0.00})";
    }

    /// <summary>
    /// Push ROS connection status from your ROS Connection script.
    ///
    /// Call on connect:    telemetryDisplay.SetConnectionStatus("CONNECTED", true);
    /// Call on disconnect: telemetryDisplay.SetConnectionStatus("OFFLINE",   false);
    /// Call on error:      telemetryDisplay.SetConnectionStatus("ERROR",     false);
    /// </summary>
    public void SetConnectionStatus(string label, bool isConnected)
    {
        if (connectionStatusText == null) return;
        connectionStatusText.text  = $"ROS: {label}";
        connectionStatusText.color = isConnected ? ColorGood : ColorBad;
    }

    /// <summary>
    /// Push the current control mode or any status string.
    /// active=true colors it green, active=false colors it gray.
    ///
    /// Example from VR Joysticks:
    ///     telemetryDisplay.SetModeStatus("JOGGING", true);
    /// </summary>
    public void SetModeStatus(string label, bool active)
    {
        if (modeStatusText == null) return;
        modeStatusText.text  = $"MODE: {label}";
        modeStatusText.color = active ? ColorGood : ColorNeutral;
    }

    // ------------------------------------------------------------------
    // PRIVATE HELPERS
    // ------------------------------------------------------------------

    private string BuildJointsString(float[] a)
    {
        return $"J1:  {a[0],8:F2} deg\n" +
               $"J2:  {a[1],8:F2} deg\n" +
               $"J3:  {a[2],8:F2} deg\n" +
               $"J4:  {a[3],8:F2} deg\n" +
               $"J5:  {a[4],8:F2} deg\n" +
               $"J6:  {a[5],8:F2} deg";
    }
}
