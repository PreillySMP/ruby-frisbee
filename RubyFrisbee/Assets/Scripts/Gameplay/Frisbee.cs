using RubyFrisbee.Core;
using RubyFrisbee.Player;
using UnityEngine;

namespace RubyFrisbee.Gameplay
{
    /// <summary>
    /// Flying disc with arc physics. Reports catch or miss back to the game systems.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(CircleCollider2D))]
    public class Frisbee : MonoBehaviour
    {
        [SerializeField] float spinSpeed = 360f;
        [SerializeField] float groundY = -2.5f;

        Rigidbody2D rb;
        bool resolved;

        public void Launch(Vector2 velocity)
        {
            rb = GetComponent<Rigidbody2D>();
            rb.linearVelocity = velocity;
            resolved = false;
        }

        void Update()
        {
            transform.Rotate(0f, 0f, spinSpeed * Time.deltaTime);

            if (resolved)
            {
                return;
            }

            if (transform.position.y <= groundY)
            {
                ResolveMiss();
            }
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            if (resolved)
            {
                return;
            }

            if (!other.TryGetComponent<RubyController>(out var ruby))
            {
                return;
            }

            bool airCatch = !ruby.IsGrounded;
            GameManager.Instance?.RegisterCatch(airCatch);
            resolved = true;
            Destroy(gameObject);
        }

        void ResolveMiss()
        {
            resolved = true;
            GameManager.Instance?.RegisterMiss();
            Destroy(gameObject);
        }
    }
}
