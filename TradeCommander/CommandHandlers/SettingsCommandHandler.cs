using System.Linq;
using System.Threading.Tasks;
using TradeCommander.Providers;

namespace TradeCommander.CommandHandlers
{
    public class SettingsCommandHandler : ICommandHandler
    {
        private readonly ConsoleOutput _console;
        private readonly SettingsProvider _settingsProvider;

        private readonly string[] availableSettings = { "content-colour", "background-colour", "crt-effect" };

        public SettingsCommandHandler(
            ConsoleOutput console,
            SettingsProvider settingsProvider
            )
        {
            _console = console;
            _settingsProvider = settingsProvider;
        }

        public string CommandName => "SETTINGS";
        public bool BackgroundCanUse => false;
        public bool RequiresLogin => false;

        public string HandleAutoComplete(string[] args, int index, bool loggedIn) => null;

        public CommandResult HandleCommand(string[] args, bool background, bool loggedIn)
        {
            if (args.Length == 0 || (args.Length == 1 && (args[0] == "?" || args[0].ToLower() == "help")))
            {
                _console.WriteLine("SETTINGS: Used to alter client settings.");
                _console.WriteLine("SETTINGS <Setting Name>: Prints the given setting's current value.");
                _console.WriteLine("SETTINGS <Setting Name> <Setting Value>: Sets the given setting to the new value.");
                _console.WriteLine("SETTINGS unset <Setting Name>: Unsets the given setting and restores the default value.");
                _console.WriteLine("Settings available");
                _console.WriteLine("content-colour: Sets the colour of the content. Use any CSS accepted colour.");
                _console.WriteLine("background-colour: Sets the colour of the background. Use any CSS accepted colour.");
                _console.WriteLine("crt-effect: Turns the scan line and glow effect on or off. Accepted values: on, off.");
                return CommandResult.SUCCESS;
            }
            else 
            {
                var settingName = args[0].ToLower();
                if (args.Length == 1)
                {
                    if (availableSettings.Contains(settingName))
                        if (_settingsProvider.TryGetSetting(settingName, out var setting))
                        {
                            _console.WriteLine(settingName + " current value: " + setting.Value);
                            return CommandResult.SUCCESS;
                        }
                        else
                            _console.WriteLine(settingName + " has not been set.");
                    else
                        _console.WriteLine("Provided setting name is invalid.");

                    return CommandResult.FAILURE;
                }
                else if (args.Length == 2)
                {
                    var value = args[1];
                    if (settingName == "unset")
                    {
                        settingName = args[1].ToLower();
                        value = null;
                    }

                    if (availableSettings.Contains(settingName))
                    {
                        if (settingName != "crt-effect" || (value?.ToLower() == "on" || value?.ToLower() == "off"))
                        {
                            _settingsProvider.SetSetting(settingName, value);
                            if(value == null)
                                _console.WriteLine(settingName + " reset to default value.");
                            else
                                _console.WriteLine(settingName + " set to " + value);
                            return CommandResult.SUCCESS;
                        }
                        else
                            _console.WriteLine("Accepted values for crt-effect are \"on\" and \"off\".");
                    }
                    else
                        _console.WriteLine("Provided setting name is invalid.");

                    return CommandResult.FAILURE;
                }
            }

            return CommandResult.INVALID;
        }
    }
}
