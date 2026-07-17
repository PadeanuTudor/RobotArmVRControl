using UnityEngine;
using UnityEngine.InputSystem; // This is the new package we just installed!

public class JoystickJointJogger : MonoBehaviour
{
    [Header("Assign the 4 Joints You Want to Control")]
    public ArticulationBody jointAxis1; // Left Stick X (e.g., Base)
    public ArticulationBody jointAxis2; // Left Stick Y (e.g., Shoulder)
    public ArticulationBody jointAxis3; // Right Stick X (e.g., Elbow)
    public ArticulationBody jointAxis4; // Right Stick Y (e.g., Wrist 1)

    [Header("Settings")]
    public float rotationSpeed = 45f; 
    public float deadzone = 0.1f;     

    void Start()
    {
        // Ensure all assigned joints have muscle power on startup
        InitializeJoint(jointAxis1);
        InitializeJoint(jointAxis2);
        InitializeJoint(jointAxis3);
        InitializeJoint(jointAxis4);
    }

    void Update()
    {
        // Safety check: Is a gamepad actually plugged in?
        if (Gamepad.current == null) return;

        // Read the raw values from the gamepad's thumbsticks (-1.0 to 1.0)
        Vector2 leftStick = Gamepad.current.leftStick.ReadValue();
        Vector2 rightStick = Gamepad.current.rightStick.ReadValue();

        // Pass the stick values to our movement function
        MoveJoint(jointAxis1, leftStick.x); 
        MoveJoint(jointAxis2, leftStick.y); 
        MoveJoint(jointAxis3, rightStick.x); 
        MoveJoint(jointAxis4, rightStick.y); 
    }

    private void MoveJoint(ArticulationBody joint, float inputValue)
    {
        if (joint == null) return;

        if (Mathf.Abs(inputValue) > deadzone)
        {
            ArticulationDrive drive = joint.xDrive;
            drive.target += inputValue * rotationSpeed * Time.deltaTime;
            joint.xDrive = drive;
        }
    }

    private void InitializeJoint(ArticulationBody joint)
    {
        if (joint != null)
        {
            ArticulationDrive drive = joint.xDrive;
            drive.stiffness = 100000f;
            drive.damping = 10000f;
            drive.forceLimit = 100000f;
            joint.xDrive = drive;
        }
    }
}