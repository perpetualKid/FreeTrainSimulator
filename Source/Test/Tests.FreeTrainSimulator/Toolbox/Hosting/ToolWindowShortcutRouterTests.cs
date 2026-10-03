using FreeTrainSimulator.Toolbox;
using FreeTrainSimulator.Toolbox.Hosting;
using FreeTrainSimulator.Toolbox.ViewModels;

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Xna.Framework.Input;

using WpfKey = System.Windows.Input.Key;

namespace Tests.FreeTrainSimulator.Toolbox.Hosting
{
    [TestClass]
    public class ToolWindowShortcutRouterTests
    {
        [TestMethod]
        [DataRow(Keys.F1, Keys.None, UserCommand.DisplayHelpWindow)]
        [DataRow(Keys.F1, Keys.LeftShift, UserCommand.DisplayHelpWindow)]
        [DataRow(Keys.F10, Keys.None, UserCommand.DisplaySettingsWindow)]
        [DataRow(Keys.F10, Keys.RightShift, UserCommand.DisplaySettingsWindow)]
        [DataRow(Keys.F11, Keys.None, UserCommand.DisplayLogWindow)]
        [DataRow(Keys.F11, Keys.LeftShift, UserCommand.DisplayLogWindow)]
        [DataRow(Keys.F8, Keys.None, UserCommand.DisplayTrainPathWindow)]
        [DataRow(Keys.F8, Keys.RightShift, UserCommand.DisplayTrainPathWindow)]
        public void WhenConfiguredShortcutIsPressedThenDeclaredCommandIsMatched(Keys key, Keys modifier, UserCommand expected)
        {
            _ = ToolWindowShortcutRouter.TryMatch(new KeyboardState(key, modifier), out UserCommand command);

            Assert.AreEqual(expected, command);
        }

        [TestMethod]
        [DataRow(Keys.F1, Keys.LeftControl)]
        [DataRow(Keys.F10, Keys.RightControl)]
        [DataRow(Keys.F11, Keys.LeftAlt)]
        [DataRow(Keys.F8, Keys.RightAlt)]
        [DataRow(Keys.F1, Keys.LeftWindows)]
        [DataRow(Keys.F5, Keys.None)]
        [DataRow(Keys.Tab, Keys.None)]
        public void WhenShortcutHasUnsupportedKeyOrModifierThenItIsNotMatched(Keys key, Keys modifier)
        {
            bool matched = ToolWindowShortcutRouter.TryMatch(new KeyboardState(key, modifier), out _);

            Assert.IsFalse(matched);
        }

        [TestMethod]
        [DataRow(UserCommand.DisplayHelpWindow)]
        [DataRow(UserCommand.DisplaySettingsWindow)]
        [DataRow(UserCommand.DisplayLogWindow)]
        [DataRow(UserCommand.DisplayTrainPathWindow)]
        public void WhenShortcutIsHandledThenOnlyItsPaneCommandExecutes(UserCommand command)
        {
            int executions = 0;
            UserCommand executed = default;
            RelayCommand help = new(_ => { executions++; executed = UserCommand.DisplayHelpWindow; });
            RelayCommand settings = new(_ => { executions++; executed = UserCommand.DisplaySettingsWindow; });
            RelayCommand log = new(_ => { executions++; executed = UserCommand.DisplayLogWindow; });
            RelayCommand trainPath = new(_ => { executions++; executed = UserCommand.DisplayTrainPathWindow; });

            _ = ToolWindowShortcutRouter.TryHandle(command, false, help, settings, log, trainPath);

            Assert.AreEqual((command, 1), (executed, executions));
        }

        [TestMethod]
        public void WhenPaneCommandIsUnavailableThenShortcutIsNotConsumed()
        {
            RelayCommand unavailable = new(_ => Assert.Fail("Unavailable pane command executed."), _ => false);

            bool handled = ToolWindowShortcutRouter.TryHandle(UserCommand.DisplaySettingsWindow, false, null, unavailable, null, null);

            Assert.IsFalse(handled);
        }

        [TestMethod]
        public void WhenPaneCommandIsMissingThenShortcutIsNotConsumed()
        {
            bool handled = ToolWindowShortcutRouter.TryHandle(UserCommand.DisplayHelpWindow, false, null, null, null, null);

            Assert.IsFalse(handled);
        }

        [TestMethod]
        public void WhenCommandIsNotAShellShortcutThenNoPaneCommandExecutes()
        {
            RelayCommand toggle = new(_ => Assert.Fail("Unrelated command executed a pane toggle."));

            bool handled = ToolWindowShortcutRouter.TryHandle(UserCommand.PathEditorUndo, false, toggle, toggle, toggle, toggle);

            Assert.IsFalse(handled);
        }

