using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace AGVRSystem.UI
{
    /// <summary>
    /// Main menu with user ID input, start session button, theme toggle,
    /// and animated scene transitions. Manages both the session board and about board.
    /// </summary>
    public class MainMenuController : MonoBehaviour
    {
        [Header("Session Panel")]
        [SerializeField] private TMP_InputField _userIdInput;
        [SerializeField] private Button _startButton;
        [SerializeField] private TMP_Text _errorText;

        [Header("Theme Toggle")]
        [SerializeField] private Button _themeToggleButton;
        [SerializeField] private TMP_Text _themeButtonLabel;

        private const string CalibrationScene = "Calibration";
        private const string UserIdKey = "LastUserId";

        private void Start()
        {
            if (_startButton != null)
            {
                _startButton.onClick.AddListener(OnStartClicked);
            }

            if (_themeToggleButton != null)
            {
                _themeToggleButton.onClick.AddListener(OnThemeToggleClicked);
            }

            if (_userIdInput != null)
            {
                string lastId = PlayerPrefs.GetString(UserIdKey, string.Empty);
                if (!string.IsNullOrEmpty(lastId))
                {
                    _userIdInput.text = lastId;
                }
            }

            if (_errorText != null)
            {
                _errorText.text = string.Empty;
            }

            UpdateThemeLabel();

            // Fade in on scene start
            if (SceneTransitionManager.Instance != null)
            {
                SceneTransitionManager.Instance.FadeIn();
            }

            // TTS welcome message
            if (Audio.TTSVoiceGuide.Instance != null)
            {
                Audio.TTSVoiceGuide.Instance.SpeakWelcome();
            }
        }

        private void OnStartClicked()
        {
            if (_userIdInput == null || string.IsNullOrWhiteSpace(_userIdInput.text))
            {
                if (_errorText != null)
                {
                    _errorText.text = "Please enter a User ID.";
                }

                if (Audio.UIAudioFeedback.Instance != null)
                {
                    Audio.UIAudioFeedback.Instance.PlayError();
                }
                return;
            }

            if (Audio.UIAudioFeedback.Instance != null)
            {
                Audio.UIAudioFeedback.Instance.PlayClick();
            }

            string visitorId = _userIdInput.text.Trim();
            PlayerPrefs.SetString(UserIdKey, visitorId);
            PlayerPrefs.Save();

            if (SceneTransitionManager.Instance != null)
            {
                Audio.UIAudioFeedback.Instance?.PlayTransition();
                SceneTransitionManager.Instance.TransitionToScene(CalibrationScene);
            }
            else
            {
                UnityEngine.SceneManagement.SceneManager.LoadScene(CalibrationScene);
            }
        }

        private void OnThemeToggleClicked()
        {
            if (Audio.UIAudioFeedback.Instance != null)
            {
                Audio.UIAudioFeedback.Instance.PlayClick();
            }

            if (ThemeManager.Instance != null)
            {
                ThemeManager.Instance.CycleTheme();
                UpdateThemeLabel();
            }
        }

        private void UpdateThemeLabel()
        {
            if (_themeButtonLabel == null || ThemeManager.Instance == null)
                return;

            var current = ThemeManager.Instance.CurrentTheme;
            if (current != null)
            {
                _themeButtonLabel.text = current.ThemeName;
            }
        }

        private void OnDestroy()
        {
            if (_startButton != null)
            {
                _startButton.onClick.RemoveListener(OnStartClicked);
            }

            if (_themeToggleButton != null)
            {
                _themeToggleButton.onClick.RemoveListener(OnThemeToggleClicked);
            }
        }
    }
}
