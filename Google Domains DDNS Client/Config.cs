using System.Xml.Serialization;

namespace Google_Domains_DDNS_Client {
   public class Config {
      [XmlElement] public string domain;
      [XmlElement] public Credentials credentials;
   }
}
