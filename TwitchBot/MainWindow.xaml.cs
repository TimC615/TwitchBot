using Newtonsoft.Json.Linq;
using NHttp;
using System.Net.Http;
using System.Net;
using System.Windows;
using TwitchLib.Api;
using TwitchLib.Client;
using TwitchLib.Client.Events;
using TwitchLib.Client.Models;
using System.IO;
using System.Diagnostics;
using System.Windows.Threading;
using Newtonsoft.Json;
using OBSWebsocketDotNet;
using OBSWebsocketDotNet.Types;
using TwitchLib.Api.Helix.Models.Moderation.BanUser;
using TwitchLib.Api.Auth;
using System.Reflection;
using TwitchLib.Api.Helix.Models.Users.GetUsers;
using TwitchLib.Api.Helix.Models.Moderation.GetModerators;
using TwitchLib.Api.Helix.Models.Moderation.GetBannedUsers;
using TwitchLib.Api.Helix.Models.Streams.CreateStreamMarker;
using TwitchBot.Utility_Code;
using System.ComponentModel;

//Base functionality taken from HonestDanGames' Youtube channel https://youtu.be/Ufgq6_QhVKw?si=QYBbDl0sYVCy3QVF
//Provides networking functionality to connect program to Twitch, beginner understanding of setting up API event handlers,
//and printing text responses to Twitch chat


//--Used for old web socket connection implementation. Keeping around for now just in case chaos happens--
//If code throws a web socket permissions error, open run window, search for "services.msc",
//and stop "World Wide Web Publishing Service". Should clear up port 80 (needed for when the local web server is created)



//---------------------------------------------------------------------------------------------------------------------------
//maybe put in timed messages (eg: after 30 min shout out socials and following)

//maybe put in automatic discord messaging? (eg: hey i'm live messages)

//could be fun to have a twitch extension (or simply apply based on role of user) to add a border or effect to talking head


//look into feature that allows user to add static chat commands during run time
//(would probably need to implement a dictionary stored in a text file. key<string> = chat command [!command]   value<string> = static string to write to chat log)


//see if toggling enabled points redeems is possible


//look into only connecting to obs websocket when needed (maybe to avoid borking sound alerts????)


//possibly provide functionality for pressing a key/key combo to put a marker in current twitch stream
/*
CreateStreamMarkerRequest markerRequest = new CreateStreamMarkerRequest();
markerRequest.UserId = TwitchChannelId;

_TwitchAPI.Helix.Streams.CreateStreamMarkerAsync(markerRequest);
*/


//Move handling of chat commands, points redeems, and ad breaks to new classes to clean up code
//Link the starting of EventSub websocket code to user pressing "start bot" WPF button

//Can move APINinja code to it's own singleton class


//timeout roulette calculations: (current record number of spins * 30 seconds) (maybe logarithmic curve?) (some sort of curve)
//start at a flat 30 seconds



//Error when pressing "Stop Bot" button
//triggers error on Line 86 of WebsocketHostedService:
//"var activeSubscriptions = _TwitchAPI.Helix.EventSub.GetEventSubSubscriptionsAsync().Result;"

//System.AggregateException: 'One or more errors occurred. (Your request was blocked due to bad credentials
//(Do you have the right scope for your access token?).)'

//BadScopeException: Your request was blocked due to bad credentials (Do you have the right scope for your access token?).



//Borks itself when losing internet connection (i believe it's from not having a channel being watched?)


//look into making bot messages (especially the ads have started messages) viewable only on host channel
//  don't want to spam shared chat feed with random bot stuff

//---------------------------------------------------------------------------------------------------------------------------
namespace TwitchBot
{
    //used in displaying the chat command to show timeout roulette leaderboard
    public class RouletteLeaderboardPosition
    {
        public string username;
        public int spinCount;

        public RouletteLeaderboardPosition(string username, int spinCount)
        {
            this.username = username;
            this.spinCount = spinCount;
        }
    }

    public partial class MainWindow : Window
    {
        //Authentication
        private HttpServer WebServer;
        private readonly string RedirectUri = "http://localhost:3000";
        private readonly string ClientId = Properties.Settings.Default.clientid;
        private readonly string ClientSecret = Properties.Settings.Default.clientsecret;
        private readonly List<string> Scopes = new List<string>
        { "user:edit", "chat:read", "chat:edit", "channel:moderate", "bits:read",
            "channel:read:subscriptions", "user:read:email", "user:read:subscriptions", "channel:manage:redemptions",
            "channel:edit:commercial", "channel:manage:ads", "moderator:manage:banned_users",
            "moderation:read", "channel:read:ads", "channel:manage:moderators"
        };      //find more Twitch API scopes at https://dev.twitch.tv/docs/authentication/scopes/



