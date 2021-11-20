using System.Threading.Tasks;
using Coflnet.Sky.Filter;
using hypixel;

namespace Coflnet.Sky.Commands
{
    public interface IFlipConnection
    {
        /// <summary>
        /// Tries to send a flip, returns false if the connection can no longer send flips
        /// </summary>
        /// <param name="flip"></param>
        /// <returns></returns>
        Task<bool> SendFlip(hypixel.FlipInstance flip);
        Task<bool> SendFlip(LowPricedAuction flip);
        Task<bool> SendSold(string uuid);
        FlipSettings Settings { get; }
        long Id { get; }
        int UserId { get; }

        void UpdateSettings(SettingsChange settings);
    }
}