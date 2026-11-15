using System.Collections;
using RubyFrisbee.Core;
using RubyFrisbee.Gameplay;
using RubysFrisbee.Assets;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;

namespace RubyFrisbee.Player
{
    /// <summary>
    /// Feeds the imported Ruby animation, VFX prefabs, floating popups, and audio from the game rules.
    /// </summary>
    public class RubyStage : MonoBehaviour
    {
        public static RubyStage Instance { get; private set; }

        [Header("Core References")]
        [SerializeField] RubyAnimationDriver visual;
        [SerializeField, FormerlySerializedAs("audio")] RubyAudioPlayer audioPlayer;

        [Header("VFX Prefabs")]
        [SerializeField] GameObject takeoffEffect;
        [SerializeField] GameObject landingEffect;
        [SerializeField] GameObject catchEffect;
        [SerializeField] GameObject airCatchEffect;
        [SerializeField] GameObject comboEffect;
        [SerializeField] GameObject missEffect;
        [SerializeField] GameObject ringTrailEffect;
        [SerializeField] Sprite tennisBallSprite;

        [Header("48-Frame Fluid Jump Sequence")]
        [SerializeField] Sprite[] jumpSprites;

        [Header("Ruby's Real Voice")]
        [SerializeField] AudioClip realRubyBarkClip;

        public event System.Action<string> OnMoodChanged;

        RubyController ruby;
        Rigidbody2D body;
        SpriteRenderer visualRenderer;
        Vector3 baseVisualLocalPos = new(0f, -0.45f, 0f);
        bool wasGrounded = true;
        float landingRecoveryTimer;
        float landingRecoveryDuration = 0.26f;
        float footstepTimer;
        bool waterBowlReady;
        float waterBowlSparkleTimer;
        int affectionCount;
        int treatsEatenCount;
        int barkCounter;
        bool treatInFlight;
        float lastPetTime = -10f;
        static Sprite cachedHeartSprite;
        static Sprite cachedBoneTreatSprite;

        static readonly string[] PetPhrases =
        {
            "GOOD GIRL RUBY!",
            "*HAPPY TAIL WAG*",
            "BELLY RUBS!",
            "SO FLUFFY!",
            "BEST SHEPHERD!"
        };

        void Awake()
        {
            Instance = this;
            ruby = GetComponent<RubyController>();
            body = GetComponent<Rigidbody2D>();
        }

