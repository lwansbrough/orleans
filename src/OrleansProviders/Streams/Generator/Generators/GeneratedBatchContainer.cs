
using System;
using System.Collections.Generic;
using Orleans.Providers.Streams.Common;
using Orleans.Streams;

namespace Orleans.Providers.Streams.Generator
{
    internal class GeneratedBatchContainer : IBatchContainer
    {
        public byte[] StreamKey { get; }
        public string StreamNamespace { get; }
        public StreamSequenceToken SequenceToken => RealToken;
        public EventSequenceTokenV2 RealToken { get;  }
        public object Payload { get; }

        public GeneratedBatchContainer(byte[] streamKey, string streamNamespace, object payload, EventSequenceTokenV2 token)
        {
            StreamKey = streamKey;
            StreamNamespace = streamNamespace;
            this.Payload = payload;
            this.RealToken = token;
        }

        public GeneratedBatchContainer(Guid streamGuid, string streamNamespace, object payload, EventSequenceTokenV2 token)
            : this(streamGuid.ToByteArray(), streamNamespace, payload, token)
        {
        }

        public IEnumerable<Tuple<T, StreamSequenceToken>> GetEvents<T>()
        {
            return new[] { Tuple.Create((T)Payload, SequenceToken) };
        }

        public bool ImportRequestContext()
        {
            return false;
        }

        public bool ShouldDeliver(IStreamIdentity stream, object filterData, StreamFilterPredicate shouldReceiveFunc)
        {
            return true;
        }
    }
}
