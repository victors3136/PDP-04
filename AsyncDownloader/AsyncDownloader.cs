using System.Net.Sockets;
using System.Text;
using CommonCore;

namespace AsyncDownloader;

public static class AsyncDownloader {
    private static Task ConnectAsync(Socket socket, string host, int port) {
        var taskResult = new TaskCompletionSource<bool>();

        socket.BeginConnect(host, port, ConnectCallback, null);

        return taskResult.Task;

        void ConnectCallback(IAsyncResult ar) {
            try {
                socket.EndConnect(ar);
                taskResult.SetResult(true);
            }
            catch (Exception ex) {
                taskResult.SetException(new Exception($"Error connecting to {host}:{port}: {ex.Message}"));
            }
        }
    }

    private static Task SendAsync(Socket socket, byte[] requestBytes) {
        var taskResult = new TaskCompletionSource<bool>();

        socket.BeginSend(requestBytes, 0, requestBytes.Length, SocketFlags.None, SendCallback, null);

        return taskResult.Task;

        void SendCallback(IAsyncResult ar) {
            try {
                socket.EndSend(ar);
                taskResult.SetResult(true);
            }
            catch (Exception ex) {
                taskResult.SetException(new Exception("Error sending data: " + ex.Message));
            }
        }
    }

    private static Task<string> ReceiveAsync(Socket socket, Uri uri, string url) {
        var taskResult = new TaskCompletionSource<string>();
        var response = new StringBuilder();
        var buffer = new byte[0x4000];

        socket.BeginReceive(buffer, 0, buffer.Length, SocketFlags.None, ReceiveCallback, null);
        return taskResult.Task;

        void ReceiveCallback(IAsyncResult ar) {
            try {
                var bytesRead = socket.EndReceive(ar);
                if (bytesRead > 0) {
                    response.Append(Encoding.ASCII.GetString(buffer, 0, bytesRead));
                    socket.BeginReceive(buffer, 0, buffer.Length, SocketFlags.None, ReceiveCallback, null);
                }
                else {
                    var content = Commons.ParseHttpResponse(response.ToString());
                    var fileName = Commons.UriToFilename(uri.AbsolutePath);
                    File.WriteAllText(fileName, content);
                    Console.WriteLine($"Downloaded content from {url} into {fileName}");

                    taskResult.SetResult(content);
                    socket.Close();
                }
            }
            catch (Exception ex) {
                taskResult.SetException(new Exception($"Error receiving data from {url}: {ex.Message}"));
                socket.Close();
            }
        }
    }

    private static async Task DownloadFileAsync(string url) {
        var uri = new Uri(url);
        var host = uri.Host;
        var port = Commons.GetHttpPort(uri);
        var requestHeaders = Commons.GetHttpRequest(uri);
        var requestBytes = Encoding.ASCII.GetBytes(requestHeaders);

        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

        try {
            await ConnectAsync(socket, host, port);
            await SendAsync(socket, requestBytes);
            await ReceiveAsync(socket, uri, url);
        }
        catch (Exception ex) {
            Console.WriteLine($"Failed to download {url}: {ex.Message}");
        }
    }

    private static async Task DownloadFilesAsync() {
        var tasks = Commons.Urls.Select(DownloadFileAsync);
        await Task.WhenAll(tasks);
        Console.WriteLine("All downloads completed successfully.");
    }

    public static async Task Main() => await DownloadFilesAsync();
}