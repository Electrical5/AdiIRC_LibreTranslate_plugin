using System;
using System.Threading.Tasks;
using AdiIRCAPIv2.Arguments.Aliasing;
using AdiIRCAPIv2.Interfaces;

namespace AdiIRC_LibreTranslate_plugin
{
    /** CommandHandler class for handling commands within the AdiIRC chat window
     * This class listens for a specific command (e.g., "/tr") and translates the provided text
     * using the LibreTranslate API. It updates the active window's editbox with the translated text for easy sending.
     */
    internal class CommandHandler
    {
        private readonly IPluginHost _host;
        private readonly LibreTranslate _translator;
        private string _commandName;

        public CommandHandler(IPluginHost host, LibreTranslate translator, String translateCommand)
        {
            _host = host;
            _translator = translator;
            _commandName = translateCommand;
            _host.HookCommand(_commandName, OnTranslateCommand);
        }

        ~CommandHandler()
        {
            _host.UnHookCommand(_commandName);
        }

        private async void OnTranslateCommand(RegisteredCommandArgs argument)
        {
            // Extract the command parameters
            string parameters = argument.Command.Trim();
            string[] parts = parameters.Split(new[] { ' ' }, 3);

            //Check if the at least the language and message are set.
            if (parts.Length < 3)
            {
                argument.Window.OutputText("Usage: " + _commandName + " <language code> <text>");
                return;
            }

            string targetLanguage = parts[1];
            string textToTranslate = parts[2];

            var result = await _translator.translate(textToTranslate, targetLanguage);
            if(result.success)
            {
                _host.ActiveIWindow.Editbox.Text = result.translatedText;
            }else
            {
                _host.ActiveIWindow.Editbox.Text = "";
                _host.ActiveIWindow.OutputText("Translation failed.");
            }
        }
    }
}