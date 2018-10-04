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
        private bool initialized = false;

        // ssh objects
        private SshClient client;
        private ShellStream stream;
        private ConnectionInfo connectionInfo = null;
        private KeyboardInteractiveAuthenticationMethod authMethod = null;       

        // connection details
        private string username, hostname, password;
       
        // delegates
        public CommandEventHandler SshRxDataToSimpl { get; set; }
        public StateChangeHandler SshStateChangeToSimpl { get; set; }
        
        // debugging
        private string debug_name;
        public ushort debug_enable = 0;

        private bool connected = false;   
        public bool SshState
        {
            get
            {
                return connected;
            }
            set
            {
                connected = value;
                SshStateChangeToSimpl(Convert.ToUInt16(value == true ? 1 : 0));

                if (value == false)
                {
                    Disconnect();
                }
            }
        }

        public void Debug(string message)
        {
            if (debug_enable >= 1)
                CrestronConsole.PrintLine("[" + debug_name + "] " + message);
        }

        public void Initialize(string hostname, string username, string password, string debug_name)
        {
            this.hostname = hostname;
            this.username = username;
            this.password = password;
            this.debug_name = debug_name;

            Debug("Initialize() called. hostname: " + hostname + ", username: " + username + ", password: " + password);
            
            initialized = true;
        }

        public ushort Connect()
        {
           Debug("Connect() called");

            if (!initialized)
            {
                Debug("Connection properties not initialized.");
                return 0;
            }

            try
            {
                // set up authentication method
                if (authMethod == null)
                {
                    authMethod = new KeyboardInteractiveAuthenticationMethod(username);
                    authMethod.AuthenticationPrompt += new EventHandler<AuthenticationPromptEventArgs>(SshAuthMethod_AuthenticationPrompt);
                    Debug("Auth Mode set");                  
                }

                // set up connection info
                if (connectionInfo == null)
                {
                    connectionInfo = new ConnectionInfo(hostname, username, authMethod);
                    Debug("Connection info set");
                }
                
                // set up client
                client = new SshClient(connectionInfo);
                client.ErrorOccurred += new EventHandler<ExceptionEventArgs>(SshClient_ErrorOccurred);
                client.HostKeyReceived += new EventHandler<HostKeyEventArgs>(SshClient_HostKeyReceived);

                Debug("Connecting...");

                client.Connect();

                // create shellstream
                try
                {
                    Debug("Creating stream...");

                    stream = client.CreateShellStream("terminal", 80, 24, 800, 600, 1024);
                    stream.DataReceived += new EventHandler<ShellDataEventArgs>(SshStream_DataReceived);
                    stream.ErrorOccurred += new EventHandler<ExceptionEventArgs>(SshStream_ErrorOccurred);
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
            Debug("Disconnect() called.");

            SshState = false;

            try
            {
                if (client != null)
                {
                    client.Disconnect();
                    client.Dispose();                    
                }

                if (stream != null)
                {
                    stream.Dispose();
                }
            }
            catch (Exception e)
            {
                Debug("Exception occured while disconnecting: " + e.Message);
            }
        }

        public void SendCommand(String Command)
        {
            try
            {
                stream.WriteLine(Command);
            }
            catch (Exception e)
            {
                Debug("Error Sending Command: " + e.Message);

                if (e.Message.ToLower().Contains("not connected"))
                {
                    Disconnect();
                }
            }
        }

        //************************************* EVENT HANDLERS

        private void SshStream_DataReceived(object sender, ShellDataEventArgs e)
        {
            Debug("Data received...");

            var stream = (ShellStream)sender;
            string dataReceived = "";
            
            // Loop as long as there is data on the stream
            while (stream.DataAvailable)
            {
                dataReceived = stream.Read();
                Debug(dataReceived);
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
            Debug("SSH Shellstream error " + e.ToString());
            Disconnect();
        }
        
        private void SshAuthMethod_AuthenticationPrompt(object sender, AuthenticationPromptEventArgs e)
        {
            Debug("Sending password...");

            foreach (AuthenticationPrompt prompt in e.Prompts)
            {
                if (prompt.Request.IndexOf("Password:", StringComparison.InvariantCultureIgnoreCase) != -1)
                {
                    prompt.Response = password;
                    Debug("Password set...");
                }
            }
        }

        private void SshClient_HostKeyReceived(object sender, HostKeyEventArgs e)
        {
            Debug("Host key received");
            e.CanTrust = true;
        }
        private void SshClient_ErrorOccurred(object sender, ExceptionEventArgs e)
        {
            Debug("SSH Client error " + e.ToString());
            Disconnect();
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
