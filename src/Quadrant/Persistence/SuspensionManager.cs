using System;
using System.Threading.Tasks;
using Quadrant.Utility;
using Windows.ApplicationModel.UserActivities;
using Windows.Storage;

namespace Quadrant.Persistence
{
    internal class SuspensionManager
    {
        private const string LastSessionIdSettingName = "LastSessionId";
        private const string LegacySuspensionName = "SuspensionData";
        private static readonly StorageFolder SuspensionDataFolder = ApplicationData.Current.LocalCacheFolder;

        private readonly string _sessionId;
        private bool _isFirstSuspend = true;
        private UserActivitySession _session;
        private UserActivity _activity;

        public SuspensionManager()
        {
            _sessionId = CreateSessionId();
        }

        public async Task InitializeUserSessionAsync()
        {
            UserActivityChannel channel = UserActivityChannel.GetDefault();
            _activity = await channel.GetOrCreateUserActivityAsync(_sessionId);
            _activity.ActivationUri = new Uri($"quadrant-app:resume?{_sessionId}");
            _activity.VisualElements.DisplayText = AppUtilities.GetString("AppName");
            await _activity.SaveAsync();

            _session = _activity.CreateSession();
        }

        public async Task DeleteOldFilesAsync(double days = 10)
        {
            DateTimeOffset now = DateTimeOffset.Now;
            foreach (StorageFile file in await SuspensionDataFolder.GetFilesAsync())
            {
                if (file.Name.StartsWith(_sessionId))
                {
                    continue;
                }

                if (file.DateCreated.AddDays(days) < now)
                {
                    await file.DeleteAsync();
                }
            }
        }

        public Task SuspendAsync(Func<Serializer, Task> serializeAsync)
        {
            SaveSessionId();
            return Serializer.SerializeAsync(SuspensionDataFolder, _sessionId, serializeAsync);
        }

        public Task UpdateUserSessionAsync(string description)
        {
            _activity.VisualElements.Description = description;
            return _activity.SaveAsync().AsTask();
        }

        public Task ResumeAsync(string sessionId, Func<Deserializer, Task> deserializeAsync)
        {
            string id = sessionId ?? GetLastSessionId();
            if (string.IsNullOrEmpty(id))
            {
                return Task.CompletedTask;
            }

            return Deserializer.DeserializeAsync(SuspensionDataFolder, id, deserializeAsync);
        }

        private void SaveSessionId()
        {
            if (_isFirstSuspend)
            {
                ApplicationData.Current.LocalSettings.Values[LastSessionIdSettingName] = _sessionId;
                _isFirstSuspend = false;
            }
        }

        private static string GetLastSessionId()
        {
            if (ApplicationData.Current.LocalSettings.Values.TryGetValue(LastSessionIdSettingName, out object value) && value is string id)
            {
                return id;
            }

            return null;
        }

        private static string CreateSessionId()
        {
            string time = DateTime.UtcNow.ToString("yyyyMMddHHmm");
            return $"session{time}";
        }
    }
}
