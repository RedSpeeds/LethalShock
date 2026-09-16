using PiShockApiLibrary;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace LethalShock
{
    public class PiShockApi
    {
        // create class and set variables
        public string username { private get; set; }
        public string apiKey { private get; set; }
        public string code { private get; set; }
        public string senderName { private get; set; }

        private static readonly ConcurrentDictionary<string, Task<IShocker>> ShockerCache = new();

        private async Task<IShocker> GetShocker()
        {
            var cacheKey = $"{username}|{apiKey}|{code}";
            var shockerTask = ShockerCache.GetOrAdd(cacheKey, _ => ShockerFactory.CreateLegacyShocker(apiKey, username, shareCode: code, agent: senderName));
            try
            {
                return await shockerTask;
            }
            catch
            {
                // Don't leave a permanently-faulted resolution cached; retry next time.
                ShockerCache.TryRemove(cacheKey, out _);
                throw;
            }
        }

        public async Task Shock(int intensity, int duration)
        {
            try
            {
                await (await GetShocker()).Shock(duration, intensity);
            }
            catch (PishockException ex)
            {
                LethalShock.instance.mls.LogError($"PiShock shock request failed: {ex.Message}");
            }
        }

        public async Task Vibrate(int intensity, int duration)
        {
            try
            {
                await (await GetShocker()).Vibrate(duration, intensity);
            }
            catch (PishockException ex)
            {
                LethalShock.instance.mls.LogError($"PiShock vibrate request failed: {ex.Message}");
            }
        }

        public async Task Beep(int duration)
        {
            try
            {
                await (await GetShocker()).Beep(duration);
            }
            catch (PishockException ex)
            {
                LethalShock.instance.mls.LogError($"PiShock beep request failed: {ex.Message}");
            }
        }
    }
}
