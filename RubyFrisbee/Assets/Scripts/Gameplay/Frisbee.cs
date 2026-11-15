using RubyFrisbee.Core;
using RubyFrisbee.Player;
using UnityEngine;

namespace RubyFrisbee.Gameplay
{
    public enum ThrowType
    {
        StandardArc,
        HighLob,
        FastZipper,
        FloatyGlide,
        BonusTennisBall
    }

    /// <summary>
    /// Flying disc (or bonus tennis ball) with arc physics, aerodynamic glide,
    /// directional spin, ground landing indicator, and catch/miss reporting.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(CircleCollider2D))]
    public class Frisbee : MonoBehaviour
    {
        [SerializeField] float spinSpeed = 420f;
        [SerializeField] float groundY = -2.25f;

        Rigidbody2D rb;
        SpriteRenderer spriteRenderer;
        bool resolved;
        float spinDirection = 1f;
        float glideLift;
        ThrowType throwType = ThrowType.StandardArc;
        int bouncesRemaining;
        float predictedTargetX;
        bool hasTargetIndicator;
        GameObject targetMarker;
        SpriteRenderer targetMarkerRenderer;
        Transform streamRig;
        TrailRenderer[] windStreams;
        float[] streamBaseOffsetsY;
        static Sprite cachedMarkerSprite;
        static Material cachedTrailMaterial;

        public ThrowType CurrentThrowType => throwType;

        public void Launch(Vector2 velocity)
        {
            Launch(
                velocity,
                ThrowType.StandardArc,
                transform.position.x + velocity.x * 0.9f,
                groundY,
                0.55f,
                0f,
                null,
                Color.white);
        }

        public void Launch(
            Vector2 velocity,
            ThrowType type,
            float targetX,
            float targetGroundY,
            float customGravityScale,
            float liftAccel,
            Sprite customSprite,
            Color tint)
        {
            rb = GetComponent<Rigidbody2D>();
            spriteRenderer = GetComponent<SpriteRenderer>();

            throwType = type;
            groundY = targetGroundY;
            glideLift = liftAccel;
            predictedTargetX = targetX;
            bouncesRemaining = type == ThrowType.BonusTennisBall ? 1 : 0;

            rb.gravityScale = customGravityScale;
            rb.linearVelocity = velocity;
            spinDirection = velocity.x <= 0f ? 1f : -1f;
            resolved = false;

            if (spriteRenderer != null)
            {
                if (customSprite != null)
                {
                    spriteRenderer.sprite = customSprite;
                    float maxDim = Mathf.Max(customSprite.bounds.size.x, customSprite.bounds.size.y);
                    if (maxDim > 0.01f)
                    {
                        float desiredWorldSize = type == ThrowType.BonusTennisBall ? 0.52f : 0.92f;
                        transform.localScale = Vector3.one * (desiredWorldSize / maxDim);
                    }
                }

                spriteRenderer.color = tint;
            }

            CreateTargetMarker(targetX, targetGroundY, type);
            CreateFlowingWindStreams(velocity, type);
            RubyStage.Instance?.NotifyThrow();
        }

        void FixedUpdate()
        {
            if (resolved || rb == null)
            {
                return;
            }

            if (glideLift > 0.001f && Mathf.Abs(rb.linearVelocity.x) > 1f)
            {
                rb.AddForce(Vector2.up * glideLift, ForceMode2D.Force);
            }
        }

        void Update()
        {
            if (GameManager.Instance != null && GameManager.Instance.State != GameState.Playing)
            {
                Destroy(gameObject);
                return;
            }

            float spinMultiplier = throwType == ThrowType.FastZipper ? 1.45f : 1f;
            transform.Rotate(0f, 0f, spinDirection * spinSpeed * spinMultiplier * Time.deltaTime);

            if (resolved)
            {
                return;
            }

            UpdateTargetMarker();

            if (transform.position.y <= groundY)
            {
                if (bouncesRemaining > 0 && rb != null)
                {
                    bouncesRemaining--;
                    transform.position = new Vector3(transform.position.x, groundY + 0.05f, transform.position.z);
                    rb.linearVelocity = new Vector2(rb.linearVelocity.x * 0.78f, 6.2f);
                    predictedTargetX = transform.position.x + rb.linearVelocity.x * 0.95f;
                    if (targetMarker != null)
                    {
                        targetMarker.transform.position = new Vector3(predictedTargetX, groundY - 0.02f, 0f);
                    }
                    RubyStage.Instance?.NotifyBounce(transform.position);
                    return;
                }

                ResolveMiss();
            }

            if (Mathf.Abs(transform.position.x) > 16f)
            {
                resolved = true;
                Destroy(gameObject);
            }
        }

