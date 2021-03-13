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
    public class ShipyardCommandHandler : ICommandHandlerAsync
    {
        private readonly UserProvider _userInfo;
        private readonly ShipsProvider _shipProvider;
        private readonly ConsoleOutput _console;
        private readonly NavigationManager _navManager;
        private readonly HttpClient _http;
        private readonly JsonSerializerOptions _serializerOptions;
        
        public ShipyardCommandHandler(
            UserProvider userInfo,
            ShipsProvider shipProvider,
            ConsoleOutput console,
            NavigationManager navManager,
            HttpClient http,
            JsonSerializerOptions serializerOptions
            )
        {
            _userInfo = userInfo;
            _shipProvider = shipProvider;
            _console = console;
            _navManager = navManager;
            _http = http;
            _serializerOptions = serializerOptions;
        }

        public string CommandName => "SHIPYARD";
        public bool BackgroundCanUse => false;
        public bool RequiresLogin => true;

        public async Task<CommandResult> HandleCommandAsync(string[] args, bool background, bool loggedIn) 
        {

            if(args.Length == 1 && (args[0] == "?" || args[0].ToLower() == "help"))
            {
                _console.WriteLine("SHIPYARD: Provides functions for managing ships.");
                _console.WriteLine("Subcommands");
                _console.WriteLine("list: Shows all ships available to purchase - SHIPYARD list");
                _console.WriteLine("      Shows locations a ship is available to purchase - SHIPYARD list <Ship Type>");
                _console.WriteLine("buy: Purchase a ship - SHIPYARD buy <Ship Type> <Location Symbol>");
                return CommandResult.SUCCESS;
            }
            else if((args.Length == 1 || args.Length == 2) && args[0].ToLower() == "list")
            {
                if(args.Length == 1)
                {
                    _console.WriteLine("Displaying ship list.");
                    _navManager.NavigateTo(_navManager.BaseUri + "shipyard");
                }
                else
                {
                    _console.WriteLine("Displaying purchase location list.");
                    _navManager.NavigateTo(_navManager.BaseUri + "shipyard/" + args[1].ToUpper());
                }

                return CommandResult.SUCCESS;
            }
            else if(args.Length == 3 && args[0].ToLower() == "buy")
            {
                using var httpResult = await _http.PostAsJsonAsync("/users/" + _userInfo.Username + "/ships", new ShipyardPurchaseRequest
                {
                    Location = args[2].ToUpper(),
                    Type = args[1].ToUpper()
                });

                _ = _shipProvider.RefreshShipData();

                if (httpResult.StatusCode == HttpStatusCode.Created)
                {
                    var details = await httpResult.Content.ReadFromJsonAsync<DetailsResponse>(_serializerOptions);

                    var cost = _userInfo.UserDetails.Credits - details.User.Credits;
                    _userInfo.UserDetails.Credits = details.User.Credits;
                    _console.WriteLine("Ship purchased successfully. Total cost: " + cost + " credits.");
                    return CommandResult.SUCCESS;
                }
                else
                {
                    var error = await httpResult.Content.ReadFromJsonAsync<ErrorResponse>(_serializerOptions);
                    if (error.Error.Message == "Ship does not exist.")
                        _console.WriteLine("Invalid ship type provided. Please check the type and try again.");
                    else if(error.Error.Message == "Location does not exist.")
                        _console.WriteLine("Location does not exist. Please check location and try again.");
                    else if(error.Error.Message == "Ship is not available for purchase on this planet.")
                        _console.WriteLine("Ship is not available for purchase at this location.");
                    else if (error.Error.Message == "User has insufficient funds to purchase ship.")
                        _console.WriteLine("Insufficient credits available for purchase.");
                    else
                        _console.WriteLine(error.Error.Message);
                }

                return CommandResult.FAILURE;
            }

            return CommandResult.INVALID;
        }
    }
}
