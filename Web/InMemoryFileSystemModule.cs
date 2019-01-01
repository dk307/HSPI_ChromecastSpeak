using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.Caching;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Unosquare.Labs.EmbedIO;
using Unosquare.Labs.EmbedIO.Constants;
using static System.FormattableString;

namespace Hspi.Web
{
    /// <summary>
    /// Represents a simple module to server static files from the file system.
    /// </summary>
    internal sealed class InMemoryFileSystemModule : WebModuleBase
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="InMemoryFileSystemModule" /> class.
        /// </summary>
        /// <param name="headers">The headers to set in every request.</param>
        /// <exception cref="System.ArgumentException">Path ' + fileSystemPath + ' does not exist.</exception>
        public InMemoryFileSystemModule(Dictionary<string, string> headers = null)
        {
            RamCache = MemoryCache.Default;

            if (headers != null)
            {
                foreach (var header in headers)
                {
                    DefaultHeaders.Add(header.Key, header.Value);
                }
            }

            AddHandler(ModuleMap.AnyPath, HttpVerbs.Head, (context, ct) => HandleGet(context, ct, false));
            AddHandler(ModuleMap.AnyPath, HttpVerbs.Get, (context, ct) => HandleGet(context, ct));
        }

        /// <summary>
        /// Gets the name of this module.
        /// </summary>
        /// <value>
        /// The name.
        /// </value>
        public override string Name => "InMemory Files Module";

        private static CultureInfo StandardHeaderCultureInfo => CultureInfo.CreateSpecificCulture("en-US");

        /// <summary>
        /// Private collection holding the contents of the RAM Cache.
        /// </summary>
        /// <value>
        /// The ram cache.
        /// </value>
        private MemoryCache RamCache { get; }

        public void AddCacheFile(byte[] buffer, DateTimeOffset lastModified, string path, DateTimeOffset expiry)
        {
            RamCache.Add(path,
                         new RamCacheEntry(buffer, lastModified, "\"" + Guid.NewGuid().ToString() + "\""),
                         expiry);
        }

        private static bool CalculateRange(string partialHeader, long fileSize, out int lowerByteIndex,
            out int upperByteIndex)
        {
            lowerByteIndex = 0;
            upperByteIndex = 0;

            var range = partialHeader.Replace("bytes=", "").Split('-');

            if (range.Length == 2 && int.TryParse(range[0], out lowerByteIndex) &&
                int.TryParse(range[1], out upperByteIndex))
            {
                return true;
            }

            if ((range.Length == 2 && int.TryParse(range[0], NumberStyles.Any, CultureInfo.InvariantCulture, out lowerByteIndex) &&
                 string.IsNullOrWhiteSpace(range[1])) ||
                (range.Length == 1 && int.TryParse(range[0], NumberStyles.Any, CultureInfo.InvariantCulture, out lowerByteIndex)))
            {
                upperByteIndex = (int)fileSize - 1;
                return true;
            }

            if (range.Length == 2 && string.IsNullOrWhiteSpace(range[0]) &&
                int.TryParse(range[1], NumberStyles.Any, CultureInfo.InvariantCulture, out upperByteIndex))
            {
                lowerByteIndex = (int)fileSize - upperByteIndex - 1;
                upperByteIndex = (int)fileSize - 1;
                return true;
            }

            return false;
        }

        private static string GetUrlPath(IHttpContext context)
        {
            var urlPath = context.RequestPathCaseSensitive().Replace('/', Path.DirectorySeparatorChar);
            urlPath = urlPath.TrimStart(Path.DirectorySeparatorChar);
            return urlPath;
        }

        private static async Task WriteToOutputMemoryStream(IHttpContext context, long byteLength, byte[] buffer,
            int lowerByteIndex, CancellationToken ct)
        {
            checked
            {
                const int chunkSize = 512 * 1024;

                while (byteLength > 0)
                {
                    int length = Math.Min(chunkSize, (int)byteLength);
                    await context.Response.OutputStream.WriteAsync(buffer, lowerByteIndex, length, ct);
                    byteLength -= length;
                    lowerByteIndex += length;
                }
            }
        }

