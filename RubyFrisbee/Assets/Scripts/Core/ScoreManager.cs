using UnityEngine;

namespace RubyFrisbee.Core
{
    /// <summary>
    /// Tracks score, combo multiplier, and persistent high score.
    /// </summary>
    public class ScoreManager : MonoBehaviour
    {
        public static ScoreManager Instance { get; private set; }

        const string HighScoreKey = "RubyFrisbee_HighScore";

        [SerializeField] int baseCatchPoints = 100;
        [SerializeField] int airCatchBonus = 50;
        [SerializeField] float comboMultiplierStep = 0.25f;
        [SerializeField] int maxComboMultiplier = 5;

        public int Score { get; private set; }
        public int Combo { get; private set; }
        public int HighScore { get; private set; }

        public event System.Action<int, int, int> OnScoreChanged;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            HighScore = PlayerPrefs.GetInt(HighScoreKey, 0);
        }

        public void ResetSession()
        {
            Score = 0;
            Combo = 0;
            Notify();
        }

        public void AddCatch(bool wasAirCatch)
        {
            Combo++;

            int multiplier = Mathf.Min(1 + Mathf.FloorToInt((Combo - 1) * comboMultiplierStep), maxComboMultiplier);
            int points = baseCatchPoints * multiplier;

            if (wasAirCatch)
            {
                points += airCatchBonus;
            }

            Score += points;

            if (Score > HighScore)
            {
                HighScore = Score;
                PlayerPrefs.SetInt(HighScoreKey, HighScore);
                PlayerPrefs.Save();
            }

            Notify();
        }

        public void BreakCombo()
        {
            Combo = 0;
            Notify();
        }

        void Notify()
        {
            OnScoreChanged?.Invoke(Score, Combo, HighScore);
        }
    }
}
