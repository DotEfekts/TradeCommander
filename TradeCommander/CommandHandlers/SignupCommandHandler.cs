using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using TradeCommander.Models;
using TradeCommander.Providers;

namespace TradeCommander.CommandHandlers
{
    public class SignupCommandHandler : ICommandHandlerAsync
    {
        private readonly HttpClient _http;
        private readonly ConsoleOutput _console;
        private readonly UserProvider _userInfo;
        private readonly JsonSerializerOptions _serializerOptions;

        public SignupCommandHandler(
            HttpClient http,
            ConsoleOutput console,
            UserProvider userInfo,
            JsonSerializerOptions serializerOptions
            )
        {
            _http = http;
            _console = console;
            _userInfo = userInfo;
            _serializerOptions = serializerOptions;
        }

        public string CommandName => "SIGNUP";
        public bool BackgroundCanUse => false;
        public bool RequiresLogin => false;

        public string HandleAutoComplete(string[] args, int index, bool loggedIn) => null;

        public async Task<CommandResult> HandleCommandAsync(string[] args, bool background, bool loggedIn)
        {
            if (loggedIn)
            {
                _console.WriteLine("You must be signed out to use this command.");
                return CommandResult.FAILURE;
            }

            if (args.Length == 1 && (args[0] == "?" || args[0].ToLower() == "help"))
            {
                _console.WriteLine("SIGNUP: Creates an account for the SpaceTraders API.");
                _console.WriteLine("Usage: SIGNUP <Username>");
                return CommandResult.SUCCESS;
            }
            else if (args.Length != 1)
                return CommandResult.INVALID;
            else
            {
                var httpResult = await _http.PostAsJsonAsync("/users/" + args[0] + "/claim", new { });

                if (httpResult.IsSuccessStatusCode)
                {
                    var signupResult = await httpResult.Content.ReadFromJsonAsync<SignupResponse>(_serializerOptions);

                    await _userInfo.SetDetailsAsync(signupResult.Token);

                    _console.Clear();
                    _console.WriteLine("Welcome, " + _userInfo.UserDetails.Username + ". Your token is: " + signupResult.Token);
                    _console.WriteLine("Please copy this token somewhere safe as it is not recoverable from the SpaceTraders API.");
                    _console.WriteLine("To view your token again use the command \"TOKEN\" while logged in.");

                    if (_userInfo.UserDetails == null)
                        _console.WriteLine("An error occurred during login. Please copy your token and login manually.");
                    else
                    {
                        _console.WriteLine("For command list see HELP.");
                        return CommandResult.SUCCESS;
                    }
                }
                else if (httpResult.StatusCode == HttpStatusCode.Conflict)
                {
                    _console.WriteLine("Username already exists. Please pick another.");
                }
                else
                {
                    var error = await httpResult.Content.ReadFromJsonAsync<ErrorResponse>(_serializerOptions);
                    _console.WriteLine(error.Error.Message);
                }

                return CommandResult.FAILURE;
            }
        }
    }
}
