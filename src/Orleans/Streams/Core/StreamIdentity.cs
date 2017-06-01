
using System;

namespace Orleans.Streams
{
    /// <summary>
    /// Stream identity contains the public stream information use to uniquely identify a stream.
    /// Stream identities are only unique per stream provider.
    /// </summary>
    [Serializable]
    public class StreamIdentity : IStreamIdentity
    {
        public StreamIdentity(Guid streamGuid, string streamNamespace)
            : this(streamGuid.ToByteArray(), streamNamespace)
        {
        }

        public StreamIdentity(byte[] key, string streamNamespace)
        {
            Key = key;
            Namespace = streamNamespace;
        }

        /// <summary>
        /// Stream primary key guid.
        /// </summary>
        public byte[] Key { get; }

        /// <summary>
        /// Stream namespace.
        /// </summary>
        public string Namespace { get; }
    }
}