        void LateUpdate()
        {
            if (resolved)
            {
                return;
            }

            UpdateFlowingWindStreams();
        }

        void CreateFlowingWindStreams(Vector2 initialVelocity, ThrowType type)
        {
            var rigGo = new GameObject("FlightStreamRig");
            streamRig = rigGo.transform;
            streamRig.SetParent(transform, false);
            streamRig.localPosition = Vector3.zero;

            // Keep streamRig at 1:1 world scale regardless of the Frisbee sprite scale
            Vector3 parentScale = transform.localScale;
            streamRig.localScale = new Vector3(
                Mathf.Abs(parentScale.x) > 0.001f ? 1f / parentScale.x : 1f,
                Mathf.Abs(parentScale.y) > 0.001f ? 1f / parentScale.y : 1f,
                1f);

            if (initialVelocity.sqrMagnitude > 0.01f)
            {
                float angle = Mathf.Atan2(initialVelocity.y, initialVelocity.x) * Mathf.Rad2Deg;
                streamRig.rotation = Quaternion.Euler(0f, 0f, angle);
            }

            if (cachedTrailMaterial == null)
            {
                Shader shader = Shader.Find("Sprites/Default");
                if (shader != null)
                {
                    cachedTrailMaterial = new Material(shader);
                }
            }

            Color streamTint = type switch
            {
                ThrowType.BonusTennisBall => new Color(0.75f, 1f, 0.45f, 1f),
                ThrowType.HighLob => new Color(1f, 0.95f, 0.65f, 1f),
                ThrowType.FastZipper => new Color(0.65f, 0.95f, 1f, 1f),
                _ => new Color(0.95f, 0.98f, 1f, 1f)
            };

            streamBaseOffsetsY = new[] { 0.16f, 0f, -0.16f };
            float[] streamOffsetsX = { -0.18f, -0.26f, -0.18f };
            float[] startWidths = { 0.036f, 0.052f, 0.036f };
            float[] durations = { 0.24f, 0.32f, 0.24f };

            windStreams = new TrailRenderer[3];
            for (int i = 0; i < 3; i++)
            {
                var emitterGo = new GameObject($"WindStream_{i}");
                emitterGo.transform.SetParent(streamRig, false);
                emitterGo.transform.localPosition = new Vector3(streamOffsetsX[i], streamBaseOffsetsY[i], 0f);

                var trail = emitterGo.AddComponent<TrailRenderer>();
                if (cachedTrailMaterial != null)
                {
                    trail.sharedMaterial = cachedTrailMaterial;
                }

                trail.sortingLayerName = "RF_Effects";
                trail.sortingOrder = 5;
                trail.time = durations[i];
                trail.minVertexDistance = 0.05f;
                trail.numCornerVertices = 4;
                trail.numCapVertices = 4;
                trail.autodestruct = false;
                trail.emitting = true;

                var widthCurve = new AnimationCurve(
                    new Keyframe(0f, startWidths[i]),
                    new Keyframe(0.35f, startWidths[i] * 0.7f),
                    new Keyframe(1f, 0f));
                trail.widthCurve = widthCurve;

                float headAlpha = i == 1 ? 0.52f : 0.36f;
                var gradient = new Gradient();
                gradient.SetKeys(
                    new[]
                    {
                        new GradientColorKey(streamTint, 0f),
                        new GradientColorKey(Color.white, 1f)
                    },
                    new[]
                    {
                        new GradientAlphaKey(headAlpha, 0f),
                        new GradientAlphaKey(headAlpha * 0.45f, 0.5f),
                        new GradientAlphaKey(0f, 1f)
                    });
                trail.colorGradient = gradient;

                windStreams[i] = trail;
            }
        }

