using System.Threading.Tasks;

namespace SpaceTraders_Client.Providers
{
    public class HelpProvider
    {
        private readonly CommandHandler _commandHandler;
        private readonly ConsoleOutput _console;
        private readonly SpaceTradersUserInfo _userInfo;

        public HelpProvider(
            CommandHandler commandHandler,
            ConsoleOutput console,
            SpaceTradersUserInfo userInfo
            )
        {
            _commandHandler = commandHandler;
            _console = console;
            _userInfo = userInfo;

            commandHandler.RegisterAsyncCommand("HELP", HandleHelpAsync);
        }

        private async Task HandleHelpAsync(string[] args)
        {
            if (_userInfo.UserDetails == null)
            {
                _console.WriteLine("You must be logged in to use this command.");
                return;
            }

            if (args.Length == 0)
            {
                _console.WriteLine("Commands available");
                _console.WriteLine("SHIP: Provides functions for managing ships.");
                _console.WriteLine("SCAN: Provides functions for searching systems for locations.");
                _console.WriteLine("MARKET: Provides functions for interacting with the marketplace.");
                _console.WriteLine("SHIPYARD: Provides functions for managing ships.");
                _console.WriteLine("LOAN: Provides functions for managing loans.");
                _console.WriteLine("CLEAR: Clears the screen.");

            }
            else if (args[0] == "?" || args[0].ToLower() == "help")
            {
                _console.WriteLine("HELP: Provides a list of commands.");
                _console.WriteLine("HELP <Command Name>: Provides a help for a specific command.");
            }
            else if (args[0].ToLower() == "clear")
            {
                _console.WriteLine("CLEAR: Clears the screen.");
            }
            else if (args.Length == 1)
            {
                await _commandHandler.HandleCommand(args[0] + " help");
            }
            else
            {
                _console.WriteLine("Invalid arguments. (See HELP help)");
            }
        }
    }
}