        //WPF
        Settings settings;

        //TwitchLib
        private TwitchClient _TwitchClient;
        private TwitchAPI _TwitchAPI;
        //Look for EventSub events in "WebsocketHostedServices.cs". Handles things like points redeems and ad break triggers.

        private readonly string BOTNAME = "CakeBot___";

        private TwitchChatCommands _TwitchChatCommands;


        private static readonly int TIMEOUTROULETTELENGTH = 30;      //timeout length, in seconds
        private static readonly int TIMEOUTROULETTETOPPOSITIONSTODISPLAY = 3;   //used to determine max number of results to show for leaderboard display
        
        public static Dictionary<string, int> rouletteLeaderboard;

        private readonly string ROULETTEJSONFILENAME = @"rouletteleaderboard.json";
        private static readonly string FIRSTREDEEMSJSONFILENAME = @"firstredeemsleaderboard.json";

        //API Ninja
        private static HttpClient NinjaAPIConnection { get; set; }  //initialized in TwitchChatCommands (doing this so that connection can be manually closed when MainWindow closes)

        //OBS Websocket
        protected OBSWebsocket obs;

        //Cached Variables
        //private string CachedOwnerOfChannelAccessToken = "needsaccesstoken"; //cached due to potentially being needed for API requests
        //private string CachedRefreshToken = "needsrefreshtoken"; //needed to ask Twitch API for new access token
        private string TwitchChannelName; //needed for bot to join Twitch channel
        private string TwitchChannelId; //needed for some API requests

        //Trigger variables
        public static bool twitchPlaysEnable = false;

        //Other Objects
        TwitchPlays TwitchPlaysObj;    //only gets an object when TwitchPlays is enabled
        SpeechSynthesis _SpeechSynth;

        //Bot Commands
        readonly Dictionary<string, string> CommandsStaticResponses = new Dictionary<string, string>
        {
            { "commands", "The current chat commands are: help, about, discord, twitter, lurk, joke, fact, roll, roulette, rouletteleaderboard, and 1st" },
            { "about", "Hello! I'm TheCakeIsAPie__ and I'm a Canadian variety streamer. We play a bunch of stuff over here in this small corner of the internet. Come pop a seat and have fun watching the shenanigans!"},
            { "discord", "Join the discord server at: https://discord.gg/uzHqnxKKkC"},
            { "twitter", "Follow me on Twitter at: https://twitter.com/TheCakeIsAPi"},
            { "lurk", "Have fun lurking!"}
        };

        //used in UtilityCode.WPFUtility to get reference to the MainWindow (complains about thread ownership otherwise)
        public static MainWindow AppWindow
        {
            get;
            private set;
        }

        public MainWindow()
        {
            InitializeComponent();
            //this.Closing += MainWindow_Closing(object sender, CancelEventArgs e);

            AppWindow = this;   //sets object reference for code like UtilityCode.WPFUtility to access MainWindow elements from different locations

            //code snippet taken from https://www.red-gate.com/simple-talk/blogs/wpf-menu-displays-to-the-left-of-the-window/
            //makes it so file menu items fall on the right hand side (by default windows puts them on the left hand side becasue of right-handed people using tablets)
            var menuDropAlignmentField = typeof(SystemParameters).GetField("_menuDropAlignment", BindingFlags.NonPublic | BindingFlags.Static);
            Action setAlignmentValue = () => {
                if (SystemParameters.MenuDropAlignment && menuDropAlignmentField != null) menuDropAlignmentField.SetValue(null, false);
            };
            setAlignmentValue();
            SystemParameters.StaticPropertyChanged += (sender, e) => { setAlignmentValue(); };

            if (Properties.Settings.Default.ConnectBotOnLaunch)
            {
                ConnectOnLaunchMenuItem.IsChecked = true;

                StartBot();
                RestartBotMenuItem.IsEnabled = true;
                StopBotMenuItem.IsEnabled = true;
            }
            else
            {
                ConnectOnLaunchMenuItem.IsChecked = false;
            }

        }


        //
        //----------------------WPF Interaction Methods----------------------
        //
        private void StartBotMenuItem_Click(object sender, RoutedEventArgs e)
        {
            StartBot();
            RestartBotMenuItem.IsEnabled = true;
            StopBotMenuItem.IsEnabled = true;
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            settings = new Settings();
            settings.Owner = this;
            settings.Show();
        }

