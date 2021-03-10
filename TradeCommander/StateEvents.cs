using System;

namespace TradeCommander
{
    public class StateEvents
    {
        public event EventHandler<string> StateChange;

        public StateEvents() { }

        public void TriggerUpdate(object updateSource, string updateType)
        {
            StateChange?.Invoke(updateSource, updateType);
        }
    }
}
