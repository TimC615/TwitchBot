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

//maybe put in timed messages (eg: after 30 min shout out socials and following)

//maybe put in automatic discord messaging? (eg: hey i'm live messages)
//---------------------------------------------------------------------------------------------------------------------------
namespace TwitchBot
{
    public partial class MainWindow : Window
    {
        //Authentication
        private HttpServer WebServer;
        private readonly string RedirectUrl = "http://localhost";
        private readonly string ClientId = Properties.Settings.Default.clientid;
        private readonly string ClientSecret = Properties.Settings.Default.clientsecret;
        private readonly List<string> Scopes = new List<string>
        { "user:edit", "chat:read", "chat:edit", "channel:moderate", "whispers:read", "bits:read",
            "channel:read:subscriptions", "user:read:email", "user:read:subscriptions", "channel:manage:redemptions",
            "channel:edit:commercial", "channel:manage:ads" };
        //find more Twitch API scopes at https://dev.twitch.tv/docs/authentication/scopes/

        //, "channel:edit:commercial" //using with WitchLib.PubSub (points redeems) and users triggering ad breaks

        //TwitchLib
        private TwitchClient OwnerOfChannelConnection;
        private TwitchAPI TheTwitchAPI;
        private TwitchPubSub PubSub;

        //API Ninja
        private static HttpClient ninjaAPIConnection { get; set; }

        //OBS Websocket
        protected OBSWebsocket obs;

        //Cached Variables
        private string CachedOwnerOfChannelAccessToken = "needsaccesstoken"; //cached due to potentially being needed for API requests
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
            { "commands", "The current commands are: help, about, discord, twitter, lurk, roll, fact, and roll" },
            { "about", "Hello! I'm TheCakeIsAPie__ and I'm a Canadian variety streamer. We play a bunch of stuff over here in this small corner of the internet. Come pop a seat and have fun watching the shenanigans!"},
            { "discord", "Join the discord server at: https://discord.gg/uzHqnxKKkC"},
            { "twitter", "Follow me on Twitter at: https://twitter.com/TheCakeIsAPi"},
            { "lurk", "Have fun lurking!"}
        };

        public MainWindow()
        {
            InitializeComponent();
        }

        private void connectToTwitch_Click(object sender, RoutedEventArgs e)
        {
            initializeWebServer();

            //var authUrl = $"https://id.twitch.tv/oauth2/authorize?response_type=code&client_id={{ClientId}}&redirect_uri={{RedirectUrl}}&scope={{String.Join("+", Scopes)}}";
            var authUrl = "https://id.twitch.tv/oauth2/authorize?response_type=code&client_id=" +
                ClientId + "&redirect_uri=" + RedirectUrl + "&scope=" + String.Join("+", Scopes);

            Trace.WriteLine(authUrl);


            System.Diagnostics.Process.Start(new ProcessStartInfo
            {
                FileName = authUrl,
                UseShellExecute = true
            });

            connectToTwitch.Opacity = 0.50;
            connectToTwitch.IsEnabled = false;
        }

