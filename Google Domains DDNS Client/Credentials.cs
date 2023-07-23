using System.Xml.Serialization;

namespace Google_Domains_DDNS_Client {
   public class Credentials {
      [XmlElement] public string username;
      [XmlElement] public string password;
   }
}
