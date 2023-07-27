#pragma warning disable CS8600 // Converting null literal or possible null value to non-nullable type.
#pragma warning disable CS8604 // Possible null reference argument.
#pragma warning disable CS8603 // Possible null reference return.
using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Net.Sockets;
using System.IO;
using System.Text;
using System.Xml.Serialization;


// Abort using http helper classes cause they suck. use sockets instead. UPDATE: needed to get ssl done so went back to httpclient and found how to add headers.
namespace Google_Domains_DDNS_Client {
   internal class Program {
      const string IP_CHECK_URL = "https://domains.google.com/checkip";
      const string LAST_KNOWN_IP_FILE = "./LastKnownIP.txt";
      const string LOG_FILE_PATH = "./log.txt";
      const string CONFIG_FILE_PATH = "./Config.xml";
      readonly static Config config;
      /// <summary>
      /// In milliseconds
      /// </summary>
      const int TIME_BETWEEN_IP_CHECKS = 1000 * 60; // 1 minute



      static Program() {
         bool shouldQuitProgram = false;

         try {
            config = GetConfigFromXml();
         } catch (FileNotFoundException) {
            CreateXmlFileWithTemplate();

            Console.Error.WriteLine("Config is not set up. Check logs for more info.");
            AddEntryToLog(LogEntryType.Error, "There was no Config.xml file. A Config.xml file is created for you. Go there and provide necessary information.");

            Environment.Exit(1);
         }

         shouldQuitProgram = HasObjectMeaninglessFields(config) || HasObjectMeaninglessFields(config.credentials);


         if (shouldQuitProgram) Environment.Exit(1);
      }


      static async Task Main(string[] args) {
         // Create log file if it doesn't exist
         if (!File.Exists(LOG_FILE_PATH)) {
            using var fileStream = File.Create(LOG_FILE_PATH);
            fileStream.Close();
         }
         
         IPAddress publicIP;

         IPAddress lastKnownIP;
         try {
            lastKnownIP = await GetLastKnownIPFromFile();
         } catch (FormatException) { // File doesn't contain an IP
            // I think i should update the dns and if the response is either good or nochg 
            // then i should set the lastKnownIP and enter the main loop
            // if it is neither good or nochg then i should exit the program and never enter the main loop
            publicIP = await GetHostIP();
            var response = await UpdateDNS(publicIP);
            string responseBody = await response.Content.ReadAsStringAsync();

            ResponseType responseType = HandleResponse(responseBody);
            if (responseType is not ResponseType.Good && responseType is not ResponseType.Nochg) {
               // Environment.Exit(1); 
               return;
            } else {
               // HandleResponse updates LastKnownIPFile if the response is either good or nochg.
               // So read back from file to get the last known ip.
               lastKnownIP = await GetLastKnownIPFromFile();
            }
         }


         // Main loop
         for (; ; await Task.Delay(TIME_BETWEEN_IP_CHECKS)) {
            // If can't get public IP, then write an warning message and continue to the next iteration
            try {
               publicIP = await GetHostIP();
            } catch (Exception e) {
               Console.Error.WriteLine("Warning: Couldn't get host public IP. Check logs for more info.");
               AddEntryToLog(LogEntryType.Warning, $"Couldn't get host public IP. Exception message: {e.Message}");

               await Console.Out.WriteLineAsync("Skipping one iteration.");
               AddEntryToLog(LogEntryType.Info, $"Continuing to the next iteration. If program can't get public IP address from the IP provider server, which is \"{IP_CHECK_URL}\", it skips one iteration without attempting to update the dns record.");
               continue;
            }

            if (!publicIP.Equals(lastKnownIP)) { // If the public IP has changed
               var response = await UpdateDNS(publicIP);
               string responseBody = await response.Content.ReadAsStringAsync();

               var responseType = HandleResponse(responseBody);

               if (responseType is not ResponseType.Good && responseType is not ResponseType._911) Environment.Exit(1); // Quitting program for now. To not get banned from Google Domains.

               lastKnownIP = publicIP;
            }
         }
      }

