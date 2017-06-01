using System;

namespace Orleans.Streams
{
    public interface IStreamIdentity
    {
        /// <summary> Stream primary key. </summary>
        byte[] Key { get; }

        /// <summary> Stream namespace. </summary>
        string Namespace { get; }
    }
}
