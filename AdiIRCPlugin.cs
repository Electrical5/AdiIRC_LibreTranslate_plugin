using System;
using AdiIRCAPIv2.Arguments.Aliasing;
using AdiIRCAPIv2.Interfaces;

namespace AdiIRC_LibreTranslate_plugin
{
    public class AdiIRC_LibreTranslate_plugin : IPlugin
    {
        public string PluginName => "AdiIRC LibreTranslate Plugin";
        public string PluginDescription => "Translates chat messages";
        public string PluginAuthor => "";
        public string PluginVersion => "0.1";
        public string PluginEmail => "";

        public IPluginHost _host;

        private ConfigManager configManager;
        private LibreTranslate translator;
        private EliteDangerousLogReader logReader;

        public void Initialize(IPluginHost pluginHost)
        {
            _host = pluginHost;
            var activeWindow = _host.ActiveIWindow;
            activeWindow.OutputText("LibreTranslate plugin loaded. Loading configuration.");

            var configPath = _host.ConfigFolder + "translate.json";
            configManager = new ConfigManager(configPath, _host);
            translator = new LibreTranslate(configManager.CurrentConfig.ApiPath, configManager.CurrentConfig.UserLanguage);
            logReader = new EliteDangerousLogReader(configManager.CurrentConfig.eliteDangerousLogPath);

            _host.HookCommand(configManager.CurrentConfig.translateCommand, OnTranslateCommand);
        }
        public void Dispose()
        {
            //TODO undo everything done in Initialize.
        }

        private async void OnTranslateCommand(RegisteredCommandArgs argument)
        {
            // Extract the command parameters
            string parameters = argument.Command.Trim();
            string[] parts = parameters.Split(new[] { ' ' }, 3);

            //Check if the at least the language and message are set.
            if (parts.Length < 3)
            {
                argument.Window.OutputText("Usage: " + configManager.CurrentConfig.translateCommand + " <language code> <text>");
                return;
            }

            string targetLanguage = parts[1];
            string textToTranslate = parts[2];

            try
            {
                LibreTranslate.TranslationResponse result = await translator.translate(textToTranslate, targetLanguage);
                string translatedMessage = result?.translatedText;
                string sourceLanguage = result?.detectedLanguage?.language;
                string confidence = result?.detectedLanguage?.confidence.ToString("F2") ?? "N/A";
                if (!string.IsNullOrEmpty(translatedMessage))
                {
                    // Print the translated message to the active window
                    string output = $"[Translated {sourceLanguage.ToUpper()}|{targetLanguage.ToUpper()} {confidence}%]: {translatedMessage}";
                    _host.ActiveIWindow.Editbox.Text = output;
                }
            }
            catch (Exception ex)
            {
                string output = $"[Error when translating]: {ex.Message}";
                argument.Window.OutputText(output);
            }
        }
    }
}
