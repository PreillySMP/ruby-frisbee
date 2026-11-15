using RubyFrisbee.Core;
using UnityEngine;

namespace RubyFrisbee.Gameplay
{
    public enum SpawnSideMode
    {
        BothSides,
        RightOnly,
        LeftOnly,
        Alternating
    }

    /// <summary>
    /// Spawns frisbees (and bonus tennis balls) from either side of the screen
    /// aimed at varied landing locations across the entire yard using ballistic arcs.
    /// </summary>
    public class FrisbeeSpawner : MonoBehaviour
    {
        [Header("Prefabs & Sprites")]
        [SerializeField] Frisbee frisbeePrefab;
        [SerializeField] Sprite bonusBallSprite;

        [Header("Spawn Timing")]
        [SerializeField] float initialSpawnInterval = 2.4f;
        [SerializeField] float minimumSpawnInterval = 0.95f;
        [SerializeField] float difficultyRampSeconds = 90f;

        [Header("Multi-Direction & Yard Targeting")]
        [SerializeField] bool useSmartYardTargeting = true;
        [SerializeField] SpawnSideMode spawnSideMode = SpawnSideMode.BothSides;
        [Range(0f, 1f)]
        [SerializeField] float leftSideSpawnChance = 0.5f;
        [SerializeField] float spawnEdgeX = 8.1f;
        [SerializeField] Vector2 spawnHeightRange = new(-1.1f, 0.45f);
        [SerializeField] Vector2 landingTargetXRange = new(-6.0f, 6.0f);
        [SerializeField] Vector2 arcApexHeightRange = new(-0.35f, 1.15f);
        [SerializeField] float targetGroundY = -2.2f;
        [SerializeField] float minHorizontalTravel = 5.0f;

        [Header("Throw Variety")]
        [Range(0f, 0.5f)]
        [SerializeField] float bonusBallChance = 0.16f;
        [Range(0f, 0.4f)]
        [SerializeField] float doubleThrowChanceAtHighDifficulty = 0.15f;

        [Header("Legacy Fallback (used if Smart Targeting is disabled)")]
        [SerializeField] Vector2 spawnPosition = new(8.1f, 0.6f);
        [SerializeField] Vector2 spawnPositionVariance = new(0.5f, 1.2f);
        [SerializeField] Vector2 throwVelocity = new(-7.5f, 4.5f);
        [SerializeField] Vector2 throwVelocityVariance = new(2.5f, 2.0f);

        float spawnTimer;
        float elapsedTime;
        int totalThrowsInSession;
        int lastSideSign = 1;
        int sameSideStreak;
        int lastTargetZone = -1;
        float lastTargetX = 999f;
        int throwsSinceBonusBall = 99;