      // TODO: Get IP from machine itself.
      static async Task<IPAddress> GetHostIP() {
         using HttpClient ipProvider = new HttpClient();
         var response = await ipProvider.GetAsync(IP_CHECK_URL);

         IPAddress hostPublicIP;
         try {
            hostPublicIP = IPAddress.Parse(await response.Content.ReadAsStringAsync());
         } catch (Exception e) when (e is ArgumentNullException || e is FormatException) {
            Console.WriteLine("Warning: IP provider server doesn't respond with a parseable IP. Check logs for more info.");
            AddEntryToLog(LogEntryType.Warning, $"IP provider server doesn't respond with a parseable IP. Exception message: {e.Message}");
            throw;
         }

         if (hostPublicIP.AddressFamily != AddressFamily.InterNetworkV6) {
            Console.Error.WriteLine("Warning: Host public IP is not IPv6");
            AddEntryToLog(LogEntryType.Warning, $"The IP provider server didn't respond with an IPv6. The IP that the provider responded with: {hostPublicIP}");
            throw new Exception("Host public IP is not IPv6");
         }

         return hostPublicIP;
      }


      static async Task<IPAddress> GetLastKnownIPFromFile() {
         string lastKnownIPString;
         try {
            lastKnownIPString = (await File.ReadAllTextAsync(LAST_KNOWN_IP_FILE)).Trim();
         } catch (FileNotFoundException) {
            using var fileStream = File.Create(LAST_KNOWN_IP_FILE);
            fileStream.Close();
            lastKnownIPString = "";
         }

         IPAddress lastKnownIP;
         try {
            lastKnownIP = IPAddress.Parse(lastKnownIPString);
         } catch (FormatException e) {
            Console.Error.WriteLine("Warning: Last known IP file doesn't contain a parseable IP. Check logs for more info.");
            AddEntryToLog(LogEntryType.Warning, $"If you run the program for the first time ignore this warning. Last known IP file doesn't contain a parseable IP. Exception message: {e.Message}");
            throw;
         }

         return lastKnownIP;
      }


      static async Task<HttpResponseMessage> UpdateDNS(IPAddress hostPublicIP) {
         string base64EncodedCredentials = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{config.credentials.username}:{config.credentials.password}"));

         using HttpClient httpClient = new HttpClient();
         httpClient.DefaultRequestHeaders.Add("User-Agent", "Chrome/41.0");
         httpClient.DefaultRequestHeaders.Add("Authorization", $"Basic {base64EncodedCredentials}");
         httpClient.DefaultRequestHeaders.Add("Host", "domains.google.com");


         var response = await httpClient.PostAsync($"https://domains.google.com/nic/update?hostname={config.domain}&myip={hostPublicIP}", null);


         return response;
      }


      static ResponseType HandleResponse(string responseBody) {
         ResponseType responseType;

         string firstWord = responseBody.Split(' ')[0];
         string secondWord;

         IPAddress newIP = null;
         try {
            secondWord = responseBody.Split(' ')[1];
            newIP = IPAddress.Parse(secondWord);
         } catch {
            secondWord = "";
         }


         switch (firstWord.ToLower()) { // ToLower() just in case
            case "good":
               Console.WriteLine($"DNS updated successfully");
               AddEntryToLog(LogEntryType.Info, $"DNS updated successfully. Google's response: \"{responseBody}\"");

               UpdateLastKnownIPFile(newIP);

               responseType = ResponseType.Good;
               break;
            case "nochg":
               Console.WriteLine($"The supplied IP address is already set for this host. You should not attempt another update until your IP address changes.");
               AddEntryToLog(LogEntryType.Warning, $"The supplied IP address is already set for this host. You should not attempt another update until your IP address changes. Google's response: \"{responseBody}\"");

               UpdateLastKnownIPFile(newIP);

               responseType = ResponseType.Nochg;
               break;
            case "nohost":
               Console.WriteLine($"The hostname does not exist, or does not have Dynamic DNS enabled.");
               AddEntryToLog(LogEntryType.Error, $"The hostname does not exist, or does not have Dynamic DNS enabled.");

               responseType = ResponseType.Nohost;
               break;
            case "badauth":
               Console.WriteLine($"The username / password combination is not valid for the specified host.");
               AddEntryToLog(LogEntryType.Error, $"The username / password combination is not valid for the specified host.");

               responseType = ResponseType.Badauth;
               break;
            case "notfqdn":
               Console.WriteLine($"The supplied hostname is not a valid fully-qualified domain name.");
               AddEntryToLog(LogEntryType.Error, $"The supplied hostname is not a valid fully-qualified domain name.");

               responseType = ResponseType.Notfqdn;
               break;
            case "badagent":
               Console.WriteLine($"Your Dynamic DNS client is making bad requests. Ensure the user agent is set in the request, and that you’re only attempting to set an IPv4 or IPv6 address, and not both.");
               AddEntryToLog(LogEntryType.Error, $"Your Dynamic DNS client is making bad requests. Ensure the user agent is set in the request, and that you’re only attempting to set an IPv4 or IPv6 address, and not both.");

               responseType = ResponseType.Badagent;
               break;
            case "abuse":
               Console.WriteLine($"Dynamic DNS access for the hostname has been blocked due to failure to interpret previous responses correctly.");
               AddEntryToLog(LogEntryType.Error, $"Dynamic DNS access for the hostname has been blocked due to failure to interpret previous responses correctly.");

               responseType = ResponseType.Abuse;
               break;
            case "911":
               Console.WriteLine($"An error happened on Google's end. Wait 5 minutes and retry.");
               AddEntryToLog(LogEntryType.Error, $"An error happened on Google's end. Wait 5 minutes and retry.");
               AddEntryToLog(LogEntryType.Info, $"Blocking the thread for 5 minutes until {DateTime.Now + new TimeSpan(0, 5, 0):T}");

               Task.Delay(1000 * 60 * 5).Wait();

               AddEntryToLog(LogEntryType.Info, "Continuing the program");

               responseType = ResponseType._911;
               break;
            case "conflict":
               Console.WriteLine($"A custom A or AAAA resource record conflicts with the update. Delete the indicated resource record within DNS settings page and try the update again.");
               AddEntryToLog(LogEntryType.Error, $"A custom A or AAAA resource record conflicts with the update. Delete the indicated resource record within DNS settings page and try the update again. The response from google: \"{responseBody}\"");

               if (secondWord == "A") responseType = ResponseType.ConflictA;
               else if (secondWord == "AAAA") responseType = ResponseType.ConflictAAAA;
               else responseType = ResponseType.Unknown;

               break;
            default:
               Console.WriteLine($"Unknown response. Check log file for more info.");
               AddEntryToLog(LogEntryType.Error, $"Unknown response. The response:\r\n{responseBody}");

               responseType = ResponseType.Unknown;
               break;
         }

         return responseType;
      }