        //Starts countdown if twitch plays is currently off, else disables it
        private void TwitchPlaysButton_Click(object sender, RoutedEventArgs e)
        {
            if (twitchPlaysEnable)
            {
                twitchPlaysEnable = false;

                Log($"Twitch Plays disabled");
                twitchPlaysButton.Header = "Enable Twitch Plays";
            }
            //enable Twitch Plays functionality after a 5 second cooldown
            else
            {
                //start countdown on a different thread, freeing up UI thread
                new Thread(TwitchPlaysCoundown).Start();
            }
        }

        private void RestartBotMenuItem_Click(object sender, RoutedEventArgs e)
        {
            CloseEverything();

            Log($"\n\nRestarting bot...\n\n");

            StartBot();
        }
        private void StopBotMenuItem_Click(object sender, RoutedEventArgs e)
        {
            CloseEverything();

            Log($"\n\nBot has been stopped...\n\n");

            ConnectToTwitch.IsEnabled = true;
            RestartBotMenuItem.IsEnabled = false;
            StopBotMenuItem.IsEnabled = false;

            SpeechSynthPauseResume.IsEnabled = false;
            SpeechSynthClearAllPrompts.IsEnabled = false;
            twitchPlaysButton.IsEnabled = false;
            CheckCurrentAccessToken.IsEnabled = false;

            TestButton.IsEnabled = false;
        }

        private void ConnectBotOnLaunchMenuItem_Checked(object sender, RoutedEventArgs e)
        {
            Properties.Settings.Default.ConnectBotOnLaunch = true;
            Properties.Settings.Default.Save();
        }

        private void ConnectBotOnLaunchMenuItem_Unchecked(object sender, RoutedEventArgs e)
        {
            Properties.Settings.Default.ConnectBotOnLaunch = false;
            Properties.Settings.Default.Save();
        }

        private void SkipCurrentTTS_Click(object sender, RoutedEventArgs e)
        {
            _SpeechSynth.SkipCurrentSpeechSynthAsync();
        }

        private void PauseResumeTTSMenuItem_Click(object sender, RoutedEventArgs e)
        {

            if (_SpeechSynth.asyncIsPaused)
            {
                _SpeechSynth.ResumeSpeechSynthAsync();
                SpeechSynthPauseResume.Header = "_Pause TTS";
                Log($"\nTTS has been resumed\n");
            }
            else
            {
                _SpeechSynth.PauseSpeechSynthAsync();
                SpeechSynthPauseResume.Header = "_Resume TTS";
                Log($"\nTTS has been paused\n");
            }
        }

        private void ClearAllTTSPromptsMenuItem_Click(object sender, RoutedEventArgs e)
        {
            _SpeechSynth.ClearAllSpeechSynthAsyncPrompts();
        }

        private void ConnectOBSMenuItem_Click(object sender, RoutedEventArgs e)
        {
            InitializeOBSWebSocket();
        }

        private void DisconnectOBSMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (obs != null)
            {
                obs.Disconnect();
            }
        }

        private async void CreateStreamMarker_Click(object sender, RoutedEventArgs e)
        {
            await CheckAccessToken();

            CreateStreamMarkerRequest markerRequest = new CreateStreamMarkerRequest();
            markerRequest.UserId = TwitchChannelId;

            _TwitchAPI.Helix.Streams.CreateStreamMarkerAsync(markerRequest);
        }

        private void ShowRouletteSuccessMenuItem_Checked(object sender, RoutedEventArgs e)
        {
            Properties.Settings.Default.DisplayRouletteSuccessMessage = true;
            Properties.Settings.Default.Save();
        }

        private void ShowRouletteSuccessMenuItem_Unchecked(object sender, RoutedEventArgs e)
        {
            Properties.Settings.Default.DisplayRouletteSuccessMessage = false;
            Properties.Settings.Default.Save();
        }

        async private void TestButton_Click(object sender, RoutedEventArgs e)
        {
            /*
            foreach(var window in Application.Current.Windows)
            {
                Trace.WriteLine($"TEST - {window.GetType}");
            }
            */

            GetUsersResponse test1 = await _TwitchAPI.Helix.Users.GetUsersAsync();
            foreach(var user1 in test1.Users)
            {
                Log($"TEST1: {user1.DisplayName}\t{user1.Login}\t{user1.Id}");
            }

            GetUsersResponse test2 = await _TwitchAPI.Helix.Users.GetUsersAsync(logins: new List<string>(new string[] {BOTNAME}));
            foreach (var user1 in test2.Users)
            {
                Log($"TEST2: {user1.DisplayName}\t{user1.Login}\t{user1.Id}");
            }

            foreach (var joinedChannel in _TwitchClient.JoinedChannels)
            {
                Log($"Channel: {joinedChannel.Channel}\t{joinedChannel.ChannelState}");
            }

            Log($"{_TwitchClient.TwitchUsername}");
        }