        private async Task<bool> HandleGet(IHttpContext context, CancellationToken ct, bool sendBuffer = true)
        {
            Trace.WriteLine($"Request Type {context.RequestVerb()} from {context.Request.RemoteEndPoint} for {context.Request.Url}");

            var requestedPath = GetUrlPath(context);
            if (string.IsNullOrEmpty(requestedPath))
            {
                return await SendFileList(context, ct).ConfigureAwait(false);
            }

            var eTagValid = false;
            var partialHeader = context.RequestHeader(Headers.Range);
            var usingPartial = string.IsNullOrWhiteSpace(partialHeader) == false &&
                                partialHeader.StartsWith("bytes=", StringComparison.Ordinal);

            var requestHash = context.RequestHeader(Headers.IfNotMatch);

            CacheItem cacheItem = RamCache.GetCacheItem(requestedPath);

            if (cacheItem == null || cacheItem.Value == null)
            {
                context.Response.StatusCode = (int)HttpStatusCode.NotFound;
                Trace.WriteLine($"Request From {context.Request.RemoteEndPoint} for {context.Request.Url} returned with {context.Response.StatusCode}");
                return true;
            }

            RamCacheEntry cacheEntry = (RamCacheEntry)cacheItem.Value;

            if (string.IsNullOrWhiteSpace(requestHash) || requestHash != cacheEntry.Hash)
            {
                context.Response.AddHeader(Headers.ETag, cacheEntry.Hash);
            }
            else
            {
                eTagValid = true;
            }

            // check to see if the file was modified or e-tag is the same
            var utcFileDateString = cacheEntry.LastModified.ToUniversalTime()
                .ToString(BrowserTimeFormat, StandardHeaderCultureInfo);

            if (usingPartial == false &&
                (eTagValid || context.RequestHeader(Headers.IfModifiedSince).Equals(utcFileDateString)))
            {
                SetStatusCode304(context);
                Trace.WriteLine($"Request From {context.Request.RemoteEndPoint} for {context.Request.Url} returned with {context.Response.StatusCode}");
                return true;
            }

            SetHeaders(context, requestedPath, utcFileDateString);

            var fileSize = cacheEntry.Buffer.Length;

            if (sendBuffer == false)
            {
                context.Response.ContentLength64 = fileSize;
                return true;
            }

            var lowerByteIndex = 0;
            var upperByteIndex = 0;
            long byteLength;
            var isPartial = usingPartial && CalculateRange(partialHeader, fileSize, out lowerByteIndex, out upperByteIndex);

            if (isPartial)
            {
                if (upperByteIndex > (fileSize - 1))
                {
                    context.Response.StatusCode = 416;
                    context.Response.AddHeader(Headers.ContentRanges, Invariant($"bytes */{fileSize}"));
                    Trace.WriteLine($"Request From {context.Request.RemoteEndPoint} for {context.Request.Url} returned with {context.Response.StatusCode}");
                    return true;
                }

                byteLength = upperByteIndex - lowerByteIndex + 1;
                context.Response.AddHeader(Headers.ContentRanges, Invariant($"bytes {lowerByteIndex}-{upperByteIndex}/{fileSize}"));
                if (byteLength != fileSize)
                {
                    context.Response.StatusCode = 206;
                }
            }
            else
            {
                byteLength = fileSize;
            }

            context.Response.ContentLength64 = byteLength;

            Trace.WriteLine($"Serving to {context.Request.RemoteEndPoint} for {context.Request.Url} with bytes {byteLength} at offset {lowerByteIndex}");

            try
            {
                await WriteToOutputMemoryStream(context, byteLength, cacheEntry.Buffer, lowerByteIndex, ct).ConfigureAwait(false);
            }
            catch (HttpListenerException ex)
            {
                Trace.WriteLine($"Request Type {context.RequestVerb()} from {context.Request.RemoteEndPoint} for {context.Request.Url} bytes {byteLength} at offset {lowerByteIndex} failed with {ex.Message}");
                return true;
            }

            Trace.WriteLine($"Finished Serving to {context.Request.RemoteEndPoint} for {context.Request.Url} bytes {byteLength} at offset {lowerByteIndex}");
            return true;
        }

