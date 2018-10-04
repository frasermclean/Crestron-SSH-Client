using System;
using System.Text;
using System.Text.RegularExpressions;
using Crestron.SimplSharp;                          				
using Crestron.SimplSharp.Ssh;
using Crestron.SimplSharp.Ssh.Common;
using System.Collections.Generic;

namespace SSHClient
{
    public delegate void CommandEventHandler(SimplSharpString StringVal);
    public delegate void StateChangeHandler(ushort State);

    public class SSHClientDevice
    {
        private ConnectionInfo SshConnectionInfo = null;
        private KeyboardInteractiveAuthenticationMethod SshAuthMethod = null;

        private String SshUsername = "";
        private String SshHost = "";
        private String SshPassword = "";

        private SshClient SshClient;
        private ShellStream SshStream;

        public CommandEventHandler SshRxDataToSimpl { get; set; }
        public StateChangeHandler SshStateChangeToSimpl { get; set; }

        public ushort Debug = 0;

        private bool sshState = false;   
        public bool SshState
        {
            get
            {
                return sshState;
            }
            set
            {
                sshState = value;
                SshStateChangeToSimpl(Convert.ToUInt16(value == true ? 1 : 0));

                if (value == false)
                {
                    Disconnect();
                }
            }
        }

        public ushort Connect(String Host, ushort Port, String Username, String Password)
        {
            CrestronConsole.PrintLine("Connect() called, Host: {0}, Port: {1}, Username: {2}, Password {3}", Host, Port, Username, Password);

            try
            {
                if (SshClient != null && SshClient.IsConnected) return 1;

                SshUsername = Username;
                SshPassword = Password;
                SshHost = Host;

                if (Debug > 0) CrestronConsole.PrintLine("Starting...");

                if (SshAuthMethod == null)
                {
                    SshAuthMethod = new KeyboardInteractiveAuthenticationMethod(SshUsername);
                    SshAuthMethod.AuthenticationPrompt += new EventHandler<AuthenticationPromptEventArgs>(SshAuthMethod_AuthenticationPrompt);
                }
                if (SshConnectionInfo == null)
                {
                    SshConnectionInfo = new ConnectionInfo(SshHost, SshUsername, SshAuthMethod);
                }

                if (Debug > 0) CrestronConsole.PrintLine("Auth Mode set...");

                SshClient = new SshClient(SshConnectionInfo);

                SshClient.ErrorOccurred += new EventHandler<ExceptionEventArgs>(SshClient_ErrorOccurred);
                SshClient.HostKeyReceived += new EventHandler<HostKeyEventArgs>(SshClient_HostKeyReceived);


                if (Debug > 0) CrestronConsole.PrintLine("Connecting...");

                SshClient.Connect();

                // Create a new shellstream
                try
                {
                    CrestronConsole.PrintLine("Creating stream...");

                    SshStream = SshClient.CreateShellStream("terminal", 80, 24, 800, 600, 1024);
                    SshStream.DataReceived += new EventHandler<ShellDataEventArgs>(SshStream_DataReceived);
                    SshStream.ErrorOccurred += new EventHandler<ExceptionEventArgs>(SshStream_ErrorOccurred);
                }
                catch (Exception ex)
                {
                    ErrorLog.Exception("Exception creating stream", ex);
                }

                SshState = true;

                return 1;
            }
            catch (Exception ex)
            {
                ErrorLog.Error(String.Format("Error Connecting: {0}", ex));
                return 0;
            }
        }

        public void Disconnect()
        {
            if (SshClient != null)
            {
                SshClient.Disconnect();
                SshClient.Dispose();
                SshStream.Dispose();
            }
        }

        public void SendCommand(String Command)
        {
            try
            {
                SshStream.WriteLine(Command);
            }
            catch (Exception e)
            {
                if (Debug > 0) CrestronConsole.PrintLine("Error Sending Command: {0}", e.Message);

                if (e.Message.ToLower().Contains("not connected"))
                {
                    SshState = false;
                }
            }
        }

        //************************************* EVENT HANDLERS

        private void SshStream_DataReceived(object sender, ShellDataEventArgs e)
        {
            if (Debug > 0) CrestronConsole.PrintLine("Data received...");

            var stream = (ShellStream)sender;
            string dataReceived = "";
            
            // Loop as long as there is data on the stream
            while (stream.DataAvailable)
            {
                dataReceived = stream.Read();
                if (Debug > 0) CrestronConsole.PrintLine(dataReceived);
            }
            
            if (dataReceived != "")
            {
                if (dataReceived.Length > 250)
                {
                    // Split into 250 character chunks for Simpl Windows
                    IEnumerable<string> dataReceivedArray = SplitDataReceived(dataReceived, 250);

                    // Return each chunk separately
                    foreach (var str in dataReceivedArray)
                    {
                        SshRxDataToSimpl(str);
                    }
                }
                else SshRxDataToSimpl(dataReceived);
            }
        }
        private void SshStream_ErrorOccurred(object sender, System.EventArgs e)
        {
            if (Debug > 0) CrestronConsole.PrintLine("SSH Shellstream error " + e.ToString());
            SshState = false;
        }
        
        private void SshAuthMethod_AuthenticationPrompt(object sender, AuthenticationPromptEventArgs e)
        {
            if (Debug > 0) CrestronConsole.PrintLine("Sending password...");

            foreach (AuthenticationPrompt prompt in e.Prompts)
            {
                if (prompt.Request.IndexOf("Password:", StringComparison.InvariantCultureIgnoreCase) != -1)
                {
                    prompt.Response = SshPassword;
                    if (Debug > 0) CrestronConsole.PrintLine("Password set...");
                }
            }
        }

        private void SshClient_HostKeyReceived(object sender, HostKeyEventArgs e)
        {
            e.CanTrust = true;
        }
        private void SshClient_ErrorOccurred(object sender, ExceptionEventArgs e)
        {
            if (Debug > 0) CrestronConsole.PrintLine("SSH Cleint error " + e.ToString());
            SshState = false;
        }

        private IEnumerable<string> SplitDataReceived(string str, int maxChunkSize)
        {
            for (int i = 0; i < str.Length; i += maxChunkSize)
            {
                yield return str.Substring(i, Math.Min(maxChunkSize, str.Length - i));
            }
        }
        private List<string> SplitDataReceived(string str, int maxChunkSize, int i)
        {
            int stringLength = str.Length;
            List<string> strArray = new List<string>();

            for (i = 0; i < stringLength; i += maxChunkSize)
            {
                if (i + maxChunkSize > stringLength)
                {
                    maxChunkSize = stringLength - i;
                }
                strArray.Add(str.Substring(i, maxChunkSize));
            }

            return strArray;
        }
    }
}
