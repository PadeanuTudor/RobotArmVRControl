using UnityEngine;

public class FreeCamCamera : MonoBehaviour
{
    public float moveSpeed = 2f;
    public float lookSpeed = 2f;
    private float rotX, rotY;

    void Update()
    {
        if (Input.GetMouseButton(1))
        {
            rotX -= Input.GetAxis("Mouse Y") * lookSpeed;
            rotY += Input.GetAxis("Mouse X") * lookSpeed;
            transform.eulerAngles = new Vector3(rotX, rotY, 0);
        }

        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");
        float u = Input.GetKey(KeyCode.E) ? 1f :
                  Input.GetKey(KeyCode.Q) ? -1f : 0f;

        transform.position += transform.forward * v * moveSpeed * Time.deltaTime;
        transform.position += transform.right * h * moveSpeed * Time.deltaTime;
        transform.position += transform.up * u * moveSpeed * Time.deltaTime;
    }
}