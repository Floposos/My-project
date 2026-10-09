using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class SUVController : MonoBehaviour
{
    [SerializeField] private float motorForce = 14000f;
    [SerializeField] private float turnSpeed = 45f;
    [SerializeField] private float maxSpeed = 20f;

    [Header("Visuals")]
    [SerializeField] private Transform[] frontWheels;
    [SerializeField] private Transform[] rearWheels;
    [SerializeField] private Transform[] frontCalipers;
    [SerializeField] private Transform steeringWheel;
    [SerializeField] private float wheelRadius = 0.37f;
    [SerializeField] private float maxSteerAngle = 32f;
    [SerializeField] private float steeringWheelRatio = 14f;
    [SerializeField] private float steerResponse = 4f;

    private Rigidbody rb;
    private float steerInput;
    private float smoothedSteer;
    private float wheelSpin;
    private Quaternion steeringWheelRest;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.centerOfMass = new Vector3(0f, -0.6f, 0f);
        if (steeringWheel != null)
        {
            steeringWheelRest = steeringWheel.localRotation;
        }
    }

    private void FixedUpdate()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
        {
            return;
        }

        float throttle = (keyboard.wKey.isPressed ? 1f : 0f) - (keyboard.sKey.isPressed ? 1f : 0f);
        steerInput = (keyboard.dKey.isPressed ? 1f : 0f) - (keyboard.aKey.isPressed ? 1f : 0f);

        if (rb.linearVelocity.magnitude < maxSpeed)
        {
            rb.AddForce(transform.forward * throttle * motorForce * Time.fixedDeltaTime, ForceMode.Acceleration);
        }

        float speedFactor = Mathf.Clamp01(rb.linearVelocity.magnitude / 4f);
        float directionSign = throttle == 0f ? 1f : Mathf.Sign(throttle);
        float turn = steerInput * turnSpeed * speedFactor * directionSign * Time.fixedDeltaTime;
        rb.MoveRotation(rb.rotation * Quaternion.Euler(0f, turn, 0f));
    }

    private void Update()
    {
        smoothedSteer = Mathf.MoveTowards(smoothedSteer, steerInput, steerResponse * Time.deltaTime);
        float steerAngle = smoothedSteer * maxSteerAngle;

        float forwardSpeed = Vector3.Dot(rb.linearVelocity, transform.forward);
        wheelSpin = Mathf.Repeat(wheelSpin + forwardSpeed / wheelRadius * Mathf.Rad2Deg * Time.deltaTime, 360f);

        Quaternion steer = Quaternion.Euler(0f, steerAngle, 0f);
        Quaternion spin = Quaternion.Euler(wheelSpin, 0f, 0f);
        foreach (Transform wheel in frontWheels)
        {
            wheel.localRotation = steer * spin;
        }
        foreach (Transform wheel in rearWheels)
        {
            wheel.localRotation = spin;
        }
        foreach (Transform caliper in frontCalipers)
        {
            caliper.localRotation = steer;
        }

        if (steeringWheel != null)
        {
            // Column axis is the wheel's local Z; negative turns clockwise as seen by the driver.
            steeringWheel.localRotation = steeringWheelRest * Quaternion.Euler(0f, 0f, -steerAngle * steeringWheelRatio);
        }
    }
}
