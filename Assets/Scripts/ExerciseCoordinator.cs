using System;
using System.Collections.Generic;
using UnityEngine;
using AGVRSystem.Data;
using AGVRSystem.Audio;
using AGVRSystem.Exercises;
using AGVRSystem.UI;

namespace AGVRSystem
{
    /// <summary>
    /// Sequences all 5 exercises in order, aggregates metrics, fires session completion.
    /// Integrates with audio systems for voice guidance and exercise cues.
    /// Drives the HUD each frame with exercise state and grip data.
    /// </summary>
    public class ExerciseCoordinator : MonoBehaviour
    {
        [SerializeField] private BaseExercise[] _exercises;
        [SerializeField] private ExerciseAudioCues _audioCues;
        [SerializeField] private HUDController _hudController;
        [SerializeField] private ExerciseObjectController[] _exerciseObjects;

        private int _currentIndex;
        private SessionData _sessionData;
        private readonly List<ExerciseMetrics> _metrics = new List<ExerciseMetrics>();
        private float _sessionStartTime;
        private bool _sessionActive;

        /// <summary>
        /// Elapsed time since the session began.
        /// </summary>
        public float SessionElapsedTime => _sessionActive ? Time.time - _sessionStartTime : 0f;

        /// <summary>
        /// Fired when all exercises are completed with the aggregated session data.
        /// </summary>
        public event Action<SessionData> OnSessionComplete;

        /// <summary>
        /// Starts a new session with the given user ID and begins the first exercise.
        /// </summary>
        public void BeginSession(string userId)
        {
            _sessionData = new SessionData
            {
                sessionId = Guid.NewGuid().ToString(),
                userId = userId,
                startTimestamp = DateTime.UtcNow.ToString("o")
            };

            _metrics.Clear();
            _currentIndex = 0;
            _sessionStartTime = Time.time;
            _sessionActive = true;

            StartCurrentExercise();
        }

        /// <summary>
        /// Pauses the currently active exercise.
        /// </summary>
        public void PauseSession()
        {
            if (_currentIndex < _exercises.Length && _exercises[_currentIndex].IsActive)
            {
                _exercises[_currentIndex].StopExercise();
            }
        }

        /// <summary>
        /// Resumes the currently paused exercise.
        /// </summary>
        public void ResumeSession()
        {
            if (_currentIndex < _exercises.Length && !_exercises[_currentIndex].IsActive)
            {
                _exercises[_currentIndex].StartExercise();
            }
        }

        /// <summary>
        /// Returns the current session data snapshot.
        /// </summary>
        public SessionData GetCurrentSessionData()
        {
            if (_sessionData != null)
            {
                _sessionData.exercises = new List<ExerciseMetrics>(_metrics);
            }

            return _sessionData;
        }

        private void Update()
        {
            if (!_sessionActive || _exercises == null || _currentIndex >= _exercises.Length)
                return;

            BaseExercise exercise = _exercises[_currentIndex];
            if (!exercise.IsActive)
                return;

            // Gather exercise state
            float elapsed = SessionElapsedTime;
            string exerciseName = exercise.GetType().Name.Replace("Exercise", "");
            int reps = exercise.CurrentReps;
            int targetReps = exercise.TargetReps;
            float accuracy = exercise.Evaluate() * 100f;

            // Get hold progress from exercises that support it
            float holdProgress = 0f;
            if (exercise is GripHoldExercise gripHold)
            {
                holdProgress = gripHold.HoldProgress;
            }
            else if (exercise is PrecisionPinchingExercise pinch)
            {
                holdProgress = pinch.HoldProgress;
            }

            // Push state to HUD
            if (_hudController != null)
            {
                string instruction = $"Complete {targetReps} repetitions.";
                _hudController.UpdateHUD(
                    elapsed,
                    exerciseName,
                    instruction,
                    _currentIndex + 1,
                    reps,
                    targetReps,
                    accuracy,
                    holdProgress);

                // Update grip panels
                if (HandTrackingManager.Instance != null)
                {
                    float leftGrip = 0f;
                    float rightGrip = 0f;
                    OVRHand leftHand = HandTrackingManager.Instance.LeftHand;
                    OVRHand rightHand = HandTrackingManager.Instance.RightHand;

                    if (leftHand != null && HandTrackingManager.Instance.IsLeftTracked)
                    {
                        leftGrip = HandTrackingManager.Instance.GetHandGripStrength(leftHand);
                    }

                    if (rightHand != null && HandTrackingManager.Instance.IsRightTracked)
                    {
                        rightGrip = HandTrackingManager.Instance.GetHandGripStrength(rightHand);
                    }

                    _hudController.UpdateGripPanels(leftHand, leftGrip, rightHand, rightGrip);
                }
            }

            // Update exercise object progress
            if (_exerciseObjects != null && _currentIndex < _exerciseObjects.Length
                && _exerciseObjects[_currentIndex] != null && targetReps > 0)
            {
                _exerciseObjects[_currentIndex].SetProgress(reps / (float)targetReps);
            }
        }