        private async Task<bool> SendFileList(IHttpContext context, CancellationToken ct)
        {
            StringBuilder stb = new StringBuilder();

            stb.AppendLine("<!DOCTYPE html><html><head>" +
                            "<style>");
            stb.AppendLine("table{ border-collapse: collapse;  width: 100 %; }");
            stb.AppendLine("th, td{text-align: left;padding: 8px;}");
            stb.AppendLine("tr:nth-child(even){background-color: #f2f2f2}");
            stb.AppendLine("th{background-color: #808080;color: white}");
            stb.AppendLine("</style >" +
                           "</head ><body>");
            stb.AppendLine(@"<table>");
            stb.AppendLine(@"<tr><th>File</th><th>Size</th><th>Last Modified</th></tr>");

            foreach (var file in RamCache.OrderBy((key) => ((RamCacheEntry)key.Value).LastModified))
            {
                RamCacheEntry entry = (RamCacheEntry)file.Value;
                stb.AppendLine(Invariant($"<tr><td><a href=\"{WebUtility.HtmlEncode(file.Key)}\">{file.Key}</a></td>"));
                stb.AppendLine(Invariant($"<td>{entry.Buffer.Length} bytes</td></td><td>{entry.LastModified}</td></tr>"));
            }
            stb.AppendLine(@"</table>");
            stb.AppendLine(@"</body></html>");

            return await context.HtmlResponseAsync(stb.ToString(), cancellationToken: ct).ConfigureAwait(false);
        }

        private void SetHeaders(IHttpContext context, string localPath, string utcFileDateString)
        {
            var fileExtension = Path.GetExtension(localPath);

            var mimeTypes = MimeTypes.DefaultMimeTypes.Value;
            if (mimeTypes.ContainsKey(fileExtension))
                context.Response.ContentType = mimeTypes[fileExtension];

            context.Response.AddHeader(Headers.CacheControl,
                DefaultHeaders.ContainsKey(Headers.CacheControl)
                    ? DefaultHeaders[Headers.CacheControl]
                    : "private");

            context.Response.AddHeader(Headers.Pragma,
                DefaultHeaders.ContainsKey(Headers.Pragma)
                    ? DefaultHeaders[Headers.Pragma]
                    : string.Empty);

            context.Response.AddHeader(Headers.Expires,
                DefaultHeaders.ContainsKey(Headers.Expires)
                    ? DefaultHeaders[Headers.Expires]
                    : string.Empty);

            context.Response.AddHeader(Headers.LastModified, utcFileDateString);
            context.Response.AddHeader(Headers.AcceptRanges, "bytes");
        }

        private void SetStatusCode304(IHttpContext context)
        {
            context.Response.AddHeader(Headers.CacheControl,
                DefaultHeaders.ContainsKey(Headers.CacheControl)
                    ? DefaultHeaders[Headers.CacheControl]
                    : "private");

            context.Response.AddHeader(Headers.Pragma,
                DefaultHeaders.ContainsKey(Headers.Pragma)
                    ? DefaultHeaders[Headers.Pragma]
                    : string.Empty);

            context.Response.AddHeader(Headers.Expires,
                DefaultHeaders.ContainsKey(Headers.Expires)
                    ? DefaultHeaders[Headers.Expires]
                    : string.Empty);

            context.Response.ContentType = string.Empty;
            context.Response.StatusCode = 304;
        }

        /// <summary>
        /// Represents a RAM Cache dictionary entry
        /// </summary>
        private class RamCacheEntry
        {
            public RamCacheEntry(byte[] buffer, DateTimeOffset lastModified, string hash)
            {
                this.Buffer = buffer;
                this.LastModified = lastModified;
                Hash = hash;
            }

            public byte[] Buffer { get; }
            public string Hash { get; }
            public DateTimeOffset LastModified { get; }
        }

        /// <summary>
        /// The default headers
        /// </summary>
        public readonly Dictionary<string, string> DefaultHeaders = new Dictionary<string, string>();

        private const string BrowserTimeFormat = "ddd, dd MMM yyyy HH:mm:ss 'GMT'";
    }
}