using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace TwitchBot
{
    /// <summary>
    /// Interaction logic for Settings.xaml
    /// </summary>
    public partial class Settings : Window
    {
        bool isTwitchAPIClientIDSet = true;
        bool isTwitchAPIClientSecretSet = true;

        public Settings()
        {
            InitializeComponent();

            TwitchAPIClientIDTextBox.Text = Properties.Settings.Default.clientid;
            TwitchAPIClientSecretTextBox.Text = Properties.Settings.Default.clientsecret;

            OBSWebSocketAuthTextBox.Text = Properties.Settings.Default.OBSWebSocketAuth;
            OBSWebSocketServerIPTextBox.Text = Properties.Settings.Default.OBSServerIP;
            OBSWebSocketServerPortTextBox.Text = Properties.Settings.Default.OBSServerPort;
            OBSWebSocketAuthTextBox.Text = Properties.Settings.Default.OBSWebSocketAuth;
            OBSWebcamSceneIDTextBox.Text = Properties.Settings.Default.OBSWebcamSceneID;
            OBSReactiveImageSceneIDTextBox.Text = Properties.Settings.Default.OBSReactiveImageSceneID;

            APINinjaKeyTextBox.Text = Properties.Settings.Default.APINinjaKey;
        }

        private void SaveChangesButton_OnClick(object sender, RoutedEventArgs e)
        {
            //ensures that the aplication will at least connect to Twitch successfully when running application
            if (TwitchAPIClientIDTextBox.Text == "")
            {
                TwitchAPIClientIDErrorMessage.IsEnabled = true;
                TwitchAPIClientIDErrorMessage.Visibility = Visibility.Visible;
                isTwitchAPIClientIDSet = false;
            }
            else
            {
                TwitchAPIClientIDErrorMessage.IsEnabled = false;
                TwitchAPIClientIDErrorMessage.Visibility = Visibility.Collapsed;
                isTwitchAPIClientIDSet = true;
            }

            if (TwitchAPIClientSecretTextBox.Text == "")
            {
                TwitchAPIClientSecretErrorMessage.IsEnabled = true;
                TwitchAPIClientSecretErrorMessage.Visibility = Visibility.Visible;
                isTwitchAPIClientSecretSet = false;
            }
            else
            {
                TwitchAPIClientSecretErrorMessage.IsEnabled = false;
                TwitchAPIClientSecretErrorMessage.Visibility = Visibility.Collapsed;
                isTwitchAPIClientSecretSet = true;
            }
            
            if(isTwitchAPIClientIDSet && isTwitchAPIClientSecretSet)
            {
                Properties.Settings.Default.clientid = TwitchAPIClientIDTextBox.Text;
                Properties.Settings.Default.clientsecret = TwitchAPIClientSecretTextBox.Text;

                Properties.Settings.Default.OBSServerIP = OBSWebSocketServerIPTextBox.Text;
                Properties.Settings.Default.OBSServerPort = OBSWebSocketServerPortTextBox.Text;
                Properties.Settings.Default.OBSWebSocketAuth = OBSWebSocketAuthTextBox.Text;
                Properties.Settings.Default.OBSWebcamSceneID = OBSWebcamSceneIDTextBox.Text;
                Properties.Settings.Default.OBSReactiveImageSceneID = OBSReactiveImageSceneIDTextBox.Text;

                Properties.Settings.Default.APINinjaKey = APINinjaKeyTextBox.Text;

                Properties.Settings.Default.Save();
                this.Close();
            }
        }
    }
}
