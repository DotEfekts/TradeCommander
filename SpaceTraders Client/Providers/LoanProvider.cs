using Blazored.LocalStorage;
using Microsoft.AspNetCore.Components;
using SpaceTraders_Client.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using System.Timers;

namespace SpaceTraders_Client.Providers
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

        private async Task HandleLoanCommandAsync(string[] args) 
        {
            if(_userInfo.UserDetails == null)
            {
                _console.WriteLine("You must be logged in to use this command.");
                return;
            }
            
            if(args.Length == 0)
            {
                _console.WriteLine("Invalid arguments. (See LOAN help)");
            }
            else if(args[0] == "?" || args[0].ToLower() == "help")
            {
                _console.WriteLine("LOAN: Provides functions for managing loans.");
                _console.WriteLine("Subcommands");
                _console.WriteLine("list: Shows all loans available to take - LOAN list");
                _console.WriteLine("owed: Shows all currently owing loans - LOAN owed");
                _console.WriteLine("paid: Shows all paid loans - LOAN paid");
                _console.WriteLine("take: Take a new loan - LOAN take <Loan Type>");
                _console.WriteLine("pay: Pay a loan - LOAN pay <Loan Id>");
            }
            else if(args[0].ToLower() == "list")
            {
                if(args.Length != 1)
                    _console.WriteLine("Invalid arguments. (See LOAN help)");
                else
                {
                    _console.WriteLine("Displaying loans available.");
                    _navManager.NavigateTo("/loans");
                }
            }
            else if(args[0].ToLower() == "owed")
            {
                if (args.Length != 1)
                    _console.WriteLine("Invalid arguments. (See LOAN help)");
                else
                {
                    _console.WriteLine("Displaying loans owed.");
                    _navManager.NavigateTo("/loans/owed");
                }
            }
            else if (args[0].ToLower() == "paid")
            {
                if (args.Length != 1)
                    _console.WriteLine("Invalid arguments. (See LOAN help)");
                else
                {
                    _console.WriteLine("Displaying loans paid.");
                    _navManager.NavigateTo("/loans/paid");
                }
            }
            else if (args[0].ToLower() == "take")
            {
                if (args.Length != 2)
                    _console.WriteLine("Invalid arguments. (See LOAN help)");
                else
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
                    }
                    else
                    {
                        //Uncomment when error messages are fixed.
                        //var error = await httpResult.Content.ReadFromJsonAsync<ErrorResponse>(_serializerOptions);
                        _console.WriteLine("An error occurred while attempting to take the loan.");
                    }
                }
            }
            else if (args[0].ToLower() == "pay")
            {
                if (args.Length != 2)
                    _console.WriteLine("Invalid arguments. (See LOAN help)");
                else
                {
                    using var httpResult = await _http.PutAsJsonAsync("/users/" + _userInfo.Username + "/loans/" + args[1], new { });

                    if (httpResult.StatusCode == HttpStatusCode.OK)
                    {
                        var details = await httpResult.Content.ReadFromJsonAsync<DetailsResponse>(_serializerOptions);

                        var payment = _userInfo.UserDetails.Credits - details.User.Credits;
                        _userInfo.UserDetails.Credits = details.User.Credits;
                        _console.WriteLine("Loan paid successfully. Payment amount: " + payment + " credits.");
                        _stateEvents.TriggerUpdate(this, "loanPaid");
                    }
                    else
                    {
                        var error = await httpResult.Content.ReadFromJsonAsync<ErrorResponse>(_serializerOptions);
                        _console.WriteLine(error.Error.Message);
                    }
                }
            }
            else
            {
                _console.WriteLine("Invalid arguments. (See SHIPYARD help)");
            }
        }
    }
}
