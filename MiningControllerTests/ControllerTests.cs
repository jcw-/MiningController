using Microsoft.VisualStudio.TestTools.UnitTesting;
using MiningController.Mining;
using Moq;
using System;

namespace MiningControllerTests
{
    [TestClass]
    public class ControllerTests
    {
        private Controller controller;

        private Mock<IMinerCommunication> minerComms;

        [TestInitialize]
        public void TestInitialize()
        {
            if (this.controller != null || this.minerComms != null)
            {
                this.TestCleanup();
                if (this.controller != null || this.minerComms != null)
                {
                    throw new ArgumentException("Test cleanup does not appear to be cleaning up correctly - one or more variables were expected to be null.");
                }
            }
            
            this.minerComms = new Mock<IMinerCommunication>(MockBehavior.Strict);
            this.minerComms.SetupAllProperties();

            this.controller = new Controller(minerComms.Object, TimeSpan.FromMilliseconds(Int32.MaxValue));
        }

        [TestCleanup]
        public void TestCleanup()
        {
            this.controller = null;
            this.minerComms = null;
        }

        [TestMethod]
        public void ControllerTestDefaults()
        {
            Assert.AreEqual(controller.DesiredMinerStatus, MinerProcessStatus.Unknown);
            minerComms.Verify(v => v.ExecuteCommand(It.IsNotNull<MinerCommand>()), Times.Never);
            minerComms.Verify(v => v.LaunchMinerProcess(It.IsAny<bool>()), Times.Never);
        }

        [TestMethod]
        public void ControllerTestIntensity()
        {
            int intensity = 14;
            
            // setup the communication object to return a json response and raise the connected event when the controller requests the device listing
            var jsonDevices = @"{""STATUS"":[{""STATUS"":""S"",""When"":1392684133,""Code"":9,""Msg"":""1 GPU(s) - 0 ASC(s) - 0 PGA(s) - "",""Description"":""cgminer 3.7.2""}],""DEVS"":[{""GPU"":0,""Enabled"":""Y"",""Status"":""Alive"",""Temperature"":77.00,""Fan Speed"":2434,""Fan Percent"":63,""GPU Clock"":840,""Memory Clock"":1280,""GPU Voltage"":1.100,""GPU Activity"":98,""Powertune"":0,""MHS av"":0.36,""MHS 5s"":0.35,""Accepted"":1,""Rejected"":0,""Hardware Errors"":0,""Utility"":1.45,""Intensity"":""" + intensity + @""",""Last Share Pool"":0,""Last Share Time"":1392684105,""Total MH"":15.1224,""Diff1 Work"":225,""Difficulty Accepted"":196.00000000,""Difficulty Rejected"":0.00000000,""Last Share Difficulty"":196.00000000,""Last Valid Work"":1392684132,""Device Hardware%"":0.0000,""Device Rejected%"":0.0000,""Device Elapsed"":41}],""id"":1}";
            
            minerComms.Setup(s => s.ExecuteCommand(It.Is<MinerCommand>(c => c.Command == "devs"))).Returns(jsonDevices);

            Assert.AreEqual(controller.Intensity, intensity, "The intensity specified in the JSON response is expected to match what the controller returns for the intensity.");                        
        }

        [TestMethod]
        public void ControllerTestMinerLaunch()
        {
            // tell the mock to indicate that the miner process is not running, and then tell the controller we want it running,
            // this should trigger the controller to launch the miner
            minerComms.SetupGet(s => s.MinerProcessDetected).Returns(false);
            minerComms.Setup(s => s.LaunchMinerProcess(It.IsAny<bool>())).Returns(true);

            Assert.IsTrue(controller.DesiredMinerStatus != MinerProcessStatus.Running, "Unexpected state - the controller should not currently expect the miner to be running.");
            controller.DesiredMinerStatus = MinerProcessStatus.Running;
            minerComms.Verify(v => v.LaunchMinerProcess(It.IsAny<bool>()), Times.Once, "It is expected that the controller will attempt to launch the miner process when DesiredMinerStatus is set to Running and the miner process is not detected.");
        }

        [TestMethod]
        public void ControllerTestMinerStop()
        {
            // tell the mock to indicate that the miner process is running, and then tell the controller we want it stopped,
            // this should trigger the controller to send a quit command to the miner and raise a disconnected event
            minerComms.SetupGet(s => s.MinerProcessDetected).Returns(true);
            minerComms.Setup(s => s.ExecuteCommand(It.Is<MinerCommand>(c => c.Command == "quit"))).Returns(string.Empty).Verifiable("It is expected that the controller will request a miner shutdown when the DesiredMinerStatus is set to Stopped and the miner process is detected.");

            var disconnected = false;
            controller.Disconnected += (s, e) => { disconnected = true; };

            Assert.IsTrue(controller.DesiredMinerStatus != MinerProcessStatus.Stopped, "Unexpected state - the controller should not currently expect the miner to be running.");
            controller.DesiredMinerStatus = MinerProcessStatus.Stopped;
            Assert.IsTrue(disconnected, "It is expected that the controller will set the status to disconnected when stopping the miner.");
            minerComms.Verify(); // verify that the quit command was executed
        }
    }
}
