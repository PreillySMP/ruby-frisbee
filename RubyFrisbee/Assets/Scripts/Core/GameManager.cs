using UnityEngine;

namespace RubyFrisbee.Core
{
    public enum GameState
    {
        Menu,
        Playing,
        GameOver
    }

    /// <summary>
    /// Central game state controller. Handles lives, game over, and restart.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [SerializeField] int startingLives = 3;

        public GameState State { get; private set; } = GameState.Menu;
        public int Lives { get; private set; }
        public int MaxLives => startingLives;

        public event System.Action<GameState> OnStateChanged;
        public event System.Action<int> OnLivesChanged;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            State = GameState.Menu;
            Lives = startingLives;
        }

        void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        public void StartGame()
        {
            Lives = startingLives;
            ScoreManager.Instance?.ResetSession();
            SetState(GameState.Playing);
            OnLivesChanged?.Invoke(Lives);
        }

        public void RegisterMiss()
        {
            if (State != GameState.Playing)
            {
                return;
            }

            ScoreManager.Instance?.BreakCombo();
            Lives = Mathf.Max(0, Lives - 1);

            OnLivesChanged?.Invoke(Lives);

            if (Lives <= 0)
            {
                SetState(GameState.GameOver);
            }
        }

        public void RegisterCatch(bool wasAirCatch)
        {
            RegisterCatch(wasAirCatch, false, false, false, Vector3.zero);
        }

        public void RegisterCatch(bool wasAirCatch, bool wasApexCatch, bool wasDivingCatch, bool wasBonusBall, Vector3 catchPosition)
        {
            if (State != GameState.Playing)
            {
                return;
            }

            bool restoredLife = false;
            if (wasBonusBall && Lives < startingLives)
            {
                Lives = Mathf.Min(startingLives, Lives + 1);
                restoredLife = true;
                OnLivesChanged?.Invoke(Lives);
            }

            ScoreManager.Instance?.AddCatch(wasAirCatch, wasApexCatch, wasDivingCatch, wasBonusBall, restoredLife, catchPosition);
        }

        public void ReturnToMenu()
        {
            SetState(GameState.Menu);
        }

        void SetState(GameState newState)
        {
            State = newState;
            OnStateChanged?.Invoke(State);
        }
    }
}
