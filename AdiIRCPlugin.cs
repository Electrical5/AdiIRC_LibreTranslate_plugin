using System;
using AdiIRCAPIv2.Arguments.ChannelMessages;
using AdiIRCAPIv2.Arguments.PrivateMessages;
using AdiIRCAPIv2.Interfaces;
using IChannel = AdiIRCAPIv2.Interfaces.IChannel;

/** Project explanation:
 * This project aims to break the language barrier in AdiIRC and Elite Dangerous by providing automatic translation of chat messages.
 * The project is very configurable, allowing users to set their preferred translation API endpoint and language.
 * 
 * This plugin has 3 configurable functionalities:
 * - "/tr <language code> <text>" command to translate text in the editbox to any language, bind is configurable.
 * - Automatic translation of foreign AdiIRC messages in channels and private messages
 * - Automatic translation of Elite Dangerous chat messages.
 * 
 * Configuration file is located in the AdiIRC config folder, named "AdiIRC_LibreTranslate_plugin_settings.json".
 * By default %userprofile%\AppData\Local\AdiIRC\AdiIRC_LibreTranslate_plugin_settings.json
 * 
 * If the configuration file does not exist, it will be created with default values.
 * 
 * There's a few classes in this project:
 * - ConfigManager: Handles loading and saving the plugin configuration.
 * - LibreTranslate: Handles communication with the LibreTranslate API for translations.
 * - EliteDangerousLogReader: Monitors the Elite Dangerous log file for chat messages and translates them.
 * - CommandHandler: Handles the "/tr" command for translating text in the editbox.
 * - AdiIRC_LibreTranslate_plugin: The main plugin class that initializes everything and hooks into AdiIRC events.
 */
namespace AdiIRC_LibreTranslate_plugin
{
    public class AdiIRC_LibreTranslate_plugin : IPlugin
    {
        public string PluginName => "AdiIRC LibreTranslate Plugin";
        public string PluginDescription => "Translates chat messages from both ED and AdiIRC";
        public string PluginAuthor => "";
        public string PluginVersion => "0.9";
        public string PluginEmail => "";

        public IPluginHost _host;

        private ConfigManager configManager;
        private LibreTranslate translator;
        private EliteDangerousLogReader logReader;
        private CommandHandler commandHandler;

        /** Initialize the plugin with the AdiIRC plugin host.
         * This method sets up the translator, log reader, and command handler using the configuration manager.
         * It also hooks into AdiIRC events to handle incoming messages and translations.
         */
        public void Initialize(IPluginHost pluginHost)
        {
            _host = pluginHost;
            _host.ActiveIWindow.OutputText("LibreTranslate plugin loaded. Loading configuration.");

            var configPath = _host.ConfigFolder + "AdiIRC_LibreTranslate_plugin_settings.json";
            configManager = new ConfigManager(configPath, _host);
            translator = new LibreTranslate(configManager.CurrentConfig.ApiPath, configManager.CurrentConfig.UserLanguage);
            
            // Initialize and set up the log reader if enabled
            if (configManager.CurrentConfig.EnableEliteDangerousLogReading)
            {
                InitializeEliteDangerousLogReader();
                _host.ActiveIWindow.OutputText("Elite Dangerous log reading is enabled.");
            }
            else
            {
                _host.ActiveIWindow.OutputText("Elite Dangerous log reading is disabled in configuration.");
            }

            // Set up channel message translation if enabled
            if (configManager.CurrentConfig.EnableAdiIRCPublicMessageTranslation)
            {
                _host.OnChannelNormalMessage += OnChannelNormalMessage;
                _host.ActiveIWindow.OutputText("AdiIRC public message translation enabled.");
            }
            else
            {
                _host.ActiveIWindow.OutputText("AdiIRC public message translation is disabled in configuration.");
            }

            // Set up private message translation if enabled
            if (configManager.CurrentConfig.EnableAdiIRCPrivateMessageTranslation)
            {
                _host.OnPrivateNormalMessage += OnPrivateNormalMessage;
                _host.ActiveIWindow.OutputText("AdiIRC private message translation enabled.");
            }
            else
            {
                _host.ActiveIWindow.OutputText("AdiIRC private message translation is disabled in configuration.");
            }

            // Initialize command handler if enabled
            if (configManager.CurrentConfig.EnableCommandHandling)
            {
                commandHandler = new CommandHandler(_host, translator, configManager.CurrentConfig.translateCommand);
                _host.ActiveIWindow.OutputText($"Command handling enabled. Use {configManager.CurrentConfig.translateCommand} to translate text.");
            }
            else
            {
                _host.ActiveIWindow.OutputText("Command handling is disabled in configuration.");
            }
        }

