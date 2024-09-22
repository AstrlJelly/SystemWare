using CaretakerCoreNET;
using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SystemWare.Persistence
{
    public static class Voorhees
    {
        private const string GUILD_PATH = "./persist/guild.json";
        private const string USER_PATH  = "./persist/user.json";
        private readonly static JsonSerializerOptions serializerSettings = 
            new JsonSerializerOptions {
                WriteIndented = true,
                IncludeFields = true,
            };

        public static async Task SaveGuilds(Dictionary<ulong, GuildPersist> dictionary) => await Save(GUILD_PATH, dictionary);
        public static async Task SaveUsers(Dictionary<ulong, UserPersist> dictionary)   => await Save(USER_PATH, dictionary);

        private static async Task Save<T>(string path, Dictionary<ulong, T> objectToSave)
        {
            CaretakerCore.LogInfo($"Start saving to {path}...", true);
            if (!Directory.Exists("./persist")) Directory.CreateDirectory("./persist");
            string? serializedDict = JsonSerializer.Serialize(objectToSave, serializerSettings);
            await File.WriteAllTextAsync(path, serializedDict); // creates file if it doesn't exist
            CaretakerCore.LogInfo("Saved!", true);
        }

        public static async Task<Dictionary<ulong, GuildPersist>> LoadGuilds() => await Load<Dictionary<ulong, GuildPersist>>(GUILD_PATH);
        public static async Task<Dictionary<ulong, UserPersist>>  LoadUsers()  => await Load<Dictionary<ulong, UserPersist>>(USER_PATH);

        // tells the compiler that T should always implement new(), so that i can construct a default dictionary. i love C#
        private static async Task<T> Load<T>(string path) where T : new()
        {
            CaretakerCore.LogInfo($"Start loading from {path}...", true);
            if (!Directory.Exists("./persist")) Directory.CreateDirectory("./persist");
            if (!File.Exists(path)) File.Create(path);
            string jsonFileStr = await File.ReadAllTextAsync(path);

            if (string.IsNullOrEmpty(jsonFileStr)) return new();

            try {
                var deserializedDict = JsonSerializer.Deserialize<T>(jsonFileStr, serializerSettings);
                if (deserializedDict != null) {
                    // LogTemp("deserializedDict : " + deserializedDict);
                    CaretakerCore.LogInfo("Loaded!", true);
                    return deserializedDict;
                } else {
                    throw new Exception($"Load (\"{path}\") failed!");
                }
            } catch (Exception err) {
                CaretakerCore.LogError(err, true);
                throw;
            }
        }
    }

}

