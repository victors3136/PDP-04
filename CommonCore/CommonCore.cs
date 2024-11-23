using System.Net.Sockets;
using System.Text;

namespace CommonCore;

public record ConnectState(Socket Socket, Uri Uri, string Url, byte[] RequestBytes);

public record ReceiveState(Socket Socket, Uri Uri, string Url, StringBuilder Response, byte[] Buffer);

public static class Commons {
    public static readonly string[] Urls = [
                                               "http://example.com/file1.txt",
                                               "http://example.com/file2.txt",
                                               "http://example.com/file3.txt"
                                           ];

    public static string ParseHttpResponse(string response) {
        const string headersEnd = "\r\n\r\n";
        var headerEndIndex = response.IndexOf(headersEnd, StringComparison.Ordinal);
        return response[(headerEndIndex + headersEnd.Length)..];
    }

    public static string UriToFilename(string uri) => uri.TrimStart('/').Replace("/", "_").Replace("~", "");
    public static int GetHttpPort(Uri uri) => uri.Port == -1 ? 80 : uri.Port;

    public static string GetHttpRequest(Uri uri) =>
        $"GET {uri.AbsolutePath} HTTP/1.1\r\nHost: {uri.Host}\r\nConnection: close\r\n\r\n";
}