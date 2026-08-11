using System.IO;
using Newtonsoft.Json;

namespace DoomSurvivor.Infrastructure
{
    public static class ConfigJson
    {
        public static T Deserialize<T>(string json) => JsonConvert.DeserializeObject<T>(json);

        public static T DeserializeFile<T>(string path) =>
            Deserialize<T>(File.ReadAllText(path));
    }
}
