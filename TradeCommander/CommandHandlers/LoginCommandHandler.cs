using System.Threading.Tasks;
using TradeCommander.Providers;

namespace TradeCommander.CommandHandlers
{
    public class LoginCommandHandler : ICommandHandlerAsync
    {
        private readonly ConsoleOutput _console;
        private readonly UserProvider _userInfo;

        public LoginCommandHandler(ConsoleOutput console, UserProvider userInfo)
        {
            _console = console;
            _userInfo = userInfo;
        }

        public string CommandName => "LOGIN";
        public bool BackgroundCanUse => false;
        public bool RequiresLogin => false;

        public string HandleAutoComplete(string[] args, int index, bool loggedIn) => null;

        public async Task<CommandResult> HandleCommandAsync(string[] args, bool background, bool loggedIn)
        {
            if (loggedIn)
            {
                _console.WriteLine("You must be signed out to use this command.");
                return CommandResult.FAILURE;
            }

            if (args.Length == 1)
            {
                if((args[0] == "?" || args[0].ToLower() == "help"))
                {
                    _console.WriteLine("LOGIN: Logs an existing user into the SpaceTraders API.");
                    _console.WriteLine("Usage: LOGIN <Token>");
                    return CommandResult.SUCCESS;
                }
                else
                {
                    await _userInfo.SetDetailsAsync(args[0]);
                    if (_userInfo.UserDetails != null)
                    {
                        _console.Clear();
                        _console.WriteLine("Welcome back, " + _userInfo.UserDetails.Username + ".");
                        _console.WriteLine("For command list see HELP.");
                        return CommandResult.SUCCESS;
                    }
                    else
                        _console.WriteLine("Incorrect login details. Please try again.");

                    return CommandResult.FAILURE;
                }
            }

            return CommandResult.INVALID;
        }
    }
}
