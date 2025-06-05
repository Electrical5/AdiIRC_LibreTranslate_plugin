# AdiIRC LibreTranslate plugin
This plugin will automatically translate incoming chat messages from both AdiIRC and in-game messages.
Besides this, it also allows translating

Translation is done using LibreTranslate with configurable API endpoint.

# Example usecases:

## Automatically translate incoming chat messages
No need for copy-pasting from Elite Dangerous log folder anymore.
![Screenshot showing automatic translation from Elite Dangerous in-game chat to AdiIRC](/Translating-ED-Chat.png)

## Automatically translate any incoming AdiIRC chat message.
Conveniently show both the native and translated message (and accuracy).
![Screenshot showing translating incoming AdiIRC messages directly](/Translating-AdiIRC-chat.png)

## Send a chat message in AdiIRC in another language:
1. Type /tr [language code] [text]:
![Screenshot showing translate command](/Translating-AdiIRC-chatbox-1.png)
2. Press enter to translate in editbox directly:
![Screenshot showing translation output](/Translating-AdiIRC-chatbox-2.png)
3. Press enter to send directly!

# Warnings

- (AI) translations can be incorrect.
- Fuel Rats: This plugin is meant for experienced rats.
- Fuel Rat Dispatchers: Always ask the client if they speak English. It is always better to dispatch cases in English if the client speaks it fluently.

# Installation

 1. [Download](https://github.com/Electrical5/AdiIRC_LibreTranslate_plugin/tags) the latest package
 2. Close AdiIRC
 3. Extract the 2 DLL files to your AdiIRC Plugin Directory
    - Default location: %localappdata%\AdiIRC\Plugins
 4. Open AdiIRC
 5. Click to Files -> Plugins.
    - If the plugin is not listed: Click "Install New". Select the "adiIRC_LibreTranslate_plugin.dll" from the Plugins directory.
    - If the plugin is listed: Select "adiIRC_DeepL.dll" and click "Load"
 6. Optionally adjust the configuration: %localappdata%\AdiIRC\AdiIRC_LibreTranslate_plugin_settings.json
 7. Reload the plugin or restart AdiIRC to reload the configuration

# Special thanks

Special thanks to:

- Delrynn and Velica Foriana for [AdiIRC_Deepl_Plugin](https://github.com/Delrynn/adiIRC_DeepL_plugin) as inspiration and starting point.

- Tzarnal for documenting the AdiIRC plugin API.

- Copilot / Claude 3.7 Sonnet for keeping me busy refactoring AI garbage.

# License

Licensed under CC-BY-NC-SA 4.0.
https://creativecommons.org/licenses/by-nc-sa/4.0/

BY: credit must be given to the creator.
NC: Only noncommercial uses of the work are permitted.
SA: Adaptations must be shared under the same terms.
