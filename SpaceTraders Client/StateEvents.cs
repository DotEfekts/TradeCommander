using System;

namespace SpaceTraders_Client
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
