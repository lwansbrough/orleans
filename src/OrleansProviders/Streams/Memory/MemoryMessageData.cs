﻿
using System;

namespace Orleans.Providers
{
    /// <summary>
    /// Represents the event sent and received from an In-Memory queue grain. 
    /// </summary>
    [Serializable]
    public struct MemoryMessageData
    {
        /// <summary>
        /// Stream key of the event data.
        /// </summary>
        public byte[] StreamKey;
        /// <summary>
        /// Stream namespace.
        /// </summary>
        public string StreamNamespace;
        /// <summary>
        /// Position of even in stream.
        /// </summary>
        public long SequenceNumber;
        /// <summary>
        /// Serialized event data.
        /// </summary>
        public ArraySegment<byte> Payload;

        internal static MemoryMessageData Create(byte[] streamKey, String streamNamespace, ArraySegment<byte> arraySegment)
        {
            return new MemoryMessageData
            {
                StreamKey = streamKey,
                StreamNamespace = streamNamespace,
                Payload = arraySegment
            };
        }

        internal static MemoryMessageData Create(Guid streamGuid, String streamNamespace, ArraySegment<byte> arraySegment)
        {
            return Create(streamGuid.ToByteArray(), streamNamespace, arraySegment);
        }
    }
}
