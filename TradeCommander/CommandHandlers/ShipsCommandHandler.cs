using Microsoft.AspNetCore.Components;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using TradeCommander.Models;
using TradeCommander.Providers;

namespace TradeCommander.CommandHandlers
{
    public class ShipsCommandHandler : ICommandHandlerAsync
    {
        private readonly UserProvider _userInfo;
        private readonly ShipsProvider _shipInfo;
        private readonly ConsoleOutput _console;
        private readonly NavigationManager _navManager;
        private readonly HttpClient _http;
        private readonly JsonSerializerOptions _serializerOptions;
        
        public ShipsCommandHandler(
            UserProvider userInfo,
            ShipsProvider shipInfo,
            ConsoleOutput console,
            NavigationManager navManager,
            HttpClient http,
            JsonSerializerOptions serializerOptions
            )
        {
            _userInfo = userInfo;
            _shipInfo = shipInfo;
            _console = console;
            _navManager = navManager;
            _http = http;
            _serializerOptions = serializerOptions;
        }

        public string CommandName => "SHIP";
        public bool BackgroundCanUse => true;
        public bool RequiresLogin => true;

        public string HandleAutoComplete(string[] args, int index, bool loggedIn) => null;

        public async Task<CommandResult> HandleCommandAsync(string[] args, bool background, bool loggedIn)
        {
            if (!background && args.Length == 1 && (args[0] == "?" || args[0].ToLower() == "help"))
            {
                _console.WriteLine("SHIP: Provides functions for managing ships.");
                _console.WriteLine("Subcommands");
                _console.WriteLine("map: Displays the local map for the ship - SHIP <Ship Id> map");
                _console.WriteLine("cargo: Displays the cargo of ship - SHIP <Ship Id> cargo");
                _console.WriteLine("fly: Enacts a flightplan for a ship - SHIP <Ship Id> fly <Location Symbol>");
                _console.WriteLine("warp: Warps a ship through the docked wormhole - SHIP <Ship Id> warp");
                _console.WriteLine("rename: Renames a ship - SHIP <Ship Id> rename <New Name>");
                _console.WriteLine("info: Prints the specifications of ship - SHIP <Ship Id> info");
                return CommandResult.SUCCESS;
            }
            else if (!background && args.Length == 2 && args[1].ToLower() == "map")
            {
                if (_shipInfo.TryGetShipDataByLocalId(args[0], out var shipData))
                {
                    _console.WriteLine("Displaying local map for " + shipData.DisplayName + ".");
                    _navManager.NavigateTo(_navManager.BaseUri + "map/" + shipData.ServerId);
                    return CommandResult.SUCCESS;
                }
                else
                    _console.WriteLine("Invalid ship id. Please use number ids and not the full string id.");
                return CommandResult.FAILURE;
            }
            else if (!background && args.Length == 2 && args[1].ToLower() == "cargo")
            {
                if (_shipInfo.TryGetShipDataByLocalId(args[0], out var shipData))
                {
                    _console.WriteLine("Displaying cargo for " + shipData.DisplayName + ".");
                    _navManager.NavigateTo(_navManager.BaseUri + "ships/cargo/" + shipData.ServerId);
                    return CommandResult.SUCCESS;
                }
                else
                    _console.WriteLine("Invalid ship id. Please use number ids and not the full string id.");
                return CommandResult.FAILURE;
            }
            else if (args.Length == 3 && args[1].ToLower() == "fly")
            {
                if (_shipInfo.TryGetShipDataByLocalId(args[0], out var shipData))
                {
                    if (!string.IsNullOrWhiteSpace(shipData.Ship.Location))
                    {
                        using var httpResult = await _http.PostAsJsonAsync("/users/" + _userInfo.Username + "/flight-plans", new FlightRequest
                        {
                            ShipId = shipData.ServerId,
                            Destination = args[2].ToUpper()
                        });

                        if (httpResult.StatusCode == HttpStatusCode.Created)
                        {
                            var flightResult = await httpResult.Content.ReadFromJsonAsync<FlightResponse>(_serializerOptions);

                            _shipInfo.AddFlightPlan(shipData.ServerId, flightResult.FlightPlan);

                            if (!background)
                            {
                                _console.WriteLine("Flight started successfully. Destination: " + args[2].ToUpper() + ".");
                                _navManager.NavigateTo(_navManager.BaseUri + "map/" + shipData.ServerId);
                            }

                            return CommandResult.SUCCESS;
                        }
                        else
                        {
                            var error = await httpResult.Content.ReadFromJsonAsync<ErrorResponse>(_serializerOptions);
                            if (error.Error.Message.ToLower().Contains("ship destination is same as departure"))
                            {
                                if (!background)
                                    _console.WriteLine("Ship is already docked in specified location.");
                                return CommandResult.SUCCESS;
                            }
                            else if (error.Error.Message.StartsWith("Destination does not exist."))
                                _console.WriteLine("Destination does not exist. Please check destination and try again.");
                            else
                            {
                                await _shipInfo.RefreshShipData();
                                _console.WriteLine(error.Error.Message);
                            }
                        }
                    }
                    else
                        _console.WriteLine("Ship is already in transit on an existing flight plan.");
                }
                else
                    _console.WriteLine("Invalid ship id. Please use number ids and not the full string id.");

                return CommandResult.FAILURE;
            }
            else if (args.Length == 2 && args[1].ToLower() == "warp")
            {
                if (_shipInfo.TryGetShipDataByLocalId(args[0], out var shipData))
                {
                    if (!string.IsNullOrWhiteSpace(shipData.Ship.Location))
                    {
                        using var httpResult = await _http.PostAsJsonAsync("/users/" + _userInfo.Username + "/warp-jump", new WarpRequest
                        {
                            ShipId = shipData.ServerId
                        });

                        if (httpResult.StatusCode == HttpStatusCode.Created)
                        {
                            var flightResult = await httpResult.Content.ReadFromJsonAsync<FlightResponse>(_serializerOptions);

                            _shipInfo.AddFlightPlan(shipData.ServerId, flightResult.FlightPlan);

                            if (!background)
                            {
                                _console.WriteLine("Warp started successfully. Destination: " + flightResult.FlightPlan.Destination + ".");
                                _navManager.NavigateTo(_navManager.BaseUri + "map/" + flightResult.FlightPlan.Destination.Split("-")[0]);
                            }

                            return CommandResult.SUCCESS;
                        }
                        else
                        {
                            var error = await httpResult.Content.ReadFromJsonAsync<ErrorResponse>(_serializerOptions);
                            if (error.Error.Message.ToLower().Contains("ship was lost or destroyed upon entering the wormhole"))
                            {
                                _console.WriteLine("Ship was destroyed while attempting to traverse wormhole.");
                                await _shipInfo.RefreshShipData();
                            }
                            else if (error.Error.Message.StartsWith("Destination does not exist."))
                                _console.WriteLine("Destination does not exist. Please check destination and try again.");
                            else
                            {
                                await _shipInfo.RefreshShipData();
                                _console.WriteLine(error.Error.Message);
                            }
                        }
                    }
                    else
                        _console.WriteLine("Ship is already in transit on an existing flight plan.");
                }
                else
                    _console.WriteLine("Invalid ship id. Please use number ids and not the full string id.");

                return CommandResult.FAILURE;
            }
            else if (!background && args.Length == 3 && args[1].ToLower() == "rename")
            {
                if (_shipInfo.TryGetShipDataByLocalId(args[0], out var shipData))
                {
                    var lastName = shipData.DisplayName;

                    _shipInfo.UpdateShipName(shipData.ServerId, args[2]);

                    _console.WriteLine("Ship " + lastName + " renamed to " + shipData.DisplayName + ".");
                    return CommandResult.SUCCESS;
                }
                else
                    _console.WriteLine("Invalid ship id. Please use number ids and not the full string id.");
                return CommandResult.FAILURE;
            }
            else if (args.Length == 2 && args[1].ToLower() == "info")
            {
                if (_shipInfo.TryGetShipDataByLocalId(args[0], out var shipData))
                {
                    _console.WriteLine("Displaying info for " + shipData.DisplayName + ".");
                    await _console.WriteLine("Server Id: " + shipData.ServerId, 500);
                    await _console.WriteLine("Type: " + shipData.Ship.Type, 500);
                    await _console.WriteLine("Class: " + shipData.Ship.Class, 100);
                    await _console.WriteLine("Manufacturer: " + shipData.Ship.Manufacturer, 100);
                    await _console.WriteLine("Cargo Capacity: " + shipData.Ship.MaxCargo, 100);
                    await _console.WriteLine("Speed: " + shipData.Ship.Speed, 100);
                    await _console.WriteLine("Plating: " + shipData.Ship.Plating, 100);
                    await _console.WriteLine("Weapons: " + shipData.Ship.Weapons, 100);

                    return CommandResult.SUCCESS;
                }
                else
                    _console.WriteLine("Invalid ship id. Please use number ids and not the full string id.");
                return CommandResult.FAILURE;
            }

            return CommandResult.INVALID;
        }
    }
}
