using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Core Player Controller untuk Aetheric Breach
/// Menghandle movement, jump, dan double jump dengan feel yang tight & responsive
/// VERSION: Fixed Jump Animation Stuck Issue (Bool-based solution)
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(Animator))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 8f;
    [SerializeField] private float acceleration = 50f;
    [SerializeField] private float deceleration = 50f;
    [SerializeField] private float airControlMultiplier = 0.7f;

    [Header("Jump Settings")]
    [SerializeField] private float jumpForce = 15f;
    [SerializeField] private float jumpCutMultiplier = 0.5f;
    [SerializeField] private float fallMultiplier = 2.5f;
    [SerializeField] private int maxAirJump = 1;
    
    [Header("Ground Check")]
    [SerializeField] private Transform groundCheck;
    [SerializeField] private Vector2 groundCheckSize = new Vector2(0.9f, 0.1f);
    [SerializeField] private LayerMask groundLayer;
    
    [Header("Coyote Time & Jump Buffer")]
    [SerializeField] private float coyoteTime = 0.15f;
    [SerializeField] private float jumpBufferTime = 0.2f;

    // Cached Components
    private Rigidbody2D rb;
    private Collider2D col;
    private Animator animator;
    
    // Input Variables
    private Vector2 moveInput;
    private bool jumpPressed;
    private bool jumpHeld;
    
    // State Variables
    private bool isGrounded;
    private int jumpCount;
    private float coyoteTimeCounter;
    private float jumpBufferCounter;

    // Property untuk akses dari script lain
    public bool IsGrounded => isGrounded;
    public Vector2 Velocity => rb.linearVelocity;
    public bool IsMoving => Mathf.Abs(moveInput.x) > 0.1f;

    #region Unity Lifecycle

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>();
        animator = GetComponent<Animator>();
        
        rb.gravityScale = 3f;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
    }

    private void Update()
    {
        CheckGroundStatus();
        UpdateTimers();
        UpdateAnimatorParameters();
    }

    private void FixedUpdate()
    {
        HandleMovement();
        HandleJump();
        ApplyBetterJumpPhysics();
    }

    #endregion

    #region Input Callbacks

    public void OnMove(InputAction.CallbackContext context)
    {
        moveInput = context.ReadValue<Vector2>();
    }

    public void OnJump(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            jumpPressed = true;
            jumpHeld = true;
            jumpBufferCounter = jumpBufferTime;
        }
        else if (context.canceled)
        {
            jumpHeld = false;
        }
    }

    #endregion

    #region Movement Logic

    private void HandleMovement()
    {
        float targetSpeed = moveInput.x * moveSpeed;
        float currentSpeed = rb.linearVelocity.x;

        float accelRate = Mathf.Abs(targetSpeed) > 0.01f ? acceleration : deceleration;
        
        if (!isGrounded)
        {
            accelRate *= airControlMultiplier;
        }

        float speedDif = targetSpeed - currentSpeed;
        float movement = speedDif * accelRate * Time.fixedDeltaTime;

        rb.linearVelocity = new Vector2(currentSpeed + movement, rb.linearVelocity.y);

        if (moveInput.x != 0)
        {
            transform.localScale = new Vector3(Mathf.Sign(moveInput.x), 1, 1);
        }
    }

    #endregion

    #region Jump Logic

    private void HandleJump()
    {
        bool canJump = (isGrounded || coyoteTimeCounter > 0f || jumpCount < maxAirJump) 
                       && (jumpPressed || jumpBufferCounter > 0f);

        if (canJump)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0f);
            rb.AddForce(Vector2.up * jumpForce, ForceMode2D.Impulse);
            
            if (!isGrounded && coyoteTimeCounter <= 0f)
            {
                jumpCount++;
            }
                        
            jumpPressed = false;
            jumpBufferCounter = 0f;
            coyoteTimeCounter = 0f;

        }
    }

    private void ApplyBetterJumpPhysics()
    {

        if (!jumpHeld && rb.linearVelocity.y > 0)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, rb.linearVelocity.y * jumpCutMultiplier);
        }


        if (rb.linearVelocity.y < 0)
        {
            rb.gravityScale = 3f * fallMultiplier;
        }
        else
        {
            rb.gravityScale = 3f;
        }
    }

    #endregion

    #region Ground Check & Timers

    private void CheckGroundStatus()
    {

        bool wasGrounded = isGrounded;
        isGrounded = Physics2D.OverlapBox(
            groundCheck.position, 
            groundCheckSize, 
            0f, 
            groundLayer
        );


        if (isGrounded && !wasGrounded)
        {
            jumpCount = 0;

        }


        if (!isGrounded && wasGrounded)
        {
            coyoteTimeCounter = coyoteTime;
        }
    }

    private void UpdateTimers()
    {

        if (coyoteTimeCounter > 0)
        {
            coyoteTimeCounter -= Time.deltaTime;
        }

        if (jumpBufferCounter > 0)
        {
            jumpBufferCounter -= Time.deltaTime;
        }
    }

    #endregion

    #region Animator Parameter

    private void UpdateAnimatorParameters()
    {
        // Speed untuk blend tree idle/run
        animator.SetFloat("Speed", Mathf.Abs(rb.linearVelocity.x));

        // IsJumping = true saat velocity.y > threshold (naik)
        // PENTING: Threshold kecil (0.1f) supaya responsif
        bool isJumping = rb.linearVelocity.y > 0.1f && !isGrounded;
        animator.SetBool("IsJumping", isJumping);

        // IsFalling = true saat velocity.y < threshold (turun)
        bool isFalling = rb.linearVelocity.y < -0.1f && !isGrounded;
        animator.SetBool("IsFalling", isFalling);

        // IsGrounded untuk transition control
        animator.SetBool("IsGrounded", isGrounded);
    }

    #endregion 

    #region Debug & Gizmos


    private void OnDrawGizmosSelected()
    {
        if (groundCheck == null) return;

        Gizmos.color = isGrounded ? Color.green : Color.red;
        Gizmos.DrawWireCube(groundCheck.position, groundCheckSize);
    }

    #endregion
}