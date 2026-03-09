using System;
using System.IO;
using System.Text.Json;

namespace Palisades.Helpers
{
    internal sealed class SessionState
    {
        public bool LastExitClean { get; set; }
        public bool RestoreOnExit { get; set; }
        public DateTime LastStartedAtUtc { get; set; }
        public DateTime LastExitedAtUtc { get; set; }
    }

    internal static class SessionStateHelper
    {
        internal static SessionState Load()
        {
            string path = GetPath();
            if (!File.Exists(path))
            {
                return new SessionState();
            }

            try
            {
                string json = File.ReadAllText(path);
                return JsonSerializer.Deserialize<SessionState>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                }) ?? new SessionState();
            }
            catch
            {
                return new SessionState();
            }
        }

        internal static void MarkSessionStarted()
        {
            SessionState state = Load();
            state.LastExitClean = false;
            state.LastStartedAtUtc = DateTime.UtcNow;
            Save(state);
        }

        internal static void MarkSessionExited(bool restoreOnExit)
        {
            SessionState state = Load();
            state.LastExitClean = true;
            state.RestoreOnExit = restoreOnExit;
            state.LastExitedAtUtc = DateTime.UtcNow;
            Save(state);
        }

        internal static string GetPath()
        {
            return Path.Combine(PDirectory.GetAppDirectory(), "session-state.json");
        }

        private static void Save(SessionState state)
        {
            string directory = PDirectory.GetAppDirectory();
            PDirectory.EnsureExists(directory);
            string json = JsonSerializer.Serialize(state, new JsonSerializerOptions
            {
                WriteIndented = true,
            });
            File.WriteAllText(GetPath(), json);
        }
    }
}
