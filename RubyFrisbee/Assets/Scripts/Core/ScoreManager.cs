using UnityEngine;

namespace RubyFrisbee.Core
{
    /// <summary>
    /// Tracks score, combo multiplier, catch style bonuses, and persistent high score.
    /// </summary>
    public class ScoreManager : MonoBehaviour
    {
        public static ScoreManager Instance { get; private set; }

        const string HighScoreKey = "RubyFrisbee_HighScore";

        [SerializeField] int baseCatchPoints = 100;
        [SerializeField] int airCatchBonus = 50;
        [SerializeField] int apexCatchBonus = 75;
        [SerializeField] int divingCatchBonus = 75;
        [SerializeField] int bonusBallPoints = 150;
        [SerializeField] float comboMultiplierStep = 0.25f;
        [SerializeField] int maxComboMultiplier = 5;

        public int Score { get; private set; }
        public int Combo { get; private set; }
        public int HighScore { get; private set; }

        int sessionStartHighScore;
        bool announcedNewHighScore;

        public event System.Action<int, int, int> OnScoreChanged;
        public event System.Action<int, string, Vector3, bool> OnCatchScored;
        public event System.Action OnNewHighScore;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            HighScore = PlayerPrefs.GetInt(HighScoreKey, 0);
            sessionStartHighScore = HighScore;
        }

        public void ResetSession()
        {
            Score = 0;
            Combo = 0;
            sessionStartHighScore = HighScore;
            announcedNewHighScore = false;
            Notify();
        }

        public void AddCatch(bool wasAirCatch)
        {
            AddCatch(wasAirCatch, false, false, false, false, Vector3.zero);
        }

        public void AddCatch(
            bool wasAirCatch,
            bool wasApexCatch,
            bool wasDivingCatch,
            bool wasBonusBall,
            bool restoredLife,
            Vector3 catchPosition)
        {
            Combo++;

            int multiplier = Mathf.Min(1 + Mathf.FloorToInt((Combo - 1) * comboMultiplierStep), maxComboMultiplier);
            int points = (wasBonusBall ? bonusBallPoints : baseCatchPoints) * multiplier;

            if (wasAirCatch)
            {
                points += airCatchBonus;
            }

            if (wasApexCatch)
            {
                points += apexCatchBonus;
            }

            if (wasDivingCatch)
            {
                points += divingCatchBonus;
            }

            Score += points;

            string label = BuildCatchLabel(points, wasAirCatch, wasApexCatch, wasDivingCatch, wasBonusBall, restoredLife);
            bool isSpecial = wasAirCatch || wasApexCatch || wasDivingCatch || wasBonusBall || Combo >= 5;

            if (Score > HighScore)
            {
                HighScore = Score;
                PlayerPrefs.SetInt(HighScoreKey, HighScore);
                PlayerPrefs.Save();

                if (!announcedNewHighScore && sessionStartHighScore > 0)
                {
                    announcedNewHighScore = true;
                    OnNewHighScore?.Invoke();
                }
            }

            Notify();
            OnCatchScored?.Invoke(points, label, catchPosition, isSpecial);
        }

        static string BuildCatchLabel(
            int points,
            bool wasAirCatch,
            bool wasApexCatch,
            bool wasDivingCatch,
            bool wasBonusBall,
            bool restoredLife)
        {
            if (wasBonusBall && restoredLife)
            {
                return $"BONUS BALL! +1 LIFE (+{points})";
            }

            if (wasBonusBall)
            {
                return $"BONUS BALL! +{points}";
            }

            if (wasApexCatch && wasDivingCatch)
            {
                return $"SUPER LEAP! +{points}";
            }

            if (wasApexCatch)
            {
                return $"SKY SNATCH! +{points}";
            }

            if (wasDivingCatch)
            {
                return $"DIVING SAVE! +{points}";
            }

            if (wasAirCatch)
            {
                return $"AIR CATCH! +{points}";
            }

            return $"+{points}";
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
