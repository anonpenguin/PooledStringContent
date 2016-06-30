using System;
using System.Buffers;
using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;

namespace Pooling.Net.Http
{
    // This is essentially a copy of the StringContent
    // sources in CoreFX, except modified to use ArrayPool
    // instead of Encoding.GetBytes to avoid allocations

    public class PooledStringContent : ByteArrayContent
    {
        private const string DefaultMediaType = "text/plain";

        private byte[] _rented;

        public PooledStringContent(string content)
            : this(content, null, null)
        {
        }

        public PooledStringContent(string content, Encoding encoding)
            : this(content, encoding, null)
        {
        }

        public PooledStringContent(string content, Encoding encoding, string mediaType)
            : this(RentContentBuffer(content, encoding), encoding, mediaType)
        {
        }

        private PooledStringContent(ArraySegment<byte> buffer, Encoding encoding, string mediaType)
            : base(buffer.Array, buffer.Offset, buffer.Count)
        {
            Debug.Assert(buffer.Array != null);
            _rented = buffer.Array;

            // Initialize the 'Content-Type' header with information provided by parameters.
            var headerValue = new MediaTypeHeaderValue(mediaType ?? DefaultMediaType);
            headerValue.CharSet = (encoding ?? Encoding.UTF8).WebName;

            Headers.ContentType = headerValue;
        }

        protected override void Dispose(bool disposing)
        {
            try
            {
                if (disposing)
                {
                    if (_rented != null)
                    {
                        ArrayPool<byte>.Shared.Return(_rented);
                        _rented = null;
                    }
                }
            }
            finally
            {
                base.Dispose(disposing);
            }
        }

        private static ArraySegment<byte> RentContentBuffer(string content, Encoding encoding)
        {
            if (content == null)
            {
                throw new ArgumentNullException(nameof(content));
            }

            encoding = encoding ?? Encoding.UTF8;

            int byteCount = encoding.GetByteCount(content);
            var rented = ArrayPool<byte>.Shared.Rent(byteCount);

            try
            {
                int written = encoding.GetBytes(content, 0, content.Length, rented, 0);
                Debug.Assert(byteCount == written);

                return new ArraySegment<byte>(rented, 0, written);
            }
            catch
            {
                // If something goes wrong return the array and rethrow
                ArrayPool<byte>.Shared.Return(rented);
                throw;
            }
        }
    }
}
