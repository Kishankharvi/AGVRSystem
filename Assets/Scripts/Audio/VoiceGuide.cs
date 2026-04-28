using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.Events;

namespace AGVRSystem.Audio
{
    /// <summary>
    /// Provides dynamic voice guidance using Meta's Text To Speech Agent.
    /// Uses reflection to access the TTS agent, avoiding direct assembly dependency.
    /// Manages a priority queue of voice lines, prevents overlap, and provides
    /// contextual guidance for calibration, exercises, and navigation.
    /// </summary>
    public class TTSVoiceGuide : MonoBehaviour
    {
        public static TTSVoiceGuide Instance { get; private set; }

        /// <summary>Priority levels for voice lines. Higher priority interrupts lower.</summary>
        public enum VoicePriority
        {
            Low = 0,
            Normal = 1,
            High = 2,
            Critical = 3
        }

        [Header("TTS Agent")]
        [Tooltip("Drag the component with TextToSpeechAgent here.")]
        [SerializeField] private MonoBehaviour _ttsAgentComponent;

        [Header("Settings")]
        [SerializeField] private float _delayBetweenLines = 0.5f;
        [SerializeField] private bool _enableVoiceGuide = true;
        [SerializeField] private float _estimatedSpeechDuration = 3f;

        private readonly Queue<(string text, VoicePriority priority)> _lineQueue =
            new Queue<(string, VoicePriority)>();

        private bool _isSpeaking;
        private Coroutine _speakCoroutine;

        // Reflection-cached members
        private PropertyInfo _currentTextProp;
        private FieldInfo _textField;
        private MethodInfo _speakMethod;
        private FieldInfo _finishedField;
        private bool _reflReady;
        private bool _evtSubscribed;

        /// <summary>Fired when a voice line starts speaking.</summary>
        public event Action<string> OnSpeakStarted;

        /// <summary>Fired when a voice line finishes.</summary>
        public event Action OnSpeakDone;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            InitReflection();
        }

        private void OnEnable()
        {
            SubscribeFinished();
        }

        private void OnDisable()
        {
            UnsubscribeFinished();
        }

        /// <summary>
        /// Queues a voice line to be spoken. Respects priority ordering.
        /// </summary>
        public void Speak(string text, VoicePriority priority = VoicePriority.Normal)
        {
            if (!_enableVoiceGuide || !_reflReady || string.IsNullOrWhiteSpace(text))
                return;

            if (_ttsAgentComponent == null || !_ttsAgentComponent.isActiveAndEnabled)
            {
                Debug.LogWarning("[TTSVoiceGuide] TTS agent is null or disabled, skipping speech.");
                return;
            }

            if (priority == VoicePriority.Critical && _isSpeaking)
            {
                StopAll();
            }

            if (priority >= VoicePriority.High)
            {
                ClearLowerPriority(priority);
            }

            _lineQueue.Enqueue((text, priority));

            if (!_isSpeaking)
            {
                _speakCoroutine = StartCoroutine(ProcessQueue());
            }
        }

        /// <summary>
        /// Stops current speech and clears the queue.
        /// </summary>
        public void StopAll()
        {
            if (_speakCoroutine != null)
            {
                StopCoroutine(_speakCoroutine);
                _speakCoroutine = null;
            }

            _lineQueue.Clear();
            _isSpeaking = false;
        }

        /// <summary>
        /// Toggles voice guidance on or off.
        /// </summary>
        public void SetEnabled(bool enabled)
        {
            _enableVoiceGuide = enabled;
            if (!enabled)
            {
                StopAll();
            }
        }

        // ===== CONTEXTUAL VOICE LINE HELPERS =====

        /// <summary>Speaks a welcome message for the main menu.</summary>
        public void SpeakWelcome()
        {
            Speak("Welcome to the hand rehabilitation system. Please enter your user ID to begin.",
                VoicePriority.Normal);
        }

        /// <summary>Speaks calibration instructions.</summary>
        public void SpeakCalibrationStart()
        {
            Speak("Please hold both hands in front of you with fingers spread apart. Keep them steady for calibration.",
                VoicePriority.High);
        }

        /// <summary>Speaks calibration progress update.</summary>
        public void SpeakCalibrationProgress()
        {
            Speak("Good. Keep your hands steady. Almost there.", VoicePriority.Normal);
        }

        /// <summary>Speaks calibration success.</summary>
        public void SpeakCalibrationComplete()
        {
            Speak("Calibration complete. Starting your rehabilitation session now.", VoicePriority.High);
        }

        /// <summary>Speaks exercise introduction.</summary>
        public void SpeakExerciseIntro(string exerciseName, string instruction)
        {
            Speak($"Next exercise: {exerciseName}. {instruction}", VoicePriority.High);
        }

        /// <summary>Speaks encouragement during exercises.</summary>
        public void SpeakEncouragement(float accuracy)
        {
            if (accuracy >= 0.85f)
            {
                Speak("Excellent work! Keep it up.", VoicePriority.Low);
            }
            else if (accuracy >= 0.6f)
            {
                Speak("Good progress. Try to match the target position more closely.", VoicePriority.Low);
            }
            else
            {
                Speak("Take your time. Focus on controlled, steady movements.", VoicePriority.Normal);
            }
        }

