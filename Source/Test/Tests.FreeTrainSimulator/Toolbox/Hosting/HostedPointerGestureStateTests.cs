using System.Windows.Forms;

using FreeTrainSimulator.Toolbox.Hosting;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tests.FreeTrainSimulator.Toolbox.Hosting
{
    [TestClass]
    public class HostedPointerGestureStateTests
    {
        private static readonly bool[] expectedtruetruetruefalse = new[] { true, true, true, false };
        private static readonly bool[] expectedtruetruetrue = new[] { true, true, true };
        private static readonly bool[] expectedfalsefalsefalsefalse = new[] { false, false, false, false };
        private static readonly bool[] expectedfalsetruetrue = new[] { false, true, true };
        private static readonly bool[] expectedfalsefalse = new[] { false, false };
        private static readonly bool[] expectedfalsetruetruefalsefalse = new[] { false, true, true, false, false };
        private static readonly bool[] expectedfalsefalsefalse = new[] { false, false, false };
        private static readonly bool[] expectedfalsetrue = new[] { false, true };
        private static readonly bool[] expectedtruetruefalse = new[] { true, true, false };
        private static readonly bool[] expectedfalsetruetruetruefalse = new[] { false, true, true, true, false };


        [TestMethod]
        [DataRow(MouseButtons.Left)]
        [DataRow(MouseButtons.Right)]
        public void WhenAForeignDragEntersTheMapThenItIsCapturedThroughItsReleaseFrame(MouseButtons button)
        {
            HostedPointerGestureState state = new();

            bool foreignPress = Poll(state, button);
            bool dragEnteringMap = Poll(state, button);
            state.RecordNativeRelease(button);
            bool releaseOverMap = Poll(state, MouseButtons.None);
            bool idleAfterRelease = Poll(state, MouseButtons.None);

            Assert.AreSequenceEqual(expectedtruetruetruefalse, new[] { foreignPress, dragEnteringMap, releaseOverMap, idleAfterRelease });
        }

        [TestMethod]
        [DataRow(MouseButtons.Left)]
        [DataRow(MouseButtons.Right)]
        public void WhenAForeignPressIsFirstPolledInsideTheMapThenItsDragAndReleaseAreCaptured(MouseButtons button)
        {
            HostedPointerGestureState state = new();

            bool dragEnteringMap = Poll(state, button);
            bool heldInsideMap = Poll(state, button);
            bool releaseInsideMap = Poll(state, MouseButtons.None);

            Assert.AreSequenceEqual(expectedtruetruetrue, new[] { dragEnteringMap, heldInsideMap, releaseInsideMap });
        }

        [TestMethod]
        [DataRow(MouseButtons.Left)]
        [DataRow(MouseButtons.Right)]
        public void WhenANativeGestureIsPolledThenItsPressHoldAndReleaseAreAccepted(MouseButtons button)
        {
            HostedPointerGestureState state = new();
            state.RecordNativePress(button);

            bool press = Poll(state, button);
            bool held = Poll(state, button);
            state.RecordNativeRelease(button);
            bool release = Poll(state, MouseButtons.None);
            bool idle = Poll(state, MouseButtons.None);

            Assert.AreSequenceEqual(expectedfalsefalsefalsefalse, new[] { press, held, release, idle });
        }

        [TestMethod]
        [DataRow(MouseButtons.Left)]
        [DataRow(MouseButtons.Right)]
        public void WhenANativeReleasePrecedesTheFirstPollThenItsReleaseFrameIsAccepted(MouseButtons button)
        {
            HostedPointerGestureState state = new();
            state.RecordNativePress(button);
            state.RecordNativeRelease(button);

            bool captured = state.BeginPolling(MouseButtons.None);
            state.CompletePolling(MouseButtons.None);

            Assert.IsFalse(captured);
        }

        [TestMethod]
        [DataRow(MouseButtons.Left)]
        [DataRow(MouseButtons.Right)]
        public void WhenNativeReleaseCompletesThenANewForeignPressDoesNotInheritOwnership(MouseButtons button)
        {
            HostedPointerGestureState state = new();
            state.RecordNativePress(button);
            _ = Poll(state, button);
            state.RecordNativeRelease(button);

            bool release = Poll(state, MouseButtons.None);
            bool foreignPress = Poll(state, button);
            bool foreignRelease = Poll(state, MouseButtons.None);

            Assert.AreSequenceEqual(expectedfalsetruetrue, new[] { release, foreignPress, foreignRelease });
        }

        [TestMethod]
        [DataRow(MouseButtons.Left)]
        [DataRow(MouseButtons.Right)]
        public void WhenPollingMissesANativeReleaseThenANewForeignHeldButtonIsCaptured(MouseButtons button)
        {
            HostedPointerGestureState state = new();
            state.RecordNativePress(button);
            _ = Poll(state, button);
            state.RecordNativeRelease(button);

            bool newForeignPress = Poll(state, button);
            bool foreignHeld = Poll(state, button);
            bool foreignRelease = Poll(state, MouseButtons.None);
            bool idle = Poll(state, MouseButtons.None);

            Assert.AreSequenceEqual(expectedtruetruetruefalse, new[] { newForeignPress, foreignHeld, foreignRelease, idle });
        }

        [TestMethod]
        [DataRow(MouseButtons.Left)]
        [DataRow(MouseButtons.Right)]
        public void WhenANewNativePressFollowsAnUnpolledReleaseThenNewOwnershipIsAccepted(MouseButtons button)
        {
            HostedPointerGestureState state = new();
            state.RecordNativePress(button);
            _ = Poll(state, button);
            state.RecordNativeRelease(button);
            state.RecordNativePress(button);

            bool newNativePress = Poll(state, button);
            state.RecordNativeRelease(button);
            bool release = Poll(state, MouseButtons.None);

            Assert.AreSequenceEqual(expectedfalsefalse, new[] { newNativePress, release });
        }

        [TestMethod]
        [DataRow(MouseButtons.Left, MouseButtons.Right)]
        [DataRow(MouseButtons.Right, MouseButtons.Left)]
        public void WhenAForeignButtonJoinsANativeDragThenBothGesturesStayCancelledThroughRelease(MouseButtons nativeButton, MouseButtons foreignButton)
        {
            HostedPointerGestureState state = new();
            state.RecordNativePress(nativeButton);
            bool nativePress = Poll(state, nativeButton);

            bool mixedDrag = Poll(state, nativeButton | foreignButton);
            state.RecordNativeRelease(foreignButton);
            bool remainingNativeDrag = Poll(state, nativeButton);
            state.RecordNativeRelease(nativeButton);
            bool release = Poll(state, MouseButtons.None);
            bool idle = Poll(state, MouseButtons.None);

            Assert.AreSequenceEqual(expectedfalsetruetruetruefalse, new[] { nativePress, mixedDrag, remainingNativeDrag, release, idle });
        }

        [TestMethod]
        [DataRow(MouseButtons.Left, MouseButtons.Right)]
        [DataRow(MouseButtons.Right, MouseButtons.Left)]
        public void WhenANativeButtonJoinsAForeignDragThenNeitherButtonBecomesAnAcceptedGesture(MouseButtons nativeButton, MouseButtons foreignButton)
        {
            HostedPointerGestureState state = new();
            _ = Poll(state, foreignButton);
            state.RecordNativePress(nativeButton);

            bool mixedDrag = Poll(state, nativeButton | foreignButton);
            bool remainingForeignDrag = Poll(state, foreignButton);
            bool release = Poll(state, MouseButtons.None);

            Assert.AreSequenceEqual(expectedtruetruetrue, new[] { mixedDrag, remainingForeignDrag, release });
        }

        [TestMethod]
        [DataRow(MouseButtons.Left)]
        [DataRow(MouseButtons.Right)]
        public void WhenOwnershipIsResetWhileHeldThenTheDragAndReleaseStayCaptured(MouseButtons button)
        {
            HostedPointerGestureState state = new();
            state.RecordNativePress(button);
            _ = Poll(state, button);
            state.Reset();

            bool held = Poll(state, button);
            state.RecordNativeRelease(button);
            bool release = Poll(state, MouseButtons.None);
            bool idle = Poll(state, MouseButtons.None);

            Assert.AreSequenceEqual(expectedtruetruefalse, new[] { held, release, idle });
        }

        [TestMethod]
        [DataRow(MouseButtons.Left)]
        [DataRow(MouseButtons.Right)]
        public void WhenOwnershipIsResetBeforeTheReleasePollThenThePendingReleaseIsCaptured(MouseButtons button)
        {
            HostedPointerGestureState state = new();
            state.RecordNativePress(button);
            _ = Poll(state, button);
            state.RecordNativeRelease(button);
            state.Reset();

            bool release = state.BeginPolling(MouseButtons.None);
            state.CompletePolling(MouseButtons.None);

            Assert.IsTrue(release);
        }

        [TestMethod]
        [DataRow(MouseButtons.Left)]
        [DataRow(MouseButtons.Right)]
        public void WhenAResetGestureEndsThenAFreshNativeGestureIsAccepted(MouseButtons button)
        {
            HostedPointerGestureState state = new();
            state.RecordNativePress(button);
            _ = Poll(state, button);
            state.Reset();
            _ = Poll(state, MouseButtons.None);
            state.RecordNativePress(button);

            bool recoveredPress = Poll(state, button);
            bool recoveredHold = Poll(state, button);
            state.RecordNativeRelease(button);
            bool recoveredRelease = Poll(state, MouseButtons.None);

            Assert.AreSequenceEqual(expectedfalsefalsefalse, new[] { recoveredPress, recoveredHold, recoveredRelease });
        }

        [TestMethod]
        [DataRow(MouseButtons.Left, MouseButtons.Right)]
        [DataRow(MouseButtons.Right, MouseButtons.Left)]
        public void WhenAMixedGestureEndsThenAFreshNativePressRecovers(MouseButtons nativeButton, MouseButtons foreignButton)
        {
            HostedPointerGestureState state = new();
            state.RecordNativePress(nativeButton);
            _ = Poll(state, nativeButton | foreignButton);
            _ = Poll(state, MouseButtons.None);
            state.RecordNativePress(nativeButton);

            bool press = Poll(state, nativeButton);
            state.RecordNativeRelease(nativeButton);
            bool release = Poll(state, MouseButtons.None);

            Assert.AreSequenceEqual(expectedfalsefalse, new[] { press, release });
        }

        [TestMethod]
        [DataRow(MouseButtons.Left)]
        [DataRow(MouseButtons.Right)]
        public void WhenPolledReleaseHasNoNativeMessageThenOwnershipStillRetires(MouseButtons button)
        {
            HostedPointerGestureState state = new();
            state.RecordNativePress(button);
            _ = Poll(state, button);

            bool release = Poll(state, MouseButtons.None);
            bool foreignPress = Poll(state, button);

            Assert.AreSequenceEqual(expectedfalsetrue, new[] { release, foreignPress });
        }

        [TestMethod]
        public void WhenBothButtonsHaveNativeOwnershipThenTheirStaggeredReleasesAreAccepted()
        {
            HostedPointerGestureState state = new();
            state.RecordNativePress(MouseButtons.Left);
            state.RecordNativePress(MouseButtons.Right);

            bool bothHeld = Poll(state, MouseButtons.Left | MouseButtons.Right);
            state.RecordNativeRelease(MouseButtons.Left);
            bool rightHeld = Poll(state, MouseButtons.Right);
            state.RecordNativeRelease(MouseButtons.Right);
            bool release = Poll(state, MouseButtons.None);

            Assert.AreSequenceEqual(expectedfalsefalsefalse, new[] { bothHeld, rightHeld, release });
        }

        [TestMethod]
        [DataRow(MouseButtons.Middle)]
        [DataRow(MouseButtons.XButton1)]
        [DataRow(MouseButtons.XButton2)]
        public void WhenOnlyANonGestureButtonIsHeldThenPollingIsNotCaptured(MouseButtons button)
        {
            HostedPointerGestureState state = new();

            bool captured = Poll(state, button);

            Assert.IsFalse(captured);
        }

        [TestMethod]
        public void WhenIdleOwnershipIsResetThenIdlePollingRemainsAccepted()
        {
            HostedPointerGestureState state = new();
            state.Reset();
            state.Reset();

            bool captured = Poll(state, MouseButtons.None);

            Assert.IsFalse(captured);
        }

        private static bool Poll(HostedPointerGestureState state, MouseButtons buttons)
        {
            bool captured = state.BeginPolling(buttons);
            state.CompletePolling(buttons);
            return captured;
        }
    }
}
