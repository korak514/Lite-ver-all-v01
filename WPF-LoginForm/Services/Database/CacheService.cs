using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading.Tasks;

namespace WPF_LoginForm.Services
{
    public class CacheService
    {
        private static readonly string CacheFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app_cache.json");
        private ConcurrentDictionary<string, CacheItem> _memoryCache;

        public CacheService()
        {
            LoadCache();
        }

        public T Get<T>(string key)
        {
            if (_memoryCache.TryGetValue(key, out var item))
            {
                if (item.Expiration > DateTime.Now)
                {
                    try
                    {
                        return JsonConvert.DeserializeObject<T>(item.JsonData);
                    }
                    catch
                    {
                        return default;
                    }
                }
                else
                {
                    // Remove expired item
                    _memoryCache.TryRemove(key, out _);
                }
            }
            return default;
        }

        public void Set(string key, object data, TimeSpan duration)
        {
            var item = new CacheItem
            {
                Expiration = DateTime.Now.Add(duration),
                JsonData = JsonConvert.SerializeObject(data)
            };

            _memoryCache.AddOrUpdate(key, item, (k, v) => item);
            SaveCache();
        }

        public void Clear()
        {
            _memoryCache.Clear();
            if (File.Exists(CacheFilePath))
            {
                try { File.Delete(CacheFilePath); } catch { }
            }
        }

        private void LoadCache()
        {
            try
            {
                if (File.Exists(CacheFilePath))
                {
                    string json = File.ReadAllText(CacheFilePath);
                    _memoryCache = JsonConvert.DeserializeObject<ConcurrentDictionary<string, CacheItem>>(json)
                                   ?? new ConcurrentDictionary<string, CacheItem>();
                }
                else
                {
                    _memoryCache = new ConcurrentDictionary<string, CacheItem>();
                }
            }
            catch
            {
                _memoryCache = new ConcurrentDictionary<string, CacheItem>();
            }
        }

        private void SaveCache()
        {
            try
            {
                // Fire and forget save to avoid blocking UI
                Task.Run(() =>
                {
                    try
                    {
                        string json = JsonConvert.SerializeObject(_memoryCache);
                        File.WriteAllText(CacheFilePath, json);
                    }
                    catch { }
                });
            }
            catch { }
        }

        private class CacheItem
        {
            public DateTime Expiration { get; set; }
            public string JsonData { get; set; }
        }
    }
}