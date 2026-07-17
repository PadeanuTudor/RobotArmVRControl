using UnityEngine;

/// <summary>
/// Attach to a World Space Canvas that is at the ROOT of the scene hierarchy
/// (NOT parented to OVRCameraRig). The canvas will lazily follow the user's
/// head direction: it stays still while you look around within the deadzone,
/// then smoothly repositions when you turn far enough away from it.
///
/// Setup:
///   1. Create a World Space Canvas at scene root.
///   2. Attach this script to that Canvas GameObject.
///   3. Leave headTransform empty -- it auto-finds OVRCameraRig.centerEyeAnchor.
///      Or drag CenterEyeAnchor in manually for reliability.
/// </summary>
public class LazyFollowHUD : MonoBehaviour
{
    [Header("Head Reference")]
    [Tooltip("Drag OVRCameraRig > TrackingSpace > CenterEyeAnchor here. " +
             "Leave empty to auto-find at Start (slightly slower first frame).")]
    public Transform headTransform;

    [Header("Positioning")]
    [Tooltip("Distance from eyes to HUD center in meters. 1.5 to 2.0 is comfortable.")]
    [Range(1.0f, 3.0f)]
    public float followDistance = 1.8f;

    [Tooltip("Vertical offset relative to eye height. Negative moves HUD below eye level. " +
             "-0.2 puts it comfortably in the lower field of view.")]
    [Range(-0.8f, 0.4f)]
    public float verticalOffset = -0.2f;

    [Header("Lazy Follow Behavior")]
    [Tooltip("Speed at which the HUD repositions once outside the deadzone. " +
             "Lower values = more lag = feels more detached. 1.5 to 3 is natural.")]
    [Range(0.5f, 8f)]
    public float positionSmoothSpeed = 2f;

    [Tooltip("Speed at which the HUD rotates to face the user. " +
             "Slightly higher than positionSmoothSpeed keeps it from feeling wobbly.")]
    [Range(0.5f, 8f)]
    public float rotationSmoothSpeed = 3f;

    [Tooltip("How many degrees the user must look away from the HUD center before " +
             "it starts repositioning. 20 degrees is a comfortable deadzone.")]
    [Range(0f, 50f)]
    public float angularDeadzone = 20f;

    // Whether the HUD has been placed for the first time
    private bool initialized = false;

    void Start()
    {
        // Auto-find CenterEyeAnchor if not assigned in Inspector
        if (headTransform == null)
        {
            OVRCameraRig rig = FindObjectOfType<OVRCameraRig>();
            if (rig != null)
            {
                headTransform = rig.centerEyeAnchor;
                Debug.Log("[LazyFollowHUD] Auto-found centerEyeAnchor on OVRCameraRig.");
            }
            else
            {
                Debug.LogError("[LazyFollowHUD] headTransform is null and no OVRCameraRig " +
                               "found in scene. Assign CenterEyeAnchor in the Inspector.");
                enabled = false;
                return;
            }
        }
    }

    // LateUpdate ensures we run AFTER OVR has updated the head pose for this frame
    void LateUpdate()
    {
        if (headTransform == null) return;

        // Project head forward onto the horizontal plane so the HUD stays
        // upright even when the user tilts their head sideways
        Vector3 flatForward = Vector3.ProjectOnPlane(headTransform.forward, Vector3.up);
        if (flatForward.sqrMagnitude < 0.001f)
            flatForward = Vector3.forward;
        flatForward.Normalize();

        // Where we want the HUD to be
        Vector3 targetPosition = headTransform.position
            + flatForward * followDistance
            + Vector3.up * verticalOffset;

        // On the very first valid frame, snap to position with no interpolation
        if (!initialized)
        {
            transform.position = targetPosition;
            transform.rotation = Quaternion.LookRotation(flatForward, Vector3.up);
            initialized = true;
            return;
        }

        // Angular deadzone check:
        // Only start moving HUD if user has turned far enough away from where the HUD is
        Vector3 toHUD = Vector3.ProjectOnPlane(
            transform.position - headTransform.position,
            Vector3.up
        );

        if (toHUD.sqrMagnitude > 0.001f)
        {
            float angle = Vector3.Angle(flatForward, toHUD.normalized);
            if (angle > angularDeadzone)
            {
                transform.position = Vector3.Lerp(
                    transform.position,
                    targetPosition,
                    Time.deltaTime * positionSmoothSpeed
                );
            }
        }

        // Always softly rotate to face the user regardless of deadzone,
        // so the panel does not appear edge-on if the user moves around it
        Vector3 lookDir = transform.position - headTransform.position;
        if (lookDir.sqrMagnitude > 0.001f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(lookDir.normalized, Vector3.up);
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                targetRotation,
                Time.deltaTime * rotationSmoothSpeed
            );
        }
    }
}
