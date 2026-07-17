using UnityEngine;
using Unity.Robotics.ROSTCPConnector;
using RosMessageTypes.Std;
using RosMessageTypes.Sensor;
using System.Collections.Generic;

/// <summary>
/// Handles:
/// 1. Sending velocity commands to UR3e
/// 2. Receiving joint_states and driving Unity digital twin
///
/// PURE I/O LAYER — no joystick logic inside.
/// </summary>
public class UrVelocityBridge : MonoBehaviour
{
    [Header("ROS Topics")]
    public string velocityTopic = "/forward_velocity_controller/commands";
    public string jointStateTopic = "/joint_states";

    [Header("Robot Twin (Unity Articulation)")]
    public ArticulationBody[] joints = new ArticulationBody[6];

    [Header("Publishing")]
    public float publishRate = 0.02f;

    // ---------------- internal state ----------------

    private ROSConnection ros;
    private float timeElapsed;

    private readonly float[] desiredVelDeg = new float[6];

    private readonly float[] robotJointDeg = new float[6];

    private readonly string[] jointNames =
    {
        "shoulder_pan_joint",
        "shoulder_lift_joint",
        "elbow_joint",
        "wrist_1_joint",
        "wrist_2_joint",
        "wrist_3_joint"
    };

    private Dictionary<string, int> map = new Dictionary<string, int>();

    void Start()
    {
        ros = ROSConnection.GetOrCreateInstance();

        ros.RegisterPublisher<Float64MultiArrayMsg>(velocityTopic);
        ros.Subscribe<JointStateMsg>(jointStateTopic, OnJointState);

        for (int i = 0; i < jointNames.Length; i++)
            map[jointNames[i]] = i;
    }

    void Update()
    {
        timeElapsed += Time.deltaTime;
        if (timeElapsed >= publishRate)
        {
            SendVelocity();
            timeElapsed = 0f;
        }
    }

    // ===================== CONTROL =====================

    public void SetJointVelocitiesDeg(float j0, float j1, float j2, float j3, float j4, float j5)
    {
        desiredVelDeg[0] = j0;
        desiredVelDeg[1] = j1;
        desiredVelDeg[2] = j2;
        desiredVelDeg[3] = j3;
        desiredVelDeg[4] = j4;
        desiredVelDeg[5] = j5;
    }

    private void SendVelocity()
    {
        var msg = new Float64MultiArrayMsg();
        msg.data = new double[6];

        for (int i = 0; i < 6; i++)
            msg.data[i] = desiredVelDeg[i] * Mathf.Deg2Rad;

        ros.Publish(velocityTopic, msg);
    }

    // ===================== TWIN MIRROR =====================

    void OnJointState(JointStateMsg msg)
    {
        for (int i = 0; i < msg.name.Length; i++)
        {
            if (map.TryGetValue(msg.name[i], out int idx))
                robotJointDeg[idx] = (float)(msg.position[i] * Mathf.Rad2Deg);
        }

        ApplyToTwin();
    }

    private void ApplyToTwin()
    {
        for (int i = 0; i < 6; i++)
        {
            if (joints[i] == null) continue;

            var drive = joints[i].xDrive;
            drive.target = robotJointDeg[i];
            joints[i].xDrive = drive;
        }
    }
}