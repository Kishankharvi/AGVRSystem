using System.Collections;
using UnityEngine;
using TMPro;

namespace AGVRSystem.UI
{
    /// <summary>
    /// World-space billboard HUD, 0.6m ahead of camera.
    /// Dark-themed rounded panels matching the reference design:
    /// - Top bar: Session Time | Exercise | Reps | Confidence Badge
    /// - Center: Left Grip Panel | Exercise Info Panel | Right Grip Panel
    /// - Bottom: Hold Progress Bar | Accuracy Bar
    /// </summary>
    public class HUDController : MonoBehaviour
    {
        [Header("Canvas")]
        [SerializeField] private Canvas _hudCanvas;

        [Header("Top Bar")]
        [SerializeField] private TMP_Text _sessionTimeLabel;
        [SerializeField] private TMP_Text _sessionTimeValue;
        [SerializeField] private TMP_Text _exerciseLabel;
        [SerializeField] private TMP_Text _exerciseValue;
        [SerializeField] private TMP_Text _repsLabel;
        [SerializeField] private TMP_Text _repsValue;
        [SerializeField] private ConfidenceBadge _confidenceBadge;

        [Header("Center Panels")]
        [SerializeField] private GripPanel _leftGripPanel;
        [SerializeField] private GripPanel _rightGripPanel;
        [SerializeField] private TMP_Text _exerciseTitleText;
        [SerializeField] private TMP_Text _exerciseInstructionText;

        [Header("Bottom Bars")]
        [SerializeField] private ProgressBar _holdProgressBar;
        [SerializeField] private TMP_Text _holdProgressLabel;
        [SerializeField] private ProgressBar _accuracyBar;
        [SerializeField] private TMP_Text _accuracyLabel;
        [SerializeField] private TMP_Text _accuracyPercentText;

        [Header("Feedback")]
        [SerializeField] private TMP_Text _feedbackText;
        [SerializeField] private GameObject _trackingLostPanel;

        [Header("References")]
        [SerializeField] private Transform _cameraTransform;

        private const float HudDistance = 0.6f;
        private const float FeedbackFlashDuration = 1.5f;

        private Coroutine _feedbackCoroutine;

        private void Start()
        {
            if (_hudCanvas != null)
            {
                _hudCanvas.renderMode = RenderMode.WorldSpace;
            }

            if (_feedbackText != null)
            {
                _feedbackText.text = string.Empty;
            }

            ShowTrackingLost(false);
        }

        private void Update()
        {
            UpdateConfidenceBadge();
        }

        private void LateUpdate()
        {
            if (_cameraTransform == null)
                return;

            transform.position = _cameraTransform.position + _cameraTransform.forward * HudDistance;
            transform.rotation = _cameraTransform.rotation;
        }

        /// <summary>
        /// Updates all HUD elements with current exercise state.
        /// </summary>
        public void UpdateHUD(
            float timer,
            string exerciseName,
            string exerciseInstruction,
            int exerciseNumber,
            int reps,
            int targetReps,
            float accuracy,
            float holdProgress)
        {
            if (_sessionTimeValue != null)
            {
                int minutes = (int)(timer / 60f);
                int seconds = (int)(timer % 60f);
                _sessionTimeValue.text = $"{minutes:D2}:{seconds:D2}";
            }

            if (_exerciseValue != null)
            {
                _exerciseValue.text = exerciseName;
            }

            if (_repsValue != null)
            {
                _repsValue.text = $"{reps} / {targetReps}";
            }

            if (_exerciseTitleText != null)
            {
                _exerciseTitleText.text = $"Exercise {exerciseNumber} \u2014 {exerciseName}";
            }

            if (_exerciseInstructionText != null)
            {
                _exerciseInstructionText.text = exerciseInstruction;
            }

            if (_holdProgressBar != null)
            {
                _holdProgressBar.SetValue(holdProgress);
            }

            if (_accuracyBar != null)
            {
                _accuracyBar.SetValue(accuracy / 100f);
            }

            if (_accuracyPercentText != null)
            {
                _accuracyPercentText.text = $"{accuracy:F0}%";
            }
        }

        /// <summary>
        /// Updates grip panels with per-finger data from both hands.
        /// </summary>
        public void UpdateGripPanels(OVRHand leftHand, float leftGrip, OVRHand rightHand, float rightGrip)
        {
            if (_leftGripPanel != null)
            {
                _leftGripPanel.UpdateGrip(leftHand, leftGrip);
            }

            if (_rightGripPanel != null)
            {
                _rightGripPanel.UpdateGrip(rightHand, rightGrip);
            }
        }

        /// <summary>
        /// Shows a temporary feedback message that fades out.
        /// </summary>
        public void ShowFeedback(string message)
        {
            if (_feedbackText == null)
                return;

            if (_feedbackCoroutine != null)
            {
                StopCoroutine(_feedbackCoroutine);
            }

            _feedbackCoroutine = StartCoroutine(FeedbackCoroutine(message));
        }

        /// <summary>
        /// Shows or hides the "TRACKING LOST" overlay panel.
        /// </summary>
        public void ShowTrackingLost(bool show)
        {
            if (_trackingLostPanel != null)
            {
                _trackingLostPanel.SetActive(show);
            }
        }

        private void UpdateConfidenceBadge()
        {
            if (_confidenceBadge == null)
                return;

            var manager = HandTrackingManager.Instance;
            if (manager == null)
            {
                _confidenceBadge.SetLost();
                return;
            }

            bool leftHigh = manager.LeftHand != null
                && manager.LeftHand.IsTracked
                && manager.LeftHand.HandConfidence == OVRHand.TrackingConfidence.High;

            bool rightHigh = manager.RightHand != null
                && manager.RightHand.IsTracked
                && manager.RightHand.HandConfidence == OVRHand.TrackingConfidence.High;

            if (leftHigh && rightHigh)
            {
                _confidenceBadge.SetHigh();
            }
            else if (manager.IsLeftTracked || manager.IsRightTracked)
            {
                _confidenceBadge.SetMedium();
            }
            else
            {
                _confidenceBadge.SetLost();
            }
        }

        private IEnumerator FeedbackCoroutine(string message)
        {
            _feedbackText.text = message;
            _feedbackText.alpha = 1f;

            float elapsed = 0f;
            while (elapsed < FeedbackFlashDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / FeedbackFlashDuration;
                _feedbackText.alpha = 1f - t;
                yield return null;
            }

            _feedbackText.text = string.Empty;
            _feedbackText.alpha = 1f;
            _feedbackCoroutine = null;
        }
    }
}
