using Microsoft.VisualBasic.Logging;
using OBSWebsocketDotNet;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Threading;
using TwitchBot.Utility_Code;

namespace TwitchBot
{
    internal class OBSConnection
    {
        private static OBSConnection _OBSConnection;
        private static OBSWebsocket _OBSWebsocket;
        private bool obsConnected = false;

        private OBSConnection()
        {
            //obs = new OBSWebsocket();
            _OBSWebsocket = new OBSWebsocket();

            _OBSWebsocket.Connected += OBS_onConnect;
            _OBSWebsocket.Disconnected += OBS_onDisconnect;

            try
            {
                //setting port to 4455 conflicts with Sound Alerts. creates jarbled mess of the incoming sound bites
                _OBSWebsocket.ConnectAsync(
                    $"ws://{Properties.Settings.Default.OBSServerIP}:{Properties.Settings.Default.OBSServerPort}", 
                    Properties.Settings.Default.OBSWebSocketAuth
                    );
            }
            catch(Exception except)
            {
                WPFUtility.WriteToLog($"Error when connecting to OBS websocket: {except.Message}");
            }
            
            //OBSWebsocketDotNet method documentation available at:
            //https://github.com/BarRaider/obs-websocket-dotnet/blob/master/obs-websocket-dotnet/OBSWebsocket_Requests.cs
        }

        public static OBSConnection GetInstance()
        {
            if (_OBSConnection == null)
            {
                _OBSConnection = new OBSConnection();
            }
            return _OBSConnection;
        }

        private void OBS_onConnect(object sender, EventArgs e)
        {
            var mainWindow = System.Windows.Application.Current.Windows.OfType<MainWindow>().FirstOrDefault();
            if (mainWindow != null)
            {
                WPFUtility.WriteToLog("OBS connected");
                obsConnected = true;

                mainWindow.Dispatcher.BeginInvoke(new Action(() => {
                    mainWindow.ConnectOBS.IsEnabled = false;
                    mainWindow.DisconnectOBS.IsEnabled = true;
                }));
            }
        }

        private void OBS_onDisconnect(object sender, OBSWebsocketDotNet.Communication.ObsDisconnectionInfo e)
        {
            //Allows for more descriptive error message in cases where OBS isn't running. Normally would output "Unknown reason"
            if (Process.GetProcessesByName("obs64").Length == 0)
            {
                WPFUtility.WriteToLog("OBS is not actively running");
            }
            else
            {
                //trycatch used becasue controlled disconnects throw error with debug output
                try
                {
                    //normal output
                    WPFUtility.WriteToLog("OBS disconnect code " + e.ObsCloseCode + ": " + e.DisconnectReason);

                    //debug output (do not use during normal operation)
                    //Log("OBS disconnect code " + e.ObsCloseCode + ": " + e.DisconnectReason + " : " + e.WebsocketDisconnectionInfo.Exception.ToString());
                }
                catch (Exception ex)
                {
                    WPFUtility.WriteToLog("obs_onDisconnect error: " + ex.Message);
                }
            }


            var mainWindow = System.Windows.Application.Current.Windows.OfType<MainWindow>().FirstOrDefault();
            if (mainWindow != null)
            {
                obsConnected = false;

                mainWindow.Dispatcher.BeginInvoke(new Action(() => {
                    mainWindow.ConnectOBS.IsEnabled = true;
                    mainWindow.DisconnectOBS.IsEnabled = false;
                }));
            }

        }
    }
}
