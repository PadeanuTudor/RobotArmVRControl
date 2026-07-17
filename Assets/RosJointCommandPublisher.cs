using UnityEngine;
using Unity.Robotics.ROSTCPConnector;
using RosMessageTypes.Std;
using RosMessageTypes.Sensor;
 
public class UrRobotController : MonoBehaviour
{
    [Header("Robot Configuration")]
    public string jointStateTopic = "/joint_states";
    public string commandTopic = "/forward_position_controller/commands";
 
    [Header("Drag 6 ArticulationBodies here (Base to Tip)")]
    public ArticulationBody[] joints = new ArticulationBody[6];
 
    private ROSConnection ros;
    private double[] lastPhysicalPositions = new double[6];
    private bool hasReceivedInitialState = false;
 
    void Start()
    {
        ros = ROSConnection.GetOrCreateInstance();
 
        // 1. Subscribe to see where the robot actually is
        ros.Subscribe<JointStateMsg>(jointStateTopic, UpdateRobotPoseFromPhysical);
 
        // 2. Register as a publisher to send commands
        ros.RegisterPublisher<Float64MultiArrayMsg>(commandTopic);
    }
 
    // This runs whenever ROS sends a joint update (Robot -> Unity)
    void UpdateRobotPoseFromPhysical(JointStateMsg msg)
    {
        // Note: UR joint names might arrive in a different order than your array
        // For simplicity, we assume the standard order. 
        for (int i = 0; i < 6; i++)
        {
            lastPhysicalPositions[i] = msg.position[i];
        }
        hasReceivedInitialState = true;
    }
 
    void Update()
    {
        // If we aren't in "Command Mode" (e.g. just started), 
        // sync the Unity ghost to the real robot.
        if (!Input.GetKey(KeyCode.Space) && hasReceivedInitialState)
        {
            SyncUnityToPhysical();
        }
        // If holding Space, send Unity's pose to the Robot (Unity -> Robot)
        else if (Input.GetKey(KeyCode.Space))
        {
            SendCommandToRobot();
        }
    }
 
    void SyncUnityToPhysical()
    {
        for (int i = 0; i < 6; i++)
        {
            var drive = joints[i].xDrive;
            drive.target = (float)(lastPhysicalPositions[i] * Mathf.Rad2Deg);
            joints[i].xDrive = drive;
        }
    }
 
    void SendCommandToRobot()
    {
        Float64MultiArrayMsg msg = new Float64MultiArrayMsg();
        msg.data = new double[6];
        for (int i = 0; i < 6; i++)
        {
            msg.data[i] = joints[i].jointPosition[0]; 
        }
        ros.Publish(commandTopic, msg);
    }
}