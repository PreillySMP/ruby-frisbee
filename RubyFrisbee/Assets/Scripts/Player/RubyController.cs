using RubyFrisbee.Core;
using RubyFrisbee.Input;
using UnityEngine;

namespace RubyFrisbee.Player
{
    /// <summary>
    /// Side-view movement, jump (with coyote time + jump buffer), and zoomie dash controls for Ruby.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class RubyController : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField] float moveSpeed = 5.2f;
        [SerializeField] float groundAcceleration = 38f;
        [SerializeField] float jumpForce = 7.1f;
        [SerializeField] float groundCheckRadius = 0.22f;
        [SerializeField] LayerMask groundLayer;

        [Header("Jump Feel")]
        [SerializeField] float baseGravityScale = 2.15f;
        [SerializeField] float coyoteTime = 0.12f;
        [SerializeField] float jumpBufferTime = 0.12f;
        [SerializeField] float fallGravityMultiplier = 1.28f;
        [SerializeField] float jumpWindupDuration = 0.055f;

        [Header("Zoomie Dash / Lunge")]
        [SerializeField] float dashSpeed = 8.8f;
        [SerializeField] float dashDuration = 0.18f;
        [SerializeField] float dashCooldown = 0.85f;
        [SerializeField] float dashHopBoost = 1.8f;

        [Header("Bounds")]
        [SerializeField] float minX = -6.6f;
        [SerializeField] float maxX = 6.6f;

        Rigidbody2D rb;
        Transform groundCheck;
        SpriteRenderer spriteRenderer;
        CircleCollider2D catchZone;
        Vector2 catchOffset;
        bool isGrounded;
        float horizontalInput;
        float coyoteTimer;
        float jumpBufferTimer;
        float jumpWindupTimer;
        float jumpTakeoffGraceTimer;
        float lastJumpLaunchSpeed = 7.1f;
        float dashTimer;
        float dashCooldownTimer;
        float dashDirection = 1f;

        // Speed boost support (e.g. Water Bowl Zoomies or Happy Treat Boost!)
        float zoomiesTimer;
        bool queuedHappyStartBoost;
        const float ZoomiesSpeedMultiplier = 1.15f;
        const float ZoomiesJumpMultiplier = 1.06f;

        // Autonomous movement on Menu / Game Over when chasing a tossed treat or ball
        bool hasAutonomousTarget;
        float autonomousTargetX = -3.2f;
        bool autonomousShouldJump;
        float returnHomeTimer;
        const float HomeMenuX = -3.2f;

        public bool IsGrounded => isGrounded;
        public bool IsFacingRight { get; private set; } = true;
        public bool IsDashing => dashTimer > 0f;
        public bool HasZoomies => zoomiesTimer > 0f;
        public bool IsInJumpWindup => jumpWindupTimer > 0f;
        public float JumpWindupProgress => jumpWindupDuration > 0.001f
            ? Mathf.Clamp01(1f - jumpWindupTimer / jumpWindupDuration)
            : 1f;
        public float ActiveJumpLaunchSpeed => Mathf.Max(3f, lastJumpLaunchSpeed);
        public Vector3 MouthWorldPosition => catchZone != null
            ? transform.TransformPoint(catchZone.offset)
            : transform.position + new Vector3(IsFacingRight ? 0.5f : -0.5f, 0.3f, 0f);

        public event System.Action Jumped;
        public event System.Action Dashed;

