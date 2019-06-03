using System;
using Crestron.SimplSharp;                          	// For Basic SIMPL# Classes
using Crestron.SimplSharpPro;                       	// For Basic SIMPL#Pro classes
using Crestron.SimplSharpPro.CrestronThread;        	// For Threading
using Crestron.SimplSharpPro.Diagnostics;		    	// For System Monitor Access
using Crestron.SimplSharpPro.DeviceSupport;         	// For Generic Device Support

using FM.SSH;

namespace TestSystem
{
    public class TestSystem : CrestronControlSystem
    {
        SSHClientManager manager;

        // enter test values here
        string hostname = "roach";
        ushort port = 22;
        string username = "test";
        string password = "test1234";

        #region Constuctor and initialization
        public TestSystem() : base()
        {
            try
            {
                Thread.MaxNumberOfUserThreads = 20;
            }
            catch (Exception e)
            {
                ErrorLog.Error("Error in the constructor: {0}", e.Message);
            }
        }

        public override void InitializeSystem()
        {
            try
            {
                manager = new SSHClientManager(hostname, port, username, password);
                manager.ConnectionStatusCallback += new ConnectionStatusCallback(ConnectionStatusCallbackHandler);
                manager.ReceiveDataCallback += new ReceiveDataCallback(ReceiveDataCallbackHandler);

                // add console commands for testing
                CrestronConsole.AddNewConsoleCommand(ConsoleTraceHandler, "Trace", "Enable / disable debugging", ConsoleAccessLevelEnum.AccessOperator);
                CrestronConsole.AddNewConsoleCommand(ConsoleConnectHandler, "Connect", "Connect to the SSH host", ConsoleAccessLevelEnum.AccessOperator);
                CrestronConsole.AddNewConsoleCommand(ConsoleDisconnectHandler, "Disconnect", "Disconnect from the SSH host", ConsoleAccessLevelEnum.AccessOperator);
                CrestronConsole.AddNewConsoleCommand(ConsoleSendHandler, "Send", "Send the specified string to the SSH host", ConsoleAccessLevelEnum.AccessOperator);
            }
            catch (Exception e)
            {
                ErrorLog.Error("Error in InitializeSystem: {0}", e.Message);
            }
        }
        #endregion

        #region Console command handlers
        void ConsoleTraceHandler(string input)
        {
            bool value = Boolean.Parse(input);
            manager.Debug = value;
            if (value)
                CrestronConsole.PrintLine("Enabled debugging.");
            else
                CrestronConsole.PrintLine("Disabled debugging.");
            
        }

        void ConsoleConnectHandler(string input)
        {
            if (manager.Connect())
                CrestronConsole.PrintLine("Connected successfully.");
            else
                CrestronConsole.PrintLine("Error, could not connect.");
        }

        void ConsoleDisconnectHandler(string input)
        {
            if (manager.Disconnect())
                CrestronConsole.PrintLine("Disconnected successfully.");
            else
                CrestronConsole.PrintLine("Error, could not disconnect.");
        }

        void ConsoleSendHandler(string input)
        {
            manager.Send(input.Trim());
        }

        void ConnectionStatusCallbackHandler(bool value)
        {
            if (value)
                CrestronConsole.PrintLine("Connection online.");
            else
                CrestronConsole.PrintLine("Connection offline.");
        }

        void ReceiveDataCallbackHandler(string data)
        {
            CrestronConsole.PrintLine(data.Trim());
        }
        #endregion
    }
}
