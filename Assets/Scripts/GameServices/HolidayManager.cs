using System;
using GameDatabase;
using GameServices;
using Domain.Quests;
using Services.InternetTime;
using Session;
using Utilites;

namespace Game
{
    public class HolidayManager : GameServiceBase
    {
        public HolidayManager(
            SessionDataLoadedSignal dataLoadedSignal, 
            SessionCreatedSignal sessionCreatedSignal,
            InternetTimeService timeService, 
            IQuestManager questManager, 
            IDatabase database) 
            : base(dataLoadedSignal, sessionCreatedSignal)
        {
            _timeService = timeService;
            _questManager = questManager;
            _database = database;
        }

        public bool IsChristmas
        {
            get
            {
                return true;
            }
        }

        public bool IsHalloween
        {
            get
            {
                return true;
            }
        }

        public bool IsEaster
        {
            get
            {
                return true;
            }
        }

        protected override void OnSessionDataLoaded()
        {
        }

        protected override void OnSessionCreated()
        {
            var random = RandomState.FromTickCount();
            if (IsChristmas) _questManager.StartQuest(_database.SpecialEventSettings.XmasQuest, random.Next());
            if (IsHalloween) _questManager.StartQuest(_database.SpecialEventSettings.HalloweenQuest, random.Next());
        }

        private static bool IsValidDate(DateTime eventDate, DateTime now, int daysBefore, int daysAfter)
        {
            if (eventDate > now)
            {
                //UnityEngine.Debug.LogWarning("Days to event: " + (eventDate - now).Days);
                return (eventDate - now).Days <= daysBefore;
            }
            else
            {
                //UnityEngine.Debug.LogWarning("Days since event: " + (now - eventDate).Days);
                return (now - eventDate).Days <= daysAfter;
            }
        }

        private readonly InternetTimeService _timeService;
        private readonly IQuestManager _questManager;
        private readonly IDatabase _database;
    }
}