        async private void TestModButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                List<string> testSearchUsers = new List<string>();
                testSearchUsers.Add("cakebot___");

                GetUsersResponse usersResult = await _TwitchAPI.Helix.Users.GetUsersAsync(null, testSearchUsers);
                Log($"{usersResult.Users[0].Login} -> {usersResult.Users[0].Id}");

                List<string> testSearchMods = new List<string>();
                testSearchMods.Add(usersResult.Users[0].Id);
                GetModeratorsResponse modsResult = await _TwitchAPI.Helix.Moderation.GetModeratorsAsync(TwitchChannelId);

                string output = "";

                foreach (var entry in modsResult.Data)
                {
                    if (output == "")
                        output += entry.UserName;
                    else
                        output += ", " + entry.UserName;
                }
                Log($"Mods: {output}");

                await _TwitchAPI.Helix.Moderation.UnbanUserAsync(TwitchChannelId, TwitchChannelId, usersResult.Users[0].Id);

                await _TwitchAPI.Helix.Moderation.AddChannelModeratorAsync(TwitchChannelId, usersResult.Users[0].Id);

                GetModeratorsResponse modsResult2 = await _TwitchAPI.Helix.Moderation.GetModeratorsAsync(TwitchChannelId);

                output = "";

                foreach (var entry in modsResult2.Data)
                {
                    if (output == "")
                        output += entry.UserName;
                    else
                        output += ", " + entry.UserName;
                }
                Log($"Mods: {output}");
            }
            catch (Exception ex)
            {
                Log($"TestModButton Error: {ex.Message}");
            }
        }

        async private void TestUnmodButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                List<string> testSearchUsers = new List<string>();
                testSearchUsers.Add("cakebot___");

                GetUsersResponse usersResult = await _TwitchAPI.Helix.Users.GetUsersAsync(null, testSearchUsers);
                Log($"{usersResult.Users[0].Login} -> {usersResult.Users[0].Id}");

                List<string> testSearchMods = new List<string>();
                testSearchMods.Add(usersResult.Users[0].Id);
                GetModeratorsResponse modsResult = await _TwitchAPI.Helix.Moderation.GetModeratorsAsync(TwitchChannelId);

                string output = "";

                foreach (var entry in modsResult.Data)
                {
                    if (output == "")
                        output += entry.UserName;
                    else
                        output += ", " + entry.UserName;
                }
                Log($"Mods: {output}");


                //ban info for current user
                BanUserRequest request = new BanUserRequest();
                request.UserId = usersResult.Users[0].Id;
                request.Reason = "Test to see if banning works";
                request.Duration = 20;

                //add specific channel and acting moderator info to current user ban info
                BanUserResponse result = _TwitchAPI.Helix.Moderation.BanUserAsync(
                    TwitchChannelId,
                    TwitchChannelId,
                    request
                    ).Result;

                GetBannedUsersResponse bannedResult = await _TwitchAPI.Helix.Moderation.GetBannedUsersAsync(TwitchChannelId, testSearchMods);
                output = "";

                foreach (var entry in bannedResult.Data)
                {
                    if (output == "")
                        output += entry.UserName;
                    else
                        output += ", " + entry.UserName;
                }
                Log($"Mods: {output}");
            }
            catch (Exception ex)
            {
                Log($"TestUnmodButton Error: {ex.Message}");
            }
        }

        private void CheckAccessTokenMenuItem_Click(object sender, RoutedEventArgs e)
        {
            ManualCheckAccessToken();
        }

        private void CloseMenuItem_Click(object sender, RoutedEventArgs e)
        {
            CloseEverything();

            this.Close();
        }

        //Ensures connections to APIs and local web server is closed when exiting application
        protected void MainWindow_Closing(object sender, EventArgs e)
        {
            if(_TwitchAPI != null)
            {
                try
                {   //updates list of subscribed api events to iterate through and close. probably not needed (leave open and let erode after enough time) but feels nice to do this
                    GlobalObjects.EventSubSubscribedEvents = _TwitchAPI.Helix.EventSub.GetEventSubSubscriptionsAsync().Result.Subscriptions;
                }
                catch (AggregateException)
                {
                    Log("Exception when closing application: AggregateException");
                }
                catch (Exception mainWindowCloseExcept)
                {
                    Log($"Exception when closing application: {mainWindowCloseExcept.Message}");
                }
            }

            CloseEverything();
        }
        //
        //----------------------End of WPF Interaction Methods----------------------
        //

        //
        //----------------------Initialization Methods----------------------
        //

        void StartBot()
        {
            InitializeWebServer();

            var authUrl = "https://id.twitch.tv/oauth2/authorize?response_type=code&client_id=" +
                ClientId + "&redirect_uri=" + RedirectUri + "&scope=" + String.Join("+", Scopes);

            //launch the above authUrl to connect to twitch, allow permissions, and start the proces of retrieving auth tokens
            try
            {
                ProcessStartInfo twitchAuthBrowser = new ProcessStartInfo()
                {
                    UseShellExecute = true,
                    FileName = authUrl
                };
                Process.Start(twitchAuthBrowser);
            }
            catch (Exception ex)
            {
                Log("StartBot Open URL Error: " + ex.Message);
            }

            ConnectToTwitch.Opacity = 0.50;
            ConnectToTwitch.IsEnabled = false;

            SkipCurrentTTSButton.IsEnabled = true;
            twitchPlaysButton.IsEnabled = true;

            //TestButton.IsEnabled = true;
            TestModButton.IsEnabled = true;
            TestUnmodButton.IsEnabled = true;

            SpeechSynthPauseResume.IsEnabled = true;
            SpeechSynthPauseResume.Header = "_Pause TTS";
            SpeechSynthClearAllPrompts.IsEnabled = true;

            CheckCurrentAccessToken.IsEnabled = true;

            //testing button
            TestButton.IsEnabled = true;

            if (Properties.Settings.Default.DisplayRouletteSuccessMessage)
                ShowRouletteSuccess.IsChecked = true;
            else
                ShowRouletteSuccess.IsChecked = false;
        }

        void InitializeWebServer()
        {
            //Create local web server (allows for requesting OAUTH token)
            WebServer = new HttpServer();

            //int port = GetFreePort();
            //WebServer.EndPoint = new IPEndPoint(IPAddress.Loopback, port);

            WebServer.EndPoint = new IPEndPoint(IPAddress.Loopback, 3000);  //must specify on twitch dev console to use "http://localhost:3000"
            //WebServer.EndPoint = new IPEndPoint(IPAddress.Loopback, 80);  //defaults to this if no port specified in twitch dev console

            WebServer.RequestReceived += async (s, e) =>
            {
                using (var writer = new StreamWriter(e.Response.OutputStream))
                {
                    if (e.Request.QueryString.AllKeys.Any("code".Contains))
                    {
                        //initialize base TwitchLib API
                        var code = e.Request.QueryString["code"];
                        var ownerOfChannelAccessAndRefresh = await GetAccessAndRefreshTokens(code);

                        Properties.Settings.Default.TwitchAccessToken = ownerOfChannelAccessAndRefresh.Item1; //access token
                        Properties.Settings.Default.TwitchClientReftreshToken = ownerOfChannelAccessAndRefresh.Item2; //refresh token

                        SetNameAndIdByOauthedUser(Properties.Settings.Default.TwitchAccessToken).Wait();
                        InitializeTwitchClient(TwitchChannelName, Properties.Settings.Default.TwitchAccessToken);
                        InitializeTwitchAPI(Properties.Settings.Default.TwitchAccessToken);

                        //initialize connection to OBS websocket
                        InitializeOBSWebSocket();

                        //initialize dictionary holding leaderboard to last saved standings
                        rouletteLeaderboard = GetRouletteLeaderboardFromJson();

                        _TwitchChatCommands = new TwitchChatCommands(_TwitchClient, _TwitchAPI, TwitchChannelName, TwitchChannelId, _SpeechSynth, NinjaAPIConnection);
                    }
                }
            };

            WebServer.Start();
            Log($"Web server started on: {WebServer.EndPoint}");

            _SpeechSynth = SpeechSynthesis.GetInstance();
            GlobalObjects.botIsActive = true;

            try
            {
                System.Media.SoundPlayer botStartup = new System.Media.SoundPlayer("C:\\Users\\timot\\source\\repos\\TwitchBot\\Bot startup sound.wav");
                botStartup.Play();
            }
            catch(Exception except)
            {
                Log($"Error when playing bot startup sound: {except.Message}");
            }
        }

        async Task SetNameAndIdByOauthedUser(string accessToken)
        {
            var api = new TwitchLib.Api.TwitchAPI();
            api.Settings.ClientId = ClientId;
            api.Settings.AccessToken = accessToken;

            var oauthedUser = await api.Helix.Users.GetUsersAsync();
            TwitchChannelId = oauthedUser.Users[0].Id;

            GlobalObjects.TwitchBroadcasterUserId = oauthedUser.Users[0].Id;

            TwitchChannelName = oauthedUser.Users[0].Login;
            GlobalObjects.TwitchChannelName = oauthedUser.Users[0].Login;
        }

        async Task<Tuple<String, String>> GetAccessAndRefreshTokens(string code)
        {
            HttpClient client = new HttpClient();
            var values = new Dictionary<string, String>
            {
                { "client_id", ClientId },
                { "client_secret", ClientSecret },
                { "code", code },
                { "grant_type", "authorization_code" },
                { "redirect_uri", RedirectUri }
            };

            var content = new FormUrlEncodedContent(values);

            var response = await client.PostAsync("https://id.twitch.tv/oauth2/token", content);

            var responseString = await response.Content.ReadAsStringAsync();

            var json = JObject.Parse(responseString);

            return new Tuple<string, string>(json["access_token"].ToString(), json["refresh_token"].ToString());
        }

        void InitializeTwitchAPI(string accessToken)
        {
            _TwitchAPI = GlobalObjects._TwitchAPI;
            _TwitchAPI.Settings.ClientId = ClientId;
            _TwitchAPI.Settings.AccessToken = accessToken;
            _TwitchAPI.Settings.Secret = ClientSecret;
        }

        void InitializeTwitchClient(string username, string accessToken)
        {
            GlobalObjects._TwitchClient = new TwitchClient();
            _TwitchClient = GlobalObjects._TwitchClient;

            _TwitchClient.Initialize(new ConnectionCredentials(username, accessToken), TwitchChannelName);

            //Events you want to subscribe to
            _TwitchClient.OnConnected += Client_OnConnected;
            _TwitchClient.OnDisconnected += TwitchClient_OnDisconnected;
            //_TwitchClient.OnLog += TwitchClient_OnLog; //good for debug
            _TwitchClient.OnChatCommandReceived += Bot_OnChatCommandReceived;
            _TwitchClient.OnMessageReceived += Client_OnMessageReceived;
            _TwitchClient.OnRateLimit += TwitchClient_OnRateLimit;

            //Other subscription examples
            //_TwitchClient.OnBanned += Client_OnBanned;
            //_TwitchClient.OnUserTimedout += Client_OnUserTimedout;
            //_TwitchClient.OnJoinedChannel += Client_OnJoinedChannel;
            //_TwitchClient.OnUserJoined += BotConnection_OnUserJoined;
            //_TwitchClient.OnUserLeft += BotConnection_OnUserLeft;
            //_TwitchClient.OnWhisperReceived += Client_OnWhisperReceived;
            //_TwitchClient.OnNewSubscriber += Client_OnNewSubscriber;
            //_TwitchClient.OnIncorrectLogin += Client_OnIncorrectLogin;
            //_TwitchClient.OnWhisperCommandReceived += Bot_OnWhisperCommandReceived;

            _TwitchClient.Connect();
        }

        void InitializeOBSWebSocket()
        {
            GlobalObjects._OBS = new OBSWebsocket();
            obs = GlobalObjects._OBS;

            obs.Connected += Obs_onConnect;
            obs.Disconnected += Obs_onDisconnect;

            try
            {
                //setting port to 4455 conflicts with Sound Alerts. creates jarbled mess of the incoming sound bites
                obs.ConnectAsync(
                    $"ws://{Properties.Settings.Default.OBSServerIP}:{Properties.Settings.Default.OBSServerPort}", 
                    Properties.Settings.Default.OBSWebSocketAuth);

                //OBSWebsocketDotNet method documentation available at:
                //https://github.com/BarRaider/obs-websocket-dotnet/blob/master/obs-websocket-dotnet/OBSWebsocket_Requests.cs
            }
            catch (Exception ex)
            {
                Log($"Error when connecting to OBS WebSocket: {ex.Message}");

                //make it so user can retry the connection
                Dispatcher.BeginInvoke(new Action(() => {
                    ConnectOBS.IsEnabled = true;
                    DisconnectOBS.IsEnabled = false;
                }));
            }
        }
        //
        //----------------------End of Initialization Methods----------------------
        //

        //
        //----------------------TwitchClient Event Hookups----------------------
        //
        private void Client_OnConnected(object sender, OnConnectedArgs e)
        {
            Log($"User {e.BotUsername} connected (bot access)");
        }

        private void TwitchClient_OnDisconnected(object sender, TwitchLib.Communication.Events.OnDisconnectedEventArgs e)
        {
            Log($"OwnerOfChannel OnDisconnected event");
        }

        private void TwitchClient_OnLog(object sender, TwitchLib.Client.Events.OnLogArgs e)
        {
            Log($"OnLog: {e.Data}");
        }

        private void TwitchClient_OnRateLimit(object sender, OnRateLimitArgs e)
        {
            Log($"OnRateLimit - Channel:{e.Channel}\tMessage: {e.Message}");
        }


        async private void Bot_OnChatCommandReceived(object sender, OnChatCommandReceivedArgs e)
        {
            _TwitchChatCommands.BaseCommandMethod(e.Command);
        }

        //!!! might need to make multiple threads to act on multiple chat messages simultaniously !!!
        //potentially add in a rate limit to smooth out commands (better for larger, more constant input volumes)
        //https://www.dougdoug.com/twitchplays-template-py-3-9-x  example threadpool-enabled bot (albeit in python)
        private void Client_OnMessageReceived(object sender, OnMessageReceivedArgs e)
        {
            if (twitchPlaysEnable)
            {
                Log($"OnMessageReceived: {e.ChatMessage.Username.ToLower()} - {e.ChatMessage.Message.ToLower()}");
                //Keyboard.KeyDownEvent();

                //TwitchPlays.SpeechSynthSync(e.ChatMessage.Message);

                //Twitch plays Skyrim SE
                TwitchPlaysObj.TwitchPlaysSkyrim(e);

                //------------------<IMPLEMENT>------------------------------
                //Twitch plays Trackmania

                //------------------</IMPLEMENT>-----------------------------


                //simple testing to hear how things sound through the speechsynth
                //TwitchPlays.SpeechSynth(e.ChatMessage.Message.ToString());

                //failed multi-treading idea
                //ThreadPool.QueueUserWorkItem(TwitchPlays.SpeechSynth, e.ChatMessage.Message) ;

            }
        }
        //
        //----------------------End of TwitchClient Event Hookups----------------------
        //


        //
        //----------------------OBS Event Hookups----------------------
        //
        private void Obs_onConnect(object sender, EventArgs e)
        {
            Log("OBS connected");

            Dispatcher.BeginInvoke(new Action(() => {
                ConnectOBS.IsEnabled = false;
                DisconnectOBS.IsEnabled = true;
            }));
        }

        private void Obs_onDisconnect(object sender, OBSWebsocketDotNet.Communication.ObsDisconnectionInfo e)
        {
            //Allows for more descriptive error message in cases where OBS isn't running. Normally would output "Unknown reason"
            if (Process.GetProcessesByName("obs64").Length == 0)
            {
                Log("OBS is not actively running");
            }
            else
            {
                //trycatch used becasue controlled disconnects throw error with debug output
                try
                {
                    //normal output
                    Log("OBS disconnect code " + e.ObsCloseCode + ": " + e.DisconnectReason);

                    //debug output (do not use during normal operation)
                    //Log("OBS disconnect code " + e.ObsCloseCode + ": " + e.DisconnectReason + " : " + e.WebsocketDisconnectionInfo.Exception.ToString());
                }
                catch (Exception ex)
                {
                    Log("obs_onDisconnect error: " + ex.Message);
                }
            }
            
            Dispatcher.BeginInvoke(new Action(() => {
                ConnectOBS.IsEnabled = true;
                DisconnectOBS.IsEnabled = false;
            }));
            
        }
        //
        //----------------------End of OBS Event Hookups----------------------
        //

        //
        //----------------------Utility Methods----------------------
        //
        //Sends log messages to both the user form and console
        public void Log(string printMessage)
        {
            Action writeToConsoleLog = () => {
                ConsoleLog.AppendText("\n" + DateTime.Now.ToString() + "\t" + printMessage);
                ConsoleLog.ScrollToEnd();
            };

            Dispatcher.BeginInvoke(writeToConsoleLog);

            //not using Console.WriteLine() as WPF doesn't have a console window
            //writes to 'Output' window during debug instead
            Trace.WriteLine(DateTime.Now.ToString() + "\t" + printMessage);
        }

        //Triggers a countdown to begin before the Twitch Plays portion of code activates
        private void TwitchPlaysCoundown()
        {
            Log($"Enabling Twitch Plays in 5 seconds...");

            
            //invoke the UI thread, allowing UI changes from a different thread
            this.Dispatcher.Invoke(() => {
                //twitchPlaysButton.Content = "Starting...";
                twitchPlaysButton.Opacity = 0.75;
                twitchPlaysButton.IsEnabled = false;
            });
            

            Thread.Sleep(5000);

            twitchPlaysEnable = true;
            TwitchPlaysObj = new TwitchPlays();

            Log($"Twitch Plays now live");

            //invoke the UI thread, allowing UI changes from a different thread
            this.Dispatcher.Invoke(() => {
                twitchPlaysButton.Header = "Stop Twitch Plays";
                twitchPlaysButton.Opacity = 1;
                twitchPlaysButton.IsEnabled = true;
            });


            System.Media.SoundPlayer twitchPlaysStartup = new System.Media.SoundPlayer("C:\\Users\\timot\\source\\repos\\TwitchBot\\Twitch Plays startup sound.wav");
            twitchPlaysStartup.Play();

            _SpeechSynth.SpeechSynth("Twitch Plays is now live");
        }

        //Checks current access token. If invalid then get new Access Token
        async public Task CheckAccessToken()
        {
            //Log("Checking AccessToken...");

            var tokenResponse = _TwitchAPI.Auth.ValidateAccessTokenAsync();
            ValidateAccessTokenResponse tokenResult = await tokenResponse;

            //tokenResult is null if current Access Token is invalid
            //added ExpiresIn case to allow for the code needing an access token to fully execute
            if (tokenResult == null || tokenResult.ExpiresIn <= 10)
            {
                Log("CheckAccessToken: Bad token, refreshing");

                try
                {
                    var result = _TwitchAPI.Auth.RefreshAuthTokenAsync(Properties.Settings.Default.TwitchClientReftreshToken, ClientSecret); //start the process of refreshing tokens
                    RefreshResponse response = await result;    //retreive the results of token refresh

                    //Log($"Old Access: {_TwitchAPI.Settings.AccessToken}\t\tOld Refresh: {CachedRefreshToken}");

                    Properties.Settings.Default.TwitchAccessToken = response.AccessToken;
                    _TwitchAPI.Settings.AccessToken = response.AccessToken;

                    Properties.Settings.Default.TwitchClientReftreshToken = response.RefreshToken;

                    //Log($"New AccessToken: {_TwitchAPI.Settings.AccessToken}\t\tNew Refresh: {CachedRefreshToken}");
                }
                catch (Exception except)
                {
                    Log($"CheckAuthToken Error: {except.Message}");
                }
            }
        }

        //Manually triggered variant of CheckAccessToken()
        async private Task ManualCheckAccessToken()
        {
            Log("Checking AccessToken...");

            var tokenResponse = _TwitchAPI.Auth.ValidateAccessTokenAsync();
            ValidateAccessTokenResponse tokenResult = await tokenResponse;

            //tokenResult is null if current Access Token is invalid
            if (tokenResult == null)
            {
                Log("CheckAccessToken: Bad token, refreshing");

                try
                {
                    var result = _TwitchAPI.Auth.RefreshAuthTokenAsync(Properties.Settings.Default.TwitchClientReftreshToken, ClientSecret); //start the process of refreshing tokens
                    RefreshResponse response = await result;    //retreive the results of token refresh

                    //Log($"Old Access: {_TwitchAPI.Settings.AccessToken}\t\tOld Refresh: {CachedRefreshToken}");

                    Properties.Settings.Default.TwitchAccessToken = response.AccessToken;
                    _TwitchAPI.Settings.AccessToken = response.AccessToken;

                    Properties.Settings.Default.TwitchClientReftreshToken = response.RefreshToken;

                    //Log($"New AccessToken: {_TwitchAPI.Settings.AccessToken}\t\tNew Refresh: {CachedRefreshToken}");
                }
                catch (Exception except)
                {
                    Log($"CheckAuthToken Error: {except.Message}");
                }
            }
            else
                Log($"Current access token is valid for {tokenResult.ExpiresIn} seconds");
        }

        //read .json file and intialize dictionary to it
        public Dictionary<string, int> GetRouletteLeaderboardFromJson()
        {
            try
            {
                string rouletteJsonInput = File.ReadAllText(ROULETTEJSONFILENAME);

                var deserializedLeaderboard = JsonConvert.DeserializeObject<Dictionary<string, int>>(rouletteJsonInput);
                if (deserializedLeaderboard == null)
                    return new Dictionary<string, int>();
                else
                    return deserializedLeaderboard;
            }
            catch (Exception except)
            {
                Log($"Read from roulette leaderboard JSON error: {except.Message}");
                return new Dictionary<string, int>();
            }
        }

        void CloseEverything()
        {
            if (_TwitchClient != null)
            {
                _TwitchClient.Disconnect();
            }

            if (WebServer != null)
            {
                //WebServer.Stop();
                WebServer.Dispose();
            }

            if (NinjaAPIConnection != null)
            {
                NinjaAPIConnection.Dispose();
            }

            if (obs != null)
            {
                obs.Disconnect();
            }

            if (settings != null && settings.IsVisible)
            {
                settings.Close();
            }

            //ensure tts queue is fully cleared (assuming user is just restarting/stopping bot instead of full shutdown)
            if(_SpeechSynth != null)
            {
                _SpeechSynth.ClearAllSpeechSynthAsyncPrompts();
            }

            GlobalObjects.botIsActive = false;

            Log($"Bot connections closed");
        }
        //
        //----------------------End of Utility Methods----------------------
        //
    }
}