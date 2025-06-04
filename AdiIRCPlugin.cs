using System;
using AdiIRCAPIv2.Arguments.ChannelMessages;
using AdiIRCAPIv2.Arguments.PrivateMessages;
using AdiIRCAPIv2.Interfaces;
using IChannel = AdiIRCAPIv2.Interfaces.IChannel;

namespace AdiIRC_LibreTranslate_plugin
{
    public class AdiIRC_LibreTranslate_plugin : IPlugin
    {
        public string PluginName => "AdiIRC LibreTranslate Plugin";
        public string PluginDescription => "Translates chat messages from both ED and AdiIRC";
        public string PluginAuthor => "";
        public string PluginVersion => "0.1";
        public string PluginEmail => "";

        public IPluginHost _host;

        private ConfigManager configManager;
        private LibreTranslate translator;
        private EliteDangerousLogReader logReader;
        private CommandHandler commandHandler;

        //TODO
        // - Optimize error in case Elite Dangerous log file is not found or not readable
        // - Add configuration options for enabling/disabling features
        // - Clean up / refactor code for better readability and maintainability
        // - Improve readme documentation for users and developers
        // - Investigate if we should increase polling rate for new logfiles or even use file system watchers

        /** Initialize the plugin with the AdiIRC plugin host.
         * This method sets up the translator, log reader, and command handler using the configuration manager.
         * It also hooks into AdiIRC events to handle incoming messages and translations.
         */
        public void Initialize(IPluginHost pluginHost)
        {
            _host = pluginHost;
            _host.ActiveIWindow.OutputText("LibreTranslate plugin loaded. Loading configuration.");

            var configPath = _host.ConfigFolder + "translate.json";
            configManager = new ConfigManager(configPath, _host);
            translator = new LibreTranslate(configManager.CurrentConfig.ApiPath, configManager.CurrentConfig.UserLanguage);
            
            // Initialize and set up the log reader
            logReader = new EliteDangerousLogReader(configManager.CurrentConfig.eliteDangerousLogPath);
            logReader.NewLogFileDetected += OnNewLogFileDetected;
            logReader.ChatMessageReceived += OnChatMessageReceived;
            logReader.StartMonitoring();

            // Translate incoming AdiIRC messages
            _host.OnChannelNormalMessage += OnChannelNormalMessage;
            _host.OnPrivateNormalMessage += OnPrivateNormalMessage;

            // Initialize command handler
            commandHandler = new CommandHandler(_host, translator, configManager.CurrentConfig.translateCommand);
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
            
            commandHandler = null;
        }
    }
}
