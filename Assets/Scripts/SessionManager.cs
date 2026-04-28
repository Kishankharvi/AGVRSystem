using System;
using System.IO;
using UnityEngine;
using AGVRSystem.Data;
using AGVRSystem.Network;
using AGVRSystem.UI;
using AGVRSystem.Audio;

namespace AGVRSystem
{
    /// <summary>
    /// State machine for session lifecycle. Handles autosave, server sync, and session summary display.
    /// </summary>
    public class SessionManager : MonoBehaviour
    {
        public enum SessionState
        {
            Idle,
            Calibrating,
            Exercising,
            Paused,
            Complete
        }

        [SerializeField] private ExerciseCoordinator _coordinator;
        [SerializeField] private APIManager _apiManager;

        [Header("Session Summary")]
        [SerializeField] private SessionSummaryUI _summaryUI;
        [SerializeField] private GameObject _reportBoard;
        [SerializeField] private GameObject _hudRoot;

        private const float AutosaveInterval = 30f;
        private const string AutosaveFileName = "autosave.json";

        public SessionState CurrentState { get; private set; } = SessionState.Idle;

        /// <summary>
        /// Fired whenever the session state changes.
        /// </summary>
        public event Action<SessionState> OnStateChanged;

        private SessionData _currentSession;
        private float _autosaveTimer;
        private string _userId;

        /// <summary>
        /// Transitions to the Calibrating state.
        /// </summary>
        public void StartCalibration()
        {
            SetState(SessionState.Calibrating);
        }

        /// <summary>
        /// Begins exercises after calibration.
        /// </summary>
        public void StartExercising(string userId)
        {
            _userId = userId;
            _autosaveTimer = AutosaveInterval;
            SetState(SessionState.Exercising);

            if (_coordinator != null)
            {
                _coordinator.OnSessionComplete += HandleSessionComplete;
                _coordinator.BeginSession(_userId);
            }
        }

        /// <summary>
        /// Pauses the current session.
        /// </summary>
        public void PauseSession()
        {
            if (CurrentState != SessionState.Exercising)
                return;

            _coordinator?.PauseSession();
            SetState(SessionState.Paused);
        }

        /// <summary>
        /// Resumes a paused session.
        /// </summary>
        public void ResumeSession()
        {
            if (CurrentState != SessionState.Paused)
                return;

            _coordinator?.ResumeSession();
            SetState(SessionState.Exercising);
        }

        /// <summary>
        /// Completes the session, shows summary, and sends data to server.
        /// </summary>
        public void CompleteSession()
        {
            if (_currentSession == null)
            {
                _currentSession = _coordinator?.GetCurrentSessionData();
            }

            SetState(SessionState.Complete);

            // Show session summary report board
            ShowSessionSummary();

            // Play completion audio
            if (UIAudioFeedback.Instance != null)
            {
                UIAudioFeedback.Instance.PlaySuccess();
            }

            if (_apiManager != null && _currentSession != null)
            {
                _apiManager.PostSession(_currentSession, success =>
                {
                    if (!success)
                    {
                        SaveSessionOffline(_currentSession);
                    }

                    Debug.Log($"[SessionManager] Session sync result: {(success ? "success" : "saved offline")}");
                });
            }
        }

        private void ShowSessionSummary()
        {
            // Hide HUD
            if (_hudRoot != null)
            {
                _hudRoot.SetActive(false);
            }

            // Show report board and populate summary
            if (_reportBoard != null)
            {
                _reportBoard.SetActive(true);
            }

            if (_summaryUI != null && _currentSession != null)
            {
                _summaryUI.ShowSummary(_currentSession);
            }
        }

        private void Update()
        {
            if (CurrentState != SessionState.Exercising)
                return;

            _autosaveTimer -= Time.deltaTime;

            if (_autosaveTimer <= 0f)
            {
                _autosaveTimer = AutosaveInterval;
                AutosaveSession();
            }
        }

        private void SetState(SessionState newState)
        {
            if (CurrentState == newState)
                return;

            CurrentState = newState;
            Debug.Log($"[SessionManager] State changed to: {newState}");
            OnStateChanged?.Invoke(newState);
        }

        private void HandleSessionComplete(SessionData data)
        {
            _currentSession = data;

            if (_coordinator != null)
            {
                _coordinator.OnSessionComplete -= HandleSessionComplete;
            }

            CompleteSession();
        }

        private void AutosaveSession()
        {
            SessionData data = _coordinator?.GetCurrentSessionData();
            if (data == null)
                return;

            string json = data.ToJson();
            string filePath = Path.Combine(Application.persistentDataPath, AutosaveFileName);

            try
            {
                File.WriteAllText(filePath, json);
                Debug.Log("[SessionManager] Autosave completed.");
            }
            catch (Exception e)
            {
                Debug.LogError($"[SessionManager] Autosave failed: {e.Message}");
            }
        }

        private void SaveSessionOffline(SessionData data)
        {
            string fileName = $"offline_{data.sessionId}.json";
            string filePath = Path.Combine(Application.persistentDataPath, fileName);

            try
            {
                File.WriteAllText(filePath, data.ToJson());
                Debug.Log($"[SessionManager] Session saved offline: {fileName}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[SessionManager] Failed to save session offline: {e.Message}");
            }
        }
    }
}
