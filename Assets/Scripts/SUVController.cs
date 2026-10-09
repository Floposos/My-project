using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class SUVController : MonoBehaviour
{
    [SerializeField] private float motorForce = 14000f;
    [SerializeField] private float turnSpeed = 45f;
    [SerializeField] private float maxSpeed = 20f;

    private Rigidbody rb;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.centerOfMass = new Vector3(0f, -0.6f, 0f);
    }

    private void FixedUpdate()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
        {
            return;
        }

        float throttle = (keyboard.wKey.isPressed ? 1f : 0f) - (keyboard.sKey.isPressed ? 1f : 0f);
        float steer = (keyboard.dKey.isPressed ? 1f : 0f) - (keyboard.aKey.isPressed ? 1f : 0f);

        if (rb.linearVelocity.magnitude < maxSpeed)
        {
            rb.AddForce(transform.forward * throttle * motorForce * Time.fixedDeltaTime, ForceMode.Acceleration);
        }

        float speedFactor = Mathf.Clamp01(rb.linearVelocity.magnitude / 4f);
        float directionSign = throttle == 0f ? 1f : Mathf.Sign(throttle);
        float turn = steer * turnSpeed * speedFactor * directionSign * Time.fixedDeltaTime;
        rb.MoveRotation(rb.rotation * Quaternion.Euler(0f, turn, 0f));
    }
}
