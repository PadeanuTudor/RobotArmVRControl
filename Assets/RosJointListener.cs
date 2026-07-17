using UnityEngine;
using Unity.Robotics.ROSTCPConnector;
using RosMessageTypes.Sensor;
 
public class RosJointListener : MonoBehaviour
{
    void Start()
    {
        // Tell Unity to listen to the /joint_states channel
        ROSConnection.GetOrCreateInstance().Subscribe<JointStateMsg>("/joint_states", ReceiveJointData);
        Debug.Log("Successfully subscribed to /joint_states!");
    }
 
    void ReceiveJointData(JointStateMsg jointMessage)
    {
        // Grab the name and angle of the first joint in the array
        string jointName = jointMessage.name[0];
        double jointAngleRadians = jointMessage.position[0];
 
        // Print it to the Unity Console
        Debug.Log($"Data from ROS -> {jointName} is at {jointAngleRadians} radians.");
    }
}