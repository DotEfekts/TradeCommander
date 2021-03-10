using Blazored.LocalStorage;
using Microsoft.AspNetCore.Components;
using TradeCommander.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using System.Timers;

namespace TradeCommander.Providers
{
    public class LoanProvider
    {
        private readonly SpaceTradersUserInfo _userInfo;
        private readonly StateEvents _stateEvents;
        private readonly ConsoleOutput _console;
        private readonly NavigationManager _navManager;
        private readonly HttpClient _http;
        private readonly JsonSerializerOptions _serializerOptions;
        
        public LoanProvider(
            SpaceTradersUserInfo userInfo,
            StateEvents stateEvents,
            CommandHandler commandHandler,
            ConsoleOutput console,
            NavigationManager navManager,
            HttpClient http,
            JsonSerializerOptions serializerOptions
            )
        {
            _userInfo = userInfo;
            _stateEvents = stateEvents;
            _console = console;
            _navManager = navManager;
            _http = http;
            _serializerOptions = serializerOptions;

            commandHandler.RegisterAsyncCommand("LOAN", HandleLoanCommandAsync);
        }

        private async Task<CommandResult> HandleLoanCommandAsync(string[] args, bool background) 
        {
            if(_userInfo.UserDetails == null)
            {
                _console.WriteLine("You must be logged in to use this command.");
                return CommandResult.FAILURE;
            }

            if (background)
            {
                _console.WriteLine("This command cannot be run automatically.");
                return CommandResult.FAILURE;
            }

            if (args.Length > 0)
            {
                if(args[0] == "?" || args[0].ToLower() == "help")
                {
                    _console.WriteLine("LOAN: Provides functions for managing loans.");
                    _console.WriteLine("Subcommands");
                    _console.WriteLine("list: Shows all loans available to take - LOAN list");
                    _console.WriteLine("owed: Shows all currently owing loans - LOAN owed");
                    _console.WriteLine("paid: Shows all paid loans - LOAN paid");
                    _console.WriteLine("take: Take a new loan - LOAN take <Loan Type>");
                    _console.WriteLine("pay: Pay a loan - LOAN pay <Loan Id>");
                    return CommandResult.SUCCESS;
                }
                else if(args[0].ToLower() == "list" && args.Length == 1)
                {
                    _console.WriteLine("Displaying loans available.");
                    _navManager.NavigateTo(_navManager.BaseUri + "loans");
                    return CommandResult.SUCCESS;
                }
                else if(args[0].ToLower() == "owed" && args.Length == 1)
                {
                    _console.WriteLine("Displaying loans owed.");
                    _navManager.NavigateTo(_navManager.BaseUri + "loans/owed");
                    return CommandResult.SUCCESS;
                }
                else if (args[0].ToLower() == "paid" && args.Length == 1)
                {
                    _console.WriteLine("Displaying loans paid.");
                    _navManager.NavigateTo(_navManager.BaseUri + "loans/paid");
                    return CommandResult.SUCCESS;
                }
                else if (args[0].ToLower() == "take" && args.Length == 2)
                {
                    using var httpResult = await _http.PostAsJsonAsync("/users/" + _userInfo.Username + "/loans", new LoanRequest
                    {
                        Type = args[1].ToUpper()
                    });

                    if (httpResult.StatusCode == HttpStatusCode.Created)
                    {
                        var details = await httpResult.Content.ReadFromJsonAsync<DetailsResponse>(_serializerOptions);

                        var credits = details.User.Credits - _userInfo.UserDetails.Credits;
                        _userInfo.UserDetails.Credits = details.User.Credits;
                        _console.WriteLine("Loan taken successfully. Loan amount: " + credits + " credits.");
                        _stateEvents.TriggerUpdate(this, "loanTaken");

                        return CommandResult.SUCCESS;
                    }
                    else
                    {
                        //Uncomment when error messages are fixed.
                        //var error = await httpResult.Content.ReadFromJsonAsync<ErrorResponse>(_serializerOptions);
                        _console.WriteLine("An error occurred while attempting to take the loan.");
                    }

                    return CommandResult.FAILURE;
                }
                else if (args[0].ToLower() == "pay" && args.Length == 2)
                {
                    using var httpResult = await _http.PutAsJsonAsync("/users/" + _userInfo.Username + "/loans/" + args[1], new { });

                    if (httpResult.StatusCode == HttpStatusCode.OK)
                    {
                        var details = await httpResult.Content.ReadFromJsonAsync<DetailsResponse>(_serializerOptions);

                        var payment = _userInfo.UserDetails.Credits - details.User.Credits;
                        _userInfo.UserDetails.Credits = details.User.Credits;
                        _console.WriteLine("Loan paid successfully. Payment amount: " + payment + " credits.");
                        _stateEvents.TriggerUpdate(this, "loanPaid");

                        return CommandResult.SUCCESS;
                    }
                    else
                    {
                        var error = await httpResult.Content.ReadFromJsonAsync<ErrorResponse>(_serializerOptions);
                        _console.WriteLine(error.Error.Message);
                    }

                    return CommandResult.FAILURE;
                }
            }

            return CommandResult.INVALID;
        }
    }
}
