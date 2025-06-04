using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;
using AdiIRCAPIv2.Interfaces;

namespace AdiIRC_LibreTranslate_plugin
{
    /**
     * ConfigManager is responsible for loading and saving the plugin configuration.
     * It reads from a JSON file and provides the current configuration to the plugin.
     * In case the configuration file does not exist, it creates a default configuration.
     * Configurations can be easily added by just adding them to the Config class, this will automatically be saved and loaded.
     */
    internal class ConfigManager
    {
        private readonly string _configPath;
        private readonly IPluginHost _host;
        public Config CurrentConfig { get; private set; }

        public ConfigManager(string configPath, IPluginHost host)
        {
            _configPath = configPath;
            _host = host;
            LoadOrCreateConfig();
        }

        private void LoadOrCreateConfig()
        {
            try
            {
                if (File.Exists(_configPath))
                {
                    string json = File.ReadAllText(_configPath);
                    var serializer = new JavaScriptSerializer();
                    CurrentConfig = serializer.Deserialize<Config>(json) ?? new Config();
                    _host.ActiveIWindow.OutputText($"Configuration loaded from {_configPath}");
                }
                else
                {
                    CurrentConfig = new Config();
                    SaveConfig();
                    _host.ActiveIWindow.OutputText($"Default configuration created at {_configPath}");
                }
            }
            catch (Exception ex)
            {
                _host.ActiveIWindow.OutputText($"Error loading config: {ex.Message}. Using defaults.");
                CurrentConfig = new Config();
            }
        }

        public void SaveConfig()
        {
            try
            {
                var serializer = new JavaScriptSerializer();
                string json = serializer.Serialize(CurrentConfig);
                File.WriteAllText(_configPath, json);
            }
            catch (Exception ex)
            {
                _host.ActiveIWindow.OutputText($"Error saving config: {ex.Message}");
            }
        }
    }

    public class Config
    {
        public string UserLanguage { get; set; } = "EN";
        public string ApiPath { get; set; } = "http://192.168.1.10:5000/translate";
        public string eliteDangerousLogPath { get; set; } = "%userprofile%\\Saved Games\\Frontier Developments\\Elite Dangerous";
        public string translateCommand { get; set; } = "/tr";
    }
}
