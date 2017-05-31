using System;
using System.Collections;
using System.Globalization;
using System.Linq;
using System.IO;
using System.Text;
using Orleans.Serialization;

namespace Orleans.Runtime
{
    [Serializable]
    internal class UniqueKey : IComparable<UniqueKey>, IEquatable<UniqueKey>
    {
        private const ulong TYPE_CODE_DATA_MASK = 0xFFFFFFFF; // Lowest 4 bytes
        private static readonly char[] KeyExtSeparationChar = {'+'};

        /// <summary>
        /// Type id values encoded into UniqueKeys
        /// </summary>
        public enum Category : byte
        {
            None = 0,
            SystemTarget = 1,
            SystemGrain = 2,
            Grain = 3,
            Client = 4,
            KeyExtGrain = 6,
            GeoClient = 7,
        }

        public byte[] KeyBytes { get; private set; }
        public UInt64 N0 { get; private set; }
        public UInt64 N1 { get; private set; }
        public UInt64 TypeCodeData { get; private set; }
        public string KeyExt { get; private set; }

        [NonSerialized]
        private uint uniformHashCache;

        public int BaseTypeCode
        {
            get { return (int)(TypeCodeData & TYPE_CODE_DATA_MASK); }
        }

        public Category IdCategory
        {
            get { return GetCategory(TypeCodeData); }
        }

        public bool IsLongKey
        {
            get { return KeyBytes.Length == 16 && N0 == 0; }
        }

        public bool IsSystemTargetKey
        {
            get { return IdCategory == Category.SystemTarget; }
        }

        public bool IsSystemGrainKey
        {
            get { return IdCategory == Category.SystemGrain; }
        }

        public bool HasKeyExt
        {
            get {
                var category = IdCategory;
                return category == Category.KeyExtGrain       
                    || category == Category.GeoClient; // geo clients use the KeyExt string to specify the cluster id
            }
        }

        internal static readonly UniqueKey Empty =
            new UniqueKey
            {
                N0 = 0,
                N1 = 0,
                TypeCodeData = 0,
                KeyBytes = null,
                KeyExt = null
            };

        internal static UniqueKey Parse(string input)
        {
            var trimmed = input.Trim();

            // first, for convenience we attempt to parse the string using GUID syntax. this is needed by unit
            // tests but i don't know if it's needed for production.
            Guid guid;
            if (Guid.TryParse(trimmed, out guid))
                return NewKey(guid);
            else
            {
                var fields = trimmed.Split(KeyExtSeparationChar, 2);
                var n0 = ulong.Parse(fields[0].Substring(0, 16), NumberStyles.HexNumber);
                var n1 = ulong.Parse(fields[0].Substring(16, 16), NumberStyles.HexNumber);
                var bytes = ParseHexString(fields[0]);
                var typeCodeData = ulong.Parse(fields[0].Substring(32, 16), NumberStyles.HexNumber);
                string keyExt = null;
                switch (fields.Length)
                {
                    default:
                        throw new InvalidDataException("UniqueKey hex strings cannot contain more than one + separator.");
                    case 1:
                        break;
                    case 2:
                        if (fields[1] != "null")
                        {
                            keyExt = fields[1];
                        }
                        break;
                }
                return NewKey(n0, n1, bytes, typeCodeData, keyExt);
            }
        }

        // Fast hex parsing provided by CainKellye on StackOverflow:
        // https://stackoverflow.com/questions/321370/how-can-i-convert-a-hex-string-to-a-byte-array
        private static byte[] ParseHexString(string hex)
        {
            if (hex.Length % 2 != 0)
                throw new ArgumentException("Hex string must have an even number of hexits.");

            byte[] bytes = new byte[hex.Length / 2];

            for (int i = 0; i < hex.Length / 2; ++i)
            {
                bytes[i] = (byte)((GetHexVal(hex[i << 1]) << 4) + (GetHexVal(hex[(i << 1) + 1])));
            }

            return bytes;
        }

        private static int GetHexVal(char hex) {
            int val = (int)hex;
            return val - (val < 58 ? 48 : (val < 97 ? 55 : 87));
        }

        private static UniqueKey NewKey(ulong n0, ulong n1, byte[] bytes, Category category, long typeData, string keyExt)
        {
            if (category != Category.KeyExtGrain && category != Category.GeoClient && keyExt != null)
                throw new ArgumentException("Only key extended grains can specify a non-null key extension.");

            var typeCodeData = ((ulong)category << 56) + ((ulong)typeData & 0x00FFFFFFFFFFFFFF);

            return NewKey(n0, n1, bytes, typeCodeData, keyExt);
        }

