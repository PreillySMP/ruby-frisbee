using RubyFrisbee.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RubyFrisbee.UI
{
    /// <summary>
    /// HUD and menu flow for score, lives, and game state screens.
    /// </summary>
    public class GameUI : MonoBehaviour
    {
        [Header("HUD")]
        [SerializeField] TextMeshProUGUI scoreText;
        [SerializeField] TextMeshProUGUI comboText;
        [SerializeField] TextMeshProUGUI livesText;
        [SerializeField] TextMeshProUGUI highScoreText;

        [Header("Panels")]
        [SerializeField] GameObject menuPanel;
        [SerializeField] GameObject hudPanel;
        [SerializeField] GameObject gameOverPanel;

        [Header("Menu")]
        [SerializeField] TextMeshProUGUI menuHighScoreText;
        [SerializeField] Button playButton;

        [Header("Game Over")]
        [SerializeField] TextMeshProUGUI finalScoreText;
        [SerializeField] TextMeshProUGUI gameOverHighScoreText;
        [SerializeField] Button restartButton;
        [SerializeField] Button menuButton;

        [Header("Mobile")]
        [SerializeField] MobileControlsUI mobileControls;

        void Start()
        {
            playButton?.onClick.AddListener(OnPlayClicked);
            restartButton?.onClick.AddListener(OnPlayClicked);
            menuButton?.onClick.AddListener(OnMenuClicked);

            if (ScoreManager.Instance != null)
            {
                ScoreManager.Instance.OnScoreChanged += HandleScoreChanged;
                HandleScoreChanged(0, 0, ScoreManager.Instance.HighScore);
            }

            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnStateChanged += HandleStateChanged;
                GameManager.Instance.OnLivesChanged += HandleLivesChanged;
                HandleStateChanged(GameManager.Instance.State);
                HandleLivesChanged(GameManager.Instance.Lives);
            }
        }

        void OnDestroy()
        {
            if (ScoreManager.Instance != null)
            {
                ScoreManager.Instance.OnScoreChanged -= HandleScoreChanged;
            }

            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnStateChanged -= HandleStateChanged;
                GameManager.Instance.OnLivesChanged -= HandleLivesChanged;
            }
        }

        void OnPlayClicked()
        {
            GameManager.Instance?.StartGame();
        }

        void OnMenuClicked()
        {
            GameManager.Instance?.ReturnToMenu();
        }

        void HandleScoreChanged(int score, int combo, int highScore)
        {
            if (scoreText != null)
            {
                scoreText.text = $"Score: {score}";
            }

            if (comboText != null)
            {
                comboText.text = combo > 1 ? $"Combo x{combo}" : string.Empty;
            }

            if (highScoreText != null)
            {
                highScoreText.text = $"Best: {highScore}";
            }

            if (menuHighScoreText != null)
            {
                menuHighScoreText.text = $"High Score: {highScore}";
            }

            if (finalScoreText != null)
            {
                finalScoreText.text = $"Score: {score}";
            }

            if (gameOverHighScoreText != null)
            {
                gameOverHighScoreText.text = $"Best: {highScore}";
            }
        }

        void HandleLivesChanged(int lives)
        {
            if (livesText != null)
            {
                livesText.text = $"Lives: {lives}";
            }
        }

        void HandleStateChanged(GameState state)
        {
            if (menuPanel != null)
            {
                menuPanel.SetActive(state == GameState.Menu);
            }

            if (hudPanel != null)
            {
                hudPanel.SetActive(state == GameState.Playing);
            }

            if (gameOverPanel != null)
            {
                gameOverPanel.SetActive(state == GameState.GameOver);
            }

            mobileControls?.SetGameplayVisible(state == GameState.Playing);
        }

        public void WireReferences(
            TextMeshProUGUI score,
            TextMeshProUGUI combo,
            TextMeshProUGUI lives,
            TextMeshProUGUI hudHighScore,
            GameObject menu,
            GameObject hud,
            GameObject gameOver,
            TextMeshProUGUI menuHigh,
            Button play,
            TextMeshProUGUI finalScore,
            TextMeshProUGUI gameOverHigh,
            Button restart,
            Button menuBtn,
            MobileControlsUI mobile = null)
        {
            scoreText = score;
            comboText = combo;
            livesText = lives;
            highScoreText = hudHighScore;
            menuPanel = menu;
            hudPanel = hud;
            gameOverPanel = gameOver;
            menuHighScoreText = menuHigh;
            playButton = play;
            finalScoreText = finalScore;
            gameOverHighScoreText = gameOverHigh;
            restartButton = restart;
            menuButton = menuBtn;
            mobileControls = mobile;
        }
    }
}
