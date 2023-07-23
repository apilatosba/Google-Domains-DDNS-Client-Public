#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
using System.Xml.Serialization;

namespace Google_Domains_DDNS_Client {
   public class Credentials {
      [XmlElement] public string username;
      [XmlElement] public string password;
   }
}
