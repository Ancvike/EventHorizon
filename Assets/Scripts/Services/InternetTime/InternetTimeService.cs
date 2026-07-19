using System;
using CommonComponents.Signals;
using Zenject;

namespace Services.InternetTime
{
    public class InternetTimeService : IInitializable
    {
        private readonly ServerTimeReceivedSignal.Trigger _timeReceivedTrigger;

        public InternetTimeService(ServerTimeReceivedSignal.Trigger timeReceivedTrigger)
        {
            _timeReceivedTrigger = timeReceivedTrigger;
        }

        public bool HasBeenReceived => true;
        public DateTime DateTime => DateTime.Now;
        public int TotalDays => (int)(DateTime.Ticks / TimeSpan.TicksPerDay);

        public void Initialize()
        {
            _timeReceivedTrigger.Fire(DateTime.Now);
        }
    }

    public class ServerTimeReceivedSignal : SmartWeakSignal<ServerTimeReceivedSignal, DateTime> {}
}