        void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }

            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnStateChanged -= ApplyState;
            }

            if (ScoreManager.Instance != null)
            {
                ScoreManager.Instance.OnCatchScored -= HandleCatchScored;
                ScoreManager.Instance.OnNewHighScore -= HandleNewHighScore;
            }
        }

        void Start()
        {
            if (visual != null)
            {
                visualRenderer = visual.GetComponent<SpriteRenderer>();
                baseVisualLocalPos = visual.transform.localPosition;
            }

            if (ruby != null)
            {
                ruby.Jumped += HandleJump;
                ruby.Dashed += HandleDash;
            }

            audioPlayer?.PlayMusic();

            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnStateChanged += ApplyState;
                ApplyState(GameManager.Instance.State);
            }

            if (ScoreManager.Instance != null)
            {
                ScoreManager.Instance.OnCatchScored += HandleCatchScored;
                ScoreManager.Instance.OnNewHighScore += HandleNewHighScore;
            }
        }

        void LateUpdate()
        {
            if (visual == null || ruby == null || body == null)
            {
                return;
            }

            float absSpeedX = Mathf.Abs(body.linearVelocity.x);
            float velY = body.linearVelocity.y;
            visual.SetLocomotion(absSpeedX, velY, ruby.IsGrounded);
            visual.SetFacing(ruby.IsFacingRight);

            if (!wasGrounded && ruby.IsGrounded)
            {
                landingRecoveryDuration = absSpeedX > 1.5f ? 0.18f : 0.26f;
                landingRecoveryTimer = landingRecoveryDuration;

                if (GameManager.Instance != null && GameManager.Instance.State == GameState.Playing)
                {
                    audioPlayer?.PlayCue("land");
                    SpawnEffect(landingEffect, GetFeetPosition());
                }
            }

            if (GameManager.Instance != null && GameManager.Instance.State == GameState.Playing)
            {
                // Footsteps while running on the grass
                if (ruby.IsGrounded && absSpeedX > 1.5f)
                {
                    footstepTimer -= Time.deltaTime;
                    if (footstepTimer <= 0f)
                    {
                        audioPlayer?.PlayCue("footstep");
                        footstepTimer = ruby.HasZoomies ? 0.18f : 0.24f;
                    }
                }
                else
                {
                    footstepTimer = 0.06f;
                }

                UpdateWaterBowlZoomies();
            }
            else if (GameManager.Instance != null && GameManager.Instance.State != GameState.Playing)
            {
                CheckDirectPetInput();
            }

            if (UnityEngine.Input.GetKeyDown(KeyCode.B))
            {
                PlayBark(preferRealBark: true);
                SpawnFloatingText("WOOF!", ruby.MouthWorldPosition + Vector3.up * 0.4f, new Color(1f, 0.88f, 0.35f));
            }

            ApplyFluidJumpAnimation(absSpeedX, velY);
            wasGrounded = ruby.IsGrounded;
        }

        void ApplyFluidJumpAnimation(float absSpeedX, float verticalSpeed)
        {
            if (visual == null || visual.GameOverActive || jumpSprites == null || jumpSprites.Length < 48)
            {
                return;
            }

            if (visualRenderer == null)
            {
                visualRenderer = visual.GetComponent<SpriteRenderer>();
                if (visualRenderer == null)
                {
                    return;
                }
            }

            // 1. Takeoff crouch anticipation (frames 05..10)
            if (ruby.IsInJumpWindup)
            {
                float t = ruby.JumpWindupProgress;
                int idx = Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(5f, 10f, t)), 5, 10);
                visualRenderer.sprite = jumpSprites[idx];
                visual.transform.localPosition = baseVisualLocalPos + new Vector3(0f, -0.065f * t, 0f);
                return;
            }

            // 2. Airborne arc (frames 11..32 mapped smoothly from +launchSpeed through apex 0 to -launchSpeed)
            if (!ruby.IsGrounded)
            {
                landingRecoveryTimer = 0f;
                float v0 = ruby.ActiveJumpLaunchSpeed;
                float u = Mathf.Clamp01((v0 - verticalSpeed) / (2.08f * v0));
                int idx = Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(11f, 32f, u)), 11, 32);
                visualRenderer.sprite = jumpSprites[idx];

                // Compensate for extended hind-paw crop box during mid-air frames so the leap arc is seamless
                float arcOffset = Mathf.Sin(u * Mathf.PI) * 0.20f;
                visual.transform.localPosition = baseVisualLocalPos + new Vector3(0f, arcOffset, 0f);
                return;
            }

            // 3. Touchdown & front-leg shock-absorption recovery (frames 33..46)
            if (landingRecoveryTimer > 0f)
            {
                landingRecoveryTimer -= Time.deltaTime;
                if (absSpeedX > 2.2f && landingRecoveryTimer < landingRecoveryDuration * 0.4f)
                {
                    landingRecoveryTimer = 0f;
                    visual.transform.localPosition = baseVisualLocalPos;
                    return;
                }

                float t = 1f - Mathf.Clamp01(landingRecoveryTimer / Mathf.Max(0.01f, landingRecoveryDuration));
                int idx = Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(33f, 46f, t)), 33, 46);
                visualRenderer.sprite = jumpSprites[idx];

                float crouchDip = Mathf.Sin(t * Mathf.PI) * -0.07f;
                visual.transform.localPosition = baseVisualLocalPos + new Vector3(0f, crouchDip, 0f);
                return;
            }

            visual.transform.localPosition = baseVisualLocalPos;
        }

        void CheckDirectPetInput()
        {
            if (!UnityEngine.Input.GetMouseButtonDown(0))
            {
                return;
            }

            if (UnityEngine.EventSystems.EventSystem.current != null &&
                UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
            {
                return;
            }

            var cam = Camera.main;
            if (cam == null || ruby == null)
            {
                return;
            }

            Vector3 mouseWorld = cam.ScreenToWorldPoint(UnityEngine.Input.mousePosition);
            mouseWorld.z = 0f;

            // Clicking directly on or near Ruby pets her!
            if (Vector2.Distance(mouseWorld, ruby.transform.position) <= 1.85f)
            {
                PetRuby();
            }
        }

        public void PetRuby()
        {
            if (ruby == null || Time.unscaledTime - lastPetTime < 0.35f)
            {
                return;
            }

            lastPetTime = Time.unscaledTime;
            affectionCount++;

            bool wasGameOver = visual != null && visual.GameOverActive;
            visual?.Recover();
            visual?.SetSitting(false);
            ruby.PerformHappyHop(5.2f);
            visual?.TriggerCelebrate();

            PlayBark(preferRealBark: true);
            SpawnEffect(catchEffect, ruby.MouthWorldPosition);
            SpawnHeartBurst(ruby.transform.position + Vector3.up * 0.85f, 4);

            ruby.QueueHappyStartBoost();

            string phrase = wasGameOver
                ? "ALL CHEERED UP!"
                : PetPhrases[(affectionCount - 1) % PetPhrases.Length];

            SpawnFloatingText(phrase, ruby.transform.position + Vector3.up * 1.15f, new Color(1f, 0.55f, 0.72f));

            OnMoodChanged?.Invoke(
                $"Ruby loves the pets! ({affectionCount} pets, {treatsEatenCount} treats)\nHappy Zoomies Boost ready for next round!");
        }

        public void TossTreat()
        {
            if (ruby == null || treatInFlight)
            {
                return;
            }

            PlayUiClick(confirm: true);
            StartCoroutine(TossInteractiveItemRoutine(isTreat: true));
        }

        public void TossPlayBall()
        {
            if (ruby == null || treatInFlight)
            {
                return;
            }

            PlayUiClick(confirm: true);
            StartCoroutine(TossInteractiveItemRoutine(isTreat: false));
        }

        IEnumerator TossInteractiveItemRoutine(bool isTreat)
        {
            treatInFlight = true;

            visual?.Recover();
            visual?.SetSitting(false);
            audioPlayer?.PlayCue("ring_whoosh");

            Vector3 startPos = new(0.6f, 0.4f, 0f);
            float targetX = Random.Range(-5.0f, -1.6f);
            if (Mathf.Abs(targetX - ruby.transform.position.x) < 1.1f)
            {
                targetX = ruby.transform.position.x > -3.2f ? -4.8f : -1.7f;
            }

            Vector3 endPos = new(targetX, -1.45f, 0f);
            ruby.SetAutonomousTarget(targetX, jumpWhenNear: false);

            var itemGo = new GameObject(isTreat ? "TossedTreat" : "PlayTennisBall");
            itemGo.transform.position = startPos;
            var sr = itemGo.AddComponent<SpriteRenderer>();
            sr.sortingLayerName = "RF_Projectiles";
            sr.sortingOrder = 15;

            if (isTreat)
            {
                if (cachedBoneTreatSprite == null)
                {
                    cachedBoneTreatSprite = BuildBoneTreatSprite();
                }
                sr.sprite = cachedBoneTreatSprite;
                itemGo.transform.localScale = Vector3.one * 0.85f;
            }
            else
            {
                sr.sprite = tennisBallSprite != null ? tennisBallSprite : BuildBoneTreatSprite();
                float maxDim = sr.sprite != null ? Mathf.Max(sr.sprite.bounds.size.x, sr.sprite.bounds.size.y) : 1f;
                itemGo.transform.localScale = Vector3.one * (0.48f / Mathf.Max(0.01f, maxDim));
            }

            const float flightDuration = 0.72f;
            float elapsed = 0f;
            bool hopped = false;

            while (elapsed < flightDuration)
            {
                if (GameManager.Instance != null && GameManager.Instance.State == GameState.Playing)
                {
                    Destroy(itemGo);
                    treatInFlight = false;
                    yield break;
                }

                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / flightDuration);

                Vector3 pos = Vector3.Lerp(startPos, endPos, t);
                pos.y += Mathf.Sin(t * Mathf.PI) * 1.95f;
                itemGo.transform.position = pos;
                itemGo.transform.Rotate(0f, 0f, -380f * Time.deltaTime);

                if (!hopped && t >= 0.62f)
                {
                    hopped = true;
                    ruby.PerformHappyHop(6.2f);
                }

                // Snap catch if Ruby's mouth gets close near the end of the arc
                if (t >= 0.75f && Vector2.Distance(pos, ruby.MouthWorldPosition) <= 1.25f)
                {
                    break;
                }

                yield return null;
            }

            Vector3 catchSpot = ruby.MouthWorldPosition;
            Destroy(itemGo);

            treatsEatenCount++;
            visual?.TriggerCatch();
            audioPlayer?.PlayCue("catch");
            PlayBark();

            SpawnEffect(comboEffect != null ? comboEffect : catchEffect, catchSpot);
            SpawnHeartBurst(ruby.transform.position + Vector3.up * 0.9f, isTreat ? 5 : 3);
            ruby.QueueHappyStartBoost();

            string popup = isTreat ? "YUM! CRUNCHY TREAT!" : "SQUEAK! GOT THE BALL!";
            Color popupColor = isTreat ? new Color(1f, 0.86f, 0.28f) : new Color(0.55f, 1f, 0.38f);
            SpawnFloatingText(popup, catchSpot + Vector3.up * 0.55f, popupColor);

            OnMoodChanged?.Invoke(
                isTreat
                    ? $"Ruby munched a crunchy treat! ({treatsEatenCount} treats, {affectionCount} pets)\nHappy Zoomies Boost ready for next round!"
                    : $"Ruby caught the squeaky ball! ({treatsEatenCount} treats, {affectionCount} pets)\nHappy Zoomies Boost ready for next round!");

            yield return new WaitForSeconds(0.28f);
            if (GameManager.Instance != null && GameManager.Instance.State != GameState.Playing)
            {
                visual?.TriggerCelebrate();
                ruby.ReturnToMenuSpot(0.6f);
            }

            treatInFlight = false;
        }

        Vector3 GetFeetPosition()
        {
            return visual != null ? visual.transform.position : transform.position + new Vector3(0f, -0.45f, 0f);
        }

        void UpdateWaterBowlZoomies()
        {
            if (!waterBowlReady || ruby == null)
            {
                return;
            }

            waterBowlSparkleTimer -= Time.deltaTime;
            if (waterBowlSparkleTimer <= 0f)
            {
                waterBowlSparkleTimer = 1.1f;
                SpawnEffect(catchEffect, new Vector3(-6.25f, -2.0f, 0f));
            }

            if (ruby.IsGrounded && ruby.transform.position.x <= -5.2f)
            {
                waterBowlReady = false;
                ruby.ActivateZoomies(6.5f);
                PlayBark(preferRealBark: true);
                SpawnEffect(comboEffect, ruby.transform.position + Vector3.up * 0.6f);
                SpawnFloatingText("WATER SIP! ZOOMIES x6s!", ruby.transform.position + Vector3.up * 1.1f, new Color(0.3f, 0.95f, 1f));
            }
        }

        public void PlayBark(bool preferRealBark = false)
        {
            barkCounter++;
            bool useRealClip = realRubyBarkClip != null &&
                               (preferRealBark || barkCounter == 1 || Random.value < 0.72f);

            if (useRealClip && audioPlayer != null && audioPlayer.sfxSource != null)
            {
                audioPlayer.sfxSource.PlayOneShot(realRubyBarkClip, 0.95f);
                return;
            }

            audioPlayer?.PlayCue("happy_bark");
        }

        public void AttachRingTrail(Transform streamRigTransform, ThrowType throwType)
        {
            if (ringTrailEffect == null || streamRigTransform == null)
            {
                return;
            }

            var trail = Instantiate(ringTrailEffect, streamRigTransform);
            trail.transform.localPosition = new Vector3(-0.10f, 0f, 0f);
            trail.transform.localRotation = Quaternion.identity;
            trail.transform.localScale = new Vector3(0.42f, 0.52f, 1f);
            if (trail.TryGetComponent<SpriteRenderer>(out var sr))
            {
                sr.color = new Color(1f, 1f, 1f, 0.45f);
            }
            trail.SetActive(true);
        }

        public void NotifyThrow()
        {
            audioPlayer?.PlayCue("ring_whoosh");
        }

        public void NotifyBounce(Vector3 bouncePos)
        {
            audioPlayer?.PlayCue("land");
            SpawnEffect(landingEffect != null ? landingEffect : takeoffEffect, bouncePos);
        }

        public void NotifyCatch(bool airCatch)
        {
            NotifyCatch(airCatch, false, false, false, ruby != null ? ruby.MouthWorldPosition : transform.position);
        }

        public void NotifyCatch(bool airCatch, bool apexCatch, bool divingCatch, bool isBonusBall, Vector3 catchPos)
        {
            visual?.TriggerCatch();
            audioPlayer?.PlayCue("catch");

            if (airCatch || divingCatch || isBonusBall)
            {
                PlayBark();
                SpawnEffect(airCatchEffect != null ? airCatchEffect : catchEffect, catchPos);
                CameraJuice.Instance?.Shake(apexCatch ? 1.4f : 1.0f);
            }
            else
            {
                SpawnEffect(catchEffect, catchPos);
                CameraJuice.Instance?.Shake(0.55f);
            }

            int combo = ScoreManager.Instance != null ? ScoreManager.Instance.Combo : 0;
            if (combo > 1)
            {
                audioPlayer?.PlayCue("combo");
            }

            if (combo > 0 && combo % 5 == 0)
            {
                SpawnEffect(comboEffect, transform.position + Vector3.up * 0.7f);
                if (!ruby.HasZoomies)
                {
                    waterBowlReady = true;
                    waterBowlSparkleTimer = 0.1f;
                }
            }
        }

        public void NotifyMiss(Vector3 impact)
        {
            audioPlayer?.PlayCue("miss");
            SpawnEffect(missEffect, impact);
            CameraJuice.Instance?.Shake(1.15f);
            SpawnFloatingText("MISS!", impact + Vector3.up * 0.5f, new Color(1f, 0.4f, 0.4f));
        }

        public void PlayUiClick(bool confirm = false)
        {
            audioPlayer?.PlayCue(confirm ? "ui_confirm" : "ui_click");
        }

        void HandleJump()
        {
            audioPlayer?.PlayCue("jump");
            if (ruby != null)
            {
                SpawnEffect(takeoffEffect, GetFeetPosition());
            }
        }

        void HandleDash()
        {
            audioPlayer?.PlayCue("jump");
            if (ruby != null)
            {
                SpawnEffect(takeoffEffect, GetFeetPosition());
            }
        }

        void HandleCatchScored(int points, string label, Vector3 catchPos, bool isSpecial)
        {
            Vector3 popupPos = catchPos != Vector3.zero
                ? catchPos + Vector3.up * 0.45f
                : transform.position + Vector3.up * 0.9f;

            Color popupColor = isSpecial
                ? new Color(1f, 0.88f, 0.22f)
                : new Color(0.95f, 1f, 0.95f);

            SpawnFloatingText(label, popupPos, popupColor);
        }

        void HandleNewHighScore()
        {
            audioPlayer?.PlayCue("high_score");
            SpawnEffect(comboEffect, transform.position + Vector3.up * 0.85f);
            SpawnFloatingText("NEW HIGH SCORE!", transform.position + Vector3.up * 1.4f, new Color(1f, 0.75f, 0.15f));
        }

        void ApplyState(GameState state)
        {
            switch (state)
            {
                case GameState.Menu:
                    waterBowlReady = false;
                    visual?.Recover();
                    visual?.SetSitting(true);
                    ruby?.ReturnToMenuSpot(0.1f);
                    audioPlayer?.ApplySnapshot("Menu", 0.25f);
                    OnMoodChanged?.Invoke("Click Ruby to pet her, or toss a treat before you play!");
                    break;
                case GameState.Playing:
                    waterBowlReady = false;
                    visual?.Recover();
                    visual?.SetSitting(false);
                    audioPlayer?.ApplySnapshot("Gameplay", 0.25f);
                    if (ruby != null && ruby.HasQueuedHappyBoost)
                    {
                        SpawnFloatingText("HAPPY RUBY BOOST! ZOOMIES!", ruby.transform.position + Vector3.up * 1.15f, new Color(0.35f, 0.95f, 1f));
                    }
                    break;
                case GameState.GameOver:
                    waterBowlReady = false;
                    visual?.SetGameOver(true);
                    ruby?.ReturnToMenuSpot(0.2f);
                    audioPlayer?.PlayCue("game_over");
                    OnMoodChanged?.Invoke("Ruby looks a little sad... Pet her or toss a treat to cheer her up!");
                    break;
            }
        }

        void SpawnHeartBurst(Vector3 centerPos, int count)
        {
            if (cachedHeartSprite == null)
            {
                cachedHeartSprite = BuildHeartSprite();
            }

            for (int i = 0; i < count; i++)
            {
                var heartGo = new GameObject("AffectionHeart");
                Vector3 offset = new(Random.Range(-0.55f, 0.55f), Random.Range(-0.15f, 0.35f), 0f);
                heartGo.transform.position = centerPos + offset;
                heartGo.transform.localScale = Vector3.one * Random.Range(0.32f, 0.48f);

                var sr = heartGo.AddComponent<SpriteRenderer>();
                sr.sprite = cachedHeartSprite;
                sr.sortingLayerName = "RF_Effects";
                sr.sortingOrder = 22;
                sr.color = i % 2 == 0
                    ? new Color(1f, 0.32f, 0.48f, 0.95f)
                    : new Color(1f, 0.55f, 0.72f, 0.95f);

                StartCoroutine(AnimateFloatingHeart(heartGo.transform, sr, i * 0.06f));
            }
        }

        static IEnumerator AnimateFloatingHeart(Transform target, SpriteRenderer sr, float delay)
        {
            if (delay > 0f)
            {
                yield return new WaitForSeconds(delay);
            }

            const float duration = 0.95f;
            float elapsed = 0f;
            Vector3 startPos = target != null ? target.position : Vector3.zero;
            float baseScale = target != null ? target.localScale.x : 0.4f;
            float swayPhase = Random.Range(0f, 6.28f);
            Color baseColor = sr != null ? sr.color : Color.white;

            while (elapsed < duration && target != null && sr != null)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float swayX = Mathf.Sin(swayPhase + t * 8f) * 0.18f;
                target.position = startPos + new Vector3(swayX, t * 1.15f, 0f);

                float pop = t < 0.2f ? Mathf.Lerp(0.3f, 1.2f, t / 0.2f) : Mathf.Lerp(1.2f, 0.85f, (t - 0.2f) / 0.8f);
                target.localScale = Vector3.one * (baseScale * pop);

                if (t > 0.55f)
                {
                    sr.color = new Color(baseColor.r, baseColor.g, baseColor.b, Mathf.Lerp(1f, 0f, (t - 0.55f) / 0.45f));
                }

                yield return null;
            }

            if (target != null)
            {
                Destroy(target.gameObject);
            }
        }

        static Sprite BuildHeartSprite()
        {
            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float nx = (x - size * 0.5f) / (size * 0.36f);
                    float ny = (y - size * 0.44f) / (size * 0.36f);
                    // Classic heart implicit curve: (x^2 + y^2 - 1)^3 - x^2 * y^3 <= 0
                    float a = nx * nx + ny * ny - 1f;
                    float eq = a * a * a - nx * nx * ny * ny * ny;
                    if (eq <= 0f)
                    {
                        float edge = Mathf.Clamp01(-eq * 6f);
                        tex.SetPixel(x, y, new Color(1f, 1f, 1f, edge));
                    }
                    else
                    {
                        tex.SetPixel(x, y, Color.clear);
                    }
                }
            }

            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 64f);
        }

        static Sprite BuildBoneTreatSprite()
        {
            const int w = 96;
            const int h = 56;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            Color biscuitFill = new(0.92f, 0.72f, 0.38f, 1f);
            Color biscuitHighlight = new(0.98f, 0.84f, 0.55f, 1f);
            Color biscuitOutline = new(0.45f, 0.28f, 0.12f, 1f);

            Vector2[] knobs =
            {
                new(22f, 17f),
                new(22f, 39f),
                new(74f, 17f),
                new(74f, 39f)
            };

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    Vector2 p = new(x, y);
                    float minKnobDist = 999f;
                    for (int k = 0; k < knobs.Length; k++)
                    {
                        minKnobDist = Mathf.Min(minKnobDist, Vector2.Distance(p, knobs[k]) - 12.5f);
                    }

                    // Center bar of the bone
                    float barDx = Mathf.Max(0f, Mathf.Abs(x - w * 0.5f) - 24f);
                    float barDy = Mathf.Max(0f, Mathf.Abs(y - h * 0.5f) - 8.5f);
                    float barDist = Mathf.Sqrt(barDx * barDx + barDy * barDy);

                    float sdf = Mathf.Min(minKnobDist, barDist);
                    if (sdf <= 1.5f)
                    {
                        if (sdf > -1.8f)
                        {
                            tex.SetPixel(x, y, biscuitOutline);
                        }
                        else if (y > h * 0.54f)
                        {
                            tex.SetPixel(x, y, biscuitHighlight);
                        }
                        else
                        {
                            tex.SetPixel(x, y, biscuitFill);
                        }
                    }
                    else
                    {
                        tex.SetPixel(x, y, Color.clear);
                    }
                }
            }

            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 96f);
        }

        void SpawnEffect(GameObject prefab, Vector3 position)
        {
            if (prefab == null)
            {
                return;
            }

            var effect = Instantiate(prefab, position, Quaternion.identity);
            effect.SetActive(true);
            foreach (var oneShot in effect.GetComponentsInChildren<RubyOneShotEffect>(true))
            {
                oneShot.Restart();
                Destroy(effect, Mathf.Max(1.2f, oneShot.duration + 0.25f));
            }
        }

        void SpawnFloatingText(string content, Vector3 worldPos, Color color)
        {
            var go = new GameObject("FloatingScorePopup");
            go.transform.position = new Vector3(
                Mathf.Clamp(worldPos.x, -6.2f, 6.2f),
                Mathf.Clamp(worldPos.y, -1.6f, 3.6f),
                0f);

            var tmp = go.AddComponent<TextMeshPro>();
            tmp.text = content;
            tmp.fontSize = 3.6f;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = color;
            if (tmp.renderer != null)
            {
                tmp.renderer.sortingLayerName = "RF_UI";
                tmp.renderer.sortingOrder = 25;
            }
            tmp.sortingOrder = 25;

            var rect = go.GetComponent<RectTransform>();
            if (rect != null)
            {
                rect.sizeDelta = new Vector2(8f, 1.5f);
            }

            StartCoroutine(AnimateFloatingText(go.transform, tmp));
        }

        static IEnumerator AnimateFloatingText(Transform target, TextMeshPro tmp)
        {
            const float duration = 0.85f;
            float elapsed = 0f;
            Vector3 startPos = target.position;
            Color baseColor = tmp.color;

            while (elapsed < duration && target != null)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float easeOut = 1f - Mathf.Pow(1f - t, 3f);

                target.position = startPos + new Vector3(0f, easeOut * 0.95f, 0f);
                float scale = t < 0.2f ? Mathf.Lerp(0.6f, 1.15f, t / 0.2f) : Mathf.Lerp(1.15f, 0.95f, (t - 0.2f) / 0.8f);
                target.localScale = Vector3.one * scale;

                if (t > 0.55f)
                {
                    float alpha = Mathf.Lerp(1f, 0f, (t - 0.55f) / 0.45f);
                    tmp.color = new Color(baseColor.r, baseColor.g, baseColor.b, alpha);
                }

                yield return null;
            }

            if (target != null)
            {
                Destroy(target.gameObject);
            }
        }
    }
}
