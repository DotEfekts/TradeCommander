using System;

namespace TradeCommander.Providers
{
    public class StateProvider
    {
        public event EventHandler<string> StateUpdated;

        public StateProvider() { }

        public void TriggerUpdate(object updateSource, string updateType)
        {
            StateUpdated?.Invoke(updateSource, updateType);
        }
    }
}
