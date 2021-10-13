using hypixel;

namespace Coflnet.Sky.Commands.MC
{
    public interface IModVersionAdapter
    {
        bool SendFlip(FlipInstance flip);
        void SendSound(string name, float pitch = 1);
        void SendMessage(params ChatPart[] parts);
    }
}