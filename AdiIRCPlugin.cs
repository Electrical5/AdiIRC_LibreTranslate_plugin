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

        private IPluginHost _host;

        public void Dispose()
        {
        }

        public void Initialize(IPluginHost pluginHost)
        {
            _host = pluginHost;
            var activeWindow = _host.ActiveIWindow;
            activeWindow.OutputText("LibreTranslate plugin loaded.");
        }
    }
}
