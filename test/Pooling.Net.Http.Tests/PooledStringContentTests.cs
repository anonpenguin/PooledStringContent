using Pooling.Net.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Xunit;

namespace Pooling.Net.Http.Tests
{
    public class PooledStringContentTests
    {
        [Fact]
        public void NullContentShouldThrow()
        {
            Assert.Throws<ArgumentNullException>("content", () => new PooledStringContent(null));
        }

        [Theory]
        [MemberData(nameof(RoundTripData))]
        public void RoundTrip(string contentString, Encoding encoding)
        {
            var content = new PooledStringContent(contentString, encoding);
            var bytes = content.ReadAsByteArrayAsync().GetAwaiter().GetResult();
            Assert.Equal(contentString, (encoding ?? Encoding.UTF8).GetString(bytes));
        }

        [Theory]
        [MemberData(nameof(EncodingData))]
        public void ShouldSetCharSet(Encoding encoding)
        {
            var content = new PooledStringContent("", encoding);
            Assert.Equal(encoding?.WebName ?? "utf-8", content.Headers.ContentType.CharSet);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("text/plain")]
        [InlineData("some/other-media-type")]
        public void ShouldSetMediaType(string value)
        {
            var content = new PooledStringContent("", null, mediaType: value);
            Assert.Equal(value ?? "text/plain", content.Headers.ContentType.MediaType);
        }

        [Fact]
        public void DoubleDispose()
        {
            using (var content = new PooledStringContent(""))
            {
                content.Dispose();
            }
        }

        [Fact]
        public void UnusableAfterDispose()
        {
            var content = new PooledStringContent("");
            content.Dispose();
            Assert.ThrowsAsync<ObjectDisposedException>(() => content.ReadAsStreamAsync());
        }

        public static IEnumerable<object[]> RoundTripData()
        {
            yield return new object[] { "test text", null }; // null should be treated as UTF8
            yield return new object[] { "abc123", Encoding.UTF8 };
            yield return new object[] { "\0\0\0\0\0\ud555", Encoding.UTF8 };
            yield return new object[] { new string('4', 4000), Encoding.BigEndianUnicode };
            yield return new object[] { Enumerable.Repeat("\u00a9 Hello, world!", 100).Aggregate((l, r) => l + r), Encoding.Unicode };
        }

        public static IEnumerable<object[]> EncodingData()
        {
            yield return new object[] { null };
            yield return new object[] { Encoding.UTF8 };
            yield return new object[] { Encoding.BigEndianUnicode };
            yield return new object[] { Encoding.Unicode };
            yield return new object[] { Encoding.ASCII };
        }
    }
}
