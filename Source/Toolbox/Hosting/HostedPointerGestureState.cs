using System.Windows.Forms;

namespace FreeTrainSimulator.Toolbox.Hosting
{
    /// <summary>
    /// Tracks native map gesture ownership independently of the globally polled pointer position.
    /// </summary>
    internal sealed class HostedPointerGestureState
    {
        private const MouseButtons gestureButtons = MouseButtons.Left | MouseButtons.Right;
        private MouseButtons ownedButtons;
        private MouseButtons rejectedButtons;
        private MouseButtons releasedNativeButtons;

        /// <summary>
        /// Grants ownership only for a button-down message received by the native map.
        /// </summary>
        internal void RecordNativePress(MouseButtons buttons)
        {
            buttons &= gestureButtons;
            ownedButtons |= buttons;
            rejectedButtons &= ~buttons;
            releasedNativeButtons &= ~buttons;
        }

        /// <summary>
        /// Marks a native release without retiring ownership before the polled release callbacks.
        /// </summary>
        internal void RecordNativeRelease(MouseButtons buttons)
        {
            releasedNativeButtons |= buttons & ownedButtons;
        }

        /// <summary>
        /// Rejects foreign gestures before the shared poller can synthesize map commands.
        /// </summary>
        internal bool BeginPolling(MouseButtons buttons)
        {
            // A released native button held again without another native down belongs to a new,
            // foreign gesture, even if polling missed the intervening release.
            if ((buttons & releasedNativeButtons) != MouseButtons.None)
                Reset();

            rejectedButtons |= buttons & gestureButtons & ~ownedButtons;
            if (rejectedButtons == MouseButtons.None)
                return false;

            // Capturing the poller resets its button history. Do not resume another held button as a
            // fresh map press after a mixed foreign/native gesture ends.
            Reset();
            return true;
        }

        /// <summary>
        /// Retires released buttons after polling has delivered any owned release callbacks.
        /// </summary>
        internal void CompletePolling(MouseButtons buttons)
        {
            ownedButtons &= buttons;
            rejectedButtons &= buttons;
            releasedNativeButtons &= buttons;
        }

        /// <summary>
        /// Invalidates ownership without allowing its eventual release to author a path edit.
        /// </summary>
        internal void Reset()
        {
            rejectedButtons |= ownedButtons;
            ownedButtons = MouseButtons.None;
            releasedNativeButtons = MouseButtons.None;
        }
    }
}
