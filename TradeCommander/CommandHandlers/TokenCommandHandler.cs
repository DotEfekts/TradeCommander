using TradeCommander.Providers;

namespace TradeCommander.CommandHandlers
{
    public class TokenCommandHandler : ICommandHandler
    {
        private readonly ConsoleOutput _console;
        private readonly UserProvider _userInfo;


        public TokenCommandHandler(
            ConsoleOutput console, 
            UserProvider userInfo
            )
        {
            _console = console;
            _userInfo = userInfo;
        }

        public string CommandName => "TOKEN";
        public bool BackgroundCanUse => false;
        public bool RequiresLogin => true;

        public string HandleAutoComplete(string[] args, int index, bool loggedIn) => null;

        public CommandResult HandleCommand(string[] args, bool background, bool loggedIn)
        {
            if (args.Length == 1 && (args[0] == "?" || args[0].ToLower() == "help"))
            {
                _console.WriteLine("TOKEN: Displays the token for the current user.");
                _console.WriteLine("Usage: TOKEN");
                return CommandResult.SUCCESS;
            }
            else if (args.Length > 0)
                return CommandResult.INVALID;
            else
            {
                _console.WriteLine("Token for " + _userInfo.Username + ": " + _userInfo.Token);
                return CommandResult.SUCCESS;
            }
        }
    }
}
