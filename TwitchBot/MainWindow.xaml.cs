using Newtonsoft.Json.Linq;
using NHttp;
using System.Net.Http;
using System.Net;
using System.Windows;
using TwitchLib.Api;
using TwitchLib.Client;
using TwitchLib.Client.Events;
using TwitchLib.Client.Models;
using TwitchLib.PubSub;
using System.IO;
using System.Diagnostics;
using System.Windows.Threading;
using WindowsInput;
using TwitchLib.PubSub.Events;
using Newtonsoft.Json;
using OBSWebsocketDotNet;
using TwitchLib.Communication.Interfaces;
using System;
using SpotifyAPI.Web;
using SpotifyAPI.Web.Auth;
using OBSWebsocketDotNet.Types;
using TwitchLib.Client.Extensions;
using TwitchLib.Api.Helix.Models.Moderation.BanUser;
using System.Net.Sockets;
using TwitchLib.Api.Auth;
using TwitchLib.Api.Core.Exceptions;
using System.Reflection;
using TwitchLib.Api.Helix.Models.Users.GetUsers;
using TwitchLib.Api.Helix.Models.Moderation.GetModerators;
using TwitchLib.Api.Helix.Models.Moderation.GetBannedUsers;
using System.Runtime.InteropServices;
using System.Security.Policy;
using TwitchLib.Api.Helix.Models.Streams.CreateStreamMarker;
using OBSWebsocketDotNet.Communication;
using TwitchLib.Api.Helix.Models.Streams.GetStreams;


//Base functionality taken from HonestDanGames' Youtube channel https://youtu.be/Ufgq6_QhVKw?si=QYBbDl0sYVCy3QVF
//Provides networking functionality to connect program to Twitch, beginner understanding of setting up API event handlers,
//and printing text responses to Twitch chat


//If code throws a web socket permissions error, open run window, search for "services.msc",
//and stop "World Wide Web Publishing Service". Should clear up port 80 (needed for when the local web server is created)



//---------------------------------------------------------------------------------------------------------------------------
//Add in spotify "now playing" info in the corner of OBS (maybe youtube info also if possible?)
//if using specifically spotify api, can add in points redeem for users to add requested song to queue
//look into TUNA plugin
//instead of pulling from spotify API directly, possibly look into Windows' global media player info. Aka see if windows is playing a
//song/video/thing and get info from there
//maybe just use the integrated sound alerts spotify widget

//maybe put in timed messages (eg: after 30 min shout out socials and following)

//maybe put in automatic discord messaging? (eg: hey i'm live messages)


//TTS REDEEM: look into role filtering for custom talking head images

//TTS Redeem: look into spam filter
//potentially have the skip (or other state changes of TTS) bound to global hotkey button like what's done in death counter program
//manual skip button implemented. still undecided on spam filter

//could be fun to have a twitch extension (or simply apply based on role of user) to add a border or effect to talking head


//look into purposefully making jarbled sound alert sounds. current theory is running obs websocket through port 4455 conflicts with sound alerts
//might need to temporarily open an obs websocket on port 4455, run sound alert, and close websocket
//NEW ERROR IDEA: could be caused by enabling the "control audio via OBS" checkbox in the SoundAlerts properties



//look into feature that allows user to add static chat commands during run time
//(would probably need to implement a dictionary stored in a text file. key<string> = chat command [!command]   value<string> = static string to write to chat log)


//see if toggling enabled points redeems is possible


//look into only connecting to obs websocket when needed (maybe to avoid borking sound alerts????)


//possibly provide functionality for pressing a key/key combo to put a marker in current twitch stream
/*
    CreateStreamMarkerRequest markerRequest = new CreateStreamMarkerRequest();
    markerRequest.UserId = TwitchChannelId;

    TheTwitchAPI.Helix.Streams.CreateStreamMarkerAsync(markerRequest);
*/

//confetti/other celebration for 1st redeem
//tiny confetti/other silly celebration for not 1st redeem

