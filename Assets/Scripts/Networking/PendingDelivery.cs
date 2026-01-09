using System;

namespace Networking.Reliability
{
    public class PendingDelivery
    {
        public ushort sequence;
        public byte[] payload;
        public float timeSent;

        public Action OnSuccess;
        public Action OnFailure;
    }
}