        /// <summary>Speaks tracking lost warning.</summary>
        public void SpeakTrackingLost()
        {
            Speak("Hand tracking lost. Please bring your hands back into view.", VoicePriority.Critical);
        }

        /// <summary>Speaks exercise completion.</summary>
        public void SpeakExerciseComplete(string exerciseName, float accuracy)
        {
            string rating = accuracy >= 85f ? "Outstanding" : accuracy >= 60f ? "Well done" : "Good effort";
            Speak($"{rating}! {exerciseName} complete with {accuracy:F0} percent accuracy.", VoicePriority.High);
        }

        /// <summary>Speaks full session completion.</summary>
        public void SpeakSessionComplete(float overallAccuracy)
        {
            Speak($"Session complete. Your overall accuracy was {overallAccuracy:F0} percent. Great job today!",
                VoicePriority.High);
        }

        /// <summary>Speaks a halfway milestone.</summary>
        public void SpeakMilestone(int currentRep, int targetReps)
        {
            Speak($"Halfway there! {currentRep} of {targetReps} reps completed.", VoicePriority.Low);
        }

        // ===== REFLECTION-BASED TTS ACCESS =====

        private void InitReflection()
        {
            if (_ttsAgentComponent == null)
            {
                Debug.LogWarning("[TTSVoiceGuide] No TTS agent component assigned. Voice guidance disabled.");
                return;
            }

            Type agentType = _ttsAgentComponent.GetType();
            BindingFlags pubInst = BindingFlags.Public | BindingFlags.Instance;
            BindingFlags allInst = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

            _currentTextProp = agentType.GetProperty("CurrentText", pubInst);
            _textField = agentType.GetField("text", allInst);

            _speakMethod = agentType.GetMethod("SpeakText", pubInst, null, Type.EmptyTypes, null)
                        ?? agentType.GetMethod("Speak", pubInst, null, Type.EmptyTypes, null);

            _finishedField = agentType.GetField("onSpeakFinished", allInst);

            _reflReady = _speakMethod != null && (_currentTextProp != null || _textField != null);

            Debug.Log($"[TTSVoiceGuide] Reflection ready={_reflReady} for {agentType.Name}");
        }

        private void SetAgentText(string text)
        {
            if (_ttsAgentComponent == null)
                return;

            if (_currentTextProp != null && _currentTextProp.CanWrite)
            {
                _currentTextProp.SetValue(_ttsAgentComponent, text);
            }
            else if (_textField != null)
            {
                _textField.SetValue(_ttsAgentComponent, text);
            }
        }

        private void InvokeSpeak()
        {
            if (_speakMethod != null && _ttsAgentComponent != null)
            {
                _speakMethod.Invoke(_ttsAgentComponent, null);
            }
        }

        private void SubscribeFinished()
        {
            if (_evtSubscribed || _finishedField == null || _ttsAgentComponent == null)
                return;

            object eventObj = _finishedField.GetValue(_ttsAgentComponent);
            if (eventObj is UnityEvent unityEvent)
            {
                unityEvent.AddListener(HandleFinished);
                _evtSubscribed = true;
            }
        }

        private void UnsubscribeFinished()
        {
            if (!_evtSubscribed || _finishedField == null || _ttsAgentComponent == null)
                return;

            object eventObj = _finishedField.GetValue(_ttsAgentComponent);
            if (eventObj is UnityEvent unityEvent)
            {
                unityEvent.RemoveListener(HandleFinished);
                _evtSubscribed = false;
            }
        }

        // ===== INTERNAL =====

        private IEnumerator ProcessQueue()
        {
            _isSpeaking = true;

            while (_lineQueue.Count > 0)
            {
                var (text, priority) = _lineQueue.Dequeue();

                Debug.Log($"[TTSVoiceGuide] Speaking ({priority}): \"{text}\"");

                SetAgentText(text);
                InvokeSpeak();

                OnSpeakStarted?.Invoke(text);

                // Wait for speech to finish via event, or timeout based on text length
                float elapsed = 0f;
                float timeout = _estimatedSpeechDuration + (text.Length * 0.06f);

                while (_isSpeaking && elapsed < timeout)
                {
                    elapsed += Time.deltaTime;
                    yield return null;
                }

                // Re-set speaking flag for next item
                if (_lineQueue.Count > 0)
                {
                    _isSpeaking = true;
                    yield return new WaitForSeconds(_delayBetweenLines);
                }
            }

            _isSpeaking = false;
            _speakCoroutine = null;
        }

        private void HandleFinished()
        {
            OnSpeakDone?.Invoke();

            if (_lineQueue.Count == 0)
            {
                _isSpeaking = false;
            }
        }

        private void ClearLowerPriority(VoicePriority minPriority)
        {
            if (_lineQueue.Count == 0)
                return;

            var temp = new Queue<(string text, VoicePriority priority)>();
            while (_lineQueue.Count > 0)
            {
                var item = _lineQueue.Dequeue();
                if (item.priority >= minPriority)
                {
                    temp.Enqueue(item);
                }
            }

            while (temp.Count > 0)
            {
                _lineQueue.Enqueue(temp.Dequeue());
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }
    }
}
