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
            Lives = startingLives;
        }

        public void StartGame()
        {
            Lives = startingLives;
            SetState(GameState.Playing);
            OnLivesChanged?.Invoke(Lives);
            ScoreManager.Instance?.ResetSession();
        }

        public void RegisterMiss()
        {
            if (State != GameState.Playing)
            {
                return;
            }

            ScoreManager.Instance?.BreakCombo();
            Lives--;

            OnLivesChanged?.Invoke(Lives);

            if (Lives <= 0)
            {
                SetState(GameState.GameOver);
            }
        }

        public void RegisterCatch(bool wasAirCatch)
        {
            if (State != GameState.Playing)
            {
                return;
            }

            ScoreManager.Instance?.AddCatch(wasAirCatch);
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
