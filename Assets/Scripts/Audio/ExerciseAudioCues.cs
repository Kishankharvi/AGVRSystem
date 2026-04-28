using System.Collections;
using UnityEngine;
using AGVRSystem.Exercises;

namespace AGVRSystem.Audio
{
    /// <summary>
    /// Provides audio feedback for exercise events: rep completion dings,
    /// exercise start/end fanfares, accuracy-based tonal variations,
    /// and countdown beeps. Hooks into BaseExercise and ExerciseCoordinator events.
    /// </summary>
    public class ExerciseAudioCues : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private AudioSource _cueSource;
        [SerializeField] private ExerciseCoordinator _coordinator;

        [Header("Volume")]
        [SerializeField] private float _repDingVolume = 0.4f;
        [SerializeField] private float _exerciseStartVolume = 0.35f;
        [SerializeField] private float _sessionCompleteVolume = 0.5f;

        [Header("Spatial")]
        [SerializeField] private float _spatialBlend = 0.3f;
        [SerializeField] private Transform _cameraTransform;

        // Cached procedural clips
        private AudioClip _repDingLow;
        private AudioClip _repDingMid;
        private AudioClip _repDingHigh;
        private AudioClip _exerciseStartClip;
        private AudioClip _exerciseCompleteClip;
        private AudioClip _sessionCompleteClip;
        private AudioClip _milestoneClip;

        private const float LowAccuracyThreshold = 0.5f;
        private const float HighAccuracyThreshold = 0.85f;
        private const float CuePositionDistance = 0.4f;

        private BaseExercise _currentExercise;

        private void Awake()
        {
            EnsureAudioSource();
            GenerateClips();
        }

        private void OnEnable()
        {
            if (_coordinator != null)
            {
                _coordinator.OnSessionComplete += HandleSessionComplete;
            }
        }

        private void OnDisable()
        {
            if (_coordinator != null)
            {
                _coordinator.OnSessionComplete -= HandleSessionComplete;
            }

            UnsubscribeFromExercise();
        }

        private void LateUpdate()
        {
            // Position audio slightly in front of the player
            if (_cameraTransform != null && _cueSource != null)
            {
                _cueSource.transform.position =
                    _cameraTransform.position + _cameraTransform.forward * CuePositionDistance;
            }
        }

        /// <summary>
        /// Subscribe to an exercise's events for audio feedback.
        /// Call this when a new exercise starts.
        /// </summary>
        public void SubscribeToExercise(BaseExercise exercise)
        {
            UnsubscribeFromExercise();

            _currentExercise = exercise;
            if (_currentExercise != null)
            {
                _currentExercise.OnRepCompleted += HandleRepCompleted;
                _currentExercise.OnExerciseCompleted += HandleExerciseCompleted;
                PlayExerciseStart();
            }
        }

        /// <summary>
        /// Plays the rep completion sound with pitch variation based on accuracy.
        /// </summary>
        public void PlayRepDing(float accuracy)
        {
            AudioClip clip;

            if (accuracy >= HighAccuracyThreshold)
            {
                clip = _repDingHigh;
            }
            else if (accuracy >= LowAccuracyThreshold)
            {
                clip = _repDingMid;
            }
            else
            {
                clip = _repDingLow;
            }

            PlayCue(clip, _repDingVolume);
        }

        /// <summary>
        /// Plays the exercise start sound.
        /// </summary>
        public void PlayExerciseStart()
        {
            PlayCue(_exerciseStartClip, _exerciseStartVolume);
        }

        /// <summary>
        /// Plays the exercise completion sound.
        /// </summary>
        public void PlayExerciseComplete()
        {
            PlayCue(_exerciseCompleteClip, _exerciseStartVolume * 1.2f);
        }

        /// <summary>
        /// Plays the full session completion fanfare.
        /// </summary>
        public void PlaySessionComplete()
        {
            PlayCue(_sessionCompleteClip, _sessionCompleteVolume);
        }

        /// <summary>
        /// Plays a milestone sound (e.g., halfway through reps).
        /// </summary>
        public void PlayMilestone()
        {
            PlayCue(_milestoneClip, _repDingVolume * 1.1f);
        }

        private void HandleRepCompleted(int repCount)
        {
            if (_currentExercise == null)
                return;

            float accuracy = _currentExercise.Evaluate();
            PlayRepDing(accuracy);

            // Milestone at halfway
            if (_currentExercise.TargetReps > 0 &&
                repCount == _currentExercise.TargetReps / 2)
            {
                StartCoroutine(DelayedPlayCoroutine(_milestoneClip, _repDingVolume, 0.2f));
            }
        }

        private void HandleExerciseCompleted(Data.ExerciseMetrics metrics)
        {
            PlayExerciseComplete();
            Debug.Log($"[ExerciseAudioCues] Exercise '{metrics.exerciseName}' completed — playing completion cue");
        }

        private void HandleSessionComplete(Data.SessionData sessionData)
        {
            StartCoroutine(DelayedPlayCoroutine(_sessionCompleteClip, _sessionCompleteVolume, 0.5f));
            Debug.Log("[ExerciseAudioCues] Session completed — playing fanfare");
        }

        private void UnsubscribeFromExercise()
        {
            if (_currentExercise != null)
            {
                _currentExercise.OnRepCompleted -= HandleRepCompleted;
                _currentExercise.OnExerciseCompleted -= HandleExerciseCompleted;
                _currentExercise = null;
            }
        }

        private void PlayCue(AudioClip clip, float volume)
        {
            if (_cueSource == null || clip == null)
                return;

            _cueSource.PlayOneShot(clip, volume);
        }

        private IEnumerator DelayedPlayCoroutine(AudioClip clip, float volume, float delay)
        {
            yield return new WaitForSeconds(delay);
            PlayCue(clip, volume);
        }

        private void EnsureAudioSource()
        {
            if (_cueSource == null)
            {
                _cueSource = gameObject.AddComponent<AudioSource>();
            }

            _cueSource.playOnAwake = false;
            _cueSource.spatialBlend = _spatialBlend;
            _cueSource.priority = 96;
        }

        private void GenerateClips()
        {
            // Rep dings at different pitches for accuracy feedback
            _repDingLow = ProceduralToneGenerator.CreateDing("Rep_Low", 660f, 0.12f, 0.35f);
            _repDingMid = ProceduralToneGenerator.CreateDing("Rep_Mid", 880f, 0.15f, 0.4f);
            _repDingHigh = ProceduralToneGenerator.CreateDing("Rep_High", 1100f, 0.15f, 0.45f);

            // Exercise start (ascending two-note)
            _exerciseStartClip = ProceduralToneGenerator.CreateDing("ExStart", 523f, 0.25f, 0.35f);

            // Exercise complete (success chime)
            _exerciseCompleteClip = ProceduralToneGenerator.CreateSuccessChime("ExComplete", 0.5f, 0.4f);

            // Session complete (longer, richer fanfare)
            _sessionCompleteClip = ProceduralToneGenerator.CreateSuccessChime("SessionComplete", 0.8f, 0.45f);

            // Milestone (bright ding)
            _milestoneClip = ProceduralToneGenerator.CreateDing("Milestone", 1320f, 0.2f, 0.4f);

            Debug.Log("[ExerciseAudioCues] Exercise audio clips generated");
        }
    }
}
