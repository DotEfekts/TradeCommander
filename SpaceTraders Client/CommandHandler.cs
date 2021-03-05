using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SpaceTraders_Client
{
    public class CommandHandler
    {
        private readonly ConsoleOutput _console;
        private readonly Dictionary<string, Action<string[]>> _handlers;
        private readonly Dictionary<string, Func<string[], Task>> _asyncHandlers;

        public CommandHandler(ConsoleOutput console)
        {
            _console = console;
            _handlers = new Dictionary<string, Action<string[]>>();
            _asyncHandlers = new Dictionary<string, Func<string[], Task>>();
        }

        public bool RegisterCommand(string commandName, Action<string[]> handler)
        {
            if (_handlers.ContainsKey(commandName.ToUpper()) || _asyncHandlers.ContainsKey(commandName.ToUpper()))
                return false;

            _handlers.Add(commandName.ToUpper(), handler);
            return true;
        }

        public bool RegisterAsyncCommand(string commandName, Func<string[], Task> handler)
        {
            if (_handlers.ContainsKey(commandName.ToUpper()) || _asyncHandlers.ContainsKey(commandName.ToUpper()))
                return false;

            _asyncHandlers.Add(commandName.ToUpper(), handler);
            return true;
        }

        public async Task HandleCommand(string command)
        {
            if (string.IsNullOrWhiteSpace(command))
                return;

            var args = command.Split(' ')
                .Where(t => !string.IsNullOrWhiteSpace(t)).Select(t => t.Trim()).ToArray();
            var commandName = args[0].ToUpper();
            var newArgs = new string[args.Length - 1];
            Array.Copy(args, 1, newArgs, 0, args.Length - 1);

            if (_handlers.ContainsKey(commandName))
                _handlers[commandName].Invoke(newArgs);
            else if(_asyncHandlers.ContainsKey(commandName.ToUpper()))
                await _asyncHandlers[commandName].Invoke(newArgs);
            else
            {
                _console.WriteLine("Unknown command: " + commandName);
            }
        }
    }
}
