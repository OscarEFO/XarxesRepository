using System.Collections.Generic;
using UnityEngine;

namespace Networking.Reliability
{
    public class ReliabilityManager
    {
        private ushort localSequence = 0;
        private ushort lastRemoteAck = 0;

        private readonly Dictionary<ushort, PendingDelivery> pending =
            new Dictionary<ushort, PendingDelivery>();

        public ushort NextSequence() => localSequence++;

        public ushort LastAck => lastRemoteAck;

        public void RegisterSend(
            ushort seq,
            byte[] payload,
            System.Action onSuccess = null,
            System.Action onFailure = null)
        {
            pending[seq] = new PendingDelivery
            {
                sequence = seq,
                payload = payload,
                timeSent = Time.time,
                OnSuccess = onSuccess,
                OnFailure = onFailure
            };
        }

        public void ProcessAck(ushort ack)
        {
            lastRemoteAck = ack;

            if (pending.TryGetValue(ack, out var delivery))
            {
                delivery.OnSuccess?.Invoke();
                pending.Remove(ack);
            }
        }
    }
}

