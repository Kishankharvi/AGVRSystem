using UnityEngine;
using TMPro;

namespace AGVRSystem.UI
{
    /// <summary>
    /// Handles keyboard input for TMP_InputField in VR.
    /// Opens the system overlay keyboard on Quest when the input field is selected,
    /// and syncs text between the overlay keyboard and the input field.
    /// Also supports physical/Bluetooth keyboard input.
    /// </summary>
    [RequireComponent(typeof(TMP_InputField))]
    public class VRKeyboardHandler : MonoBehaviour
    {
        [Header("Keyboard Settings")]
        [SerializeField] private TouchScreenKeyboardType _keyboardType = TouchScreenKeyboardType.Default;
        [SerializeField] private int _maxCharacters = 50;
        [SerializeField] private string _placeholder = "Enter text...";

        private TMP_InputField _inputField;
        private TouchScreenKeyboard _overlayKeyboard;
        private bool _isKeyboardOpen;

        private void Awake()
        {
            _inputField = GetComponent<TMP_InputField>();
        }

        private void OnEnable()
        {
            if (_inputField != null)
            {
                _inputField.onSelect.AddListener(OnInputFieldSelected);
                _inputField.onDeselect.AddListener(OnInputFieldDeselected);
            }
        }

        private void OnDisable()
        {
            if (_inputField != null)
            {
                _inputField.onSelect.RemoveListener(OnInputFieldSelected);
                _inputField.onDeselect.RemoveListener(OnInputFieldDeselected);
            }

            CloseKeyboard();
        }

        private void Update()
        {
            if (!_isKeyboardOpen || _overlayKeyboard == null)
                return;

            // Sync keyboard text to input field
            if (_overlayKeyboard.status == TouchScreenKeyboard.Status.Visible)
            {
                string keyboardText = _overlayKeyboard.text;
                if (_inputField.text != keyboardText)
                {
                    _inputField.text = keyboardText;
                }
            }
            else if (_overlayKeyboard.status == TouchScreenKeyboard.Status.Done
                     || _overlayKeyboard.status == TouchScreenKeyboard.Status.Canceled
                     || _overlayKeyboard.status == TouchScreenKeyboard.Status.LostFocus)
            {
                if (_overlayKeyboard.status == TouchScreenKeyboard.Status.Done)
                {
                    _inputField.text = _overlayKeyboard.text;
                }

                CloseKeyboard();
            }
        }

        private void OnInputFieldSelected(string text)
        {
            OpenKeyboard();
        }

        private void OnInputFieldDeselected(string text)
        {
            // Don't immediately close - let the keyboard finish
        }

        /// <summary>
        /// Opens the system overlay keyboard on Quest.
        /// </summary>
        private void OpenKeyboard()
        {
            if (_isKeyboardOpen)
                return;

            _overlayKeyboard = TouchScreenKeyboard.Open(
                _inputField.text,
                _keyboardType,
                false, // autocorrection
                false, // multiline
                false, // secure
                false, // alert
                _placeholder,
                _maxCharacters);

            _isKeyboardOpen = true;

            Debug.Log("[VRKeyboardHandler] Overlay keyboard opened");
        }

        /// <summary>
        /// Closes the overlay keyboard.
        /// </summary>
        private void CloseKeyboard()
        {
            if (_overlayKeyboard != null && _overlayKeyboard.status == TouchScreenKeyboard.Status.Visible)
            {
                _overlayKeyboard.active = false;
            }

            _overlayKeyboard = null;
            _isKeyboardOpen = false;
        }
    }
}