        internal static UniqueKey NewKey(long longKey, Category category = Category.None, long typeData = 0, string keyExt = null)
        {
            ThrowIfIsSystemTargetKey(category);

            var n1 = unchecked((ulong)longKey);
            var n1Bytes = BitConverter.GetBytes(n1);

            var bytes = new byte[16];
            Array.Copy(n1Bytes, 0, bytes, 8, 8);
            
            return NewKey(0, n1, bytes, category, typeData, keyExt);
        }

        public static UniqueKey NewKey()
        {
            return NewKey(Guid.NewGuid());
        }

        internal static UniqueKey NewKey(Guid guid, Category category = Category.None, long typeData = 0, string keyExt = null)
        {
            ThrowIfIsSystemTargetKey(category);

            var guidBytes = guid.ToByteArray();
            var n0 = BitConverter.ToUInt64(guidBytes, 0);
            var n1 = BitConverter.ToUInt64(guidBytes, 8);
            return NewKey(n0, n1, guidBytes, category, typeData, keyExt);
        }

        internal static UniqueKey NewKey(byte[] bytes, Category category = Category.None, long typeData = 0)
        {
            ThrowIfIsSystemTargetKey(category);

            var nBytes = new byte[16];
            Array.Copy(bytes, 0, nBytes, 0, Math.Min(bytes.Length, 16));
            var n0 = BitConverter.ToUInt64(nBytes, 0);
            var n1 = BitConverter.ToUInt64(nBytes, 8);

            return NewKey(n0, n1, bytes, category, typeData, null);
        }

        public static UniqueKey NewSystemTargetKey(Guid guid, long typeData)
        {
            var guidBytes = guid.ToByteArray();
            var n0 = BitConverter.ToUInt64(guidBytes, 0);
            var n1 = BitConverter.ToUInt64(guidBytes, 8);
            return NewKey(n0, n1, guidBytes, Category.SystemTarget, typeData, null);
        }

        public static UniqueKey NewSystemTargetKey(short systemId)
        {
            var n1 = unchecked((ulong)systemId);
            var n1Bytes = BitConverter.GetBytes(n1);
            return NewKey(0, n1, n1Bytes, Category.SystemTarget, 0, null);
        }

        public static UniqueKey NewGrainServiceKey(short key, long typeData)
        {
            var n1 = unchecked((ulong)key);
            var n1Bytes = BitConverter.GetBytes(n1);
            return NewKey(0, n1, n1Bytes, Category.SystemTarget, typeData, null);
        }

        internal static UniqueKey NewKey(ulong n0, ulong n1, byte[] bytes, ulong typeCodeData, string keyExt)
        {
            ValidateKeyExt(keyExt, typeCodeData);
            return
                new UniqueKey
                {
                    N0 = n0,
                    N1 = n1,
                    KeyBytes = bytes,
                    TypeCodeData = typeCodeData,
                    KeyExt = keyExt
                };
        }

        private void ThrowIfIsNotLong()
        {
            if (!IsLongKey)
                throw new InvalidOperationException("this key cannot be interpreted as a long value");
        }

        private static void ThrowIfIsSystemTargetKey(Category category)
        {
            if (category == Category.SystemTarget)
                throw new ArgumentException(
                    "This overload of NewKey cannot be used to construct an instance of UniqueKey containing a SystemTarget id.");
        }

        private void ThrowIfHasKeyExt(string methodName)
        {
            if (HasKeyExt)
                throw new InvalidOperationException(
                    string.Format(
                        "This overload of {0} cannot be used if the grain uses the primary key extension feature.",
                        methodName));
        }

        private void ThrowIfKeyBytesTooLong(string methodName, int maxLength)
        {
            if (KeyBytes.Length > maxLength)
                throw new InvalidOperationException(
                    string.Format(
                        "This overload of {0} cannot be used if the grain uses a primary key whose data does not "
                        + "fit within the size constraints of the return type.",
                        methodName));
        }

        public long PrimaryKeyToLong(out string extendedKey)
        {
            ThrowIfIsNotLong();
            ThrowIfKeyBytesTooLong("UniqueKey.PrimaryKeyToLong", 8);

            extendedKey = this.KeyExt;
            return unchecked((long)N1);
        }

        public long PrimaryKeyToLong()
        {
            ThrowIfHasKeyExt("UniqueKey.PrimaryKeyToLong");
            string unused;
            return PrimaryKeyToLong(out unused);
        }

        public Guid PrimaryKeyToGuid(out string extendedKey)
        {
            ThrowIfKeyBytesTooLong("UniqueKey.PrimaryKeyToGuid", 16);
            extendedKey = this.KeyExt;
            return ConvertToGuid();
        }