        void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                rb.gravityScale = baseGravityScale;
            }
            spriteRenderer = GetComponent<SpriteRenderer>();
            catchZone = GetComponent<CircleCollider2D>();
            if (catchZone != null)
            {
                catchOffset = catchZone.offset;
            }

            var existingCheck = transform.Find("GroundCheck");
            if (existingCheck != null)
            {
                groundCheck = existingCheck;
            }
            else
            {
                var checkObj = new GameObject("GroundCheck");
                checkObj.transform.SetParent(transform, false);
                checkObj.transform.localPosition = new Vector3(0f, -0.45f, 0f);
                groundCheck = checkObj.transform;
            }
        }

        void Update()
        {
            if (GameManager.Instance != null && GameManager.Instance.State != GameState.Playing)
            {
                UpdateAutonomousMenuBehavior();
                UpdateFacing();
                return;
            }

            if (queuedHappyStartBoost)
            {
                queuedHappyStartBoost = false;
                ActivateZoomies(7f);
            }

            PlayerInputProvider.Refresh();
            horizontalInput = PlayerInputProvider.Horizontal;

            if (zoomiesTimer > 0f)
            {
                zoomiesTimer -= Time.deltaTime;
            }

            if (dashCooldownTimer > 0f)
            {
                dashCooldownTimer -= Time.deltaTime;
            }

            if (dashTimer > 0f)
            {
                dashTimer -= Time.deltaTime;
            }

            if (jumpTakeoffGraceTimer > 0f)
            {
                jumpTakeoffGraceTimer -= Time.deltaTime;
            }

            if (isGrounded)
            {
                coyoteTimer = coyoteTime;
            }
            else
            {
                coyoteTimer -= Time.deltaTime;
            }

            if (PlayerInputProvider.JumpPressed)
            {
                jumpBufferTimer = jumpBufferTime;
            }
            else
            {
                jumpBufferTimer -= Time.deltaTime;
            }

            if (jumpWindupTimer > 0f)
            {
                jumpWindupTimer -= Time.deltaTime;
                if (jumpWindupTimer <= 0f)
                {
                    LaunchJumpAfterWindup();
                }
            }
            else if (jumpBufferTimer > 0f && coyoteTimer > 0f)
            {
                ExecuteJump();
            }

            if (PlayerInputProvider.DashPressed && dashCooldownTimer <= 0f)
            {
                ExecuteDash();
            }

            UpdateFacing();
        }

        void FixedUpdate()
        {
            float velocityY = rb.linearVelocity.y;

            if (jumpTakeoffGraceTimer > 0f && velocityY > 0.1f)
            {
                isGrounded = false;
            }
            else
            {
                isGrounded = Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer);
            }

            float speedMultiplier = HasZoomies ? ZoomiesSpeedMultiplier : 1f;
            float desiredVelocityX = dashTimer > 0f
                ? dashDirection * dashSpeed * speedMultiplier
                : horizontalInput * moveSpeed * speedMultiplier;

            float newVelocityX = dashTimer > 0f
                ? desiredVelocityX
                : Mathf.MoveTowards(rb.linearVelocity.x, desiredVelocityX, groundAcceleration * Time.fixedDeltaTime);

            // Lifelike canine gravity: heavier base gravity + slightly faster descent
            if (!isGrounded && velocityY < -0.1f)
            {
                rb.gravityScale = baseGravityScale * fallGravityMultiplier;
            }
            else
            {
                rb.gravityScale = baseGravityScale;
            }

            rb.linearVelocity = new Vector2(newVelocityX, velocityY);

            Vector2 clampedPosition = rb.position;
            clampedPosition.x = Mathf.Clamp(clampedPosition.x, minX, maxX);
            rb.position = clampedPosition;
        }

        void ExecuteJump()
        {
            jumpBufferTimer = 0f;
            coyoteTimer = 0f;

            if (jumpWindupDuration > 0.005f)
            {
                jumpWindupTimer = jumpWindupDuration;
            }
            else
            {
                LaunchJumpAfterWindup();
            }
        }

        void LaunchJumpAfterWindup()
        {
            jumpWindupTimer = 0f;
            coyoteTimer = 0f;
            jumpTakeoffGraceTimer = 0.12f;
            isGrounded = false;

            float jumpMultiplier = HasZoomies ? ZoomiesJumpMultiplier : 1f;
            lastJumpLaunchSpeed = jumpForce * jumpMultiplier;
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, lastJumpLaunchSpeed);
            Jumped?.Invoke();
        }

        void ExecuteDash()
        {
            dashTimer = dashDuration;
            dashCooldownTimer = dashCooldown;
            dashDirection = Mathf.Abs(horizontalInput) > 0.01f
                ? Mathf.Sign(horizontalInput)
                : (IsFacingRight ? 1f : -1f);

            IsFacingRight = dashDirection > 0f;

            if (isGrounded && rb.linearVelocity.y <= 0.5f)
            {
                rb.linearVelocity = new Vector2(dashDirection * dashSpeed, dashHopBoost);
            }

            Dashed?.Invoke();
        }

        public void ActivateZoomies(float duration = 6f)
        {
            zoomiesTimer = Mathf.Max(zoomiesTimer, duration);
        }

        public void QueueHappyStartBoost()
        {
            queuedHappyStartBoost = true;
        }

        public bool HasQueuedHappyBoost => queuedHappyStartBoost;

        public void PerformHappyHop(float hopVelocityY = 6.4f)
        {
            if (rb != null && isGrounded)
            {
                lastJumpLaunchSpeed = hopVelocityY;
                jumpTakeoffGraceTimer = 0.12f;
                rb.linearVelocity = new Vector2(rb.linearVelocity.x, hopVelocityY);
                isGrounded = false;
                Jumped?.Invoke();
            }
        }

        public void SetAutonomousTarget(float targetX, bool jumpWhenNear)
        {
            hasAutonomousTarget = true;
            autonomousTargetX = Mathf.Clamp(targetX, minX, -0.6f);
            autonomousShouldJump = jumpWhenNear;
            returnHomeTimer = 0f;
        }

        public void ReturnToMenuSpot(float delay = 0.8f)
        {
            hasAutonomousTarget = false;
            autonomousShouldJump = false;
            returnHomeTimer = delay;
        }

        void UpdateAutonomousMenuBehavior()
        {
            jumpBufferTimer = 0f;
            dashTimer = 0f;

            if (hasAutonomousTarget)
            {
                float dx = autonomousTargetX - transform.position.x;
                if (Mathf.Abs(dx) > 0.22f)
                {
                    horizontalInput = Mathf.Clamp(dx * 2.2f, -0.85f, 0.85f);
                }
                else
                {
                    horizontalInput = 0f;
                    if (autonomousShouldJump && isGrounded)
                    {
                        autonomousShouldJump = false;
                        PerformHappyHop(6.8f);
                    }
                }
                return;
            }

            if (returnHomeTimer > 0f)
            {
                returnHomeTimer -= Time.deltaTime;
                horizontalInput = 0f;
                return;
            }

            // Gently trot back to HomeMenuX (-3.2) on the left side of the yard so Ruby isn't hidden behind the menu card
            float homeDx = HomeMenuX - transform.position.x;
            if (Mathf.Abs(homeDx) > 0.35f)
            {
                horizontalInput = Mathf.Clamp(homeDx * 1.4f, -0.65f, 0.65f);
            }
            else
            {
                horizontalInput = 0f;
                IsFacingRight = true;
            }
        }

        void UpdateFacing()
        {
            if (dashTimer <= 0f)
            {
                if (horizontalInput > 0.01f)
                {
                    IsFacingRight = true;
                }
                else if (horizontalInput < -0.01f)
                {
                    IsFacingRight = false;
                }
            }

            if (spriteRenderer != null)
            {
                spriteRenderer.flipX = !IsFacingRight;
            }

            if (catchZone != null)
            {
                catchZone.offset = new Vector2(Mathf.Abs(catchOffset.x) * (IsFacingRight ? 1f : -1f), catchOffset.y);
            }
        }

        void OnDrawGizmosSelected()
        {
            if (groundCheck != null)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
            }

            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(new Vector3(minX, -3f, 0f), new Vector3(minX, 3f, 0f));
            Gizmos.DrawLine(new Vector3(maxX, -3f, 0f), new Vector3(maxX, 3f, 0f));
        }
    }
}
