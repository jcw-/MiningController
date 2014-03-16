using Microsoft.VisualStudio.TestTools.UnitTesting;
using MiningController;
using MiningController.Mining;
using MiningController.ViewModel;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MiningControllerTests
{
    [TestClass]
    public class MiningViewModelTests
    {
        private MiningViewModel vm;

        private Mock<IController> controller;

        private Mock<IIdleTimeProvider> idleTimeProvider;

        private Mock<ISummaryDataManager> summaryDataManager;

        private Mock<IWindowController> windowController;

        private Mock<IVersionService> versionService;

        [TestInitialize]
        public void TestInitialize()
        {
            if (this.vm != null || this.controller != null || this.idleTimeProvider != null)
            {
                this.TestCleanup();
                if (this.vm != null || this.controller != null || this.idleTimeProvider != null)
                {
                    throw new ArgumentException("Test cleanup does not appear to be cleaning up correctly - one or more variables were expected to be null.");
                }
            }

            this.controller = new Mock<IController>(MockBehavior.Strict);
            this.idleTimeProvider = new Mock<IIdleTimeProvider>(MockBehavior.Strict);
            this.summaryDataManager = new Mock<ISummaryDataManager>(MockBehavior.Strict);
            this.summaryDataManager.Setup(s => s.LoadDataAsync(It.IsAny<DateTime>(), It.IsAny<TimeSpan>(), It.IsAny<double>(), It.IsNotNull<Action<IEnumerable<SummaryData>>>()));
            this.summaryDataManager.SetupAllProperties();
            this.windowController = new Mock<IWindowController>(MockBehavior.Strict);
            this.windowController.Setup(s => s.SetWindowVisibilityByProcessName(It.IsAny<string>(), It.IsAny<bool>()));
            this.versionService = new Mock<IVersionService>(MockBehavior.Strict);
            this.versionService.Setup(s => s.IsUpdateAvailableAsync(It.IsNotNull<Action<bool>>()));
            this.versionService.SetupAllProperties();

            this.controller.SetupAllProperties();
            this.controller.Setup(s => s.ImportantProcessDetected).Returns(false);

            this.vm = new MiningViewModel(this.controller.Object, this.idleTimeProvider.Object, this.summaryDataManager.Object, this.windowController.Object, this.versionService.Object);
        }

        [TestCleanup]
        public void TestCleanup()
        {
            this.vm = null;
            this.controller = null;
            this.idleTimeProvider = null;
        }

        [TestMethod]
        public void VMTestDefaults()
        {
            Assert.IsTrue(vm.SnoozeDurations.Count() > 0);
            Assert.IsTrue(vm.Activity == UserActivity.Active);
            Assert.IsTrue(vm.IsUserActive);
            Assert.IsTrue(vm.SnoozeDurationRemaining == default(TimeSpan));
            Assert.IsFalse(vm.IsConnected);
        }

        [TestMethod]
        public void VMTestConnected()
        {
            controller.Raise(r => r.Connected += null, EventArgs.Empty);            
            Assert.IsTrue(vm.IsConnected);
            Assert.IsTrue(vm.Messages.Any(m => m.EndsWith("Connected")));
            
            controller.Raise(r => r.Disconnected += null, EventArgs.Empty);
            Assert.IsFalse(vm.IsConnected);
            Assert.IsTrue(vm.Messages.Any(m => m.EndsWith("Disconnected")));
        }

        [TestMethod]
        public void VMTestMessages()
        {
            controller.VerifySet(c => c.DesiredMinerStatus = MinerProcessStatus.Running, Times.Once);

            const string msg = "abc";
            Assert.IsTrue(vm.Messages.Count == 0, "No messages are expected in initial construction.");
            controller.Raise(r => r.Message += null, new MessageEventArgs() { Message = msg });
            Assert.IsTrue(vm.Messages.Count == 1 && vm.Messages.First().EndsWith(msg), "After raising the message event, the message is expected to be available in the messages collection.");
            
            vm.ClearCommand.Execute(null);
            Assert.IsTrue(vm.Messages.Count == 0, "After calling Clear, there should be no messages.");
        }

        [TestMethod]
        public void VMTestActivity()
        {
            var isActivityRaised = false;
            var isUserActiveRaised = false;

            vm.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == "Activity")
                {
                    isActivityRaised = true;
                }

                if (e.PropertyName == "IsUserActive")
                {
                    isUserActiveRaised = true;
                }
            };

            // change the Activity to a different value
            int state = (int)vm.Activity;
            state = (state == 0 && state != Enum.GetValues(typeof(UserActivity)).Length - 1) ? state + 1 : state - 1;
            vm.Activity = (UserActivity)state;

            Assert.IsTrue(isActivityRaised && isUserActiveRaised, "Two events are expected to be raised when user activity changes.");
            Assert.AreEqual(vm.IsUserActive, vm.Activity == UserActivity.Active);
            
#if DEBUG
            this.versionService.Verify(v => v.IsUpdateAvailableAsync(It.IsNotNull<Action<bool>>()), Times.Never, "The version service should not be invoked in the debug configuration.");
#else
            this.versionService.Verify(v => v.IsUpdateAvailableAsync(It.IsNotNull<Action<bool>>()), Times.Once, "The version service should be used to check for available updates.");
#endif
        }

        [TestMethod]
        public void VMTestClipboard()
        {   
            try
            {
                var originalClipboardText = System.Windows.Clipboard.GetText();
                const string msg = "zzz";
                vm.Messages.Add(msg);
                vm.CopyCommand.Execute(null);

                Assert.IsTrue(System.Windows.Clipboard.GetText().Contains(msg), "Clipboard is expected to contain the text copied to it.");

                // attempt to restore clipboard (only works if previous contents was text)
                System.Windows.Clipboard.SetData(System.Windows.DataFormats.Text, originalClipboardText);
            }
            catch (System.Runtime.InteropServices.COMException ex)
            {
                var CLIPBRD_E_CANT_OPEN = -2147221040;
                Assert.IsTrue(ex.ErrorCode == CLIPBRD_E_CANT_OPEN);
            }
        }

        [TestMethod]
        public void VMTestIdleAndActiveIntensity()
        {
            var origIntensity = controller.Object.Intensity;
            controller.Raise(r => r.Connected += null, EventArgs.Empty);
            Assert.IsTrue(vm.IsConnected);

            vm.IdleTime = TimeSpan.MaxValue;
            Assert.AreEqual(vm.Activity, UserActivity.Idle);
            Assert.AreEqual(vm.IsUserActive, vm.Activity == UserActivity.Active);

            var idleIntensity = controller.Object.Intensity;
            Assert.IsTrue(idleIntensity > origIntensity, "The intensity is expected to increase when idling.");

            vm.IdleTime = TimeSpan.Zero;
            Assert.AreEqual(vm.Activity, UserActivity.Active);
            Assert.IsTrue(controller.Object.Intensity < idleIntensity, "The intensity is expected to decrease when the user is active.");
            Assert.AreEqual(vm.IsUserActive, vm.Activity == UserActivity.Active);
        }

        [TestMethod]
        public void VMTestSnooze()
        {
            Assert.IsFalse(vm.IsSnoozeEnabled);

            var isSnoozeEnabledRaised = false;

            vm.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == "IsSnoozeEnabled")
                {
                    isSnoozeEnabledRaised = true;
                }
            };

            var snooze = TimeSpan.FromMinutes(10);
            vm.SnoozeDuration = snooze;
            vm.SnoozeCommand.Execute(null);

            Assert.AreEqual(vm.SnoozeDurationRemaining, snooze, "Snooze duration should match what was requested.");
            Assert.AreEqual(vm.Activity, UserActivity.SnoozeRequested);
            controller.VerifySet(v => v.DesiredMinerStatus = MinerProcessStatus.Stopped, Times.Once, "The desired miner status should be stopped when a snooze is requested.");
            Assert.IsTrue(vm.IsSnoozeEnabled);
            Assert.IsTrue(isSnoozeEnabledRaised, "The snooze request should raise the IsSnoozeEnabled notification in order to trigger the UI to refresh the appropriate bindings.");
        }
    }
}
