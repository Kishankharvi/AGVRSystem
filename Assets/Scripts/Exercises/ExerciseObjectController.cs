using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using TMPro;
using UnityEngine.UI;

namespace AGVRSystem.Exercises
{
    /// <summary>
    /// Controls realistic exercise object behaviour: deformation on grip, floating labels,
    /// mini progress bar, and auto-reset when objects fall out of bounds.
    /// </summary>
    [RequireComponent(typeof(XRGrabInteractable))]
    public class ExerciseObjectController : MonoBehaviour
    {
        [Header("Object Identity")]
        [SerializeField] private string _displayName = "Object";
        [SerializeField] private ExerciseObjectType _objectType = ExerciseObjectType.Ball;

        [Header("Deformation")]
        [SerializeField] private bool _enableDeformation = true;
        [SerializeField] private float _squishAmount = 0.15f;
        [SerializeField] private float _deformSpeed = 8f;

        [Header("Label")]
        [SerializeField] private bool _showLabel = true;
        [SerializeField] private float _labelHeight = 0.06f;
        [SerializeField] private Color _labelColor = new Color(1f, 1f, 1f, 0.7f);
        [SerializeField] private Color _progressBgColor = new Color(0.2f, 0.2f, 0.3f, 0.6f);
        [SerializeField] private Color _progressFillColor = new Color(0.2f, 0.8f, 0.5f, 0.85f);

        [Header("Reset")]
        [SerializeField] private float _fallThreshold = -2f;
        [SerializeField] private float _maxDistance = 3f;

        private XRGrabInteractable _grabInteractable;
        private Rigidbody _rb;
        private Vector3 _originalScale;
        private Vector3 _originalPosition;
        private Quaternion _originalRotation;
        private Vector3 _targetScale;
        private bool _isGrabbed;
        private float _gripStrength;

        // Label UI
        private Canvas _labelCanvas;
        private TMP_Text _nameLabel;
        private Image _progressBg;
        private Image _progressFill;
        private RectTransform _progressFillRect;
        private float _currentProgress;

        private Camera _mainCamera;

        private const float LabelFontSize = 1.8f;
        private const float ProgressBarWidth = 40f;
        private const float ProgressBarHeight = 4f;
        private const float CanvasScale = 0.005f;

        /// <summary>
        /// Type of exercise object, determines deformation behaviour.
        /// </summary>
        public enum ExerciseObjectType
        {
            Ball,       // Squishes uniformly
            Cylinder,   // Compresses along length
            Flat        // Minimal deformation
        }

        private void Awake()
        {
            _grabInteractable = GetComponent<XRGrabInteractable>();
            _rb = GetComponent<Rigidbody>();
            _originalScale = transform.localScale;
            _originalPosition = transform.localPosition;
            _originalRotation = transform.localRotation;
            _targetScale = _originalScale;

            _mainCamera = Camera.main;

            if (_grabInteractable != null)
            {
                _grabInteractable.selectEntered.AddListener(OnGrab);
                _grabInteractable.selectExited.AddListener(OnRelease);
            }

            if (_showLabel)
            {
                CreateFloatingLabel();
            }
        }

        private void Update()
        {
            // Deformation interpolation
            if (_enableDeformation)
            {
                transform.localScale = Vector3.Lerp(transform.localScale, _targetScale,
                    Time.deltaTime * _deformSpeed);
            }

            // Update grip deformation if grabbed
            if (_isGrabbed && _enableDeformation)
            {
                UpdateGripDeformation();
            }

            // Billboard label to face camera
            if (_labelCanvas != null && _mainCamera != null)
            {
                UpdateLabel();
            }

            // Auto-reset if fallen or too far
            CheckBounds();
        }

        private void OnGrab(SelectEnterEventArgs args)
        {
            _isGrabbed = true;
            _gripStrength = 0f;

            if (_nameLabel != null)
            {
                _nameLabel.color = new Color(_labelColor.r, _labelColor.g, _labelColor.b, 0.4f);
            }
        }

        private void OnRelease(SelectExitEventArgs args)
        {
            _isGrabbed = false;
            _gripStrength = 0f;

            // Restore original scale
            _targetScale = _originalScale;

            if (_nameLabel != null)
            {
                _nameLabel.color = _labelColor;
            }
        }

        private void UpdateGripDeformation()
        {
            // Simulate grip strength from hand tracking
            float grip = GetCurrentGripStrength();
            _gripStrength = Mathf.Lerp(_gripStrength, grip, Time.deltaTime * _deformSpeed);

            float squish = _gripStrength * _squishAmount;

            switch (_objectType)
            {
                case ExerciseObjectType.Ball:
                    // Ball squishes: flatten Y, expand XZ
                    _targetScale = new Vector3(
                        _originalScale.x * (1f + squish * 0.5f),
                        _originalScale.y * (1f - squish),
                        _originalScale.z * (1f + squish * 0.5f)
                    );
                    break;

                case ExerciseObjectType.Cylinder:
                    // Pen/cylinder: compress along local Y (length), slight bulge XZ
                    _targetScale = new Vector3(
                        _originalScale.x * (1f + squish * 0.3f),
                        _originalScale.y * (1f - squish * 0.1f),
                        _originalScale.z * (1f + squish * 0.3f)
                    );
                    break;

                case ExerciseObjectType.Flat:
                    // Coin: minimal deformation, slight flatten
                    _targetScale = new Vector3(
                        _originalScale.x * (1f + squish * 0.1f),
                        _originalScale.y * (1f - squish * 0.2f),
                        _originalScale.z * (1f + squish * 0.1f)
                    );
                    break;
            }
        }

