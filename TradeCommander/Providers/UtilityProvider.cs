namespace TradeCommander.Providers
{
    public class UtilityProvider
    {
        private readonly ConsoleOutput _console;

        public UtilityProvider(
            CommandHandler commandHandler,
            ConsoleOutput console
            )
        {
            _console = console;
            commandHandler.RegisterCommand("CLEAR", HandleClear);
        }

        private CommandResult HandleClear(string[] args, bool background)
        {
            if(background)
                return CommandResult.FAILURE;
            _console.Clear();
            return CommandResult.SUCCESS;
        }
    }
}
