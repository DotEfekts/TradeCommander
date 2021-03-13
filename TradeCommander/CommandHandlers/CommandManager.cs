using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using TradeCommander.Providers;

namespace TradeCommander.CommandHandlers
{
    public class CommandManager
    {
        private readonly ConsoleOutput _console;
        private readonly UserProvider _userInfo;
        private readonly IServiceProvider _services;
        private readonly Dictionary<string, ICommandHandler> _handlers;
        private readonly Dictionary<string, ICommandHandlerAsync> _asyncHandlers;

        public CommandManager(ConsoleOutput console, UserProvider userInfo, IServiceProvider services)
        {
            _console = console;
            _userInfo = userInfo;
            _services = services;
            _handlers = new Dictionary<string, ICommandHandler>();
            _asyncHandlers = new Dictionary<string, ICommandHandlerAsync>();
        }

        public void RegisterCommands()
        {
            var assembly = Assembly.GetAssembly(typeof(CommandManager));

            var syncType = typeof(ICommandHandler);
            var syncCommands = assembly.GetTypes()
                            .Where(m => m.GetInterfaces().Contains(syncType));
            foreach (var handler in syncCommands)
            {
                try
                {
                    var handlerInstance = (ICommandHandler)ActivatorUtilities.CreateInstance(_services, handler);
                    var command = handlerInstance.CommandName.Trim();
                    if (!command.Contains(' '))
                    {
                        if (!RegisterCommand(handlerInstance.CommandName, handlerInstance))
                        {
                            Console.Error.WriteLine("Failed to register command: " + handlerInstance.CommandName);
                            Console.Error.WriteLine("Command was already registered.");
                        }
                    }
                    else
                    {
                        Console.Error.WriteLine("Failed to register command: " + handlerInstance.CommandName);
                        Console.Error.WriteLine("Direct subcommand handling is currently not supported.");
                    }
                }
                catch (InvalidOperationException)
                {
                    Console.Error.WriteLine("Failed to register command handler: " + handler.Name);
                    Console.Error.WriteLine("Are all constructor parameters provided by services?");
                }
            }

            var asyncType = typeof(ICommandHandlerAsync);
            var asyncCommands = assembly.GetTypes()
                            .Where(m => m.GetInterfaces().Contains(asyncType));
            foreach (var handler in asyncCommands)
            {
               try
                {
                    var handlerInstance = (ICommandHandlerAsync)ActivatorUtilities.CreateInstance(_services, handler);
                    var command = handlerInstance.CommandName.Trim();
                    if (!command.Contains(' '))
                    {
                        if (!RegisterAsyncCommand(handlerInstance.CommandName, handlerInstance))
                        {
                            Console.Error.WriteLine("Failed to register command: " + handlerInstance.CommandName);
                            Console.Error.WriteLine("Command was already registered.");
                        }
                    }
                    else
                    {
                        Console.Error.WriteLine("Failed to register command: " + handlerInstance.CommandName);
                        Console.Error.WriteLine("Direct subcommand handling is currently not supported.");
                    }
                }
                catch (InvalidOperationException)
                {
                    Console.Error.WriteLine("Failed to register command handler: " + handler.Name);
                    Console.Error.WriteLine("Are all constructor parameters provided by services?");
                }
            }

        }

        private bool RegisterCommand<T>(string commandName, T handler) where T : ICommandHandler
        {
            if (_handlers.ContainsKey(commandName.ToUpper()) || _asyncHandlers.ContainsKey(commandName.ToUpper()))
                return false;

            _handlers.Add(commandName.ToUpper(), handler);
            return true;
        }

        private bool RegisterAsyncCommand<T>(string commandName, T handler) where T : ICommandHandlerAsync
        {
            if (_handlers.ContainsKey(commandName.ToUpper()) || _asyncHandlers.ContainsKey(commandName.ToUpper()))
                return false;

            _asyncHandlers.Add(commandName.ToUpper(), handler);
            return true;
        }

        public async Task<CommandResult> InvokeCommand(string command, bool background = false)
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
            {
                var handler = _handlers[commandName];

                if (handler.RequiresLogin && _userInfo.UserDetails == null)
                {
                    _console.WriteLine("You must be signed in to use this command.");
                    return CommandResult.FAILURE;
                }
                else if(!handler.BackgroundCanUse && background == true)
                {
                    _console.WriteLine("This command cannot be run automatically.");
                    return CommandResult.FAILURE;
                }

                result = handler.HandleCommand(newArgs, background, _userInfo.UserDetails != null);
            }
            else if (_asyncHandlers.ContainsKey(commandName.ToUpper()))
            {
                var handler = _asyncHandlers[commandName];

                if (handler.RequiresLogin && _userInfo.UserDetails == null)
                {
                    _console.WriteLine("You must be logged in to use this command.");
                    return CommandResult.FAILURE;
                }
                else if (!handler.BackgroundCanUse && background == true)
                {
                    _console.WriteLine("This command cannot be run automatically.");
                    return CommandResult.FAILURE;
                }

                result = await handler.HandleCommandAsync(newArgs, background, _userInfo.UserDetails != null);
            }
            else
            {
                _console.WriteLine("Unknown command: " + commandName);
                return CommandResult.INVALID;
            }

            if(result == CommandResult.INVALID)
                _console.WriteLine("Invalid arguments. (See " + commandName + " help)");
            return result;
        }
    }

    public enum CommandResult
    {
        SUCCESS, FAILURE, INVALID
    }
}
