using TradeCommander.Providers;

namespace TradeCommander.CommandHandlers
{
    public class LogoutCommandHandler : ICommandHandler
    {
        private readonly ConsoleOutput _console;
        private readonly UserProvider _userInfo;


        public LogoutCommandHandler(
            ConsoleOutput console, 
            UserProvider userInfo
            )
        {
            _console = console;
            _userInfo = userInfo;
        }

        public string CommandName => "LOGOUT";
        public bool BackgroundCanUse => false;
        public bool RequiresLogin => true;

        public CommandResult HandleCommand(string[] args, bool background, bool loggedIn)
        {
            if (args.Length == 1 && (args[0] == "?" || args[0].ToLower() == "help"))
            {
                _console.WriteLine("LOGOUT: Logs current user out of the SpaceTraders API.");
                _console.WriteLine("Usage: LOGOUT");
                return CommandResult.SUCCESS;
            }
            else if (args.Length > 0)
                return CommandResult.INVALID;
            else
            {
                _userInfo.Logout();
                
                _console.Clear();
                _console.WriteLine("Goodbye.");

                return CommandResult.SUCCESS;
            }
        }
    }
}
