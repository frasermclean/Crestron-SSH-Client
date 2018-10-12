using System;
using System.Text;
using System.Text.RegularExpressions;
using Crestron.SimplSharp;                          				
using Crestron.SimplSharp.Ssh;
using Crestron.SimplSharp.Ssh.Common;
using System.Collections.Generic;

namespace SSHClient
{
    public delegate void InitializedDataHandler(ushort state);
    public delegate void ConnectionStateHandler(ushort state);
    public delegate void ReceivedDataHandler(SimplSharpString data);    

    public class SSHClientDevice
    {
        private bool initialized = false;

        // ssh objects
        private SshClient client;
        private ShellStream stream;       

        // connection details
        private string username, hostname, password;
       
        // delegates
        public InitializedDataHandler InitializedData { get; set; }
        public ReceivedDataHandler ReceivedData { get; set; }
        public ConnectionStateHandler ConnectionState { get; set; }
        
        // debugging
        private string debug_name;
        public ushort debug_enable = 0;       

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
            InitializedData(Convert.ToUInt16(1));
        }

        public void Connect()
        {
            // check that data has been initialized
            if (!initialized)
            {
                Debug("Connection properties not initialized.");
                return;
            }
          
            // set up authentication method
            KeyboardInteractiveAuthenticationMethod authMethod = new KeyboardInteractiveAuthenticationMethod(username);
            authMethod.AuthenticationPrompt += new EventHandler<AuthenticationPromptEventArgs>(AuthenticationPromptHandler);

            // set up connection info
            ConnectionInfo connectionInfo = new ConnectionInfo(hostname, username, authMethod);

            // set up client
            client = new SshClient(connectionInfo);
            client.ErrorOccurred += new EventHandler<ExceptionEventArgs>(ClientErrorHandler);
            client.HostKeyReceived += new EventHandler<HostKeyEventArgs>(HostKeyReceivedHandler);

            // try to connect
            Debug("Attempting connection to: " + hostname);
            try
            {
                client.Connect();
            }
            catch (SshConnectionException e)
            {
                Debug ("Connection error: " + e.Message + ", Reason: " + e.DisconnectReason);
                Disconnect(); // free up allocated resources
                return;
            }
            
            // create shellstream
            stream = client.CreateShellStream("terminal", 80, 24, 800, 600, 1024);
            stream.DataReceived += new EventHandler<ShellDataEventArgs>(StreamDataReceivedHandler);
            stream.ErrorOccurred += new EventHandler<ExceptionEventArgs>(StreamErrorOccurredHandler);    
                
            // set connected flag
            if (client.IsConnected)
            {
                Debug("Connected.");
                ConnectionState(Convert.ToUInt16(1));
            }
            else
            {
                Debug("Could not complete connection.");
            }
        }

        public void Disconnect()
        {
            Debug("Disconnect() called.");

            // set connected flag
            ConnectionState(Convert.ToUInt16(0));

            // free stream
            try
            {
                if (stream != null)
                    stream.Dispose();
            }
            catch (Exception e)
            {
                Debug("Disconnect() exception occured freeing stream: " + e.Message);
            }

            // free client
            try
            {
                if (client != null && client.IsConnected)
                    client.Disconnect();
                client.Dispose();
            }                
            catch (Exception e)
            {
                Debug("Disconnect() Exception occured freeing client: " + e.Message);
            }
        }

        public void SendCommand(String Command)
        {
            if (client == null || client.IsConnected == false)
            {
                Debug("SendCommand() called but not connected.");
                Disconnect();
                return;
            }

            if (stream != null && stream.CanWrite)
                stream.WriteLine(Command);           
        }

        //************************************* EVENT HANDLERS

        private void StreamDataReceivedHandler(object sender, ShellDataEventArgs e)
        {
            var stream = (ShellStream)sender;
            string dataReceived = "";
            //Debug("Received Data. Length: " + stream.Length);
            
            // Loop as long as there is data on the stream
            while (stream.DataAvailable)
            {
                dataReceived = stream.Read();
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
                        ReceivedData(str);
                    }
                }
                else ReceivedData(dataReceived);
            }
        }
        private void StreamErrorOccurredHandler(object sender, System.EventArgs e)
        {
            Debug("SSH Shellstream error " + e.ToString());
            Disconnect();            
        }
        
        private void AuthenticationPromptHandler(object sender, AuthenticationPromptEventArgs e)
        {
            Debug("Sending password");

            foreach (AuthenticationPrompt prompt in e.Prompts)
            {
                if (prompt.Request.IndexOf("Password:", StringComparison.InvariantCultureIgnoreCase) != -1)
                {
                    prompt.Response = password;
                    
                }
            }
        }

        private void HostKeyReceivedHandler(object sender, HostKeyEventArgs e)
        {
            Debug("Host key received");
            e.CanTrust = true;
        }
        private void ClientErrorHandler(object sender, ExceptionEventArgs e)
        {
            Debug("SSH client error: " + e.Exception.Message);
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