        private float GetCurrentGripStrength()
        {
            if (HandTrackingManager.Instance == null)
                return 0.5f; // Default fallback for testing

            float leftGrip = 0f;
            float rightGrip = 0f;

            if (HandTrackingManager.Instance.LeftHand != null && HandTrackingManager.Instance.IsLeftTracked)
            {
                leftGrip = HandTrackingManager.Instance.GetHandGripStrength(
                    HandTrackingManager.Instance.LeftHand) / 100f;
            }

            if (HandTrackingManager.Instance.RightHand != null && HandTrackingManager.Instance.IsRightTracked)
            {
                rightGrip = HandTrackingManager.Instance.GetHandGripStrength(
                    HandTrackingManager.Instance.RightHand) / 100f;
            }

            return Mathf.Max(leftGrip, rightGrip);
        }

        /// <summary>
        /// Sets the exercise progress (0-1) shown on the mini progress bar.
        /// </summary>
        public void SetProgress(float progress)
        {
            _currentProgress = Mathf.Clamp01(progress);

            if (_progressFillRect != null)
            {
                _progressFillRect.anchorMax = new Vector2(_currentProgress, 1f);
            }
        }

        /// <summary>
        /// Updates the display name shown on the floating label.
        /// </summary>
        public void SetDisplayName(string name)
        {
            _displayName = name;
            if (_nameLabel != null)
            {
                _nameLabel.text = _displayName;
            }
        }

        private void CreateFloatingLabel()
        {
            // Create label root
            GameObject labelObj = new GameObject($"{_displayName}_Label");
            labelObj.transform.SetParent(transform, false);
            labelObj.transform.localPosition = Vector3.up * (_labelHeight / transform.localScale.y);

            // Canvas
            _labelCanvas = labelObj.AddComponent<Canvas>();
            _labelCanvas.renderMode = RenderMode.WorldSpace;

            RectTransform canvasRect = _labelCanvas.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(80, 24);
            canvasRect.localScale = Vector3.one * CanvasScale;

            // Name text
            GameObject textObj = new GameObject("NameText");
            textObj.transform.SetParent(labelObj.transform, false);
            _nameLabel = textObj.AddComponent<TextMeshProUGUI>();
            _nameLabel.text = _displayName;
            _nameLabel.fontSize = LabelFontSize;
            _nameLabel.color = _labelColor;
            _nameLabel.alignment = TextAlignmentOptions.Center;
            _nameLabel.fontStyle = FontStyles.Bold;
            _nameLabel.raycastTarget = false;
            _nameLabel.enableAutoSizing = false;

            RectTransform textRect = _nameLabel.GetComponent<RectTransform>();
            textRect.anchorMin = new Vector2(0, 0.4f);
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            // Progress bar background
            GameObject progressBgObj = new GameObject("ProgressBG");
            progressBgObj.transform.SetParent(labelObj.transform, false);
            _progressBg = progressBgObj.AddComponent<Image>();
            _progressBg.color = _progressBgColor;
            _progressBg.raycastTarget = false;

            RectTransform bgRect = _progressBg.GetComponent<RectTransform>();
            bgRect.anchorMin = new Vector2(0.15f, 0.05f);
            bgRect.anchorMax = new Vector2(0.85f, 0.3f);
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;

            // Progress bar fill
            GameObject progressFillObj = new GameObject("ProgressFill");
            progressFillObj.transform.SetParent(progressBgObj.transform, false);
            _progressFill = progressFillObj.AddComponent<Image>();
            _progressFill.color = _progressFillColor;
            _progressFill.raycastTarget = false;

            _progressFillRect = _progressFill.GetComponent<RectTransform>();
            _progressFillRect.anchorMin = Vector2.zero;
            _progressFillRect.anchorMax = new Vector2(0f, 1f); // Start at 0
            _progressFillRect.offsetMin = Vector2.zero;
            _progressFillRect.offsetMax = Vector2.zero;
        }

        private void UpdateLabel()
        {
            if (_labelCanvas == null || _mainCamera == null)
                return;

            // Billboard: face camera
            Vector3 dirToCamera = _mainCamera.transform.position - _labelCanvas.transform.position;
            if (dirToCamera.sqrMagnitude > 0.001f)
            {
                _labelCanvas.transform.rotation = Quaternion.LookRotation(-dirToCamera.normalized, Vector3.up);
            }
        }

        private void CheckBounds()
        {
            if (_isGrabbed)
                return;

            Vector3 worldPos = transform.position;
            Vector3 originWorld = transform.parent != null
                ? transform.parent.TransformPoint(_originalPosition)
                : _originalPosition;

            bool outOfBounds = worldPos.y < _fallThreshold
                || Vector3.Distance(worldPos, originWorld) > _maxDistance;

            if (outOfBounds)
            {
                ResetToOriginalPosition();
            }
        }

        /// <summary>
        /// Resets the object to its original position on the table.
        /// </summary>
        public void ResetToOriginalPosition()
        {
            if (_rb != null)
            {
                _rb.linearVelocity = Vector3.zero;
                _rb.angularVelocity = Vector3.zero;
            }

            transform.localPosition = _originalPosition;
            transform.localRotation = _originalRotation;
            transform.localScale = _originalScale;
            _targetScale = _originalScale;
        }

        private void OnDestroy()
        {
            if (_grabInteractable != null)
            {
                _grabInteractable.selectEntered.RemoveListener(OnGrab);
                _grabInteractable.selectExited.RemoveListener(OnRelease);
            }
        }
    }
}