        private void InitializeEliteDangerousLogReader()
        {
            try
            {
                // Pass the plugin host to the log reader for proper logging
                logReader = new EliteDangerousLogReader(
                    configManager.CurrentConfig.eliteDangerousLogPath,
                    _host,
                    false); // Set to true to enable debug logging
                
                logReader.NewLogFileDetected += OnNewLogFileDetected;
                logReader.ChatMessageReceived += OnChatMessageReceived;
                logReader.StartMonitoring();
                _host.ActiveIWindow.OutputText("Elite Dangerous log reading enabled.");
            }
            catch (Exception ex)
            {
                _host.ActiveIWindow.OutputText($"Error initializing Elite Dangerous log reader: {ex.Message}");
            }
        }
        
        private void OnNewLogFileDetected(object sender, string newLogFileName)
        {
            _host.ActiveIWindow.OutputText($"[ED Log] New log file detected: {newLogFileName}");
        }

        private async void OnPrivateNormalMessage(PrivateNormalMessageArgs args)
        {
            // Get the nickname of the sender
            string senderNick = args.User.Nick;

            // Find the private message window for this user by manually iterating through the windows
            IWindow pmWindow = null;
            foreach (IWindow window in _host.GetWindows)
            {
                if (window.Name.Equals(senderNick, StringComparison.OrdinalIgnoreCase))
                {
                    pmWindow = window;
                    break;
                }
            }

            // If no PM window exists for this sender, create one
            if (pmWindow != null)
            {
                var result = await translator.translate(args.Message);
                if (result.success)
                {
                    pmWindow.OutputText(result.printableResponse);
                }
            }
        }

        // When receiving a normal message in a channel, translate it and display it in the channel
        private async void OnChannelNormalMessage(ChannelNormalMessageArgs args)
        {
            IChannel channel = args.Channel;
            var result = await translator.translate(args.Message);
            if (result.success)
            {
                channel.OutputText(result.printableResponse);
            }
        }

        // When receiving a chat message from Elite Dangerous, translate it and display it in the active window
        private async void OnChatMessageReceived(object sender, ChatMessageEventArgs e)
        {
            string channelDisplay = char.ToUpper(e.Channel[0]) + e.Channel.Substring(1);
            string messagePrefix = $"[ED {channelDisplay}] {e.From}: ";
            
            // Display the original message
            _host.ActiveIWindow.OutputText(messagePrefix + e.Message);
            var result = await translator.translate(e.Message);
            if (result.success)
            {
                _host.ActiveIWindow.OutputText(result.printableResponse);
            }
        }
        
        public void Dispose()
        {
            // Clean up resources
            if (logReader != null)
            {
                logReader.NewLogFileDetected -= OnNewLogFileDetected;
                logReader.ChatMessageReceived -= OnChatMessageReceived;
                logReader.Dispose();
                logReader = null;
            }

            // Unsubscribe from event handlers if they were set up
            if (configManager.CurrentConfig.EnableAdiIRCPublicMessageTranslation)
            {
                _host.OnChannelNormalMessage -= OnChannelNormalMessage;
            }

            if (configManager.CurrentConfig.EnableAdiIRCPrivateMessageTranslation)
            {
                _host.OnPrivateNormalMessage -= OnPrivateNormalMessage;
            }
            
            commandHandler = null;
        }
    }
}
