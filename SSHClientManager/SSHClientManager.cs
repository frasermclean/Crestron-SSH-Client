using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Crestron.SimplSharp;
using Crestron.SimplSharp.Ssh;
using Crestron.SimplSharp.Ssh.Common;

namespace FM.SSH
{
    public delegate void ConnectionStatusCallback(bool connected);
    public delegate void ReceiveDataCallback(string data);

    public class SSHClientManager
    {
        #region Class variables
        bool debug;
        string debugName, hostname, username, password;
        ushort port;
        SshClient client;
        ShellStream stream;
        public event ConnectionStatusCallback ConnectionStatusCallback;
        public event ReceiveDataCallback ReceiveDataCallback;
        #endregion

        #region Constructor
        public SSHClientManager(string hostname, ushort port, string username, string password)
        {
            Initialize(false, hostname, port, username, password);
        }

        public SSHClientManager(bool debug, string hostname, ushort port, string username, string password)
        {
            Initialize(debug, hostname, port, username, password);
        }

        void Initialize(bool debug, string hostname, ushort port, string username, string password)
        {
            try
            {
                debugName = this.GetType().Name;

                // assign class variables
                this.debug = debug;
                this.hostname = hostname;
                this.port = port;
                this.username = username;
                this.password = password;

                Trace("Initialize() initialized successfully.");
            }
            catch (Exception e)
            {
                Trace("Initialize() caught exception: " + e.Message);
            }
        }
        #endregion

        #region Properties
        public bool Debug
        {
            get { return debug; }
            set { debug = value; }
        }
        public string DebugName
        {
            get { return debugName; }
            set { debugName = value; }
        }
        public bool Connected
        {
            get
            {
                if (client != null)
                    return client.IsConnected;
                else
                    return false;
            }
        }
        #endregion

        #region Public methods
        public bool Connect()
        {
            if (client == null)
            {
                // set up authentication method
                KeyboardInteractiveAuthenticationMethod authMethod = new KeyboardInteractiveAuthenticationMethod(username);
                authMethod.AuthenticationPrompt += new EventHandler<AuthenticationPromptEventArgs>(AuthenticationPromptHandler);

                // set up connection info
                ConnectionInfo connectionInfo = new ConnectionInfo(hostname, username, authMethod);

                // create new client
                client = new SshClient(hostname, port, username, password);
                client.ErrorOccurred += new EventHandler<ExceptionEventArgs>(ClientErrorOccurredHandler);
                client.HostKeyReceived += new EventHandler<HostKeyEventArgs>(ClientHostKeyEventHandler);

                // attempt to connect
                Trace(String.Format("Connect() attempting connection to {0} on port {1}.", hostname, port));
                try
                {
                    client.Connect();
                }
                catch (SshConnectionException e)
                {
                    Trace("Connect() connection exception: " + e.Message + ", reason: " + e.DisconnectReason);
                    Reset();
                    return false;
                }
                Trace("Connect() connection successful.");
                if (ConnectionStatusCallback != null)
                    ConnectionStatusCallback(true);

                // create shellstream
                stream = client.CreateShellStream("terminal", 80, 24, 800, 600, 1024);
                stream.DataReceived += new EventHandler<ShellDataEventArgs>(StreamDataReceivedHandler);
                stream.ErrorOccurred += new EventHandler<ExceptionEventArgs>(StreamErrorOccurredHandler);

                return true;
            }
            else
            {
                Trace("Connect() called, but client already exists. Connection status: " + client.IsConnected);
                return client.IsConnected;
            }
        }

        public bool Disconnect()
        {
            return Reset();
        }

        public bool Send(string s)
        {
            try
            {
                // attempt to connect if not already
                if (client == null || !client.IsConnected)
                {
                    Trace("Send() Not connected. Will attempt connection...");
                    if (!Connect())
                    {
                        Trace("Send() error, could not connect.");
                        return false;
                    }
                }

                // check if stream is ready
                if (stream != null && stream.CanWrite)
                {
                    // send string to host
                    Trace("SendString() sending: " + s.Trim());
                    stream.WriteLine(s);
                    return true;
                }
                else
                {
                    Trace("SendString() stream is null or not writable.");
                    Reset();
                    return false;
                }
            }
            catch (Exception e)
            {
                Trace("SendString() exception occurred: " + e.Message);
                return false;
            }
        }
        #endregion

        #region Private methods
        void Trace(string message)
        {
            if (debug)
            {
                string line = String.Format("[{0}] {1}", debugName, message.Trim());
                CrestronConsole.PrintLine(line);
            }
        }

        bool Reset()
        {
            try
            {
                Trace("Reset() resetting all objects.");

                if (stream != null)
                {
                    stream.Dispose();
                    stream = null;
                    Trace("Reset() stream reset.");
                }

                if (client != null)
                {
                    if (client.IsConnected)
                        client.Disconnect();
                    client.Dispose();
                    client = null;
                    Trace("Reset() client reset.");

                    if (ConnectionStatusCallback != null)
                        ConnectionStatusCallback(false);
                }

                return true;
            }
            catch (Exception e)
            {
                Trace("Reset() caught exception: " + e.Message);
                return false;
            }
        }
        #endregion

        #region Event handlers
        void ClientErrorOccurredHandler(object sender, ExceptionEventArgs args)
        {
            Trace("ClientErrorOccurredHandler() error occurred: " + args.Exception.Message);
            Reset();
        }

        void ClientHostKeyEventHandler(object sender, HostKeyEventArgs args)
        {
            Trace("ClientHostKeyEventHandler() host key received.");
            args.CanTrust = true;
        }

        void AuthenticationPromptHandler(object sender, AuthenticationPromptEventArgs args)
        {
            Trace("AuthenticationPromptHandler() sending password.");
            foreach (AuthenticationPrompt prompt in args.Prompts)
            {
                if (prompt.Request.IndexOf("Password:", StringComparison.InvariantCultureIgnoreCase) != -1)
                    prompt.Response = password;
            }
        }

        void StreamDataReceivedHandler(object sender, ShellDataEventArgs args)
        {
            Trace("StreamDataReceivedHandler() received data. Length: " + args.Data.Length);

            var stream = (ShellStream)sender;

            while (stream.DataAvailable)
            {
                string data = stream.Read();
                if (ReceiveDataCallback != null)
                    ReceiveDataCallback(data);
            }
        }

        void StreamErrorOccurredHandler(object sender, EventArgs args)
        {
            Trace("StreamErrorOccurredHandler() error occurred: " + args.ToString());
            Reset();
        }
        #endregion
    }
}