using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Core Player Controller untuk Aetheric Breach
/// Menghandle movement, jump, dan double jump dengan feel yang tight & responsive
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 8f;
    [SerializeField] private float acceleration = 50f; // Seberapa cepat mencapai max speed
    [SerializeField] private float deceleration = 50f; // Seberapa cepat berhenti
    [SerializeField] private float airControlMultiplier = 0.7f; // Kontrol saat di udara (0-1)

    [Header("Jump Settings")]
    [SerializeField] private float jumpForce = 15f;
    [SerializeField] private float jumpCutMultiplier = 0.5f; // Untuk variable height jump
    [SerializeField] private float fallMultiplier = 2.5f; // Gravity lebih kuat saat jatuh (feel better)
    [SerializeField] private int maxAirJump = 1;
    [SerializeField] private bool isCurrentlyTakingOff;
    
    [Header("Ground Check")]
    [SerializeField] private Transform groundCheck;
    [SerializeField] private Vector2 groundCheckSize = new Vector2(0.9f, 0.1f);
    [SerializeField] private LayerMask groundLayer;
    
    [Header("Coyote Time & Jump Buffer")]
    [SerializeField] private float coyoteTime = 0.15f; // Grace period setelah jatuh dari platform
    [SerializeField] private float jumpBufferTime = 0.2f; // Input jump sedikit sebelum landing

    // Cached Components (OPTIMIZE: caching untuk avoid GetComponent tiap frame)
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
    private float lastGroundedTime;

    // Property untuk akses dari script lain (misal: Animator)
    public bool IsGrounded => isGrounded;
    public Vector2 Velocity => rb.linearVelocity;
    public bool IsMoving => Mathf.Abs(moveInput.x) > 0.1f;

    #region Unity Lifecycle

    private void Awake()
    {
        // OPTIMIZE: Cache component references di Awake (lebih cepat dari Start)
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>();
        animator = GetComponent<Animator>();
        
        // Setup Rigidbody untuk platformer feel yang optimal
        rb.gravityScale = 3f; // Gravity yang lebih kuat = jump terasa lebih snappy
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate; // Smooth visual movement
    }

    private void Update()
    {
        // Ground check harus di Update karena perlu presisi per frame
        CheckGroundStatus();
        
        // Handle coyote time & jump buffer timers
        UpdateTimers();

        UpdateAnimatorParameters();
    }

    private void FixedUpdate()
    {
        // Semua physics-based movement harus di FixedUpdate
        HandleMovement();
        HandleJump();
        ApplyBetterJumpPhysics();
    }

    #endregion

    #region Input Callbacks (dari Input System)

    // Dipanggil oleh Input System saat Move input berubah
    public void OnMove(InputAction.CallbackContext context)
    {
        moveInput = context.ReadValue<Vector2>();
    }

    // Dipanggil oleh Input System saat Jump button ditekan/dilepas
    public void OnJump(InputAction.CallbackContext context)
    {
        if (context.performed) // Button ditekan
        {
            jumpPressed = true;
            jumpHeld = true;
            jumpBufferCounter = jumpBufferTime; // Set jump buffer
        }
        else if (context.canceled) // Button dilepas
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

        // Tentukan acceleration/deceleration rate
        float accelRate;
        if (Mathf.Abs(targetSpeed) > 0.01f)
        {
            accelRate = acceleration;
        }
        else
        {
            accelRate = deceleration;
        }

        // Apply air control multiplier jika tidak di ground
        if (!isGrounded)
        {
            accelRate *= airControlMultiplier;
        }

        // Smooth acceleration menggunakan Lerp
        // OPTIMIZE: Time.fixedDeltaTime karena di FixedUpdate
        float speedDif = targetSpeed - currentSpeed;
        float movement = speedDif * accelRate * Time.fixedDeltaTime;

        // Apply movement
        rb.linearVelocity = new Vector2(currentSpeed + movement, rb.linearVelocity.y);

        // JUICE TIP: Flip sprite berdasarkan arah gerak (bisa dipindah ke script terpisah)
        if (moveInput.x != 0)
        {
            transform.localScale = new Vector3(Mathf.Sign(moveInput.x), 1, 1);
        }
    }

    #endregion

    #region Jump Logic

    private void HandleJump()
    {
        isCurrentlyTakingOff = false;

        // Cek apakah bisa jump
        bool canJump = (isGrounded || coyoteTimeCounter > 0f || jumpCount < maxAirJump) 
                       && (jumpPressed || jumpBufferCounter > 0f);

        if (canJump)
        {
            // Reset velocity.y sebelum jump (supaya consistent)
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0f);
            
            // Apply jump force
            rb.AddForce(Vector2.up * jumpForce, ForceMode2D.Impulse);

            if (!isCurrentlyTakingOff)
            {
               animator.SetTrigger("Jump");
               isCurrentlyTakingOff = true;
            }
            
            
            // Increment jump count jika di udara (double jump)
            if (!isGrounded && coyoteTimeCounter <= 0f)
            {
                jumpCount++;
            }
            
            // Reset counters
            jumpPressed = false;
            jumpBufferCounter = 0f;
            coyoteTimeCounter = 0f;

            
            // JUICE TIP: Trigger particle effect atau SFX di sini
            // OnJumpPerformed?.Invoke();
        }
    }

    private void ApplyBetterJumpPhysics()
    {
        // Variable Height Jump: jika player lepas jump button saat naik, potong velocity
        if (!jumpHeld && rb.linearVelocity.y > 0)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, rb.linearVelocity.y * jumpCutMultiplier);
        }

        // Better Fall Physics: tambah gravity saat jatuh supaya terasa lebih "weighted"
        if (rb.linearVelocity.y < 0)
        {
            rb.gravityScale = 3f * fallMultiplier;
        }
        else
        {
            rb.gravityScale = 3f; // Normal gravity saat naik
        }
    }

    #endregion

    #region Ground Check & Timers

    private void CheckGroundStatus()
    {
        // OPTIMIZE: OverlapBox lebih akurat & efficient daripada Raycast untuk platformer
        bool wasGrounded = isGrounded;
        isGrounded = Physics2D.OverlapBox(
            groundCheck.position, 
            groundCheckSize, 
            0f, 
            groundLayer
        );

        // Reset jump count saat mendarat
        if (isGrounded && !wasGrounded)
        {
            jumpCount = 0;
            lastGroundedTime = Time.time;
            isCurrentlyTakingOff = false;
            // JUICE TIP: Trigger landing particle/SFX di sini
        }

        // Start coyote time saat baru meninggalkan ground
        if (!isGrounded && wasGrounded)
        {
            coyoteTimeCounter = coyoteTime;
        }
    }

    private void UpdateTimers()
    {
        // Countdown timers
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
        animator.SetFloat("Speed", Mathf.Abs(rb.linearVelocity.x));
        bool isCurrentFalling = rb.linearVelocity.y < -0.1f;
        animator.SetBool("IsFalling", isCurrentFalling);
    }
    #endregion 

    #region Debug & Gizmos

    // Visualisasi ground check di Scene view
    private void OnDrawGizmosSelected()
    {
        if (groundCheck == null) return;

        Gizmos.color = isGrounded ? Color.green : Color.red;
        Gizmos.DrawWireCube(groundCheck.position, groundCheckSize);
    }

    #endregion
}