//---------------------------------------------------------------------------------------------------------------------------
namespace TwitchBot
{
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
            "channel:edit:commercial", "channel:manage:ads", "user:read:email", "moderator:manage:banned_users",
            "moderation:read", "channel:manage:moderators"
        };
        //find more Twitch API scopes at https://dev.twitch.tv/docs/authentication/scopes/

        //, "channel:edit:commercial" //using with WitchLib.PubSub (points redeems) and users triggering ad breaks

        //WPF
        Settings settings;

        //TwitchLib
        private TwitchClient OwnerOfChannelConnection;
        private TwitchAPI TheTwitchAPI;
        private TwitchPubSub PubSub;
        private static readonly int TIMEOUTROULETTELENGTH = 30;      //timeout length, in seconds

        //API Ninja
        private static HttpClient NinjaAPIConnection { get; set; }

        //OBS Websocket
        protected OBSWebsocket obs;
        private string ttsSceneName;
        private SceneItemDetails ttsSceneItem;
        private readonly string TTSTalkingHeadName = "TTS Talking Head";



        //Spotify API (Experimental)
        //private static HttpClient spotifyAPIConnection { get; set; }
        //private SpotifyClientConfig spotifyClientConfig { get; set; }
        protected static SpotifyClient spotify;
        protected static string spotifyAccessToken;
        private static EmbedIOAuthServer _server;



        //Cached Variables
        private string CachedOwnerOfChannelAccessToken = "needsaccesstoken"; //cached due to potentially being needed for API requests
        private string CachedRefreshToken = "needsrefreshtoken"; //needed to ask Twitch API for new access token
        private string TwitchChannelName; //needed for bot to join Twitch channel
        private string TwitchChannelId; //needed for some API requests

        //Trigger variables
        private bool twitchPlaysEnable = false;
        private bool obsConnected = false;

        //Global objects
        TwitchPlays TwitchPlaysObj;    //only gets an object when TwitchPlays is enabled
        InputSimulator inSim = new InputSimulator();
        SpeechSynthesis SpeechSynthObj = new SpeechSynthesis();

        //Bot Commands
        readonly Dictionary<string, string> CommandsStaticResponses = new Dictionary<string, string>
        {
            { "commands", "The current chat commands are: help, about, discord, twitter, lurk, joke, fact, roll, and roulette" },
            { "about", "Hello! I'm TheCakeIsAPie__ and I'm a Canadian variety streamer. We play a bunch of stuff over here in this small corner of the internet. Come pop a seat and have fun watching the shenanigans!"},
            { "discord", "Join the discord server at: https://discord.gg/uzHqnxKKkC"},
            { "twitter", "Follow me on Twitter at: https://twitter.com/TheCakeIsAPi"},
            { "lurk", "Have fun lurking!"}
        };

        public MainWindow()
        {
            InitializeComponent();

            //code snippet taken from https://www.red-gate.com/simple-talk/blogs/wpf-menu-displays-to-the-left-of-the-window/
            //makes it so file menu items fall on the right hand side (by default windows puts them on the left hand side becasue of right-handed people using tablets)
            var menuDropAlignmentField = typeof(SystemParameters).GetField("_menuDropAlignment", BindingFlags.NonPublic | BindingFlags.Static);
            Action setAlignmentValue = () => {
                if (SystemParameters.MenuDropAlignment && menuDropAlignmentField != null) menuDropAlignmentField.SetValue(null, false);
            };
            setAlignmentValue();
            SystemParameters.StaticPropertyChanged += (sender, e) => { setAlignmentValue(); };

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

        private void SkipCurrentTTS_Click(object sender, RoutedEventArgs e)
        {
            SpeechSynthObj.SkipCurrentSpeechSynthAsync();
        }

        private void PauseResumeTTSMenuItem_Click(object sender, RoutedEventArgs e)
        {

            if (SpeechSynthObj.asyncIsPaused)
            {
                SpeechSynthObj.ResumeSpeechSynthAsync();
                SpeechSynthPauseResume.Header = "_Pause TTS";
                Log($"\nTTS has been resumed\n");
            }
            else
            {
                SpeechSynthObj.PauseSpeechSynthAsync();
                SpeechSynthPauseResume.Header = "_Resume TTS";
                Log($"\nTTS has been paused\n");
            }
        }

        private void ClearAllTTSPromptsMenuItem_Click(object sender, RoutedEventArgs e)
        {
            SpeechSynthObj.ClearAllSpeechSynthAsyncPrompts();
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

            TheTwitchAPI.Helix.Streams.CreateStreamMarkerAsync(markerRequest);
        }

        async private void TestButton_Click(object sender, RoutedEventArgs e)
        {

            Log("This is a test");
            Log($"{DateTime.Now.TimeOfDay.Hours}:{DateTime.Now.TimeOfDay.Minutes}:{DateTime.Now.TimeOfDay.Seconds}");
            Log($"{DateTime.Now.ToString("MMM")} {DateTime.Now.Day} {DateTime.Now.Year}   {DateTime.Now.TimeOfDay.Hours}:{DateTime.Now.TimeOfDay.Minutes}:{DateTime.Now.TimeOfDay.Seconds}");


            CreateStreamMarkerRequest test = new CreateStreamMarkerRequest();

            CreateStreamMarkerRequest markerRequest = new CreateStreamMarkerRequest();
            markerRequest.UserId = TwitchChannelId;
            markerRequest.Description = "Marker created through bot app";

            TheTwitchAPI.Helix.Streams.CreateStreamMarkerAsync(markerRequest);
        }

        async private void TestModButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                List<string> testSearchUsers = new List<string>();
                testSearchUsers.Add("cakebot___");

                GetUsersResponse usersResult = await TheTwitchAPI.Helix.Users.GetUsersAsync(null, testSearchUsers);
                Log($"{usersResult.Users[0].Login} -> {usersResult.Users[0].Id}");

                List<string> testSearchMods = new List<string>();
                testSearchMods.Add(usersResult.Users[0].Id);
                GetModeratorsResponse modsResult = await TheTwitchAPI.Helix.Moderation.GetModeratorsAsync(TwitchChannelId);

                string output = "";

                foreach (var entry in modsResult.Data)
                {
                    if (output == "")
                        output += entry.UserName;
                    else
                        output += ", " + entry.UserName;
                }
                Log($"Mods: {output}");

                await TheTwitchAPI.Helix.Moderation.UnbanUserAsync(TwitchChannelId, TwitchChannelId, usersResult.Users[0].Id);

                await TheTwitchAPI.Helix.Moderation.AddChannelModeratorAsync(TwitchChannelId, usersResult.Users[0].Id);

                GetModeratorsResponse modsResult2 = await TheTwitchAPI.Helix.Moderation.GetModeratorsAsync(TwitchChannelId);

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

                GetUsersResponse usersResult = await TheTwitchAPI.Helix.Users.GetUsersAsync(null, testSearchUsers);
                Log($"{usersResult.Users[0].Login} -> {usersResult.Users[0].Id}");

                List<string> testSearchMods = new List<string>();
                testSearchMods.Add(usersResult.Users[0].Id);
                GetModeratorsResponse modsResult = await TheTwitchAPI.Helix.Moderation.GetModeratorsAsync(TwitchChannelId);

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
                BanUserResponse result = TheTwitchAPI.Helix.Moderation.BanUserAsync(
                    TwitchChannelId,
                    TwitchChannelId,
                    request
                    ).Result;

                GetBannedUsersResponse bannedResult = await TheTwitchAPI.Helix.Moderation.GetBannedUsersAsync(TwitchChannelId, testSearchMods);
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

            //var authUrl = $"https://id.twitch.tv/oauth2/authorize?response_type=code&client_id={{ClientId}}&redirect_uri={{RedirectUri}}&scope={{String.Join("+", Scopes)}}";
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

                        CachedOwnerOfChannelAccessToken = ownerOfChannelAccessAndRefresh.Item1; //access token
                        CachedRefreshToken = ownerOfChannelAccessAndRefresh.Item2; //refresh token

                        SetNameAndIdByOauthedUser(CachedOwnerOfChannelAccessToken).Wait();
                        InitializeOwnerOfChannelConnection(TwitchChannelName, CachedOwnerOfChannelAccessToken);
                        InitializeTwitchAPI(CachedOwnerOfChannelAccessToken);


                        //initialize connection to facts api
                        InitializeNinjaAPI();

                        //initialize connection to OBS websocket
                        InitializeOBSWebSocket();

                        //initialize Twitch points redeem API
                        InitializeTwitchLibPubSub();
                    }
                }
            };

            WebServer.Start();
            Log($"Web server started on: {WebServer.EndPoint}");

            System.Media.SoundPlayer botStartup = new System.Media.SoundPlayer("C:\\Users\\timot\\source\\repos\\TwitchBot\\Bot startup sound.wav");
            botStartup.Play();
        }

        async Task SetNameAndIdByOauthedUser(string accessToken)
        {
            var api = new TwitchLib.Api.TwitchAPI();
            api.Settings.ClientId = ClientId;
            api.Settings.AccessToken = accessToken;

            var oauthedUser = await api.Helix.Users.GetUsersAsync();
            TwitchChannelId = oauthedUser.Users[0].Id;
            TwitchChannelName = oauthedUser.Users[0].Login;
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
            TheTwitchAPI = new TwitchAPI();
            TheTwitchAPI.Settings.ClientId = ClientId;
            TheTwitchAPI.Settings.AccessToken = accessToken;
            TheTwitchAPI.Settings.Secret = ClientSecret;
        }

        void InitializeOwnerOfChannelConnection(string username, string accessToken)
        {
            OwnerOfChannelConnection = new TwitchClient();
            OwnerOfChannelConnection.Initialize(new ConnectionCredentials(username, accessToken), TwitchChannelName);

            //Events you want to subscribe to
            OwnerOfChannelConnection.OnConnected += Client_OnConnected;
            OwnerOfChannelConnection.OnDisconnected += OwnerOfChannelConnection_OnDisconnected;
            //OwnerOfChannelConnection.OnLog += OwnerOfChannelConnection_OnLog; //good for debug
            OwnerOfChannelConnection.OnChatCommandReceived += Bot_OnChatCommandReceived;
            OwnerOfChannelConnection.OnMessageReceived += Client_OnMessageReceived;


            //Other subscription examples
            //OwnerOfChannelConnection.OnBanned += Client_OnBanned;
            //OwnerOfChannelConnection.OnUserTimedout += Client_OnUserTimedout;
            //OwnerOfChannelConnection.OnJoinedChannel += Client_OnJoinedChannel;
            //OwnerOfChannelConnection.OnUserJoined += BotConnection_OnUserJoined;
            //OwnerOfChannelConnection.OnUserLeft += BotConnection_OnUserLeft;
            //OwnerOfChannelConnection.OnWhisperReceived += Client_OnWhisperReceived;
            //OwnerOfChannelConnection.OnNewSubscriber += Client_OnNewSubscriber;
            //OwnerOfChannelConnection.OnIncorrectLogin += Client_OnIncorrectLogin;
            //OwnerOfChannelConnection.OnWhisperCommandReceived += Bot_OnWhisperCommandReceived;

            OwnerOfChannelConnection.Connect();
        }
        
        void InitializeTwitchLibPubSub()
        {
            PubSub = new TwitchPubSub();

            //PubSub.OnLog += PubSub_OnLog;
            PubSub.OnPubSubServiceConnected += PubSub_OnPubSubServiceConnected;
            PubSub.OnListenResponse += PubSub_OnListenResponse;
            PubSub.OnChannelPointsRewardRedeemed += PubSub_OnChannelPointsRewardRedeemed;
            PubSub.OnCommercial += PubSub_OnCommercial;

            PubSub.OnPubSubServiceError += PubSub_OnServiceError;

            PubSub.ListenToChannelPoints(TwitchChannelId);
            PubSub.ListenToVideoPlayback(TwitchChannelId);      //USED FOR DETECTING WHEN ADS START  //used for StreamUp() and StreamDown()?? Maybe??
            //PubSub.ListenToBitsEventsV2(TwitchChannelId);

            PubSub.Connect();
        }

        void InitializeNinjaAPI()
        {
            NinjaAPIConnection = new HttpClient();
            NinjaAPIConnection.BaseAddress = new Uri("https://api.api-ninjas.com/v1/");
            //requires an API key to be added to requests but will be handled during actual GET requests

            //ninjaAPIConnection.BaseAddress = new Uri("https://jsonplaceholder.typicode.com/");
        }

        void InitializeOBSWebSocket()
        {
            obs = new OBSWebsocket();

            obs.Connected += Obs_onConnect;
            obs.Disconnected += Obs_onDisconnect;

            //setting port to 4455 conflicts with Sound Alerts. creates jarbled mess of the incoming sound bites
            obs.ConnectAsync("ws://192.168.2.22:49152", Properties.Settings.Default.OBSWebSocketAuth);

            //OBSWebsocketDotNet method documentation available at:
            //https://github.com/BarRaider/obs-websocket-dotnet/blob/master/obs-websocket-dotnet/OBSWebsocket_Requests.cs
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

        private void OwnerOfChannelConnection_OnDisconnected(object sender, TwitchLib.Communication.Events.OnDisconnectedEventArgs e)
        {
            Log($"OwnerOfChannel OnDisconnected event");
        }

        private void OwnerOfChannelConnection_OnLog(object sender, TwitchLib.Client.Events.OnLogArgs e)
        {
            Log($"OnLog: {e.Data}");
        }

        async private void Bot_OnChatCommandReceived(object sender, OnChatCommandReceivedArgs e)
        {
            string commandText = e.Command.CommandText.ToLower();

            //2 ways to deal with commands: if/switch statements OR dictionary lookups

            //
            //---------------------------------------------------------------------------------------------------------------------
            //

            //responses are added to dictionary in lowercase
            if (CommandsStaticResponses.TryGetValue(commandText, out string? value))
            {
                OwnerOfChannelConnection.SendMessage(TwitchChannelName, value);
            }
            //
            //---------------------------------------------------------------------------------------------------------------------
            //

            //more complex comands
            else
            {
                //Tells user how to use commands
                if (commandText.Equals("help"))
                {
                    List<string> test = e.Command.ArgumentsAsList;

                    if (e.Command.ArgumentsAsList.Count == 0)
                        OwnerOfChannelConnection.SendMessage(TwitchChannelName, "Type \"!help <command>\" to see how you can use it (E.g. !help roll)");
                    else
                    {
                        try
                        {
                            string helpSpecifier = e.Command.ArgumentsAsList[0].ToLower();

                            switch (helpSpecifier)
                            {
                                case "about":
                                case "discord":
                                case "twitter":
                                case "lurk":
                                case "fact":
                                case "joke":
                                    OwnerOfChannelConnection.SendMessage(TwitchChannelName, "Enter \"!" + helpSpecifier + "\" and I'll do all the rest");
                                    break;
                                case "roll":
                                    OwnerOfChannelConnection.SendMessage(TwitchChannelName,
                                        "Enter \"!roll d<number of sides>\" to roll a single die or \"!roll <number of dice>d<number of sides>\" to roll multiple dice! (e.g. !roll d6) or !roll 3d20");
                                    break;
                                case "roulette":
                                    OwnerOfChannelConnection.SendMessage(TwitchChannelName,
                                        "Enter \"!roulette\" for a chance to time yourself out for " + TIMEOUTROULETTELENGTH + " seconds");
                                    break;
                                default:
                                    break;
                            }
                        }
                        catch (Exception except)
                        {
                            Log("Help command: " + except.Message);
                        }
                    }
                }
                //
                //------------------------------------------------------------------------------------------------------------------
                //
                //roll a random dX sided die
                if (commandText.Contains("roll", StringComparison.OrdinalIgnoreCase) && e.Command.ArgumentsAsList.Count >= 1)
                {
                    try
                    {
                        //retrieve and split roll command into 2 segments: *number of dice* and *size of die*
                        List<string> rollCommand = e.Command.ArgumentsAsList;

                        string rollInput = rollCommand[0].ToLower();
                        string[] rollParams;

                        if (!rollInput.Contains('d'))
                        {
                            rollParams = ["1", rollInput];
                        }
                        else
                            rollParams = rollInput.Split("d");


                        //convert param0 to a long
                        int diceToRoll;
                        try
                        {
                            //take care of all reasonable ways a user might want to roll just 1 die
                            if (rollParams[0] == null || rollParams[0] == "" || rollParams[0] == "1")
                            {
                                diceToRoll = 1;
                            }
                            else
                            {
                                diceToRoll = Int32.Parse(rollParams[0]);
                            }
                        }
                        catch (Exception ex)
                        {
                            Log("Roll command error converting param 0: " + ex.Message);
                            OwnerOfChannelConnection.SendMessage(TwitchChannelName, "The number of dice to roll needs to be a whole number greater than or equal to 1");
                            diceToRoll = -1;
                        }

                        //try to convert param[1] to long if param[0] was a valid number
                        if(diceToRoll != -1)
                        {
                            try
                            {
                                long sizeOfDie = Int64.Parse(rollParams[1]);
                                if (sizeOfDie <= 1)
                                    OwnerOfChannelConnection.SendMessage(TwitchChannelName, "The size of the die to roll needs to be a whole number greater than 1");
                                else
                                    Roll(diceToRoll, sizeOfDie);    //method assumes provided parameters are always valid numbers
                            }
                            catch (Exception ex)
                            {
                                Log("Roll command error converting param 1: " + ex.Message);
                            }
                        }
                    }
                    catch (Exception except)
                    {
                        Log("Roll command: " + except.Message);
                    }
                }
                //
                //------------------------------------------------------------------------------------------------------------------
                //
                //Grab random fact from https://api-ninjas.com/
                if (commandText.Equals("fact"))
                {
                    new Thread(APINinjaGetFact).Start();
                }
                //
                //------------------------------------------------------------------------------------------------------------------
                //
                //Grab random dad joke from https://api-ninjas.com/
                if (commandText.Equals("joke"))
                {
                    new Thread(APINinjaGetDadJoke).Start();
                }
                //
                //------------------------------------------------------------------------------------------------------------------
                //
                //Random chance for user to time themself out. If user has roles, automatically re-apply once timeout is done
                if (commandText.Equals("roulette"))
                {
                    try
                    {
                        Log("Roulette triggered");
                        Random random = new Random();

                        if (e.Command.ChatMessage.IsMe || e.Command.ChatMessage.IsBroadcaster || e.Command.ChatMessage.IsStaff)
                        {
                            OwnerOfChannelConnection.SendMessage(TwitchChannelName, $"Sorry {e.Command.ChatMessage.Username}, you're not able to be timed out so you can't spin the roulette");
                            throw new Exception("User tried to timeout as restricted role");
                        }

                        //1 in 10 chance 
                        if (random.Next(1, 11) == 1)
                        {
                            await CheckAccessToken();

                            bool isMod = false;
                            if(e.Command.ChatMessage.IsModerator)
                                isMod = true;

                            string timeoutRouletteMessage = $"Congrats {e.Command.ChatMessage.Username}! You won the roulette and timed yourself out!";
                            OwnerOfChannelConnection.SendMessage(TwitchChannelName, timeoutRouletteMessage);

                            //ban info for current user
                            BanUserRequest request = new BanUserRequest();
                            request.UserId = e.Command.ChatMessage.UserId;
                            request.Reason = "Congrats! You won the timeout roulette!";
                            request.Duration = TIMEOUTROULETTELENGTH;

                            //add specific channel and acting moderator info to current user ban info
                            BanUserResponse result = TheTwitchAPI.Helix.Moderation.BanUserAsync(
                                TwitchChannelId,
                                TwitchChannelId,
                                request
                                ).Result;
                            
                            Log("Roulette result: " + result.Data);

                            if (isMod)
                            {
                                new Thread(delegate () {
                                    ReinstateModRole(e.Command.ChatMessage.UserId, e.Command.ChatMessage.Username, TIMEOUTROULETTELENGTH);
                                }).Start();
                            }
                        }
                    }
                    catch (Exception except)
                    {
                        Log("Roulette Exception: " + except.Message);
                    }
                }
                //
                //------------------------------------------------------------------------------------------------------------------
                //
                //Tell user what skyrim spawn commands are avaialble (only when Twitch Plays is active)
                if (commandText.Equals("skyrim") && twitchPlaysEnable)
                {
                    string skyrimCommands = "You can mess with skyrim by saying any of the following: forward, back, stop, left, right, jump, " +
                        "cheese, soup, wine, potions, rabbits, skeevers, bears, lydia, spiders, dragons, cheesemageddon, and soupmageddon";
                    OwnerOfChannelConnection.SendMessage(TwitchChannelName, skyrimCommands);
                }

            }
            //
            //---------------------------------------------------------------------------------------------------------------------
            //
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
        //----------------------PubSub Event Hookups----------------------
        //
        private void PubSub_OnPubSubServiceConnected(object sender, EventArgs e)
        {
            PubSub.SendTopics(CachedOwnerOfChannelAccessToken);
        }

        private void PubSub_OnListenResponse(object sender, OnListenResponseArgs e)
        {
            if (!e.Successful)
            {
                Log($"Failed to listen! Response: {e.Topic} + {e.Response.Error}");
                throw new Exception($"Failed to listen! Response: {e.Topic} + {e.Response.Error}");
            }
            else
            {
                Log("PubSub Listen: " + e.Topic);
                Log("PubSub Connected");
            }
        }

        private void PubSub_OnLog (object sender, TwitchLib.PubSub.Events.OnLogArgs e)
        {
            Log("PubSub Log: " + e.Data);
        }

        private void PubSub_OnServiceError(object sender, OnPubSubServiceErrorArgs e)
        {
            Log($"PubSub Service Error: {e.Exception.Message} {e.Exception}");
        }

        private void PubSub_OnChannelPointsRewardRedeemed(object sender, OnChannelPointsRewardRedeemedArgs e)
        {
            //Log("PubSub: " + e.RewardRedeemed.Redemption.Reward.Title);
            string redeemTitle = e.RewardRedeemed.Redemption.Reward.Title.ToLower();
            Log("Points Reward: " + redeemTitle);

            switch (redeemTitle)
            {
                case "toggle cake face":
                    if (obsConnected)
                    {
                        try
                        {
                            Log("toggle webcam triggered");

                            //read in visibility of webcam
                            //if false, tell obs to set to true
                            //else, set to false
                            string currSceneName = obs.GetCurrentProgramScene();
                            int webcamItemID = obs.GetSceneItemId(currSceneName, "iPhone Webcam - Elgato Camera Hub", 0);
                            bool webcamEnabled = obs.GetSceneItemEnabled(currSceneName, webcamItemID);

                            //Log("1: " + e.RewardRedeemed.Redemption.Status);

                            int reactiveImageID = obs.GetSceneItemId(currSceneName, "Reactive Images - Myself", 0);
                            bool reactiveImageEnabled = obs.GetSceneItemEnabled(currSceneName, reactiveImageID);

                            if (!webcamEnabled)
                            {
                                obs.SetSceneItemEnabled(currSceneName, webcamItemID, true);
                                obs.SetSceneItemEnabled(currSceneName, reactiveImageID, false);
                                Log("Toggle Webcam: Webcam enabled");
                            }
                            else
                            {
                                obs.SetSceneItemEnabled(currSceneName, webcamItemID, false);
                                obs.SetSceneItemEnabled(currSceneName, reactiveImageID, true);
                                Log("Toggle Webcam: Webcam disabled");
                            }
                            //e.RewardRedeemed.Redemption.Status = "FULFILLED";

                            //Log("2: " + e.RewardRedeemed.Redemption.Status);
                        }
                        catch (Exception except)
                        {
                            Log("Toggle webcam: " + except.Message);

                            //Log(e.RewardRedeemed.Redemption.Status);
                        }
                    }
                    break;

                case "tts (random speech rate)":
                    Random random = new Random();
                    int randRate = random.Next(1, 21) - 10;

                    TtsRedeem(e, randRate);
                    break;

                case "tts (normal speech rate)":
                    TtsRedeem(e, SpeechSynthesis.SPEECHSYNTH_RATE);
                    break;
            }
        }

        private void PubSub_OnCommercial(object sender, OnCommercialArgs e)
        {
            //do actual OnCommercial handling on new thread (method will sleep the thread)
            new Thread(delegate () {
                OnCommercial_NewThread(sender, e);
            }).Start();
        }
        //
        //----------------------End of PubSub Event Hookups----------------------
        //


        //
        //----------------------OBS Event Hookups----------------------
        //
        private void Obs_onConnect(object sender, EventArgs e)
        {
            Log("OBS connected");
            obsConnected = true;

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
        //----------------------Experimental Spotify API Methods----------------------
        //
        /* Spotify API v1
        void InitializeSpotifyAPI()
        {
            var loginRequest = new LoginRequest(
                new Uri("http://localhost:3000"),
                Properties.Settings.Default.SpotifyClientId,
                LoginRequest.ResponseType.Code)
            {
                Scope = new[] { Scopes.PlaylistReadPrivate, Scopes.PlaylistReadCollaborative }
            };
            var uri = loginRequest.ToUri;
        }
        */

        public static async Task InitializeSpotifyAPI()
        {
            // Make sure "http://localhost:5543/callback" is in your spotify application as redirect uri!
            _server = new EmbedIOAuthServer(new Uri("http://localhost:5543/callback"), 5543);
            await _server.Start();

            _server.AuthorizationCodeReceived += OnAuthorizationCodeReceived;
            _server.ErrorReceived += OnErrorReceived;

            var request = new LoginRequest(_server.BaseUri, "ClientId", LoginRequest.ResponseType.Code)
            {
                //Scope = new List<string> { "Scopes.UserReadEmail" }
                //Scope = new List<string> { "Scopes.UserReadEmail" }
            };
            BrowserUtil.Open(request.ToUri());
        }

        private static async Task OnAuthorizationCodeReceived(object sender, AuthorizationCodeResponse response)
        {
            await _server.Stop();

            var config = SpotifyClientConfig.CreateDefault();
            var tokenResponse = await new OAuthClient(config).RequestToken(
                new AuthorizationCodeTokenRequest(
                    Properties.Settings.Default.SpotifyClientId,
                    Properties.Settings.Default.SpotifyClientSecret,
                    response.Code,
                    new Uri("http://localhost:5543/callback")
                )
            );

            spotifyAccessToken = tokenResponse.AccessToken;
            spotify = new SpotifyClient(spotifyAccessToken);
            // do calls with Spotify and save token?

            var track = await spotify.Tracks.Get("1s6ux0lNiTziSrd7iUAADH");
            Console.WriteLine(track.Name);
        }

        private static async Task OnErrorReceived(object sender, string error, string state)
        {
            Console.WriteLine($"Aborting authorization, error received: {error}");
            await _server.Stop();
        }

        private void SetSpotifyAccessToken(string token)
        {
            spotifyAccessToken = token;
        }
        //
        //----------------------End of Experimental Spotify API Methods----------------------
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

            SpeechSynthObj.SpeechSynth("Twitch Plays is now live");
        }

        //Handles TTS points redeems and enabling OBS talking head if available
        async private void TtsRedeem(OnChannelPointsRewardRedeemedArgs e, int speechRate)
        {

            if (obs.IsConnected)
            {
                ttsSceneName = obs.GetCurrentProgramScene();

                List<SceneItemDetails> sceneItemList = obs.GetSceneItemList(ttsSceneName);

                ttsSceneItem = sceneItemList.FirstOrDefault(sceneItem => sceneItem.SourceName == TTSTalkingHeadName);

                if (ttsSceneName != null && ttsSceneItem != null)
                {
                    try
                    {
                        await CheckAccessToken();

                        //Log("TTS Talking Head Source Found");

                        //get id and login of user, send request to Twitch API to get profile image url, and set obs browser source to url
                        List<string> idSearch = new List<string>();
                        idSearch.Add(e.RewardRedeemed.Redemption.User.Id);
                        List<string> userSearch = new List<string>();
                        userSearch.Add(e.RewardRedeemed.Redemption.User.Login);

                        var users = TheTwitchAPI.Helix.Users.GetUsersAsync(idSearch, userSearch);
                        string profileImageUrl = users.Result.Users[0].ProfileImageUrl;

                        InputSettings testInSet = obs.GetInputSettings("TTS Talking Head");

                        testInSet.Settings["url"] = profileImageUrl;

                        obs.SetInputSettings(testInSet);

                        obs.SetSceneItemEnabled(ttsSceneName, ttsSceneItem.ItemId, true);


                        SpeechSynthObj.SpeechSynthAsync(e.RewardRedeemed.Redemption.UserInput, speechRate);

                        CloseTTSFace();
                    }
                    catch (Exception except)
                    {
                        Log($"TtsRedeem Error: {except.Message}");

                        SpeechSynthObj.SpeechSynthAsync(e.RewardRedeemed.Redemption.UserInput, speechRate);
                    }
                }
                else
                {
                    Log("TTS Talking Head Source not found in current OBS scene");

                    SpeechSynthObj.SpeechSynthAsync(e.RewardRedeemed.Redemption.UserInput, speechRate);
                }
            }
            else
            {
                //trigger TTS without calling obs-related methods
                if (speechRate == -100)
                    SpeechSynthObj.SpeechSynthAsync(e.RewardRedeemed.Redemption.UserInput);
                else
                    SpeechSynthObj.SpeechSynthAsync(e.RewardRedeemed.Redemption.UserInput, speechRate);
            }


        }

        //Write ad break starting message, wait for however long the current ad break is, and send an ad break ending message
        async private void OnCommercial_NewThread(object sender, OnCommercialArgs e)
        {
            int commercialBreakLength = e.Length;
            int threadSleepLength = commercialBreakLength * 1000;

            Log("PubSub_OnCommercial: Ads started for " + commercialBreakLength + " seconds");

            await CheckAccessToken();

            if (commercialBreakLength >= 60)
            {
                double commercialBreakLengthMin = (double)commercialBreakLength / 60;
                OwnerOfChannelConnection.SendMessage(TwitchChannelName, "Ads have started and will last for " + commercialBreakLengthMin
                    + " minutes. Feel free to stretch a bit, hydrate, or just chill out in chat!");
            }
            else
            {
                OwnerOfChannelConnection.SendMessage(TwitchChannelName, "Ads have started and will last for " + commercialBreakLength
                    + " seconds. Feel free to stretch a bit, hydrate, or just chill out in chat!");
            }

            Thread.Sleep(threadSleepLength);
            Log("PubSub_OnCommercial: Ads have finished");
            //OwnerOfChannelConnection.SendMessage(TwitchChannelName, "Ad break is now done!");
        }

        //Ping API Ninja for a fact and output to twitch chat
        async private void APINinjaGetFact()
        {
            try
            {
                //no need to add a limit since default is already 1
                string apiUrl = "facts?X-Api-Key=" + Properties.Settings.Default.APINinjaKey;
                var response = await NinjaAPIConnection.GetAsync(apiUrl);

                if (response.IsSuccessStatusCode)
                {
                    //API get request
                    string stringResponse = await response.Content.ReadAsStringAsync();

                    //convert API call to array of objects. we only ever call 1 result from API so length will always be 1
                    APINinjaFacts[] result = JsonConvert.DeserializeObject<APINinjaFacts[]>(stringResponse);

                    OwnerOfChannelConnection.SendMessage(TwitchChannelName, result[0].fact);
                    //TwitchPlays.SpeechSynthSync(result[0].fact);
                    SpeechSynthObj.SpeechSynth(result[0].fact);
                }
            }
            catch (Exception except)
            {
                Log("Fact command: " + except.Message);
            }
        }

        //Ping API Ninja for a dad joke and output to twitch chat
        async private void APINinjaGetDadJoke()
        {
            try
            {
                //no need to add a limit since default is already 1
                string apiUrl = "dadjokes?X-Api-Key=" + Properties.Settings.Default.APINinjaKey;
                var response = await NinjaAPIConnection.GetAsync(apiUrl);

                if (response.IsSuccessStatusCode)
                {
                    //API get request
                    string stringResponse = await response.Content.ReadAsStringAsync();

                    //convert API call to array of objects. we only ever call 1 result from API so length will always be 1
                    APINinjaDadJokes[] result = JsonConvert.DeserializeObject<APINinjaDadJokes[]>(stringResponse);

                    OwnerOfChannelConnection.SendMessage(TwitchChannelName, result[0].joke);
                    //TwitchPlays.SpeechSynthSync(result[0].joke);
                    SpeechSynthObj.SpeechSynth(result[0].joke);
                }
            }
            catch (Exception except)
            {
                Log("Dad Joke command: " + except.Message);
            }
        }

        //Checks current access token. If invalid then get new Access Token
        async private Task CheckAccessToken()
        {
            //Log("Checking AccessToken...");

            var tokenResponse = TheTwitchAPI.Auth.ValidateAccessTokenAsync();
            ValidateAccessTokenResponse tokenResult = await tokenResponse;

            //tokenResult is null if current Access Token is invalid
            //added ExpiresIn case to allow for the code needing an access token to fully execute
            if (tokenResult == null || tokenResult.ExpiresIn <= 10)
            {
                Log("CheckAccessToken: Bad token, refreshing");

                try
                {
                    var result = TheTwitchAPI.Auth.RefreshAuthTokenAsync(CachedRefreshToken, ClientSecret); //start the process of refreshing tokens
                    RefreshResponse response = await result;    //retreive the results of token refresh

                    //Log($"Old Access: {TheTwitchAPI.Settings.AccessToken}\t\tOld Refresh: {CachedRefreshToken}");

                    CachedOwnerOfChannelAccessToken = response.AccessToken;
                    TheTwitchAPI.Settings.AccessToken = response.AccessToken;

                    CachedRefreshToken = response.RefreshToken;

                    //Log($"New AccessToken: {TheTwitchAPI.Settings.AccessToken}\t\tNew Refresh: {CachedRefreshToken}");
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

            var tokenResponse = TheTwitchAPI.Auth.ValidateAccessTokenAsync();
            ValidateAccessTokenResponse tokenResult = await tokenResponse;

            //tokenResult is null if current Access Token is invalid
            if (tokenResult == null)
            {
                Log("CheckAccessToken: Bad token, refreshing");

                try
                {
                    var result = TheTwitchAPI.Auth.RefreshAuthTokenAsync(CachedRefreshToken, ClientSecret); //start the process of refreshing tokens
                    RefreshResponse response = await result;    //retreive the results of token refresh

                    //Log($"Old Access: {TheTwitchAPI.Settings.AccessToken}\t\tOld Refresh: {CachedRefreshToken}");

                    CachedOwnerOfChannelAccessToken = response.AccessToken;
                    TheTwitchAPI.Settings.AccessToken = response.AccessToken;

                    CachedRefreshToken = response.RefreshToken;

                    //Log($"New AccessToken: {TheTwitchAPI.Settings.AccessToken}\t\tNew Refresh: {CachedRefreshToken}");
                }
                catch (Exception except)
                {
                    Log($"CheckAuthToken Error: {except.Message}");
                }
            }
            else
                Log($"Current access token is valid for {tokenResult.ExpiresIn} seconds");
        }

        async private void ReinstateModRole(string userIdToMod, string username, int banLength)
        {
            //------------------------------TEST  (add 2 seconds to sleep. might fix issue of mods not getting role back )
            Thread.Sleep(banLength * 1000); //wait for user's timeout to finish (seconds)

            try
            {
                //OwnerOfChannelConnection.Mod(TwitchChannelName, userIdToMod);
                await TheTwitchAPI.Helix.Moderation.AddChannelModeratorAsync(TwitchChannelId, userIdToMod);

                List<string> testSearchMods = new List<string>();
                testSearchMods.Add(userIdToMod);
                GetModeratorsResponse modsResult = await TheTwitchAPI.Helix.Moderation.GetModeratorsAsync(TwitchChannelId, testSearchMods);

                if (modsResult.Data.Length > 0)
                    Log($"Mod Role reinstated for: {modsResult.Data[0].UserName}");
                else
                    throw new Exception("Unable to restore mod role for: " + username);
            }
            catch (Exception except)
            {
                Log($"ReinstateModRole Error: {except.Message}");
            }
        }

        //Handles the disabling of talking head image in OBS after TTS points redeem events are called
        public void CloseTTSFace()
        {
            try
            {
                //ensure TTS is running before starting to check states
                Thread.Sleep(100);

                //check every 100ms if TTS is actively speaking. if finished speaking, disable TTS face
                while (SpeechSynthObj.asyncSynth.State.ToString() == "Speaking")
                {
                    Thread.Sleep(100);
                }

                if (obs.IsConnected && ttsSceneItem != null)
                {
                    obs.SetSceneItemEnabled(ttsSceneName, ttsSceneItem.ItemId, false);
                }
            }
            catch (Exception e)
            {
                Log("CloseTTSFace Error: " + e.Message);
            }
        }

        //handles returning a total value for !roll chat command
        public void Roll(long numOfDice, long sizeOfDie)
        {
            Random random = new Random();
            long total = 0;

            for(int x = 1; x <= numOfDice; x++)
            {
                total += random.NextInt64(1, sizeOfDie + 1);
            }

            if (total == 1)
            {
                OwnerOfChannelConnection.SendMessage(TwitchChannelName, "You rolled: a nat 1! Good job!");
            }
            else
            {
                OwnerOfChannelConnection.SendMessage(TwitchChannelName, "You rolled: " + total);
            }

        }

        void CloseEverything()
        {
            if (OwnerOfChannelConnection != null)
            {
                OwnerOfChannelConnection.Disconnect();
            }

            if (WebServer != null)
            {
                //WebServer.Stop();
                WebServer.Dispose();
            }

            if (PubSub != null)
            {
                PubSub.Disconnect();
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
            SpeechSynthObj.ClearAllSpeechSynthAsyncPrompts();
            SpeechSynthObj.ResumeSpeechSynthAsync();                //allows tts to function again if bot is stopping/restarting

            Log($"Bot connections closed");
        }
        //
        //----------------------End of Utility Methods----------------------
        //
    }
}