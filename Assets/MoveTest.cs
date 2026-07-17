using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RobotMover : MonoBehaviour
{
    [Header("Assign the joints you want to move")]
    public ArticulationBody jointToMove1;
    public ArticulationBody jointToMove2;
    public ArticulationBody jointToMove3;
    public ArticulationBody jointToMove4;
    public ArticulationBody jointToMove5;
    public ArticulationBody jointToMove6;

    [Header("Target angle for each joint")]
    [Range(-360f, 360f)]
    public float targetAngle1 = 0f;
    [Range(-360f, 360f)]
    public float targetAngle2 = 0f;
    [Range(-360f, 360f)]
    public float targetAngle3 = 0f;
    [Range(-360f, 360f)]
    public float targetAngle4 = 0f;
    [Range(-360f, 360f)]
    public float targetAngle5 = 0f;
    [Range(-360f, 360f)]
    public float targetAngle6 = 0f;

    void Start()
    {
        foreach (var joint in GetJoints())
        {
            if (joint == null)
                continue;

            ArticulationDrive drive = joint.xDrive;
            drive.stiffness = 100000f;
            drive.damping = 10000f;
            drive.forceLimit = 100000f;
            joint.xDrive = drive;
        }
    }

    void Update()
    {
        var joints = GetJoints();
        var targets = GetTargetAngles();
        int count = Mathf.Min(joints.Length, targets.Length);

        for (int i = 0; i < count; i++)
        {
            var joint = joints[i];
            if (joint == null)
                continue;

            ArticulationDrive drive = joint.xDrive;
            drive.target = targets[i];
            joint.xDrive = drive;
        }
    }

    private ArticulationBody[] GetJoints()
    {
        return new[] { jointToMove1, jointToMove2, jointToMove3, jointToMove4, jointToMove5, jointToMove6 };
    }

    private float[] GetTargetAngles()
    {
        return new[] { targetAngle1, targetAngle2, targetAngle3, targetAngle4, targetAngle5, targetAngle6 };
    }
}
