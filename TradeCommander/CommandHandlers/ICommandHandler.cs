using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace TradeCommander.CommandHandlers
{
    public interface ICommandHandler
    {
        string CommandName { get; }
        bool BackgroundCanUse { get; }
        bool RequiresLogin { get; }

        CommandResult HandleCommand(string[] args, bool background, bool loggedIn);
    }
    public interface ICommandHandlerAsync
    {
        string CommandName { get; }
        bool BackgroundCanUse { get; }
        bool RequiresLogin { get; }

        Task<CommandResult> HandleCommandAsync(string[] args, bool background, bool loggedIn);
    }
}