        void UpdateFlowingWindStreams()
        {
            if (streamRig == null || rb == null)
            {
                return;
            }

            Vector2 v = rb.linearVelocity;
            float speed = v.magnitude;

            // Lock world rotation to the flight velocity vector so the trail NEVER spins with the disc!
            if (speed > 0.15f)
            {
                float angle = Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg;
                streamRig.rotation = Quaternion.Euler(0f, 0f, angle);
            }

            // Stretch the animated sprite trail slightly based on current speed & add gentle airflow wave to ribbons
            float speedScale = Mathf.Clamp(speed / 7.5f, 0.55f, 1.25f);
            Vector3 parentScale = transform.localScale;
            float invX = Mathf.Abs(parentScale.x) > 0.001f ? 1f / parentScale.x : 1f;
            float invY = Mathf.Abs(parentScale.y) > 0.001f ? 1f / parentScale.y : 1f;
            streamRig.localScale = new Vector3(invX * speedScale, invY, 1f);

            if (windStreams != null && streamBaseOffsetsY != null)
            {
                float waveTime = Time.time * 16f;
                for (int i = 0; i < windStreams.Length; i++)
                {
                    if (windStreams[i] == null)
                    {
                        continue;
                    }

                    float waveOffset = Mathf.Sin(waveTime + i * 1.9f) * 0.035f;
                    Vector3 localPos = windStreams[i].transform.localPosition;
                    localPos.y = streamBaseOffsetsY[i] + waveOffset;
                    windStreams[i].transform.localPosition = localPos;
                }
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
            bool apexCatch = airCatch && transform.position.y >= -0.75f;
            bool divingCatch = ruby.IsDashing;
            bool isBonusBall = throwType == ThrowType.BonusTennisBall;
            Vector3 catchPos = ruby.MouthWorldPosition;

            GameManager.Instance?.RegisterCatch(airCatch, apexCatch, divingCatch, isBonusBall, catchPos);
            RubyStage.Instance?.NotifyCatch(airCatch, apexCatch, divingCatch, isBonusBall, catchPos);
            resolved = true;
            Destroy(gameObject);
        }

        void ResolveMiss()
        {
            resolved = true;

            // Missing a bonus tennis ball does not penalize lives or break combo!
            if (throwType == ThrowType.BonusTennisBall)
            {
                RubyStage.Instance?.NotifyBounce(transform.position);
                Destroy(gameObject);
                return;
            }

            GameManager.Instance?.RegisterMiss();
            RubyStage.Instance?.NotifyMiss(transform.position);
            Destroy(gameObject);
        }

        void CreateTargetMarker(float targetX, float targetGroundY, ThrowType type)
        {
            if (cachedMarkerSprite == null)
            {
                cachedMarkerSprite = BuildSoftDiscSprite();
            }

            targetMarker = new GameObject("LandingTargetMarker");
            targetMarker.transform.position = new Vector3(Mathf.Clamp(targetX, -6.5f, 6.5f), targetGroundY - 0.04f, 0f);
            targetMarker.transform.localScale = new Vector3(1.1f, 0.28f, 1f);

            targetMarkerRenderer = targetMarker.AddComponent<SpriteRenderer>();
            targetMarkerRenderer.sprite = cachedMarkerSprite;
            targetMarkerRenderer.sortingLayerName = "RF_Props";
            targetMarkerRenderer.sortingOrder = 2;

            Color markerColor = type switch
            {
                ThrowType.BonusTennisBall => new Color(0.55f, 1f, 0.25f, 0.45f),
                ThrowType.HighLob => new Color(1f, 0.85f, 0.2f, 0.45f),
                ThrowType.FastZipper => new Color(0.3f, 0.9f, 1f, 0.45f),
                _ => new Color(1f, 1f, 1f, 0.35f)
            };
            targetMarkerRenderer.color = markerColor;
            hasTargetIndicator = true;
        }

        void UpdateTargetMarker()
        {
            if (!hasTargetIndicator || targetMarker == null)
            {
                return;
            }

            float heightAboveGround = Mathf.Max(0f, transform.position.y - groundY);
            float normalizedHeight = Mathf.Clamp01(heightAboveGround / 4.5f);
            float pulse = 1f + Mathf.Sin(Time.time * 10f) * 0.08f;
            float width = Mathf.Lerp(0.65f, 1.35f, normalizedHeight) * pulse;
            targetMarker.transform.localScale = new Vector3(width, 0.24f * pulse, 1f);

            if (targetMarkerRenderer != null)
            {
                Color c = targetMarkerRenderer.color;
                c.a = Mathf.Lerp(0.62f, 0.22f, normalizedHeight);
                targetMarkerRenderer.color = c;
            }
        }

        static Sprite BuildSoftDiscSprite()
        {
            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            float center = (size - 1) * 0.5f;
            float maxR = size * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - center;
                    float dy = y - center;
                    float dist01 = Mathf.Sqrt(dx * dx + dy * dy) / maxR;
                    if (dist01 > 1f)
                    {
                        tex.SetPixel(x, y, Color.clear);
                    }
                    else
                    {
                        // Ring + soft inner shadow
                        float ring = dist01 > 0.72f ? Mathf.SmoothStep(1f, 0.2f, (dist01 - 0.72f) / 0.28f) : 0.35f;
                        tex.SetPixel(x, y, new Color(1f, 1f, 1f, ring));
                    }
                }
            }

            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 64f);
        }

        void OnDestroy()
        {
            if (targetMarker != null)
            {
                Destroy(targetMarker);
            }
        }
    }
}
