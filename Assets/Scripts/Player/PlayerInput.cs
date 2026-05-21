using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerInput : MonoBehaviour
{
    [SerializeField]
    private float speed = 5f;
    [SerializeField]
    private float sprintMultiplier = 1.65f;
    [SerializeField]
    private float acceleration = 18f;
    [SerializeField]
    private float airControl = 0.55f;
    [SerializeField]
    private float lookSensitivity = 8f;
    [SerializeField]
    private float thrusterForce = 20f;
    [SerializeField]
    private PlayerController controller;

    private float distToGround = 0f;
    private float currentSpeed = 0f;
    private float jumpBufferTimer = 0f;
    private float groundedGraceTimer = 0f;

    // Start is called before the first frame update
    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            distToGround = col.bounds.extents.y;
        }
    }

    // Update is called once per frame
    void Update()
    {
        if (TrialChallengeDirector.Singleton != null && TrialChallengeDirector.Singleton.IsPaused())
        {
            if (controller != null)
            {
                controller.Move(Vector3.zero);
                controller.Rotate(Vector3.zero, Vector3.zero);
            }
            return;
        }

        float xMov = Input.GetAxisRaw("Horizontal");
        float yMov = Input.GetAxisRaw("Vertical");
        bool hasMovement = Mathf.Abs(xMov) > 0.01f || Mathf.Abs(yMov) > 0.01f;
        bool grounded = Physics.Raycast(transform.position, -Vector3.up, distToGround + 0.16f);
        bool wantsSprint = Input.GetKey(KeyCode.LeftShift) && yMov > 0.1f && !Input.GetMouseButton(1);

        groundedGraceTimer = grounded ? 0.14f : groundedGraceTimer - Time.deltaTime;
        if (Input.GetButtonDown("Jump"))
        {
            jumpBufferTimer = 0.14f;
        }
        else
        {
            jumpBufferTimer -= Time.deltaTime;
        }

        float targetSpeed = hasMovement ? speed * (wantsSprint ? sprintMultiplier : 1f) : 0f;
        currentSpeed = Mathf.MoveTowards(currentSpeed, targetSpeed, acceleration * Time.deltaTime);

        Vector3 velocity = (transform.right * xMov + transform.forward * yMov).normalized * currentSpeed;
        if (!grounded)
        {
            velocity *= airControl;
        }
        if (controller != null)
        {
            controller.Move(velocity);
        }

        float xMouse = Input.GetAxisRaw("Mouse X");
        float yMouse = Input.GetAxisRaw("Mouse Y");

        Vector3 yRotation = new Vector3(0f, xMouse, 0f) * lookSensitivity;
        Vector3 xRotation = new Vector3(-yMouse, 0f, 0f) * lookSensitivity;
        if (controller != null)
        {
            controller.Rotate(yRotation, xRotation);
        }

        if (jumpBufferTimer > 0f && groundedGraceTimer > 0f)
        {
            Vector3 force = Vector3.up * thrusterForce;
            if (controller != null)
            {
                controller.Thrust(force);
            }
            jumpBufferTimer = 0f;
            groundedGraceTimer = 0f;
        }
    }
}
