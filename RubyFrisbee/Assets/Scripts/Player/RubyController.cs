using RubyFrisbee.Core;
using RubyFrisbee.Input;
using UnityEngine;

namespace RubyFrisbee.Player
{
    /// <summary>
    /// Side-view movement and jump controls for Ruby.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class RubyController : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField] float moveSpeed = 8f;
        [SerializeField] float jumpForce = 14f;
        [SerializeField] float groundCheckRadius = 0.15f;
        [SerializeField] LayerMask groundLayer;

        [Header("Bounds")]
        [SerializeField] float minX = -8f;
        [SerializeField] float maxX = 8f;

        Rigidbody2D rb;
        Transform groundCheck;
        SpriteRenderer spriteRenderer;
        bool isGrounded;
        float horizontalInput;

        public bool IsGrounded => isGrounded;
        public bool IsFacingRight { get; private set; } = true;

        void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            spriteRenderer = GetComponent<SpriteRenderer>();

            var checkObj = new GameObject("GroundCheck");
            checkObj.transform.SetParent(transform);
            checkObj.transform.localPosition = new Vector3(0f, -0.45f, 0f);
            groundCheck = checkObj.transform;
        }

        void Update()
        {
            if (GameManager.Instance != null && GameManager.Instance.State != GameState.Playing)
            {
                horizontalInput = 0f;
                return;
            }

            PlayerInputProvider.Refresh();
            horizontalInput = PlayerInputProvider.Horizontal;

            if (PlayerInputProvider.JumpPressed)
            {
                TryJump();
            }

            UpdateFacing();
        }

        void FixedUpdate()
        {
            isGrounded = Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer);

            float targetVelocityX = horizontalInput * moveSpeed;
            rb.linearVelocity = new Vector2(targetVelocityX, rb.linearVelocity.y);

            Vector2 clampedPosition = rb.position;
            clampedPosition.x = Mathf.Clamp(clampedPosition.x, minX, maxX);
            rb.position = clampedPosition;
        }

        void TryJump()
        {
            if (!isGrounded)
            {
                return;
            }

            rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
        }

        void UpdateFacing()
        {
            if (horizontalInput > 0.01f)
            {
                IsFacingRight = true;
            }
            else if (horizontalInput < -0.01f)
            {
                IsFacingRight = false;
            }

            if (spriteRenderer != null)
            {
                spriteRenderer.flipX = !IsFacingRight;
            }
        }

        void OnDrawGizmosSelected()
        {
            if (groundCheck == null)
            {
                return;
            }

            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
        }
    }
}
