namespace Networking.Reliability
{
    public struct ReliableHeader
    {
        public ushort sequence;
        public ushort ack;

        public const int Size = 4;
    }
}
