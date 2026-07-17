using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Converts gamepad input into joint velocity commands.
/// No physics. No ArticulationBody writes.
/// </summary>
public class JointVelocityTeleop : MonoBehaviour
{
    [Header("Bridge")]
    public UrVelocityBridge bridge;

    [Header("Speed Limits (deg/s)")]
    public float[] maxSpeed = new float[6]
    {
        30f, 20f, 30f, 40f, 40f, 40f
    };

    [Header("Input")]
    public float deadzone = 0.1f;
    public float smoothing = 12f;

    private float[] filtered = new float[6];

    void Update()
    {
        if (Gamepad.current == null || bridge == null)
            return;

        Vector2 left = Gamepad.current.leftStick.ReadValue();
        Vector2 right = Gamepad.current.rightStick.ReadValue();

        float[] raw = new float[6];

        raw[0] = left.x;
        raw[1] = left.y;
        raw[2] = right.x;
        raw[3] = right.y;

        raw[4] = Gamepad.current.leftShoulder.isPressed ? -1f :
                 Gamepad.current.rightShoulder.isPressed ? 1f : 0f;

        raw[5] = Gamepad.current.leftTrigger.ReadValue()
               - Gamepad.current.rightTrigger.ReadValue();

        for (int i = 0; i < 6; i++)
        {
            if (Mathf.Abs(raw[i]) < deadzone)
                raw[i] = 0f;

            filtered[i] = Mathf.Lerp(filtered[i], raw[i], Time.deltaTime * smoothing);
        }

        bridge.SetJointVelocitiesDeg(
            filtered[0] * maxSpeed[0],
            filtered[1] * maxSpeed[1],
            filtered[2] * maxSpeed[2],
            filtered[3] * maxSpeed[3],
            filtered[4] * maxSpeed[4],
            filtered[5] * maxSpeed[5]
        );
    }
}