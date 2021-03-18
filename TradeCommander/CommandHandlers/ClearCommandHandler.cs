namespace TradeCommander.CommandHandlers
{
    public class ClearCommandHandler : ICommandHandler
    {
        private readonly ConsoleOutput _console;

        public ClearCommandHandler(ConsoleOutput console)
        {
            _console = console;
        }

        public string CommandName => "CLEAR";
        public bool BackgroundCanUse => false;
        public bool RequiresLogin => false;

        public string HandleAutoComplete(string[] args, int index, bool loggedIn) => null;

        public CommandResult HandleCommand(string[] args, bool background, bool loggedIn)
        {
            if(background)
                return CommandResult.FAILURE;
            _console.Clear();
            return CommandResult.SUCCESS;
        }
    }
}
