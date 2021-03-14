using System.Threading.Tasks;
using TradeCommander.Providers;

namespace TradeCommander.CommandHandlers
{
    public class HelpCommandHandler : ICommandHandlerAsync
    {
        private readonly ConsoleOutput _console;
        private readonly CommandManager _commandManager;

        public HelpCommandHandler(
            ConsoleOutput console,
            CommandManager commandManager
            )
        {
            _console = console;
            _commandManager = commandManager;
        }

        public string CommandName => "HELP";
        public bool BackgroundCanUse => false;
        public bool RequiresLogin => false;

        public async Task<CommandResult> HandleCommandAsync(string[] args, bool background, bool loggedIn)
        {
            if (args.Length == 0 && loggedIn)
            {
                _console.WriteLine("Commands available");
                _console.WriteLine("SHIP: Provides functions for managing ships.");
                _console.WriteLine("SCAN: Provides functions for searching systems for locations.");
                _console.WriteLine("MARKET: Provides functions for interacting with the marketplace.");
                _console.WriteLine("AUTO: Provides functions for creating automatic routes for ships.");
                _console.WriteLine("SHIPYARD: Provides functions for managing ships.");
                _console.WriteLine("LOAN: Provides functions for managing loans.");
                _console.WriteLine("SETTINGS: Provides functions for changing the client settings.");
                _console.WriteLine("CLEAR: Clears the screen.");
                _console.WriteLine("LOGOUT: Logs out of the current user.");
                return CommandResult.SUCCESS;
            }
            else if(args.Length == 0 && !loggedIn)
            {
                _console.WriteLine("Commands available");
                _console.WriteLine("LOGIN: Logs an existing user into the SpaceTraders API.");
                _console.WriteLine("SIGNUP: Creates an account for the SpaceTraders API.");
                _console.WriteLine("SETTINGS: Provides functions for changing the client settings.");
                _console.WriteLine("CLEAR: Clears the screen.");
                return CommandResult.SUCCESS;
            }
            else if (args.Length == 1 && (args[0] == "?" || args[0].ToLower() == "help"))
            {
                _console.WriteLine("HELP: Provides a list of commands.");
                _console.WriteLine("HELP <Command Name>: Provides a help for a specific command.");
                return CommandResult.SUCCESS;
            }
            else if (args.Length == 1 && args[0].ToUpper() == "CLEAR")
            {
                _console.WriteLine("CLEAR: Clears the screen.");
                return CommandResult.SUCCESS;
            }
            else if (args.Length == 1)
            {
                var result = await _commandManager.InvokeCommand(args[0] + " help");
                return result == CommandResult.INVALID ? CommandResult.FAILURE : result;
            }

            return CommandResult.INVALID;
        }
    }
}
