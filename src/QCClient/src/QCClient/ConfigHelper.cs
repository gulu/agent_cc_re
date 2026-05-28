using System;
using System.IO;
using System.Text;
using Newtonsoft.Json;

namespace QCClient
{
    public class AppConfig
    {
        [JsonProperty("BackendSettings")]
        public BackendSettings Backend { get; set; } = new();

        [JsonProperty("OcrSettings")]
        public OcrSettings Ocr { get; set; } = new();

        [JsonProperty("WebSettings")]
        public WebSettings Web { get; set; } = new();

        [JsonProperty("Logging")]
        public LogSettings Logging { get; set; } = new();
    }

    public class BackendSettings
    {
        [JsonProperty("Url")]
        public string Url { get; set; } = "http://localhost:5263";

        [JsonProperty("ApiTimeoutSeconds")]
        public int ApiTimeoutSeconds { get; set; } = 30;
    }

    public class OcrSettings
    {
        [JsonProperty("DefaultIntervalSeconds")]
        public int DefaultIntervalSeconds { get; set; } = 5;

        [JsonProperty("IdleSeconds")]
        public int IdleSeconds { get; set; } = 2;

        [JsonProperty("Areas")]
        public OcrAreaConfig[] Areas { get; set; } = Array.Empty<OcrAreaConfig>();
    }

    public class OcrAreaConfig
    {
        [JsonProperty("Name")] public string Name { get; set; } = "";
        [JsonProperty("Type")] public string Type { get; set; } = "";
        [JsonProperty("X")] public int X { get; set; }
        [JsonProperty("Y")] public int Y { get; set; }
        [JsonProperty("Width")] public int Width { get; set; }
        [JsonProperty("Height")] public int Height { get; set; }
        [JsonProperty("IntervalSeconds")] public int IntervalSeconds { get; set; } = 5;
        [JsonProperty("Enabled")] public bool Enabled { get; set; } = true;
    }

    public class WebSettings
    {
        [JsonProperty("EnableNotification")] public bool EnableNotification { get; set; } = true;
        [JsonProperty("SidebarWidth")] public int SidebarWidth { get; set; } = 320;
        [JsonProperty("AlwaysOnTop")] public bool AlwaysOnTop { get; set; } = true;
        [JsonProperty("EnableSound")] public bool EnableSound { get; set; } = true;
        [JsonProperty("Theme")] public string Theme { get; set; } = "light";
        [JsonProperty("ShowDebugLog")] public bool ShowDebugLog { get; set; } = true;
    }

    public class LogSettings
    {
        [JsonProperty("LogLevel")] public LogLevelConfig LogLevel { get; set; } = new();
        [JsonProperty("File")] public LogFileConfig File { get; set; } = new();
    }

    public class LogLevelConfig
    {
        [JsonProperty("Default")] public string Default { get; set; } = "Information";
    }

    public class LogFileConfig
    {
        [JsonProperty("Path")] public string Path { get; set; } = "logs\\qcclient-.log";
        [JsonProperty("RollingInterval")] public string RollingInterval { get; set; } = "Day";
        [JsonProperty("RetainedFileCountLimit")] public int RetainedFileCountLimit { get; set; } = 30;
    }

    public class ConfigHelper
    {
        private readonly string _configPath;
        private AppConfig _config;
        private readonly object _lock = new();
        private DateTime _lastLoadTime;
        private FileSystemWatcher _watcher;

        public event Action<AppConfig> ConfigChanged;

        public ConfigHelper()
        {
            _configPath = FindConfigPath();
            _config = Load();
            StartWatcher();
        }

        private string FindConfigPath()
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string path = Path.Combine(baseDir, "appsettings.json");
            if (File.Exists(path)) return path;

            path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "appsettings.json");
            if (File.Exists(path)) return Path.GetFullPath(path);

            return Path.Combine(baseDir, "appsettings.json");
        }

        public AppConfig Load()
        {
            try
            {
                if (File.Exists(_configPath))
                {
                    string json = File.ReadAllText(_configPath, new UTF8Encoding(false));
                    var cfg = JsonConvert.DeserializeObject<AppConfig>(json);
                    if (cfg != null)
                    {
                        _config = cfg;
                        _lastLoadTime = File.GetLastWriteTime(_configPath);
                        return cfg;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Warning("Failed to load config: " + ex.Message);
            }
            _config = new AppConfig();
            return _config;
        }

        public AppConfig Get() { lock (_lock) return _config; }

        public void Save(AppConfig config)
        {
            lock (_lock)
            {
                _config = config;
                string json = JsonConvert.SerializeObject(config, Formatting.Indented);
                File.WriteAllText(_configPath, json, new UTF8Encoding(false));
            }
        }

        public void Reload()
        {
            Load();
            ConfigChanged?.Invoke(_config);
        }

        private void StartWatcher()
        {
            try
            {
                string dir = Path.GetDirectoryName(_configPath);
                string file = Path.GetFileName(_configPath);
                _watcher = new FileSystemWatcher(dir, file)
                {
                    NotifyFilter = NotifyFilters.LastWrite,
                    EnableRaisingEvents = true
                };
                _watcher.Changed += (s, e) =>
                {
                    try
                    {
                        System.Threading.Thread.Sleep(500);
                        var lastWrite = File.GetLastWriteTime(_configPath);
                        if (lastWrite != _lastLoadTime) Reload();
                    }
                    catch { }
                };
            }
            catch { }
        }
    }
}
