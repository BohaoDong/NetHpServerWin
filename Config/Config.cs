using NetHpServer.Logger;
using NetHpServer.utility.JsonHelper;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Serialization;

namespace NetHpServer.Config
{
    public class Config
    {
        public string ServerIp { get; set; }
        public string TcpPorts { get; set; }
        public string UdpPorts { get; set; }
        public int UdpPortStart { get; set; }
        public int UdpPortEnd { get; set; }
        public int HearIntervalTime { get; set; }

        [XmlIgnore]
        public List<int> TcpPortList => TcpPorts?.Split(',').Select(a => Convert.ToInt32(a)).ToList();

        [XmlIgnore]
        public List<int> UdpPortList => UdpPorts?.Split(',').Select(a => Convert.ToInt32(a)).ToList();

        [XmlIgnore]
        private static readonly string ConfigPath = System.Environment.CurrentDirectory + "\\Config\\Config.json";

        [XmlIgnore]
        public static readonly Config Instance = new Config() { ServerIp = "10.1.24.164,", TcpPorts = "554", UdpPortStart = 60000, UdpPortEnd = 65000, HearIntervalTime = 6000 };

        static Config()
        {
            Load<Config>(ref Instance);
        }

        protected static void Load<T>(ref T ac) where T : Config, new()
        {
            if (File.Exists(ConfigPath))
            {
                if (true)   //_serializerMode == "json"
                {
                    try
                    {
                        var json = File.ReadAllText(ConfigPath);
                        //ac = Newtonsoft.Json.JsonConvert.DeserializeObject<T>(json);
                        ac = json.JsonConvert<T>();
                        //var aa = ac.TcpPorts;
                    }
                    catch (Exception ex)
                    {
                        NetLogger.Log($"Configuration parameters were not loaded incorrectly. StackTrace:{ex.StackTrace}");
                    }
                }
            }
            else
            {
                NetLogger.Log($"Profile does not exist.ConfigPath: {ConfigPath}");
            }
        }
    }
}