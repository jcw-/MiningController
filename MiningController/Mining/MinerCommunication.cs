using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Linq;
using System.Threading;

namespace MiningController.Mining
{
    public class MinerCommunication : IMinerCommunication
    {
        private const int PORT = 4028;
        private int consecutiveLaunches = 0;
        private const int MaxConsecutiveLaunches = 20;
        private string minerProcessName;
        private string launchCommand;
        private bool knownConnected = false;
        private IEnumerable<string> importantProcessNames;

        public event EventHandler<EventArgs> Connected = delegate { };

        public event EventHandler<MessageEventArgs> Message = delegate { };

        public MinerCommunication(string minerProcessName, string launchCommand, IEnumerable<string> importantProcessNames)
        {
            this.minerProcessName = minerProcessName;
            this.launchCommand = launchCommand;
            this.importantProcessNames = importantProcessNames.Select(n => n.ToLower());
        }

        public bool MinerProcessDetected
        {
            get
            {
                return System.Diagnostics.Process.GetProcessesByName(this.minerProcessName).Length > 0;
            }
        }

        public bool ImportantProcessDetected
        {
            get
            {
                var processNames = System.Diagnostics.Process.GetProcesses().Select(p => p.ProcessName.ToLower());
                return importantProcessNames.Any(n => processNames.Contains(n));
            }
        }

        public void KillMinerProcess()
        {
            this.consecutiveLaunches = 0; // reset consecutive launches

            var process = System.Diagnostics.Process.GetProcessesByName(this.minerProcessName).FirstOrDefault();
            if (process != null)
            {
                process.Kill();
            }
        }

        public bool LaunchMinerProcess(bool visible)
        {
            bool launched = false;

            if (File.Exists(this.launchCommand))
            {
                // protect against launching the miner indefinitely (which shouldn't ever happen, short of outside interference or a bug)
                if (this.consecutiveLaunches < MaxConsecutiveLaunches)
                {
                    this.consecutiveLaunches++;
                    this.Message(this, new MessageEventArgs() { Message = "Miner does not appear to be running - launching miner." });

                    var workingDir = Path.GetDirectoryName(this.launchCommand);
                    var psi = new ProcessStartInfo()
                    {
                        WorkingDirectory = workingDir,
                        FileName = this.launchCommand,
                        WindowStyle = visible ? ProcessWindowStyle.Normal : ProcessWindowStyle.Hidden
                    };

                    Process.Start(psi);

                    launched = true;
                }
                else
                {
                    throw new TooManyMinerLaunches(string.Format("Maximum consecutive launches [{0}] reached. There appears to be a problem that requires manual intervention. Correct the issue and then re-open this application to continue - no further action will automatically occur.", MaxConsecutiveLaunches));                    
                }
            }
            else
            {
                throw new ArgumentException("Unable to perform launch command: " + this.launchCommand);
            }

            return launched;
        }

        public string ExecuteCommand(MinerCommand command)
        {
            string response = string.Empty;
            byte[] byteBuffer = Encoding.ASCII.GetBytes(JsonConvert.SerializeObject(command));

            try
            {
                using (var client = new TcpClient())
                {
                    IAsyncResult ar = client.BeginConnect("localhost", PORT, null, null);

                    WaitHandle wh = ar.AsyncWaitHandle;
                    try
                    {
                        if (!ar.AsyncWaitHandle.WaitOne(TimeSpan.FromSeconds(5), false))
                        {
                            client.Close();
                            throw new TimeoutException();
                        }

                        client.EndConnect(ar);
                    }
                    finally
                    {
                        wh.Close();
                    }

                    var stream = client.GetStream();
                    stream.Write(byteBuffer, 0, byteBuffer.Length);

                    var responseBytes = ReadFully(stream);
                    if (responseBytes != null && responseBytes.Length > 0)
                    {
                        response = Encoding.ASCII.GetString(responseBytes, 0, responseBytes.Length);
                    }
                }
            }
            catch (Exception ex)
            {

                this.knownConnected = false;

                if (!(ex is SocketException))
                {
                    this.Message(this, new MessageEventArgs() { Message = "Unexpected error in ExecuteCommand: " + ex.ToString() });
                }

                return response;
            }

            if (command.Command != "quit")
            {
                this.consecutiveLaunches = 0; // reset consecutive launches

                if (!this.knownConnected)
                {
                    this.Connected(this, EventArgs.Empty);
                }

                this.knownConnected = true;
            }

            return response;
        }

        /// <summary>
        /// Reads data from a stream until the end is reached. The
        /// data is returned as a byte array. An IOException is
        /// thrown if any of the underlying IO calls fail.
        /// </summary>
        /// <param name="stream">The stream to read data from</param>
        private static byte[] ReadFully(Stream stream)
        {
            byte[] buffer = new byte[32768];
            using (MemoryStream ms = new MemoryStream())
            {
                while (true)
                {
                    int read = stream.Read(buffer, 0, buffer.Length);
                    if (read <= 0)
                        return ms.ToArray();
                    ms.Write(buffer, 0, read);
                }
            }
        }
    }
}
