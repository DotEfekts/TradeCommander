using Microsoft.AspNetCore.Components;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using TradeCommander.Models;
using TradeCommander.Providers;

namespace TradeCommander.CommandHandlers
{
    public class LoanCommandHandler : ICommandHandlerAsync
    {
        private readonly UserProvider _userInfo;
        private readonly ConsoleOutput _console;
        private readonly NavigationManager _navManager;
        private readonly HttpClient _http;
        private readonly JsonSerializerOptions _serializerOptions;
        private readonly StateProvider _stateProvider;

        public LoanCommandHandler(
            UserProvider userInfo,
            ConsoleOutput console,
            NavigationManager navManager,
            HttpClient http,
            JsonSerializerOptions serializerOptions,
            StateProvider stateProvider
            )
        {
            _userInfo = userInfo;
            _console = console;
            _navManager = navManager;
            _http = http;
            _serializerOptions = serializerOptions;
            _stateProvider = stateProvider;
        }

        public string CommandName => "LOAN";
        public bool BackgroundCanUse => false;
        public bool RequiresLogin => true;

        public string HandleAutoComplete(string[] args, int index, bool loggedIn) => null;

        public async Task<CommandResult> HandleCommandAsync(string[] args, bool background, bool loggedIn)
        {
            if(args.Length == 1 && (args[0] == "?" || args[0].ToLower() == "help"))
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
            else if(args.Length == 1 && args[0].ToLower() == "list")
            {
                _console.WriteLine("Displaying loans available.");
                _navManager.NavigateTo(_navManager.BaseUri + "loans");
                return CommandResult.SUCCESS;
            }
            else if(args.Length == 1 && args[0].ToLower() == "owed")
            {
                _console.WriteLine("Displaying loans owed.");
                _navManager.NavigateTo(_navManager.BaseUri + "loans/owed");
                return CommandResult.SUCCESS;
            }
            else if (args.Length == 1 && args[0].ToLower() == "paid")
            {
                _console.WriteLine("Displaying loans paid.");
                _navManager.NavigateTo(_navManager.BaseUri + "loans/paid");
                return CommandResult.SUCCESS;
            }
            else if (args.Length == 2 && args[0].ToLower() == "take")
            {
                using var httpResult = await _http.PostAsJsonAsync("/users/" + _userInfo.Username + "/loans", new LoanRequest
                {
                    Type = args[1].ToUpper()
                });


                if (httpResult.IsSuccessStatusCode)
                {
                    var details = await httpResult.Content.ReadFromJsonAsync<LoanResponse>(_serializerOptions);
                    var credits = details.Credits - _userInfo.UserDetails.Credits;

                    _stateProvider.TriggerUpdate(this, "loansUpdated");

                    _userInfo.SetCredits(details.Credits);
                    _console.WriteLine("Loan taken successfully. Loan amount: " + credits + " credits.");

                    return CommandResult.SUCCESS;
                }
                else
                {
                    var error = await httpResult.Content.ReadFromJsonAsync<ErrorResponse>(_serializerOptions);
                    _console.WriteLine(error.Error.Message);
                }

                return CommandResult.FAILURE;
            }
            else if (args.Length == 2 && args[0].ToLower() == "pay")
            {
                using var httpResult = await _http.PutAsJsonAsync("/users/" + _userInfo.Username + "/loans/" + args[1], new { });


                if (httpResult.IsSuccessStatusCode)
                {
                    var details = await httpResult.Content.ReadFromJsonAsync<DetailsResponse>(_serializerOptions);
                    var payment = _userInfo.UserDetails.Credits - details.User.Credits;

                    _stateProvider.TriggerUpdate(this, "loansUpdated");

                    _userInfo.SetCredits(details.User.Credits);
                    _console.WriteLine("Loan paid successfully. Payment amount: " + payment + " credits.");

                    return CommandResult.SUCCESS;
                }
                else
                {
                    var error = await httpResult.Content.ReadFromJsonAsync<ErrorResponse>(_serializerOptions);
                    _console.WriteLine(error.Error.Message);
                }

                return CommandResult.FAILURE;
            }

            return CommandResult.INVALID;
        }
    }
}
