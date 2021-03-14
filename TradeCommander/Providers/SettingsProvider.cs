using Blazored.LocalStorage;
using System;
using System.Collections.Generic;
using System.Linq;
using TradeCommander.Models;

namespace TradeCommander.Providers
{
    public class SettingsProvider
    {
        private readonly ISyncLocalStorageService _localStorage;
        private readonly UserProvider _userProvider;

        private Dictionary<string, Setting> _settings;

        public event EventHandler<SettingsEventArgs> SettingsUpdated;

        public SettingsProvider(
            ISyncLocalStorageService localStorage,
            UserProvider userProvider
            )
        {
            _localStorage = localStorage;
            _userProvider = userProvider;

            _userProvider.UserUpdated += UserUpdated;

            LoadSettings();
        }

        public Setting[] GetSettings()
        {
            if (_settings != null)
                return _settings.Values.ToArray();
            return Array.Empty<Setting>();
        }

        public bool TryGetSetting(string settingKey, out Setting setting)
        {
            setting = GetSetting(settingKey);
            return setting != null;
        }

        public Setting GetSetting(string setting)
        {
            if (_settings != null && _settings.ContainsKey(setting))
                return _settings[setting];
            return null;
        }

        public void SetSetting(string setting, string value)
        {
            if(_settings != null)
            {
                _settings[setting] = new Setting
                {
                    Key = setting,
                    Value = value,
                    Inherited = false
                };

                SaveSettings();

                SettingsUpdated?.Invoke(this, new SettingsEventArgs
                {
                    Settings = _settings.Values.ToArray()
                });
            }
        }

        public void UserUpdated(object sender, UserEventArgs args)
        {
            if(args.IsFullRefresh)
                LoadSettings();
        }

        public void LoadSettings()
        {
            _settings = new Dictionary<string, Setting>();

            if (_userProvider.Username != null && _localStorage.ContainKey("SettingsData." + _userProvider.Username))
            {
                var settings = _localStorage.GetItem<Dictionary<string, Setting>>("SettingsData." + _userProvider.Username);
                foreach (var setting in settings)
                    _settings[setting.Key] = setting.Value;
            }

            if (_localStorage.ContainKey("SettingsData"))
            {
                var settings = _localStorage.GetItem<Dictionary<string, Setting>>("SettingsData");
                foreach (var setting in settings)
                {
                    setting.Value.Inherited = _userProvider.Username == null;
                    _settings[setting.Key] = setting.Value;
                }
            }

            SettingsUpdated?.Invoke(this, new SettingsEventArgs
            {
                Settings = _settings.Values.ToArray()
            });
        }

        private void SaveSettings()
        {
            if (_settings != null)
                if(_userProvider.Username != null)
                    _localStorage.SetItem("SettingsData." + _userProvider.Username, _settings);
                else
                    _localStorage.SetItem("SettingsData", _settings);
        }
    }

    public class SettingsEventArgs
    {
        public Setting[] Settings { get; set; }
    }
}