        public Guid PrimaryKeyToGuid()
        {
            ThrowIfHasKeyExt("UniqueKey.PrimaryKeyToGuid");
            string unused;
            return PrimaryKeyToGuid(out unused);
        }

        public string ClusterId
        {
            get
            {
                if (IdCategory != Category.GeoClient)
                    throw new InvalidOperationException("ClusterId is only defined for geo clients");
                return this.KeyExt;
            }
        }

        public override bool Equals(object o)
        {
            return o is UniqueKey && Equals((UniqueKey)o);
        }

        // We really want Equals to be as fast as possible, as a minimum cost, as close to native as possible.
        // No function calls, no boxing, inline.
        public bool Equals(UniqueKey other)
        {
            return TypeCodeData == other.TypeCodeData
                   && (!HasKeyExt || KeyExt == other.KeyExt)
                   && KeyBytes.SequenceEqual(other.KeyBytes);
        }

        // We really want CompareTo to be as fast as possible, as a minimum cost, as close to native as possible.
        // No function calls, no boxing, inline.
        public int CompareTo(UniqueKey other)
        {
            var bytesCmp = KeyBytes.Length < other.KeyBytes.Length ? -1
                         : KeyBytes.Length > other.KeyBytes.Length ? 1
                         : StructuralComparisons.StructuralComparer.Compare(KeyBytes, other.KeyBytes);

            return TypeCodeData < other.TypeCodeData ? -1
               : TypeCodeData > other.TypeCodeData ? 1
               : !HasKeyExt || KeyExt == null ? 0
               : bytesCmp != 0 ? bytesCmp
               : String.Compare(KeyExt, other.KeyExt, StringComparison.Ordinal);
        }

        public override int GetHashCode()
        {
            return unchecked((int)GetUniformHashCode());
        }

        internal uint GetUniformHashCode()
        {
            // Disabling this ReSharper warning; hashCache is a logically read-only variable, so accessing them in GetHashCode is safe.
            // ReSharper disable NonReadonlyFieldInGetHashCode
            if (uniformHashCache == 0)
            {
                uint n;
                if (HasKeyExt && KeyExt != null)
                {
                    var writer = new BinaryTokenStreamWriter();
                    writer.Write(this);
                    byte[] bytes = writer.ToByteArray();
                    writer.ReleaseBuffers();
                    n = JenkinsHash.ComputeHash(bytes);
                }
                else
                {
                    n = JenkinsHash.ComputeHash(TypeCodeData, N0, N1);
                }
                // Unchecked is required because the Jenkins hash is an unsigned 32-bit integer, 
                // which we need to convert to a signed 32-bit integer.
                uniformHashCache = n;
            }
            return uniformHashCache;
            // ReSharper restore NonReadonlyFieldInGetHashCode
        }

        private Guid ConvertToGuid()
        {
            return new Guid((UInt32)(N0 & 0xffffffff), (UInt16)(N0 >> 32), (UInt16)(N0 >> 48), (byte)N1, (byte)(N1 >> 8), (byte)(N1 >> 16), (byte)(N1 >> 24), (byte)(N1 >> 32), (byte)(N1 >> 40), (byte)(N1 >> 48), (byte)(N1 >> 56));
        }

        public override string ToString()
        {
            return ToHexString();
        }

        internal string ToHexString()
        {
            var s = new StringBuilder();

            for (var i = 0; i < KeyBytes.Length; i++)
                s.AppendFormat("{0:x16}", KeyBytes[i]);

            s.AppendFormat("{0:x16}", TypeCodeData);
            if (!HasKeyExt) return s.ToString();

            s.Append("+");
            s.Append(KeyExt ?? "null");
            return s.ToString();
        }

        private static void ValidateKeyExt(string keyExt, UInt64 typeCodeData)
        {
            Category category = GetCategory(typeCodeData);
            if (category == Category.KeyExtGrain)
            {
                if (string.IsNullOrWhiteSpace(keyExt))
                {
                    if (null == keyExt)
                    {
                        throw new ArgumentNullException("keyExt");

                    }
                    else
                    {
                        throw new ArgumentException("Extended key is empty or white space.", "keyExt");
                    }
                }
            }
            else if (category != Category.GeoClient && null != keyExt)
            {
                throw new ArgumentException("Extended key field is not null in non-extended UniqueIdentifier.");
            }
        }

        private static Category GetCategory(UInt64 typeCodeData)
        {
            return (Category)((typeCodeData >> 56) & 0xFF);
        }
    }
}
