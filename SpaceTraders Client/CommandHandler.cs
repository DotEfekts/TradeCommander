using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SpaceTraders_Client
{
    public class CommandHandler
    {
        private readonly ConsoleOutput _console;
        private readonly Dictionary<string, Func<string[], CommandResult>> _handlers;
        private readonly Dictionary<string, Func<string[], Task<CommandResult>>> _asyncHandlers;

        public CommandHandler(ConsoleOutput console)
        {
            _console = console;
            _handlers = new Dictionary<string, Func<string[], CommandResult>>();
            _asyncHandlers = new Dictionary<string, Func<string[], Task<CommandResult>>>();
        }

        public bool RegisterCommand(string commandName, Func<string[], CommandResult> handler)
        {
            if (_handlers.ContainsKey(commandName.ToUpper()) || _asyncHandlers.ContainsKey(commandName.ToUpper()))
                return false;

            _handlers.Add(commandName.ToUpper(), handler);
            return true;
        }

        public bool RegisterAsyncCommand(string commandName, Func<string[], Task<CommandResult>> handler)
        {
            if (_handlers.ContainsKey(commandName.ToUpper()) || _asyncHandlers.ContainsKey(commandName.ToUpper()))
                return false;

            _asyncHandlers.Add(commandName.ToUpper(), handler);
            return true;
        }

        public async Task<CommandResult> HandleCommand(string command)
        {
            if (string.IsNullOrWhiteSpace(command))
                return CommandResult.SUCCESS;

            var args = command.Split(' ')
                .Where(t => !string.IsNullOrWhiteSpace(t)).Select(t => t.Trim()).ToArray();
            var commandName = args[0].ToUpper();
            var newArgs = new string[args.Length - 1];
            Array.Copy(args, 1, newArgs, 0, args.Length - 1);

            CommandResult result;
            if (_handlers.ContainsKey(commandName))
                result = _handlers[commandName].Invoke(newArgs);
            else if(_asyncHandlers.ContainsKey(commandName.ToUpper()))
                result = await _asyncHandlers[commandName].Invoke(newArgs);
            else
            {
                _console.WriteLine("Unknown command: " + commandName);
                return CommandResult.INVALID;
            }

            if(result == CommandResult.INVALID)
                _console.WriteLine("Invalid arguments. (See " + command.ToUpper() + " help)");
            return result;
        }
    }

    public enum CommandResult
    {
        SUCCESS, FAILURE, INVALID
    }
}
