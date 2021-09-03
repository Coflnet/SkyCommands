namespace Coflnet.Sky.Commands
{
    public interface IFlipConection
    {
        bool SendFlip(hypixel.FlipInstance flip);
        bool SendSold(string uuid);
        long Id { get; }
    }
}