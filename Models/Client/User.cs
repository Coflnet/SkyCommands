using System.Collections.Generic;
using System.Runtime.Serialization;

namespace Coflnet.Sky.McConnect.Models
{
    [DataContract]
    public class User
    {
        [IgnoreDataMember]
        public int Id { get; set; }
        /// <summary>
        /// The identifier of the account system
        /// </summary>
        /// <value></value>
        [DataMember]
        public string ExternalId { get; set; }
        /// <summary>
        /// Accounts connected to this user
        /// </summary>
        /// <value></value>
        [DataMember]
        public List<MinecraftUuid> Accounts { get; set; } = new List<MinecraftUuid>();
    }
}