        private void StartCurrentExercise()
        {
            if (_exercises == null || _currentIndex >= _exercises.Length)
            {
                Debug.LogWarning("[ExerciseCoordinator] No exercises configured or all completed.");
                return;
            }

            BaseExercise exercise = _exercises[_currentIndex];
            exercise.OnExerciseCompleted += OnExerciseFinished;
            exercise.StartExercise();

            // Subscribe audio cues to current exercise
            if (_audioCues != null)
            {
                _audioCues.SubscribeToExercise(exercise);
            }

            // Initialize HUD tracking state
            if (_hudController != null)
            {
                _hudController.ShowTrackingLost(false);
            }

            // Subscribe to tracking events for HUD
            if (HandTrackingManager.Instance != null)
            {
                HandTrackingManager.Instance.OnTrackingLost += HandleTrackingLostForHUD;
                HandTrackingManager.Instance.OnTrackingRestored += HandleTrackingRestoredForHUD;
            }

            // TTS exercise introduction
            if (TTSVoiceGuide.Instance != null)
            {
                string exerciseName = exercise.GetType().Name.Replace("Exercise", "");
                TTSVoiceGuide.Instance.SpeakExerciseIntro(
                    exerciseName,
                    $"Complete {exercise.TargetReps} repetitions.");
            }

            Debug.Log($"[ExerciseCoordinator] Started exercise {_currentIndex + 1}/{_exercises.Length}: {exercise.GetType().Name}");
        }

        private void OnExerciseFinished(ExerciseMetrics metrics)
        {
            BaseExercise exercise = _exercises[_currentIndex];
            exercise.OnExerciseCompleted -= OnExerciseFinished;

            // Unsubscribe tracking events for this exercise
            if (HandTrackingManager.Instance != null)
            {
                HandTrackingManager.Instance.OnTrackingLost -= HandleTrackingLostForHUD;
                HandTrackingManager.Instance.OnTrackingRestored -= HandleTrackingRestoredForHUD;
            }

            if (HandTrackingManager.Instance != null)
            {
                float leftGrip = HandTrackingManager.Instance.GetHandGripStrength(HandTrackingManager.Instance.LeftHand);
                float rightGrip = HandTrackingManager.Instance.GetHandGripStrength(HandTrackingManager.Instance.RightHand);
                metrics.gripStrength = (leftGrip + rightGrip) / 2f;
            }

            _metrics.Add(metrics);
            _currentIndex++;

            // Show feedback on HUD
            if (_hudController != null)
            {
                _hudController.ShowFeedback($"Exercise complete! Accuracy: {metrics.accuracy:F0}%");
            }

            // TTS exercise completion
            if (TTSVoiceGuide.Instance != null)
            {
                TTSVoiceGuide.Instance.SpeakExerciseComplete(metrics.exerciseName, metrics.accuracy);
            }

            Debug.Log($"[ExerciseCoordinator] Exercise completed: {metrics.exerciseName} — Accuracy: {metrics.accuracy:F1}%");

            if (_currentIndex < _exercises.Length)
            {
                StartCurrentExercise();
            }
            else
            {
                FinalizeSession();
            }
        }

        private void HandleTrackingLostForHUD()
        {
            if (_hudController != null)
            {
                _hudController.ShowTrackingLost(true);
            }
        }

        private void HandleTrackingRestoredForHUD()
        {
            if (_hudController != null)
            {
                _hudController.ShowTrackingLost(false);
            }
        }

        private void FinalizeSession()
        {
            _sessionActive = false;
            _sessionData.endTimestamp = DateTime.UtcNow.ToString("o");
            _sessionData.exercises = new List<ExerciseMetrics>(_metrics);

            float totalAccuracy = 0f;
            float totalGrip = 0f;
            float totalDuration = 0f;

            for (int i = 0; i < _metrics.Count; i++)
            {
                totalAccuracy += _metrics[i].accuracy;
                totalGrip += _metrics[i].gripStrength;
                totalDuration += _metrics[i].duration;
            }

            int count = Mathf.Max(1, _metrics.Count);
            _sessionData.overallAccuracy = totalAccuracy / count;
            _sessionData.averageGripStrength = totalGrip / count;
            _sessionData.totalDuration = totalDuration;

            Debug.Log($"[ExerciseCoordinator] Session complete. Overall accuracy: {_sessionData.overallAccuracy:F1}%, Duration: {_sessionData.totalDuration:F1}s");

            // TTS session completion
            if (TTSVoiceGuide.Instance != null)
            {
                TTSVoiceGuide.Instance.SpeakSessionComplete(_sessionData.overallAccuracy);
            }

            OnSessionComplete?.Invoke(_sessionData);
        }
    }
}
