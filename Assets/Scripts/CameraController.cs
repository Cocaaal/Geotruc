using UnityEngine;
using UnityEngine.InputSystem;

public class CameraController : MonoBehaviour
{

    [SerializeField, Range(30, 100)]
    public float minDistance = 35f;

    [SerializeField, Range(30, 100)]
    public float maxDistance = 50f;

    [SerializeField]
    public Transform focusPoint;

    [SerializeField, Range(0, 1)]
    public float sensitivity = 0.04f;

    private Vector2 camRotation = new Vector2(0, 0);
    private float radius;

    private bool isDragging = false;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        radius = (minDistance + maxDistance) / 2;
    }

    // Update is called once per frame
    void LateUpdate()
    {
        camRotation.y = Mathf.Clamp(camRotation.y, -85, 85);
        radius = Mathf.Clamp(radius, minDistance, maxDistance);

        Quaternion rot = Quaternion.Euler(camRotation.y, camRotation.x, 0);

        Vector3 offset = rot * Vector3.back * radius;

        transform.position = focusPoint.position + offset;
        transform.LookAt(focusPoint);
    }

    public void OnMove(InputAction.CallbackContext context)
    {
        if (!isDragging) return;
        Vector2 input = context.ReadValue<Vector2>();
        camRotation.x += input.x * sensitivity;
        camRotation.y -= input.y * sensitivity;
    }

    public void OnZoom(InputAction.CallbackContext context)
    {
        Vector2 input = context.ReadValue<Vector2>();
        radius -= input.y;
    }

    public void OnClick(InputAction.CallbackContext context)
    {
        if (context.performed) isDragging = true;
        if (context.canceled) isDragging = false;
    }
}
