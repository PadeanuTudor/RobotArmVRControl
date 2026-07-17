using UnityEngine;
using Unity.Robotics.ROSTCPConnector;
using RosMessageTypes.Sensor; // Required for JointStateMsg

public class RobotTwinSubscriber : MonoBehaviour
{
    [Header("ROS Connection")]
    public string jointStateTopic = "/joint_states";

    [Header("Robot Mapping")]
    [Tooltip("Drag your Unity ArticulationBodies here")]
    public ArticulationBody[] unityJoints;

    [Tooltip("Type the exact ROS joint names here, in the SAME ORDER as the ArticulationBodies above")]
    public string[] rosJointNames = new string[] 
    {
        "shoulder_pan_joint",
        "shoulder_lift_joint",
        "elbow_joint",
        "wrist_1_joint",
        "wrist_2_joint",
        "wrist_3_joint"
    };

    [Header("Settings")]
    public bool isTracking = true;

    void Start()
    {
        // Subscribe to the ROS topic. Whenever a message arrives, it triggers the OnJointStateReceived function.
        ROSConnection.GetOrCreateInstance().Subscribe<JointStateMsg>(jointStateTopic, OnJointStateReceived);
    }

    void OnJointStateReceived(JointStateMsg message)
    {
        if (!isTracking) return;

        // The ROS message contains arrays: name[], position[], velocity[], effort[]
        // We only care about name and position right now.
        for (int i = 0; i < message.name.Length; i++)
        {
            string incomingJointName = message.name[i];
            
            // ROS sends angles in Radians. We must cast to float and convert to Degrees for Unity.
            float incomingAngleDegrees = (float)message.position[i] * Mathf.Rad2Deg;

            // Find which index in our array matches this joint name
            int mappedIndex = System.Array.IndexOf(rosJointNames, incomingJointName);

            // If we found a match, apply the angle to the corresponding Unity joint
            if (mappedIndex != -1 && mappedIndex < unityJoints.Length)
            {
                ArticulationBody joint = unityJoints[mappedIndex];
                if (joint != null)
                {
                    ArticulationDrive drive = joint.xDrive;
                    
                    // NOTE: Depending on how the URDF was generated, you MIGHT need to invert the angle here
                    // e.g., drive.target = -incomingAngleDegrees;
                    drive.target = incomingAngleDegrees; 
                    
                    joint.xDrive = drive;
                }
            }
        }
    }
}