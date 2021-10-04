using System.Runtime.Serialization;

namespace Coflnet.Sky.Filter
{
    [DataContract]
    public class ModSettings
    {
        /// <summary>
        /// Display only the profit instead of cost and median
        /// </summary>
        [DataMember(Name = "justProfit")]
        public bool DisplayJustProfit;
        /// <summary>
        /// Play a sound when a flip message is sent
        /// </summary>
        [DataMember(Name = "soundOnFlip")]
        public bool PlaySoundOnFlip;
    }
}