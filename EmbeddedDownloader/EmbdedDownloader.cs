using System.Diagnostics;
using System.Net.Sockets;
using System.Text;
using CommonCore;

namespace EmbeddedDownloader;

public static class EmbeddedDownloader {
    private static void ReceiveCallback(IAsyncResult result) {
        var state = result.AsyncState as ReceiveState;
        Debug.Assert(state != null);

        try {
            var bytesReceived = state.Socket.EndReceive(result);
            if (bytesReceived > 0) {
                state.Response.Append(Encoding.ASCII.GetString(state.Buffer, 0, bytesReceived));
                state.Socket.BeginReceive(state.Buffer, 0, state.Buffer.Length, SocketFlags.None, ReceiveCallback,
                                          state);
            }
            else {
                var content = Commons.ParseHttpResponse(state.Response.ToString());
                var fileName = Commons.UriToFilename(state.Uri.AbsolutePath);
                File.WriteAllText(fileName, content);
                Console.WriteLine($"Downloaded content from {state.Url} into {fileName}");

                state.Socket.Close();
            }
        }
        catch (Exception ex) {
            Console.WriteLine($"Error receiving data from {state.Url}: {ex.Message}");
            state.Socket.Close();
        }
    }

    private static void SendCallback(IAsyncResult result) {
        var state = result.AsyncState as ConnectState;
        Debug.Assert(state != null);

        try {
            state.Socket.EndSend(result);

            var buffer = new byte[0x4000];
            var response = new StringBuilder();
            state.Socket.BeginReceive(buffer, 0, buffer.Length, SocketFlags.None, ReceiveCallback,
                                      new ReceiveState(state.Socket, state.Uri, state.Url, response, buffer));
        }
        catch (Exception ex) {
            Console.WriteLine($"Error sending data to {state.Url}: {ex.Message}");
            state.Socket.Close();
        }
    }

    private static void ConnectCallback(IAsyncResult result) {
        var state = result.AsyncState as ConnectState;
        Debug.Assert(state != null);

        try {
            state.Socket.EndConnect(result);
            state.Socket.BeginSend(state.RequestBytes, 0, state.RequestBytes.Length,
                                   SocketFlags.None, SendCallback, state);
        }
        catch (Exception ex) {
            Console.WriteLine($"Error connecting to {state.Url}: {ex.Message}");
            state.Socket.Close();
        }
    }

    private static void DownloadFiles() {
        foreach (var url in Commons.Urls) {
            var uri = new Uri(url);
            var port = Commons.GetHttpPort(uri);
            var requestHeaders = Commons.GetHttpRequest(uri);
            var requestBytes = Encoding.ASCII.GetBytes(requestHeaders);

            var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            socket.BeginConnect(uri.Host, port, ConnectCallback, new ConnectState(socket, uri, url, requestBytes));
        }

        Console.ReadLine();
    }

    public static void Main() => DownloadFiles();
}