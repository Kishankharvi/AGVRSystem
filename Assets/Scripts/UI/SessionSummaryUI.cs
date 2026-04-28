using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using AGVRSystem.Data;

namespace AGVRSystem.UI
{
    /// <summary>
    /// Displays post-session results with refined layout:
    /// - Stat cards (duration, accuracy, grip) with icons
    /// - Per-exercise bar chart with accuracy bars
    /// - Improvement indicator comparing to previous session
    /// - Exercise breakdown table
    /// - Animated entrance
    /// </summary>
    public class SessionSummaryUI : MonoBehaviour
    {
        [Header("Stat Cards")]
        [SerializeField] private TMP_Text _durationText;
        [SerializeField] private TMP_Text _accuracyText;
        [SerializeField] private TMP_Text _gripText;

        [Header("Stat Values (large)")]
        [SerializeField] private TMP_Text _durationValue;
        [SerializeField] private TMP_Text _accuracyValue;
        [SerializeField] private TMP_Text _gripValue;

        [Header("Stat Icons")]
        [SerializeField] private TMP_Text _durationIcon;
        [SerializeField] private TMP_Text _accuracyIcon;
        [SerializeField] private TMP_Text _gripIcon;

        [Header("Chart")]
        [SerializeField] private RectTransform _chartArea;
        [SerializeField] private TMP_Text _chartTitle;

        [Header("Improvement")]
        [SerializeField] private TMP_Text _improvementText;
        [SerializeField] private Image _improvementBG;

        [Header("Exercise Table")]
        [SerializeField] private Transform _exerciseTableParent;
        [SerializeField] private GameObject _exerciseRowPrefab;
        [SerializeField] private TMP_Text _tableHeader;

        [Header("Button")]
        [SerializeField] private Button _newSessionButton;

        [Header("Chart Colors")]
        [SerializeField] private Color _barColorHigh = new Color(0.2f, 0.8f, 0.45f, 0.9f);
        [SerializeField] private Color _barColorMid = new Color(0.95f, 0.75f, 0.15f, 0.9f);
        [SerializeField] private Color _barColorLow = new Color(0.9f, 0.3f, 0.25f, 0.9f);
        [SerializeField] private Color _barBgColor = new Color(0.15f, 0.15f, 0.2f, 0.5f);

        private const string CalibrationSceneName = "Calibration";
        private const float BarMaxHeight = 100f;
        private const float BarWidth = 50f;
        private const float BarSpacing = 12f;
        private const float LabelFontSize = 10f;
        private const float ValueFontSize = 9f;

        private List<GameObject> _chartBars = new List<GameObject>();

        private void Awake()
        {
            if (_newSessionButton != null)
            {
                _newSessionButton.onClick.AddListener(OnNewSession);
            }
        }

        /// <summary>
        /// Populates all summary fields with the completed session data.
        /// </summary>
        public void ShowSummary(SessionData data)
        {
            if (data == null)
            {
                Debug.LogWarning("[SessionSummaryUI] No session data to display.");
                return;
            }

            PopulateStatCards(data);
            BuildChart(data);
            PopulateExerciseTable(data);
            CalculateImprovement(data);
        }

        private void PopulateStatCards(SessionData data)
        {
            int minutes = (int)(data.totalDuration / 60f);
            int seconds = (int)(data.totalDuration % 60f);

            // Labels
            if (_durationText != null) _durationText.text = "DURATION";
            if (_accuracyText != null) _accuracyText.text = "ACCURACY";
            if (_gripText != null) _gripText.text = "AVG GRIP";

            // Values
            if (_durationValue != null) _durationValue.text = $"{minutes:D2}:{seconds:D2}";
            if (_accuracyValue != null) _accuracyValue.text = $"{data.overallAccuracy:F1}%";
            if (_gripValue != null) _gripValue.text = $"{data.averageGripStrength:F1}%";

            // Icons (ASCII-safe labels)
            if (_durationIcon != null) _durationIcon.text = "[T]";
            if (_accuracyIcon != null) _accuracyIcon.text = "[A]";
            if (_gripIcon != null) _gripIcon.text = "[G]";
        }

        private void BuildChart(SessionData data)
        {
            if (_chartArea == null || data.exercises == null || data.exercises.Count == 0)
                return;

            // Clear old bars
            foreach (var bar in _chartBars)
            {
                if (bar != null) Destroy(bar);
            }
            _chartBars.Clear();

            if (_chartTitle != null)
                _chartTitle.text = "EXERCISE ACCURACY";

            float totalWidth = data.exercises.Count * (BarWidth + BarSpacing) - BarSpacing;
            float startX = -totalWidth * 0.5f;

            for (int i = 0; i < data.exercises.Count; i++)
            {
                var ex = data.exercises[i];
                float accuracy = Mathf.Clamp01(ex.accuracy / 100f);
                float xPos = startX + i * (BarWidth + BarSpacing) + BarWidth * 0.5f;

                // Bar background
                CreateBarElement(_chartArea, $"BarBG_{i}",
                    new Vector2(xPos, BarMaxHeight * 0.5f),
                    new Vector2(BarWidth, BarMaxHeight),
                    _barBgColor);

                // Bar fill
                float barHeight = accuracy * BarMaxHeight;
                Color barColor = accuracy >= 0.8f ? _barColorHigh :
                                 accuracy >= 0.5f ? _barColorMid : _barColorLow;

                CreateBarElement(_chartArea, $"BarFill_{i}",
                    new Vector2(xPos, barHeight * 0.5f),
                    new Vector2(BarWidth - 4f, barHeight),
                    barColor);

                // Value label on top of bar
                CreateTextElement(_chartArea, $"BarVal_{i}",
                    new Vector2(xPos, barHeight + 8f),
                    new Vector2(BarWidth + 10f, 14f),
                    $"{ex.accuracy:F0}%", ValueFontSize,
                    barColor, TextAlignmentOptions.Center);

                // Exercise name label below
                string shortName = ShortenName(ex.exerciseName);
                CreateTextElement(_chartArea, $"BarLabel_{i}",
                    new Vector2(xPos, -10f),
                    new Vector2(BarWidth + 20f, 18f),
                    shortName, LabelFontSize,
                    new Color(0.7f, 0.7f, 0.75f, 1f), TextAlignmentOptions.Center);
            }
        }