        void initializeWebServer()
        {
            //Create local web server (allows for requesting OAUTH token)
            WebServer = new HttpServer();
            WebServer.EndPoint = new IPEndPoint(IPAddress.Loopback, 80);
            //WebServer.EndPoint = new IPEndPoint(IPAddress.Loopback, 8080);
            //WebServer.EndPoint = new IPEndPoint(IPAddress.Loopback, 49152);

            //
            WebServer.RequestReceived += async (s, e) =>
            {
                using (var writer = new StreamWriter(e.Response.OutputStream))
                {
                    if (e.Request.QueryString.AllKeys.Any("code".Contains))
                    {
                        //initialize base TwitchLib API
                        var code = e.Request.QueryString["code"];
                        var ownerOfChannelAccessAndRefresh = await getAccessAndRefreshTokens(code);
                        CachedOwnerOfChannelAccessToken = ownerOfChannelAccessAndRefresh.Item1;

                        SetNameAndIdByOauthedUser(CachedOwnerOfChannelAccessToken).Wait();
                        InitializeOwnerOfChannelConnection(TwitchChannelName, CachedOwnerOfChannelAccessToken);
                        InitializeTwitchAPI(CachedOwnerOfChannelAccessToken);


                        //initialize connection to facts api
                        initializeNinjaAPI();

                        //initialize connection to OBS websocket
                        initializeOBSWebSocket();

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

        void InitializeTwitchAPI(string accessToken)
        {
            TheTwitchAPI = new TwitchAPI();
            TheTwitchAPI.Settings.ClientId = ClientId;
            TheTwitchAPI.Settings.AccessToken = accessToken;
            //TheTwitchAPI.Settings.Secret = ClientSecret;
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
            PubSub.ListenToVideoPlayback(TwitchChannelId);        //used for StreamUp() and StreamDown()?? Maybe??

            PubSub.Connect();
        }

        void initializeNinjaAPI()
        {
            ninjaAPIConnection = new HttpClient();
            ninjaAPIConnection.BaseAddress = new Uri("https://api.api-ninjas.com/v1/");
            //requires an API key to be added to requests but will be handled during actual GET requests

            //ninjaAPIConnection.BaseAddress = new Uri("https://jsonplaceholder.typicode.com/");
        }

        void initializeOBSWebSocket()
        {
            obs = new OBSWebsocket();

            obs.Connected += obs_onConnect;
            obs.Disconnected += obs_onDisconnect;

            obs.ConnectAsync("ws://192.168.2.22:4455", Properties.Settings.Default.OBSWebSocketAuth);

            //OBSWebsocketDotNet method documentation available at:
            //https://github.com/BarRaider/obs-websocket-dotnet/blob/master/obs-websocket-dotnet/OBSWebsocket_Requests.cs
        }

        //OBS Event Hookups
        private void obs_onConnect(object sender, EventArgs e)
        {
            Log("OBS connected");
            obsConnected = true;
        }

        private void obs_onDisconnect(object sender, OBSWebsocketDotNet.Communication.ObsDisconnectionInfo e)
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

        //PubSub Event Hookups
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
            Log("PubSub Service Error: " + e.Exception.Message);
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

                            if (!webcamEnabled)
                            {
                                obs.SetSceneItemEnabled(currSceneName, webcamItemID, true);
                                Log("Toggle Webcam: Webcam enabled");
                            }
                            else
                            {
                                obs.SetSceneItemEnabled(currSceneName, webcamItemID, false);
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
                    try
                    {
                        Random random = new Random();
                        int randRate = random.Next(1, 21) - 10;

                        string currSceneName = obs.GetCurrentProgramScene();
                        int ttsItemID = obs.GetSceneItemId(currSceneName, "TwitchChatFace", 0);
                        obs.SetSceneItemEnabled(currSceneName, ttsItemID, true);


                        //SpeechSynthObj.SpeechSynth(e.RewardRedeemed.Redemption.UserInput, randRate);
                        SpeechSynthObj.SpeechSynthAsync(e.RewardRedeemed.Redemption.UserInput, randRate);

                        //disable TTS face
                    }
                    catch (Exception err)
                    {
                        Log("Random speech TTS Error: " + err.Message);
                    }
                    break;
                case "tts (normal speech rate)":
                    try
                    {
                        string currSceneName = obs.GetCurrentProgramScene();
                        int ttsItemID = obs.GetSceneItemId(currSceneName, "TwitchChatFace", 0);
                        obs.SetSceneItemEnabled(currSceneName, ttsItemID, true);

                        //TwitchPlays.SpeechSynthSync(e.RewardRedeemed.Redemption.UserInput);
                        SpeechSynthObj.SpeechSynthAsync(e.RewardRedeemed.Redemption.UserInput);

                        //disable TTS face
                    }
                    catch (Exception err)
                    {
                        Log("Normal speech TTS Error: " + err.Message);
                    }

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

        private void OnCommercial_NewThread(object sender, OnCommercialArgs e)
        {
            int commercialBreakLength = e.Length;
            int threadSleepLength = commercialBreakLength * 1000;

            Log("PubSub_OnCommercial: Ads started at " + e.ServerTime + " for " + commercialBreakLength + " seconds");

            if (commercialBreakLength >= 60)
            {
                double commercialBreakLengthMin = (double)commercialBreakLength / 60;
                OwnerOfChannelConnection.SendMessage(TwitchChannelName, "Ads have started and will last for " + commercialBreakLengthMin
                    + " minutes. Feel free to stretch a bit, hydrate, or just chill out in chat!");
                Thread.Sleep(threadSleepLength);
                OwnerOfChannelConnection.SendMessage(TwitchChannelName, "Ad break is now done!");
            }
            else
            {
                OwnerOfChannelConnection.SendMessage(TwitchChannelName, "Ads have started and will last for " + commercialBreakLength
                    + " seconds. Feel free to stretch a bit, hydrate, or just chill out in chat!");
                Thread.Sleep(threadSleepLength);
                OwnerOfChannelConnection.SendMessage(TwitchChannelName, "Ad break is now done!");
            }
        }

        //TwitchClient Event Hookups
        private void Client_OnConnected(object sender, OnConnectedArgs e)
        {
            Log($"User {e.BotUsername} connected (bot access)");
        }

        private void OwnerOfChannelConnection_OnDisconnected(object sender, TwitchLib.Communication.Events.OnDisconnectedEventArgs e)
        {
            Log($"OnDisconnected event");
        }

        private void OwnerOfChannelConnection_OnLog(object sender, TwitchLib.Client.Events.OnLogArgs e)
        {
            Log($"OnLog: {e.Data}");
        }

        private void Bot_OnChatCommandReceived(object sender, OnChatCommandReceivedArgs e)
        {
            string commandText = e.Command.CommandText.ToLower();
            //2 ways to deal with commands: if/switch statements OR dictionary lookups

            //
            //---------------------------------------------------------------------------------------------------------------------
            //

            //responses are added to dictionary in lowercase
            if (CommandsStaticResponses.ContainsKey(commandText))
            {
                OwnerOfChannelConnection.SendMessage(TwitchChannelName, CommandsStaticResponses[commandText]);
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

                                    //sending whispers doesn't seem to work atm
                                    //OwnerOfChannelConnection.SendWhisper(e.Command.ChatMessage.UserId, "Enter \"!" + helpSpecifier + "\" and I'll do all the rest");
                                    //OwnerOfChannelConnection.SendWhisper(e.Command.ChatMessage.Username, "Enter \"!" + helpSpecifier + "\" and I'll do all the rest");
                                    break;
                                case "roll":
                                    OwnerOfChannelConnection.SendMessage(TwitchChannelName,
                                        "Enter \"!roll d<number of sides>\" and just put in however many sides you want (e.g. !roll d6)");
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
                        List<string> rollCommand = e.Command.ArgumentsAsList;
                        long die = Int64.Parse(rollCommand[0].ToLower().Remove(0, 1));

                        Random random = new Random();
                        if (die > 1)
                        {
                            long result = random.NextInt64(1, die + 1);
                            OwnerOfChannelConnection.SendMessage(TwitchChannelName, "You rolled: " + result);
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
                //Tell user what skyrim spawn commands are avaialble
                if (commandText.Equals("skyrim"))
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
            //twitchChatMoveOBSImage(e.ChatMessage.Message);

            if (twitchPlaysEnable)
            {
                Log($"OnMessageReceived: {e.ChatMessage.Username.ToLower()} - {e.ChatMessage.Message.ToLower()}");
                //Keyboard.KeyDownEvent();

                //TwitchPlays.SpeechSynthSync(e.ChatMessage.Message);

                //Twitch plays Skyrim SE
                TwitchPlaysObj.TwitchPlaysSkyrim(e);



                //simple testing to hear how things sound through the speechsynth
                //TwitchPlays.SpeechSynth(e.ChatMessage.Message.ToString());

                //failed multi-treading idea
                //ThreadPool.QueueUserWorkItem(TwitchPlays.SpeechSynth, e.ChatMessage.Message) ;

            }
        }


        /*
        void twitchChatMoveOBSImage(string message)
        {
            try
            {
                //string currSceneName = obs.GetCurrentProgramScene();
                //int twitchChatFaceItemID = obs.GetSceneItemId(currSceneName, "TwitchChatFace", 0);
                //bool twitchChatFaceEnabled = obs.GetSceneItemEnabled(currSceneName, twitchChatFaceItemID);

                //Log("1: " + e.RewardRedeemed.Redemption.Status);

                //obs.SetSceneItemEnabled(currSceneName, twitchChatFaceItemID, true);

                TwitchPlays.SpeechSynth(message);

                //obs.SetSceneItemEnabled(currSceneName, twitchChatFaceItemID, false);

            }
            catch (Exception except)
            {
                Log("Twitch Chat Speaking Error: " + except.Message);

                //Log(e.RewardRedeemed.Redemption.Status);
            }
        }
        */

        //Sends log messages to both the user form and console
        public void Log(string printMessage)
        {
            Action writeToConsoleLog = () => {
                ConsoleLog.AppendText("\n" + printMessage);
                ConsoleLog.ScrollToEnd();
                };

            Dispatcher.BeginInvoke(writeToConsoleLog);

            //not using Console.WriteLine() as WPF doesn't have a console window
            //writes to 'Output' window during debug instead
            Trace.WriteLine(printMessage);
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

        async Task<Tuple<String, String>> getAccessAndRefreshTokens(string code)
        {
            HttpClient client = new HttpClient();
            var values = new Dictionary<string, String>
            {
                { "client_id", ClientId },
                { "client_secret", ClientSecret },
                { "code", code },
                { "grant_type", "authorization_code" },
                { "redirect_uri", RedirectUrl }
            };

            var content = new FormUrlEncodedContent(values);

            var response = await client.PostAsync("https://id.twitch.tv/oauth2/token", content);

            var responseString = await response.Content.ReadAsStringAsync();
            var json = JObject.Parse(responseString);

            return new Tuple<string, string>(json["access_token"].ToString(), json["refresh_token"].ToString());
        }

        private void TwitchPlaysButton_Click(object sender, RoutedEventArgs e)
        {
            if (twitchPlaysEnable)
            {
                twitchPlaysEnable = false;

                Log($"Twitch Plays disabled");
                twitchPlaysButton.Content = "Enable Twitch Plays";
            }
            //enable Twitch Plays functionality after a 5 second cooldown
            else
            {
                //start countdown on a different thread, freeing up UI thread
                new Thread(TwitchPlaysCoundown).Start();
            }
        }

        private void ConnectToOBS_Click(object sender, RoutedEventArgs e)
        {

        }

        private void TwitchPlaysCoundown()
        {
            Log($"Enabling Twitch Plays in 5 seconds...");

            //invoke the UI thread, allowing UI changes from a different thread
            this.Dispatcher.Invoke(() => {
                twitchPlaysButton.Content = "Starting...";
                twitchPlaysButton.Opacity = 0.75;
                twitchPlaysButton.IsEnabled = false;
            });

            Thread.Sleep(5000);

            twitchPlaysEnable = true;
            TwitchPlaysObj = new TwitchPlays();

            Log($"Twitch Plays now live");

            //invoke the UI thread, allowing UI changes from a different thread
            this.Dispatcher.Invoke(() => {
                twitchPlaysButton.Content = "Disable Twitch Plays";
                twitchPlaysButton.Opacity = 1;
                twitchPlaysButton.IsEnabled = true;
            });


            System.Media.SoundPlayer twitchPlaysStartup = new System.Media.SoundPlayer("C:\\Users\\timot\\source\\repos\\TwitchBot\\Twitch Plays startup sound.wav");
            twitchPlaysStartup.Play();

            SpeechSynthObj.SpeechSynth("Twitch Plays is now live");
        }

        async private void APINinjaGetFact()
        {
            try
            {
                //no need to add a limit since default is already 1
                string apiUrl = "facts?X-Api-Key=" + Properties.Settings.Default.APINinjaKey;
                var response = await ninjaAPIConnection.GetAsync(apiUrl);

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

        async private void APINinjaGetDadJoke()
        {
            try
            {
                //no need to add a limit since default is already 1
                string apiUrl = "dadjokes?X-Api-Key=" + Properties.Settings.Default.APINinjaKey;
                var response = await ninjaAPIConnection.GetAsync(apiUrl);

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


        private void MainWindow_Closing(object sender, EventArgs e)
        {
            //could probably just call Application.Exit()
            if (OwnerOfChannelConnection != null)
            {
                OwnerOfChannelConnection.Disconnect();
            }

            if (WebServer != null)
            {
                WebServer.Stop();
                WebServer.Dispose();
            }

            if(PubSub != null)
            {
                PubSub.Disconnect();
            }

            if (ninjaAPIConnection != null)
            {
                ninjaAPIConnection.Dispose();
            }

            if (obs != null)
            {
                obs.Disconnect();
            }
        }
    }
}