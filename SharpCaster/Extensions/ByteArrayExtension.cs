using NullGuard;
using ProtoBuf;
using SharpCaster.Models;
using System;
using System.IO;
using System.Linq;

namespace SharpCaster.Extensions
{
    [NullGuard(ValidationFlags.Arguments | ValidationFlags.NonPublic)]
    internal static class ByteArrayExtension
    {
        public static byte[] AddHeader(this byte[] array)
        {
            var header = BitConverter.GetBytes((uint)array.Length);
            var dataToSend = header.Reverse().ToList();
            dataToSend.AddRange(array.ToList());
            return dataToSend.ToArray();
        }

        public static CastMessage ToCastMessage(this byte[] array)
        {
            using (var bufStream = new MemoryStream())
            {
                bufStream.Write(array, 0, array.Length);
                bufStream.Position = 0;
                var msg = Serializer.Deserialize<CastMessage>(bufStream);
                return msg;
            }
        }
    }
}