        private void CreateBarElement(RectTransform parent, string name,
            Vector2 position, Vector2 size, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;

            var img = go.GetComponent<Image>();
            img.color = color;
            img.raycastTarget = false;

            _chartBars.Add(go);
        }

        private void CreateTextElement(RectTransform parent, string name,
            Vector2 position, Vector2 size, string text, float fontSize,
            Color color, TextAlignmentOptions alignment)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;

            var tmp = go.GetComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.color = color;
            tmp.alignment = alignment;
            tmp.fontStyle = FontStyles.Bold;
            tmp.raycastTarget = false;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.overflowMode = TextOverflowModes.Ellipsis;

            _chartBars.Add(go);
        }

        private void CalculateImprovement(SessionData data)
        {
            if (_improvementText == null) return;

            // Load previous session accuracy from PlayerPrefs
            float prevAccuracy = PlayerPrefs.GetFloat("LastSessionAccuracy", -1f);
            float currentAccuracy = data.overallAccuracy;

            // Save current for next comparison
            PlayerPrefs.SetFloat("LastSessionAccuracy", currentAccuracy);
            PlayerPrefs.Save();

            if (prevAccuracy < 0f)
            {
                _improvementText.text = "First session — great start!";
                if (_improvementBG != null)
                    _improvementBG.color = new Color(0.15f, 0.5f, 0.8f, 0.3f);
            }
            else
            {
                float delta = currentAccuracy - prevAccuracy;
                if (delta > 0.5f)
                {
                    _improvementText.text = $"▲ +{delta:F1}% improvement!";
                    if (_improvementBG != null)
                        _improvementBG.color = new Color(0.15f, 0.7f, 0.35f, 0.3f);
                }
                else if (delta < -0.5f)
                {
                    _improvementText.text = $"▼ {delta:F1}% — keep practicing!";
                    if (_improvementBG != null)
                        _improvementBG.color = new Color(0.8f, 0.5f, 0.15f, 0.3f);
                }
                else
                {
                    _improvementText.text = "● Consistent performance";
                    if (_improvementBG != null)
                        _improvementBG.color = new Color(0.3f, 0.5f, 0.7f, 0.3f);
                }
            }
        }

        private void PopulateExerciseTable(SessionData data)
        {
            if (_exerciseTableParent == null || _exerciseRowPrefab == null)
                return;

            // Clear existing rows
            for (int i = _exerciseTableParent.childCount - 1; i >= 0; i--)
            {
                Destroy(_exerciseTableParent.GetChild(i).gameObject);
            }

            if (_tableHeader != null)
                _tableHeader.text = "EXERCISE BREAKDOWN";

            foreach (ExerciseMetrics metrics in data.exercises)
            {
                GameObject row = Instantiate(_exerciseRowPrefab, _exerciseTableParent);
                TMP_Text[] texts = row.GetComponentsInChildren<TMP_Text>();

                if (texts.Length >= 4)
                {
                    texts[0].text = metrics.exerciseName;
                    texts[1].text = $"{metrics.repsCompleted}/{metrics.targetReps}";
                    texts[2].text = $"{metrics.accuracy:F1}%";
                    texts[3].text = $"{metrics.duration:F1}s";
                }
                else if (texts.Length >= 1)
                {
                    texts[0].text = $"{metrics.exerciseName}: {metrics.repsCompleted}/{metrics.targetReps} reps — {metrics.accuracy:F1}%";
                }
            }
        }

        private string ShortenName(string name)
        {
            if (string.IsNullOrEmpty(name)) return "?";
            // "Grip Hold" → "Grip", "Finger Tapping" → "Tap", etc.
            if (name.Contains("Grip")) return "Grip";
            if (name.Contains("Tap")) return "Tap";
            if (name.Contains("Pinch")) return "Pinch";
            if (name.Contains("Spread")) return "Spread";
            if (name.Contains("Thumb")) return "Thumb";
            return name.Length > 6 ? name.Substring(0, 6) : name;
        }

        private void OnNewSession()
        {
            SceneManager.LoadScene(CalibrationSceneName);
        }

        private void OnDestroy()
        {
            if (_newSessionButton != null)
            {
                _newSessionButton.onClick.RemoveListener(OnNewSession);
            }

            foreach (var bar in _chartBars)
            {
                if (bar != null) Destroy(bar);
            }
            _chartBars.Clear();
        }
    }
}
