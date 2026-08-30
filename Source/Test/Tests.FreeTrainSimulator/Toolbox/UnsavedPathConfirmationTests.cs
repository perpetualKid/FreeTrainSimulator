using System.Threading.Tasks;

using FreeTrainSimulator.Toolbox;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tests.FreeTrainSimulator.Toolbox
{
    [TestClass]
    public class UnsavedPathConfirmationTests
    {
        [TestMethod]
        public async Task WhenNoUnsavedChangesThenRouteChangeProceedsWithoutPrompt()
        {
            bool requested = false;

            bool confirmed = await UnsavedPathConfirmationEventArgs.RequestAsync(false, this, (_, _) => requested = true);

            Assert.IsTrue(confirmed);
            Assert.IsFalse(requested);
        }

        [TestMethod]
        public async Task WhenUnsavedChangesHaveNoConfirmationUiThenRouteChangeIsBlocked()
        {
            bool confirmed = await UnsavedPathConfirmationEventArgs.RequestAsync(true, this, null);

            Assert.IsFalse(confirmed);
        }

        [TestMethod]
        [DataRow(true)]
        [DataRow(false)]
        public async Task WhenUnsavedChangesArePromptedThenUserDecisionIsHonored(bool decision)
        {
            bool confirmed = await UnsavedPathConfirmationEventArgs.RequestAsync(true, this,
                (_, args) => args.Completion.SetResult(decision));

            Assert.AreEqual(decision, confirmed);
        }
    }
}