        [TestMethod]
        public void WhenMatchedShortcutRepeatsThenItIsConsumedWithoutAnotherToggle()
        {
            RelayCommand toggle = new(_ => Assert.Fail("Repeated shortcut executed a pane toggle."));

            bool handled = ToolWindowShortcutRouter.TryHandle(UserCommand.DisplaySettingsWindow, true, null, toggle, null, null);

            Assert.IsTrue(handled);
        }

        [TestMethod]
        [DataRow(WpfKey.F1, Keys.F1)]
        [DataRow(WpfKey.F10, Keys.F10)]
        [DataRow(WpfKey.F11, Keys.F11)]
        [DataRow(WpfKey.F8, Keys.F8)]
        public void WhenWpfShortcutReceivesTwoDownsThenBothAreConsumedWithOneToggle(WpfKey key, Keys virtualKey)
        {
            ToolWindowShortcutPressTracker tracker = new();
            int executions = 0;
            RelayCommand toggle = new(_ => executions++);

            bool firstHandled = tracker.TryHandleKeyDown(key, new KeyboardState(virtualKey), true, toggle, toggle, toggle, toggle);
            bool secondHandled = tracker.TryHandleKeyDown(key, new KeyboardState(virtualKey), true, toggle, toggle, toggle, toggle);

            Assert.AreEqual((true, true, 1), (firstHandled, secondHandled, executions));
        }

        [TestMethod]
        [DataRow(WpfKey.F1, Keys.F1)]
        [DataRow(WpfKey.F10, Keys.F10)]
        [DataRow(WpfKey.F11, Keys.F11)]
        [DataRow(WpfKey.F8, Keys.F8)]
        public void WhenShiftIsPressedBetweenWpfShortcutDownsThenHeldShortcutDoesNotRetoggle(WpfKey key, Keys virtualKey)
        {
            ToolWindowShortcutPressTracker tracker = new();
            int executions = 0;
            RelayCommand toggle = new(_ => executions++);

            _ = tracker.TryHandleKeyDown(key, new KeyboardState(virtualKey), true, toggle, toggle, toggle, toggle);
            _ = tracker.TryHandleKeyDown(WpfKey.LeftShift, new KeyboardState(Keys.LeftShift), true, toggle, toggle, toggle, toggle);
            bool handled = tracker.TryHandleKeyDown(key, new KeyboardState(virtualKey, Keys.LeftShift), true, toggle, toggle, toggle, toggle);

            Assert.AreEqual((true, 1), (handled, executions));
        }

        [TestMethod]
        public void WhenShiftIsReleasedBetweenWpfShortcutDownsThenHeldShortcutDoesNotRetoggle()
        {
            ToolWindowShortcutPressTracker tracker = new();
            int executions = 0;
            RelayCommand toggle = new(_ => executions++);

            _ = tracker.TryHandleKeyDown(WpfKey.F10, new KeyboardState(Keys.F10, Keys.LeftShift), true, null, toggle, null, null);
            tracker.ReleaseKey(WpfKey.LeftShift);
            bool handled = tracker.TryHandleKeyDown(WpfKey.F10, new KeyboardState(Keys.F10), true, null, toggle, null, null);

            Assert.AreEqual((true, 1), (handled, executions));
        }

        [TestMethod]
        [DataRow(WpfKey.F1, Keys.F1)]
        [DataRow(WpfKey.F10, Keys.F10)]
        [DataRow(WpfKey.F11, Keys.F11)]
        [DataRow(WpfKey.F8, Keys.F8)]
        public void WhenWpfShortcutIsReleasedAndRepressedThenItTogglesAgain(WpfKey key, Keys virtualKey)
        {
            ToolWindowShortcutPressTracker tracker = new();
            int executions = 0;
            RelayCommand toggle = new(_ => executions++);

            _ = tracker.TryHandleKeyDown(key, new KeyboardState(virtualKey), true, toggle, toggle, toggle, toggle);
            tracker.ReleaseKey(key);
            bool handled = tracker.TryHandleKeyDown(key, new KeyboardState(virtualKey), true, toggle, toggle, toggle, toggle);

            Assert.AreEqual((true, 2), (handled, executions));
        }

        [TestMethod]
        public void WhenUnsupportedModifierIsRemovedWhileWpfShortcutIsHeldThenItDoesNotBecomeANewPress()
        {
            ToolWindowShortcutPressTracker tracker = new();
            RelayCommand toggle = new(_ => Assert.Fail("Held key became a new shortcut after Control was released."));

            bool firstHandled = tracker.TryHandleKeyDown(WpfKey.F10, new KeyboardState(Keys.F10, Keys.LeftControl), true, null, toggle, null, null);
            tracker.ReleaseKey(WpfKey.LeftCtrl);
            bool secondHandled = tracker.TryHandleKeyDown(WpfKey.F10, new KeyboardState(Keys.F10), true, null, toggle, null, null);

            Assert.AreEqual((false, true), (firstHandled, secondHandled));
        }

