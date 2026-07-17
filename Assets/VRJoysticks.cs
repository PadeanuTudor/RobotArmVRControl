using UnityEngine;
using Unity.XR.CoreUtils;
using UnityEngine.XR;
using System.Collections.Generic;

/// <summary>
/// Converts Meta XR Quest 3 controller input into joint velocity commands.
/// 1:1 mapping from the gamepad version:
///
///   Left  Stick X          Joint 0 (Shoulder Pan)
///   Left  Stick Y          Joint 1 (Shoulder Lift)
///   Right Stick X          Joint 2 (Elbow)
///   Right Stick Y          Joint 3 (Wrist 1)
///   Left  Grip (hold)      Joint 4 (Wrist 2) negative
///   Right Grip (hold)      Joint 4 (Wrist 2) positive
///   Left  Trigger * Right Trigger      Joint 5 (Wrist 3)
///
/// No physics. No ArticulationBody writes.
/// </summary>
public class VRJoysticks : MonoBehaviour
{
    [Header("Bridge")]
    public UrVelocityBridge bridge;

    [Header("Speed Limits (deg/s)")]
    public float[] maxSpeed = new float[6] { 15f, 10f, 15f, 20f, 20f, 20f };

    [Header("Input")]
    public float deadzone = 0.1f;
    public float smoothing = 12f;

    // -------------------------------------------------------------------------
    // Private State
    // -------------------------------------------------------------------------

    private float[] filtered = new float[6];

    // XR device references
    private InputDevice leftController;
    private InputDevice rightController;
    private bool devicesFound = false;

    // -------------------------------------------------------------------------
    // Unity Lifecycle
    // -------------------------------------------------------------------------

    void Start()
    {
        TryFindControllers();
    }

    void Update()
    {
        if (bridge == null) return;

        // Retry finding controllers if not found yet
        if (!devicesFound)
        {
            TryFindControllers();
            if (!devicesFound) return;
        }

        float[] raw = new float[6];

        // --- Left Stick → Joints 0 and 1 ---
        if (leftController.TryGetFeatureValue(CommonUsages.primary2DAxis, out Vector2 leftStick))
        {
            raw[0] = leftStick.x; // Shoulder Pan
            raw[1] = leftStick.y; // Shoulder Lift
        }

        // --- Right Stick → Joints 2 and 3 ---
        if (rightController.TryGetFeatureValue(CommonUsages.primary2DAxis, out Vector2 rightStick))
        {
            raw[2] = rightStick.x; // Elbow
            raw[3] = rightStick.y; // Wrist 1
        }

        // --- Grip Buttons → Joint 4 (maps to left/right shoulder buttons) ---
        // Left grip = negative, Right grip = positive
        rightController.TryGetFeatureValue(CommonUsages.gripButton, out bool rightGrip);
        leftController.TryGetFeatureValue(CommonUsages.gripButton, out bool leftGrip);

        if (rightGrip && !leftGrip)
            raw[4] = 1f;
        else if (leftGrip && !rightGrip)
            raw[4] = -1f;
        else
            raw[4] = 0f;

        // --- Both Triggers multiplied → Joint 5 (maps to leftTrigger * rightTrigger) ---
        leftController.TryGetFeatureValue(CommonUsages.trigger, out float leftTrigger);
        rightController.TryGetFeatureValue(CommonUsages.trigger, out float rightTrigger);
        if (leftTrigger > 0f && rightTrigger == 0f)
            raw[5] = 1f;
        else if (leftTrigger == 0f && rightTrigger > 0f)
            raw[5] = -1f;
        else
            raw[5] = 0f;

        // --- Apply deadzone ---
        for (int i = 0; i < 6; i++)
        {
            if (Mathf.Abs(raw[i]) < deadzone)
                raw[i] = 0f;
        }

        // --- Smooth inputs ---
        for (int i = 0; i < 6; i++)
            filtered[i] = Mathf.Lerp(filtered[i], raw[i], Time.deltaTime * smoothing);

        // --- Send to bridge ---
        bridge.SetJointVelocitiesDeg(
            filtered[0] * maxSpeed[0],
            filtered[1] * maxSpeed[1],
            filtered[2] * maxSpeed[2],
            filtered[3] * maxSpeed[3],
            filtered[4] * maxSpeed[4],
            filtered[5] * maxSpeed[5]
        );
    }

    // -------------------------------------------------------------------------
    // Device Discovery
    // -------------------------------------------------------------------------

    void TryFindControllers()
    {
        var leftDevices = new List<InputDevice>();
        var rightDevices = new List<InputDevice>();

        InputDevices.GetDevicesWithCharacteristics(
            InputDeviceCharacteristics.Left | InputDeviceCharacteristics.Controller,
            leftDevices);

        InputDevices.GetDevicesWithCharacteristics(
            InputDeviceCharacteristics.Right | InputDeviceCharacteristics.Controller,
            rightDevices);

        if (leftDevices.Count > 0 && rightDevices.Count > 0)
        {
            leftController = leftDevices[0];
            rightController = rightDevices[0];
            devicesFound = true;
            Debug.Log("JointVelocityTeleop: Quest 3 controllers found.");
        }
    }
}