      static void UpdateLastKnownIPFile(in string ip) {
         File.Delete(LAST_KNOWN_IP_FILE);
         File.WriteAllText(LAST_KNOWN_IP_FILE, ip);
      }

      static void UpdateLastKnownIPFile(in IPAddress ip) {
         UpdateLastKnownIPFile(ip.ToString());
      }

      static void AddEntryToLog(LogEntryType entryType, string entry) {
         AddEntryToLog(new LogEntry(entryType, entry));
      }

      static void AddEntryToLog(LogEntry logEntry) {
         using StreamWriter logWriter = new StreamWriter(LOG_FILE_PATH, true);
         logWriter.WriteLine(logEntry.ToString());
      }

      static Config GetConfigFromXml() {
         XmlSerializer serializer = new XmlSerializer(typeof(Config));

         using Stream reader = new FileStream(CONFIG_FILE_PATH, FileMode.Open);
         return serializer.Deserialize(reader) as Config;
      }

      static void CreateXmlFileWithTemplate() {
         string xmlTemplate =
            "<?xml version=\"1.0\" encoding=\"utf-8\" ?>\r\n" +
            "<Config>\r\n" +
            "   <domain></domain>\r\n" +
            "   <credentials>\r\n" +
            "      <username></username>\r\n" +
            "      <password></password>\r\n" +
            "   </credentials>\r\n" +
            "</Config>\r\n"
            ;

         File.WriteAllText(CONFIG_FILE_PATH, xmlTemplate);
      }


      /// <summary>
      /// 
      /// </summary>
      /// <param name="obj"></param>
      /// <returns>True if any field is null. Also returns true if there is empty string</returns>
      static bool HasObjectMeaninglessFields(object obj) {
         bool hasObjectMeaninglessField = false;

         foreach (var v in obj.GetType().GetFields()) {
            if (v.GetValue(obj) == null || string.IsNullOrEmpty(v.GetValue(obj)?.ToString())) {
               Console.Error.WriteLine($"ERROR: \"{v.Name}\" is null. Check logs for more info.");
               AddEntryToLog(LogEntryType.Error, $"\"{v.Name}\" can't be null. Go to Config.xml and fill it. The xml file should be located at the third parent directory of the executable file ie project root directory");
               hasObjectMeaninglessField = true;
            }
         }

         return hasObjectMeaninglessField;
      }
   }
}
