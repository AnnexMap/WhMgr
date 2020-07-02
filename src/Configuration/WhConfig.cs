﻿namespace WhMgr.Configuration
{
    using System;
    using System.Collections.Generic;
    using System.IO;

    using Newtonsoft.Json;

    using WhMgr.Data;

    /// <summary>
    /// Configuration file class
    /// </summary>
    public class WhConfig
    {
        /// <summary>
        /// Gets or sets the HTTP listening interface/host address
        /// </summary>
        [JsonProperty("host")]
        public string ListeningHost { get; set; }

        /// <summary>
        /// Gets or sets the HTTP listening port
        /// </summary>
        [JsonProperty("port")]
        public ushort WebhookPort { get; set; }

        /// <summary>
        /// Gets or sets the locale translation file to use
        /// </summary>
        [JsonProperty("locale")]
        public string Locale { get; set; }

        /// <summary>
        /// Gets or sets the short url API url (yourls.org)
        /// </summary>
        [JsonProperty("shortUrlApiUrl")]
        public string ShortUrlApiUrl { get; set; }

        /// <summary>
        /// Gets or sets the Stripe API key
        /// </summary>
        [JsonProperty("stripeApiKey")]
        public string StripeApiKey { get; set; }

        /// <summary>
        /// Gets or sets the Discord servers configuration
        /// </summary>
        [JsonProperty("servers")]
        public Dictionary<ulong, DiscordServerConfig> Servers { get; set; }

        /// <summary>
        /// Gets or sets the Database configuration
        /// </summary>
        [JsonProperty("database")]
        public ConnectionStringsConfig Database { get; set; }

        /// <summary>
        /// Gets or sets the Urls configuration
        /// </summary>
        [JsonProperty("urls")]
        public UrlConfig Urls { get; set; }

        //[JsonProperty("staticMap")]
        //public StaticMapConfig StaticMap { get; set; }

        /// <summary>
        /// Gets or sets the event Pokemon IDs list
        /// </summary>
        [JsonProperty("eventPokemonIds")]
        public List<int> EventPokemonIds { get; set; }

        /// <summary>
        /// Gets or sets the icon styles
        /// </summary>
        [JsonProperty("iconStyles")]
        public Dictionary<string, string> IconStyles { get; set; }

        /// <summary>
        /// Gets or sets the static maps config
        /// </summary>
        [JsonProperty("staticMaps")]
        public StaticMaps StaticMaps { get; set; }

        /// <summary>
        /// Gets or sets whether to enable Day Light Savings time for despawn timers
        /// </summary>
        [JsonProperty("enableDST")]
        public bool EnableDST { get; set; }

        /// <summary>
        /// Gets or sets whether to enable Leap Year date adjustment for despawn timers
        /// </summary>
        [JsonProperty("enableLeapYear")]
        public bool EnableLeapYear { get; set; }

        /// <summary>
        /// Gets or sets whether to log incoming webhook data to a file
        /// </summary>
        [JsonProperty("debug")]
        public bool Debug { get; set; }

        /// <summary>
        /// Gets or sets the configuration file path
        /// </summary>
        [JsonIgnore]
        public string FileName { get; set; }

        /// <summary>
        /// Instantiate a new <see cref="WhConfig"/> class
        /// </summary>
        public WhConfig()
        {
            ListeningHost = "127.0.0.1";
            WebhookPort = 8008;
            Locale = "en";
            Servers = new Dictionary<ulong, DiscordServerConfig>();
            Database = new ConnectionStringsConfig();
            Urls = new UrlConfig();
            EventPokemonIds = new List<int>();
            IconStyles = new Dictionary<string, string>();
            StaticMaps = new StaticMaps();
        }

        /// <summary>
        /// Save the current configuration object
        /// </summary>
        /// <param name="filePath">Path to save the configuration file</param>
        public void Save(string filePath)
        {
            var data = JsonConvert.SerializeObject(this, Formatting.Indented);
            File.WriteAllText(filePath, data);
        }

        /// <summary>
        /// Load the configuration from a file
        /// </summary>
        /// <param name="filePath">Path to load the configuration file from</param>
        /// <returns>Returns the deserialized configuration object</returns>
        public static WhConfig Load(string filePath)
        {
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException("Config not loaded because file not found.", filePath);
            }

            var config = MasterFile.LoadInit<WhConfig>(filePath);
            config.StaticMaps.LoadConfigs();
            return config;
        }
    }
}