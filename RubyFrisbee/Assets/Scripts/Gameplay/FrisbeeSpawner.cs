using RubyFrisbee.Core;
using UnityEngine;

namespace RubyFrisbee.Gameplay
{
    /// <summary>
    /// Spawns frisbees from the right side with increasing difficulty.
    /// </summary>
    public class FrisbeeSpawner : MonoBehaviour
    {
        [Header("Prefab")]
        [SerializeField] Frisbee frisbeePrefab;

        [Header("Spawn Timing")]
        [SerializeField] float initialSpawnInterval = 2.5f;
        [SerializeField] float minimumSpawnInterval = 0.8f;
        [SerializeField] float difficultyRampSeconds = 120f;

        [Header("Throw Variation")]
        [SerializeField] Vector2 spawnPosition = new(11f, 1.5f);
        [SerializeField] Vector2 spawnPositionVariance = new(0f, 1.5f);
        [SerializeField] Vector2 throwVelocity = new(-9f, 5f);
        [SerializeField] Vector2 throwVelocityVariance = new(2f, 2f);

        float spawnTimer;
        float elapsedTime;

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
            }
        }

        float GetCurrentInterval()
        {
            float t = Mathf.Clamp01(elapsedTime / difficultyRampSeconds);
            return Mathf.Lerp(initialSpawnInterval, minimumSpawnInterval, t);
        }

        void SpawnFrisbee()
        {
            if (frisbeePrefab == null)
            {
                Debug.LogWarning("FrisbeeSpawner is missing a frisbee prefab reference.");
                return;
            }

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
    }
}
