using System;
using System.Collections.Generic;
using System.Configuration;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace AS2TwitchBot
{
    class Program
    {
        private static readonly HttpClient client = new HttpClient();

        static async Task Main(string[] args)
        {
            Mutex mutex = new Mutex(true, "AS2TwitchBotOrell12");
            Console.Title = "AS2TwitchBot by orell12";

            Console.WriteLine("Starting bot");

            if (mutex.WaitOne(TimeSpan.Zero, true))
            {
                try
                {
                    var response = await client.GetAsync("https://raw.githubusercontent.com/audiosurf/AS2TwitchBot/master/lastestversion.txt");

                    if (await response.Content.ReadAsStringAsync() == System.Reflection.Assembly.GetEntryAssembly().GetName().Version.ToString())
                    {
                        runProgram();
                    }
                    else
                    {
                        Console.WriteLine("New bot version available please download it");
                        Console.WriteLine("Press enter to close...");
                        Console.ReadLine();
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine("Could not check for updates " + e);
                    Console.WriteLine("Press enter to close...");
                    Console.ReadLine();

                }
            }
            else
            {
                Console.WriteLine("Only one instance at a time!");
                Console.WriteLine("Press enter to close...");
                Console.ReadLine();
            }

            String getSetting(String key, String message)
            {
                var configFile = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
                var settings = configFile.AppSettings.Settings;

                if (settings[key] != null)
                {
                    return settings[key].Value;
                }
                else
                {
                    String input;
                    if (key == "serverIp")
                    {
                        input = "https://audiosurf2.info";
                    }
                    else
                    {
                        Console.WriteLine(message);
                        input = Console.ReadLine().Trim();
                    }
                    settings.Add(key, input);
                    configFile.Save(ConfigurationSaveMode.Modified);
                    ConfigurationManager.RefreshSection(configFile.AppSettings.SectionInformation.Name);
                    return input;
                }
            }

            void runProgram()
            {
                string _twitchOAuth = getSetting("twitchOAuth", "\nPlease enter Twitch OAuth Token for the bot from https://twitchapps.com/tmi/");
                string _botName = getSetting("botName", "\nPlease enter Bot username (the account name you used on the OAuth step)").ToLower() ;
                string _broadcasterName = getSetting("broadcasterName", "\nPlease enter the twitch channelname").ToLower();
                string _as2ApiKey = getSetting("as2ApiKey", "\nPlease enter the Audiosurf2.info API Key from https://audiosurf2.info/user/settings");
                string _withPrefix = getSetting("withPrefix", "\nShould the bot use the prefix !sr and !songrequest ? if not then all youtube links will be used\n(yes/no)").ToLower();
                string _serverIp = getSetting("serverIp", "");

                Console.Clear();

                // Initialize and connect to Twitch chat
                IrcClient irc = new IrcClient("irc.twitch.tv", 6667, _botName, _twitchOAuth, _broadcasterName);

                // Ping to the server to make sure this bot stays connected to the chat
                // Server will respond back to this bot with a PONG (without quotes):
                // Example: ":tmi.twitch.tv PONG tmi.twitch.tv :irc.twitch.tv"
                PingSender ping = new PingSender(irc);
                ping.Start();

                // Listen to the chat until program exits
                while (true)
                {
                    // Read any message from the chat room
                    string message = irc.ReadMessage();
                    Console.WriteLine(message); // Print raw irc messages

                    if (message.Contains("PRIVMSG"))
                    {
                        // Messages from the users will look something like this (without quotes):
                        // Format: ":[user]![user]@[user].tmi.twitch.tv PRIVMSG #[channel] :[message]"

                        // Modify message to only retrieve user and message
                        int intIndexParseSign = message.IndexOf('!');
                        string userName = message.Substring(1, intIndexParseSign - 1); // parse username from specific section (without quotes)
                                                                                       // Format: ":[user]!"
                                                                                       // Get user's message
                        intIndexParseSign = message.IndexOf(" :");
                        message = message.Substring(intIndexParseSign + 2);

                        //Console.WriteLine(message); // Print parsed irc message (debugging only)

                        // Broadcaster commands
                        if (userName.Equals(_broadcasterName))
                        {
                            if (message.Equals("!exitbot"))
                            {
                                irc.SendPublicChatMessage("Bye! Have a beautiful time!");
                                mutex.ReleaseMutex();
                                Environment.Exit(0); // Stop the program
                            }
                        }

                        // General commands anyone can use
                        var regex = @"(?i)(?:youtube\.com\/\S*(?:(?:\/e(?:mbed))?\/|watch\?(?:\S*?&?v\=))|youtu\.be\/)([a-zA-Z0-9_-]{6,11})(?-i)";

                        var match = Regex.Match(message, regex);

                        if (match.Success && match.Groups[1].Value.Length == 11)
                        {
                            if (_withPrefix.StartsWith("y"))
                            {
                                if (message.ToLower().StartsWith("!sr") || message.ToLower().StartsWith("!songrequest"))
                                {
                                    sendToServer(match.Groups[1].Value);
                                }
                            }
                            else
                            {
                                sendToServer(match.Groups[1].Value);
                            }
                        }

                        async void sendToServer(String youtubeId)
                        {
                            Console.WriteLine("sending id: " + youtubeId + " to server");
                            var values = new Dictionary<string, string> {
                               { "username", userName },
                               { "youtubeId", youtubeId },
                               { "apiKey", _as2ApiKey }
                            };

                            var content = new FormUrlEncodedContent(values);

                            try
                            {
                                var response = await client.PostAsync(_serverIp + "/twitch/queue_add", content);

                                var responseString = await response.Content.ReadAsStringAsync();

                                if (responseString.Length < 200 && response.IsSuccessStatusCode)
                                {
                                    irc.SendPublicChatMessage("@" + userName + " " + responseString);
                                } else
                                {
                                    Console.WriteLine("response error " + responseString);
                                }
                            }
                            catch (Exception e)
                            {
                                Console.WriteLine(e);
                            }
                        }
                    }
                }
            }
        }
    }
}
