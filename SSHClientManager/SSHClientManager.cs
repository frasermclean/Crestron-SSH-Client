using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Crestron.SimplSharp;
using Crestron.SimplSharp.Ssh;
using Crestron.SimplSharp.Ssh.Common;

namespace FM.SSH
{
    public class SshClientManager
    {
        #region Class variables
        protected bool traceEnabled, initialized;
        protected string traceName, hostname, username, password;
        protected ushort port;
        SshClient client;
        ShellStream stream;
        #endregion

        #region Delegates
        public delegate void InitializedDelegate(bool value);
        public delegate void ConnectionStatusDelegate(bool connected);
        public delegate void ReceiveDataDelegate(string data);
        public event InitializedDelegate InitializedCallback;
        public event ConnectionStatusDelegate ConnectionStatusCallback;
        public event ReceiveDataDelegate ReceiveDataCallback;
        #endregion

        #region Constructor
        public SshClientManager()
        {
        }
        public SshClientManager(string hostname, ushort port, string username, string password)
        {
            initialized = Initialize(false, hostname, port, username, password);
        }
        public SshClientManager(bool debug, string hostname, ushort port, string username, string password)
        {
            initialized = Initialize(debug, hostname, port, username, password);
        }

        #endregion

        #region Properties
        public bool TraceEnabled
        {
            get { return traceEnabled; }
            set { traceEnabled = value; }
        }
        public string TraceName
        {
            get { return traceName; }
            set { traceName = value; }
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
        public bool Initialized
        {
            get { return initialized; }
        }
        public string Hostname
        {
            get { return hostname; }
            set { hostname = value; }
        }
        public ushort Port
        {
            get { return port; }
            set { port = value; }
        }
        public string Username
        {
            get { return username; }
            set { username = password; }
        }
        public string Password
        {
            get { return password; }
            set { password = value; }
        }
        #endregion

        #region Public methods
        public bool Connect()
        {
            if (!initialized)
            {
                Trace("Connect() called but data is not initialized.");
                return false;
            }
            else if (client == null)
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
                
                try
                {
                    // attempt to connect
                    Trace(String.Format("Connect() attempting connection to {0} on port {1}.", hostname, port));
                    client.Connect();

                    // create shellstream
                    stream = client.CreateShellStream("terminal", 80, 24, 800, 600, 1024);
                    stream.DataReceived += new EventHandler<ShellDataEventArgs>(StreamDataReceivedHandler);
                    stream.ErrorOccurred += new EventHandler<ExceptionEventArgs>(StreamErrorOccurredHandler);
                }
                catch (Exception e)
                {
                    Trace("Connect() connection exception: " + e.Message);
                    Reset();
                    return false;
                }

                // report success
                Trace("Connect() connection successful.");
                if (ConnectionStatusCallback != null)
                    ConnectionStatusCallback(true);

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
            if (traceEnabled)
            {
                string line = String.Format("[{0}] {1}", traceName, message.Trim());
                CrestronConsole.PrintLine(line);
            }
        }
        protected bool Initialize(bool traceEnabled, string hostname, ushort port, string username, string password)
        {
            try
            {
                // assign default traceEnabled name
                if (traceName == null)
                    traceName = this.GetType().Name;

                // assign class variables
                this.traceEnabled = traceEnabled;
                this.hostname = hostname;
                this.port = port;
                this.username = username;
                this.password = password;

                // validate data
                if (Validate(hostname, port, username, password))
                {
                    initialized = true;
                    Trace("Initialize() initialized successfully.");
                    if (InitializedCallback != null)
                        InitializedCallback(true);
                    return true;
                }
                else
                {
                    initialized = false;
                    Trace("Initialize() error validating parameters.");
                    if (InitializedCallback != null)
                        InitializedCallback(false);
                    return false;
                }
            }
            catch (Exception e)
            {
                Trace("Initialize() caught exception: " + e.Message);
                return false;
            }
        }
        bool Validate(string hostname, ushort port, string username, string password)
        {
            try
            {
                // check hostname
                if (hostname == null || hostname.Length == 0)
                    return false;

                // check port
                if (port == 0)
                    return false;

                // check username
                if (username == null || username.Length == 0)
                    return false;

                // check password
                if (password == null)
                    return false;

                return true;
            }
            catch (Exception e)
            {
                Trace("Validate() exception caught: " + e.Message);
                return false;
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