        void Start()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnStateChanged += HandleGameStateChanged;
            }
        }

        void OnDestroy()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnStateChanged -= HandleGameStateChanged;
            }
        }

        void HandleGameStateChanged(GameState state)
        {
            if (state == GameState.Playing)
            {
                elapsedTime = 0f;
                spawnTimer = 0.6f; // First throw arrives quickly when Play is clicked!
                totalThrowsInSession = 0;
                sameSideStreak = 0;
                lastTargetZone = -1;
                lastTargetX = 999f;
                throwsSinceBonusBall = 0;
            }
        }

        void Update()
        {
            if (GameManager.Instance == null || GameManager.Instance.State != GameState.Playing)
            {
                return;
            }

            elapsedTime += Time.deltaTime;
            spawnTimer -= Time.deltaTime;

            if (spawnTimer <= 0f)
            {
                SpawnFrisbee();
                spawnTimer = GetCurrentInterval();

                // At higher difficulty, occasionally toss a staggered second ring from the opposite side!
                float difficulty01 = Mathf.Clamp01(elapsedTime / difficultyRampSeconds);
                if (difficulty01 > 0.35f && Random.value < doubleThrowChanceAtHighDifficulty * difficulty01)
                {
                    Invoke(nameof(SpawnOppositeFollowUp), 0.55f);
                }
            }
        }

        void SpawnOppositeFollowUp()
        {
            if (GameManager.Instance == null || GameManager.Instance.State != GameState.Playing)
            {
                return;
            }

            SpawnFrisbee(forceSideSign: -lastSideSign);
        }

        float GetCurrentInterval()
        {
            float t = Mathf.Clamp01(elapsedTime / difficultyRampSeconds);
            return Mathf.Lerp(initialSpawnInterval, minimumSpawnInterval, t);
        }

        void SpawnFrisbee(int forceSideSign = 0)
        {
            if (frisbeePrefab == null)
            {
                Debug.LogWarning("FrisbeeSpawner is missing a frisbee prefab reference.");
                return;
            }

            totalThrowsInSession++;
            throwsSinceBonusBall++;

            if (!useSmartYardTargeting)
            {
                SpawnLegacy();
                return;
            }

            int sideSign = forceSideSign != 0 ? forceSideSign : ChooseSpawnSideSign();
            ThrowType throwType = ChooseThrowType();

            float spawnX = sideSign * Mathf.Abs(spawnEdgeX);
            float spawnY = ChooseSpawnY(throwType);
            Vector2 startPos = new(spawnX, spawnY);

            float targetX = ChooseLandingTargetX(sideSign, spawnX);
            float gravityScale = GetGravityScaleForType(throwType);
            float glideLift = throwType == ThrowType.FloatyGlide ? 2.4f : 0f;
            float apexY = ChooseApexY(throwType, spawnY);

            Vector2 launchVelocity = CalculateBallisticVelocity(
                startPos,
                new Vector2(targetX, targetGroundY),
                apexY,
                gravityScale,
                glideLift);

            Sprite overrideSprite = throwType == ThrowType.BonusTennisBall ? bonusBallSprite : null;
            Color tint = GetTintForType(throwType);

            Frisbee frisbee = Instantiate(frisbeePrefab, startPos, Quaternion.identity);
            frisbee.Launch(launchVelocity, throwType, targetX, targetGroundY, gravityScale, glideLift, overrideSprite, tint);
        }

        int ChooseSpawnSideSign()
        {
            int chosenSign;
            switch (spawnSideMode)
            {
                case SpawnSideMode.RightOnly:
                    chosenSign = 1;
                    break;
                case SpawnSideMode.LeftOnly:
                    chosenSign = -1;
                    break;
                case SpawnSideMode.Alternating:
                    chosenSign = -lastSideSign;
                    break;
                case SpawnSideMode.BothSides:
                default:
                    if (sameSideStreak >= 2)
                    {
                        chosenSign = -lastSideSign;
                    }
                    else
                    {
                        chosenSign = Random.value < leftSideSpawnChance ? -1 : 1;
                    }
                    break;
            }

            if (chosenSign == lastSideSign)
            {
                sameSideStreak++;
            }
            else
            {
                sameSideStreak = 1;
                lastSideSign = chosenSign;
            }

            return chosenSign;
        }

        ThrowType ChooseThrowType()
        {
            if (totalThrowsInSession == 1)
            {
                return ThrowType.StandardArc;
            }

            if (throwsSinceBonusBall >= 4 && Random.value < bonusBallChance)
            {
                throwsSinceBonusBall = 0;
                return ThrowType.BonusTennisBall;
            }

            float roll = Random.value;
            if (roll < 0.42f)
            {
                return ThrowType.StandardArc;
            }
            if (roll < 0.66f)
            {
                return ThrowType.HighLob;
            }
            if (roll < 0.84f)
            {
                return ThrowType.FloatyGlide;
            }
            return ThrowType.FastZipper;
        }

        float ChooseSpawnY(ThrowType type)
        {
            return type switch
            {
                ThrowType.FastZipper => Random.Range(-1.15f, -0.35f),
                ThrowType.HighLob => Random.Range(-0.3f, spawnHeightRange.y),
                ThrowType.FloatyGlide => Random.Range(-0.4f, spawnHeightRange.y),
                _ => Random.Range(spawnHeightRange.x, spawnHeightRange.y)
            };
        }

        float ChooseApexY(ThrowType type, float spawnY)
        {
            float rawApex = type switch
            {
                ThrowType.HighLob => Random.Range(0.45f, arcApexHeightRange.y),
                ThrowType.FastZipper => Random.Range(-0.55f, 0.05f),
                ThrowType.FloatyGlide => Random.Range(0.05f, 0.75f),
                ThrowType.BonusTennisBall => Random.Range(0.15f, 0.85f),
                _ => Random.Range(arcApexHeightRange.x, 0.55f)
            };

            return Mathf.Max(spawnY + 0.22f, targetGroundY + 0.65f, rawApex);
        }

        static float GetGravityScaleForType(ThrowType type)
        {
            return type switch
            {
                ThrowType.HighLob => 0.46f,
                ThrowType.FastZipper => 0.34f,
                ThrowType.FloatyGlide => 0.48f,
                ThrowType.BonusTennisBall => 0.68f,
                _ => 0.52f
            };
        }

        static Color GetTintForType(ThrowType type)
        {
            return type switch
            {
                ThrowType.HighLob => new Color(1f, 0.92f, 0.55f, 1f),
                ThrowType.FastZipper => new Color(0.75f, 0.96f, 1f, 1f),
                ThrowType.FloatyGlide => new Color(1f, 0.82f, 0.95f, 1f),
                _ => Color.white
            };
        }

        float ChooseLandingTargetX(int sideSign, float spawnX)
        {
            float minX = Mathf.Min(landingTargetXRange.x, landingTargetXRange.y);
            float maxX = Mathf.Max(landingTargetXRange.x, landingTargetXRange.y);

            // Prevent throws from dropping right at the spawn edge while still covering the whole yard
            float effectiveMinX = sideSign < 0 ? Mathf.Max(minX, spawnX + 3.2f) : minX;
            float effectiveMaxX = sideSign > 0 ? Mathf.Min(maxX, spawnX - 3.2f) : maxX;
            float span = Mathf.Max(1f, effectiveMaxX - effectiveMinX);

            // Divide the valid landing area into 3 zones (Left, Center, Right) and rotate zones
            int zone = Random.Range(0, 3);
            if (zone == lastTargetZone)
            {
                zone = (zone + Random.Range(1, 3)) % 3;
            }
            lastTargetZone = zone;

            float zoneWidth = span / 3f;
            float zoneMin = effectiveMinX + zone * zoneWidth;
            float zoneMax = zoneMin + zoneWidth;

            float candidateX = Random.Range(zoneMin, zoneMax);

            // Ensure consecutive throws never land near the same X coordinate
            if (Mathf.Abs(candidateX - lastTargetX) < 1.8f)
            {
                float shifted = candidateX + (candidateX > 0f ? -Random.Range(2.2f, 3.8f) : Random.Range(2.2f, 3.8f));
                candidateX = Mathf.Clamp(shifted, effectiveMinX, effectiveMaxX);
            }

            lastTargetX = candidateX;
            return candidateX;
        }

        static Vector2 CalculateBallisticVelocity(
            Vector2 start,
            Vector2 target,
            float apexY,
            float gravityScale,
            float upwardGlideAccel)
        {
            float gravity = Mathf.Abs(Physics2D.gravity.y) * gravityScale - upwardGlideAccel;
            gravity = Mathf.Max(1.2f, gravity);

            float peakY = Mathf.Max(start.y + 0.25f, target.y + 0.5f, apexY);
            float riseHeight = Mathf.Max(0.1f, peakY - start.y);
            float fallHeight = Mathf.Max(0.1f, peakY - target.y);

            float vy = Mathf.Sqrt(2f * gravity * riseHeight);
            float timeUp = vy / gravity;
            float timeDown = Mathf.Sqrt(2f * fallHeight / gravity);
            float totalFlightTime = Mathf.Max(0.35f, timeUp + timeDown);

            float vx = (target.x - start.x) / totalFlightTime;
            return new Vector2(vx, vy);
        }

        void SpawnLegacy()
        {
            Vector2 position = spawnPosition + new Vector2(
                Random.Range(-spawnPositionVariance.x, spawnPositionVariance.x),
                Random.Range(-spawnPositionVariance.y, spawnPositionVariance.y));

            Vector2 velocity = throwVelocity + new Vector2(
                Random.Range(-throwVelocityVariance.x, throwVelocityVariance.x),
                Random.Range(-throwVelocityVariance.y, throwVelocityVariance.y));

            Frisbee frisbee = Instantiate(frisbeePrefab, position, Quaternion.identity);
            frisbee.Launch(velocity);
        }

        public void SetPrefab(Frisbee prefab)
        {
            frisbeePrefab = prefab;
        }

        public void SetBonusBallSprite(Sprite sprite)
        {
            bonusBallSprite = sprite;
        }

        void OnDrawGizmosSelected()
        {
            // Draw left and right spawn ranges
            Gizmos.color = new Color(1f, 0.5f, 0.1f, 0.85f);
            Vector3 rightMin = new(spawnEdgeX, spawnHeightRange.x, 0f);
            Vector3 rightMax = new(spawnEdgeX, spawnHeightRange.y, 0f);
            Gizmos.DrawLine(rightMin, rightMax);
            Gizmos.DrawWireSphere(rightMin, 0.2f);
            Gizmos.DrawWireSphere(rightMax, 0.2f);

            if (spawnSideMode == SpawnSideMode.BothSides || spawnSideMode == SpawnSideMode.LeftOnly || spawnSideMode == SpawnSideMode.Alternating)
            {
                Vector3 leftMin = new(-spawnEdgeX, spawnHeightRange.x, 0f);
                Vector3 leftMax = new(-spawnEdgeX, spawnHeightRange.y, 0f);
                Gizmos.DrawLine(leftMin, leftMax);
                Gizmos.DrawWireSphere(leftMin, 0.2f);
                Gizmos.DrawWireSphere(leftMax, 0.2f);
            }

            // Draw yard landing target range
            Gizmos.color = new Color(0.2f, 1f, 0.4f, 0.9f);
            Vector3 targetLeft = new(landingTargetXRange.x, targetGroundY, 0f);
            Vector3 targetRight = new(landingTargetXRange.y, targetGroundY, 0f);
            Gizmos.DrawLine(targetLeft, targetRight);
            Gizmos.DrawWireSphere(targetLeft, 0.22f);
            Gizmos.DrawWireSphere(targetRight, 0.22f);
        }
    }
}