        [TestMethod]
        public void WhenWpfShortcutDownIsIneligibleThenItIsStillTrackedUntilRelease()
        {
            ToolWindowShortcutPressTracker tracker = new();
            RelayCommand toggle = new(_ => Assert.Fail("Held key toggled after routing became eligible."));

            bool firstHandled = tracker.TryHandleKeyDown(WpfKey.F10, new KeyboardState(Keys.F10), false, null, toggle, null, null);
            bool secondHandled = tracker.TryHandleKeyDown(WpfKey.F10, new KeyboardState(Keys.F10), true, null, toggle, null, null);

            Assert.AreEqual((false, true), (firstHandled, secondHandled));
        }

        [TestMethod]
        public void WhenAnotherWpfShortcutIsPressedThenEachPhysicalKeyTogglesOnce()
        {
            ToolWindowShortcutPressTracker tracker = new();
            int helpExecutions = 0;
            int settingsExecutions = 0;
            RelayCommand help = new(_ => helpExecutions++);
            RelayCommand settings = new(_ => settingsExecutions++);

            _ = tracker.TryHandleKeyDown(WpfKey.F10, new KeyboardState(Keys.F10), true, help, settings, null, null);
            _ = tracker.TryHandleKeyDown(WpfKey.F1, new KeyboardState(Keys.F1), true, help, settings, null, null);
            _ = tracker.TryHandleKeyDown(WpfKey.F10, new KeyboardState(Keys.F10), true, help, settings, null, null);

            Assert.AreEqual((1, 1), (helpExecutions, settingsExecutions));
        }

        [TestMethod]
        public void WhenWpfPressTrackingIsResetThenMissingReleaseDoesNotLeaveShortcutStuck()
        {
            ToolWindowShortcutPressTracker tracker = new();
            int executions = 0;
            RelayCommand toggle = new(_ => executions++);

            _ = tracker.TryHandleKeyDown(WpfKey.F10, new KeyboardState(Keys.F10), true, null, toggle, null, null);
            tracker.Reset();
            _ = tracker.TryHandleKeyDown(WpfKey.F10, new KeyboardState(Keys.F10), true, null, toggle, null, null);

            Assert.AreEqual(2, executions);
        }

        [TestMethod]
        [DataRow(Keys.F1, UserCommand.DisplayHelpWindow)]
        [DataRow(Keys.F10, UserCommand.DisplaySettingsWindow)]
        [DataRow(Keys.F11, UserCommand.DisplayLogWindow)]
        [DataRow(Keys.F8, UserCommand.DisplayTrainPathWindow)]
        public void WhenShortcutKeyIsNewlyPressedThenItIsAPhysicalPress(Keys key, UserCommand command)
        {
            bool pressed = ToolWindowShortcutRouter.IsNewPress(command, new KeyboardState(key), new KeyboardState());

            Assert.IsTrue(pressed);
        }

        [TestMethod]
        [DataRow(Keys.F1, UserCommand.DisplayHelpWindow)]
        [DataRow(Keys.F10, UserCommand.DisplaySettingsWindow)]
        [DataRow(Keys.F11, UserCommand.DisplayLogWindow)]
        [DataRow(Keys.F8, UserCommand.DisplayTrainPathWindow)]
        public void WhenShiftIsPressedWhileShortcutKeyIsHeldThenItIsNotAnotherPhysicalPress(Keys key, UserCommand command)
        {
            bool pressed = ToolWindowShortcutRouter.IsNewPress(command, new KeyboardState(key, Keys.LeftShift), new KeyboardState(key));

            Assert.IsFalse(pressed);
        }

        [TestMethod]
        [DataRow(Keys.F1, UserCommand.DisplayHelpWindow)]
        [DataRow(Keys.F10, UserCommand.DisplaySettingsWindow)]
        [DataRow(Keys.F11, UserCommand.DisplayLogWindow)]
        [DataRow(Keys.F8, UserCommand.DisplayTrainPathWindow)]
        public void WhenShiftIsReleasedWhileShortcutKeyIsHeldThenItIsNotAnotherPhysicalPress(Keys key, UserCommand command)
        {
            bool pressed = ToolWindowShortcutRouter.IsNewPress(command, new KeyboardState(key), new KeyboardState(key, Keys.LeftShift));

            Assert.IsFalse(pressed);
        }
    }
}
