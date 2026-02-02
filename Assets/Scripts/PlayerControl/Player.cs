// ===============================================================================
// Player.cs
// Reads player input to dictate movement to the controller
// Uses coyote time and jump buffering to account for variable player-drawn slopes
// ===============================================================================
using System.Collections;
using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(Controller2D))]
public class Player : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] float moveSpeed = 6f;
    [SerializeField] float moveSpeedIce = 10f;
    [SerializeField] float accelerationTimeGrounded = .05f;
    [SerializeField] float accelerationTimeAirborne = .15f;
    [SerializeField] float accelerationTimeIcy = .3f;

    [Header("Jump")]
    [SerializeField] float baseVelocity = 6f;
    [SerializeField] float holdAcceleration = 25f;
    [SerializeField] float holdAccelerationFalloff = 35f;
    [SerializeField] float holdDuration = .30f;
    [SerializeField] float coyoteDuration = .1f;

    [Header("Gravity")]
    [SerializeField] float gravity = -25f;
    [SerializeField] float maxVelocityY = 15f;
    [SerializeField] float fallMultiplier = 1.15f;

    [Header("Audio")]
    public AudioClip jumpSound;
    public AudioClip deathSound;
    public AudioClip respawnSound;

    [Header("Respawn")]
    public UnityEvent respawnObjects;

    Controller2D controller;
    SpriteRenderer spriteRenderer;
    Collider2D playerCollider;

    Vector2 velocity;
    float velocityXSmoothing;

    // Jump state
    bool jumping;
    float jumpTimeElapsed;
    float curHoldAcceleration;

    // Coyote time and jump buffering
    float coyoteTime;
    bool coyoteCheck;
    float jumpBufferTime;
    bool jumpBuffer;

    // Respawn
    Vector2 startPos;
    bool isDead;

    void Awake()
    {
        controller = GetComponent<Controller2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        playerCollider = GetComponent<Collider2D>();
    }

    void Start()
    {
        startPos = transform.position;
    }

    void Update()
    {
        HandleVerticalCollisionCancel();
        Vector2 input = ReadInput();
        HandleSpriteFlip(input.x);

        HandleCoyoteTime();
        HandleJumpInput();
        HandleJumpHold();
        HandleHorizontalMovement(input.x);
        ApplyGravity();

        controller.Move(velocity * Time.deltaTime, input);
    }

    #region Input

    Vector2 ReadInput()
    {
        return new Vector2(
            Input.GetAxisRaw("Horizontal"),
            Input.GetAxisRaw("Vertical")
        );
    }

    void HandleSpriteFlip(float xInput)
    {
        if (xInput != 0)
            spriteRenderer.flipX = xInput < 0;
    }

    #endregion

    #region Jump Logic

    void StartJump()
    {
        AudioSource.PlayClipAtPoint(jumpSound, transform.position);
        velocity.y = baseVelocity;
        jumpTimeElapsed = 0f;
        jumping = true;
        curHoldAcceleration = holdAcceleration;
    }

    void HandleJumpInput()
    {
        if (!Input.GetKeyDown(KeyCode.Space)) return;

        if (controller.collisions.below || coyoteCheck)
        {
            StartJump();
            coyoteCheck = false;
            jumpBuffer = false;
        }
        else
        {
            jumpBufferTime = 0;
            jumpBuffer = true;
        }
    }

    void HandleJumpHold()
    {
        if (!Input.GetKey(KeyCode.Space))
        {
            jumping = false;
            return;
        }

        if (jumping && jumpTimeElapsed < holdDuration)
        {
            velocity.y += curHoldAcceleration * Time.deltaTime;
            curHoldAcceleration -= holdAccelerationFalloff * Time.deltaTime;
            jumpTimeElapsed += Time.deltaTime;
        }
        else
        {
            jumping = false;
        }

        HandlePostCoyoteJump();
    }

    void HandlePostCoyoteJump()
    {
        if (!jumpBuffer) return;

        if (jumpBufferTime < coyoteDuration)
        {
            if (controller.collisions.below)
            {
                StartJump();
                jumpBuffer = false;
            }
            else
            {
                jumpBufferTime += Time.deltaTime;
            }
        }
        else
        {
            jumpBuffer = false;
        }
    }

    void HandleCoyoteTime()
    {
        if (controller.collisions.below && !jumping)
        {
            coyoteTime = 0;
            coyoteCheck = true;
            return;
        }

        if (coyoteCheck)
        {
            coyoteTime += Time.deltaTime;
            if (coyoteTime > coyoteDuration)
                coyoteCheck = false;
        }
    }

    #endregion

    #region Movement & Gravity

    void HandleHorizontalMovement(float xInput)
    {
        float targetSpeed =
            xInput * (controller.collisions.onIce ? moveSpeedIce : moveSpeed);

        float smoothTime =
            controller.collisions.below
                ? (controller.collisions.onIce ? accelerationTimeIcy : accelerationTimeGrounded)
                : accelerationTimeAirborne;

        velocity.x = Mathf.SmoothDamp(
            velocity.x,
            targetSpeed,
            ref velocityXSmoothing,
            smoothTime
        );

        if ((controller.collisions.left || controller.collisions.right) && !jumping)
            velocity.x = 0;
    }

    void ApplyGravity()
    {
        float multiplier = velocity.y <= 0 ? fallMultiplier : 1f;
        velocity.y += gravity * multiplier * Time.deltaTime;
        velocity.y = Mathf.Clamp(velocity.y, -maxVelocityY, maxVelocityY);
    }

    void HandleVerticalCollisionCancel()
    {
        if ((controller.collisions.above && velocity.y > 0) ||
            (controller.collisions.below && velocity.y < 0 && !jumping))
        {
            if (!controller.collisions.slidingDownMaxSlope)
                velocity.y = 0;
        }
    }

    #endregion

    #region Respawn

    public void Die()
    {
        if (isDead) return;

        isDead = true;
        AudioSource.PlayClipAtPoint(deathSound, transform.position);
        playerCollider.enabled = false;
        StartCoroutine(Respawn(1f));
    }

    IEnumerator Respawn(float delay)
    {
        spriteRenderer.enabled = false;
        yield return new WaitForSeconds(delay);

        AudioSource.PlayClipAtPoint(respawnSound, transform.position);
        transform.position = startPos;

        spriteRenderer.enabled = true;
        playerCollider.enabled = true;
        isDead = false;

        respawnObjects.Invoke();
    }

    public void UpdateCheckpoint(Vector2 pos)
    {
        startPos = pos;
    }

    #endregion
}