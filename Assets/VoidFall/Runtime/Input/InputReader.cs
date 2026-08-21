using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace VoidFall.Runtime
{
    /// <summary>
    /// Owns gameplay device polling. Produces the movement axis from keyboard,
    /// gamepad, or the floating touch joystick, in that precedence order.
    /// Touch joystick state lives here so HUD rendering and resets have one
    /// owner. Menu hotkeys remain in the runtime flow layer.
    /// </summary>
    public sealed class InputReader
    {
        private bool _touchActive;
        private bool _touchBlockedByUi;
        private Vector2 _touchOrigin;
        private Vector2 _touchAxis;

        public bool TouchActive => _touchActive;
        public Vector2 TouchOrigin => _touchOrigin;
        public Vector2 TouchAxis => _touchAxis;

        public void ResetTouch()
        {
            _touchActive = false;
            _touchBlockedByUi = false;
            _touchAxis = Vector2.zero;
        }

        public Vector2 ReadMoveAxis(float touchSizeScale)
        {
            var input = Vector2.zero;
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                input = KeyboardMoveAxis(
                    keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed,
                    keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed,
                    keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed,
                    keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed);
            }

            var gamepad = Gamepad.current;
            if (gamepad != null && input.sqrMagnitude < 0.01f) input = gamepad.leftStick.ReadValue();
            // Poll touch even when another device drives movement: the floating
            // joystick HUD reads this state every frame.
            var touchAxis = ReadTouchAxis(touchSizeScale);
            if (input.sqrMagnitude < 0.01f) input = touchAxis;
            if (input.sqrMagnitude > 1) input.Normalize();
            return input;
        }

        private Vector2 ReadTouchAxis(float touchSizeScale)
        {
            _touchActive = false;
            _touchAxis = Vector2.zero;
            var touchscreen = Touchscreen.current;
            var primaryTouch = touchscreen?.primaryTouch;
            if (primaryTouch == null || !primaryTouch.press.isPressed)
            {
                _touchBlockedByUi = false;
                return Vector2.zero;
            }

            var pointerId = primaryTouch.touchId.ReadValue();
            var pointerOverUi = EventSystem.current != null &&
                EventSystem.current.IsPointerOverGameObject(pointerId);
            _touchBlockedByUi = TouchJoystickBlockedByUi(
                primaryTouch.press.isPressed,
                primaryTouch.press.wasPressedThisFrame,
                pointerOverUi,
                _touchBlockedByUi);
            if (_touchBlockedByUi) return Vector2.zero;

            var position = primaryTouch.position.ReadValue();
            if (primaryTouch.press.wasPressedThisFrame) _touchOrigin = position;
            var delta = position - _touchOrigin;
            var maximum = 64f * Mathf.Clamp(touchSizeScale, 0.75f, 1.35f);
            var axis = TouchAxisFromDelta(delta, maximum, out var originShift);
            _touchOrigin += originShift;
            _touchActive = true;
            _touchAxis = axis;
            return _touchAxis;
        }

        private static Vector2 KeyboardMoveAxis(bool up, bool down, bool left, bool right)
        {
            var axis = new Vector2(
                (right ? 1 : 0) - (left ? 1 : 0),
                (up ? 1 : 0) - (down ? 1 : 0));
            if (axis.sqrMagnitude > 1f) axis.Normalize();
            return axis;
        }

        private static bool TouchJoystickBlockedByUi(
            bool touchPressed,
            bool touchStartedThisFrame,
            bool pointerOverUi,
            bool alreadyBlocked)
        {
            if (!touchPressed) return false;
            return alreadyBlocked || (touchStartedThisFrame && pointerOverUi);
        }

        private static Vector2 TouchAxisFromDelta(
            Vector2 delta,
            float maximum,
            out Vector2 originShift)
        {
            // Keep this in the same order as src/game/input.ts: a long swipe
            // recentres the floating base first, then reports a unit direction;
            // shorter movement uses the fixed radius and the 6px deadzone.
            originShift = Vector2.zero;
            maximum = Mathf.Max(0.001f, maximum);
            var length = delta.magnitude;
            if (length > maximum && length > 0.001f)
            {
                var direction = delta / length;
                originShift = direction * (length - maximum);
                return direction;
            }

            return length > 6f ? delta / maximum : Vector2.zero;
        }
    }
}
