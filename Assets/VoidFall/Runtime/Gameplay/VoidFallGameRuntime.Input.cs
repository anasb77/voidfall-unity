using System;
using System.Buffers;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using VoidFall.Core;
using VoidFall.Persistence;
using VoidFall.Runtime.Rendering;
using VoidFall.UI;
namespace VoidFall.Runtime
{
    public sealed partial class VoidFallGameRuntime
    {

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
