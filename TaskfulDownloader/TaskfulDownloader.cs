using System.Diagnostics;
using System.Net.Sockets;
using System.Text;
using CommonCore;

namespace TaskfulDownloader;

public static class TaskfulDownloader {
    private static Task<string> ReceiveAsync(Socket socket, ReceiveState state) {
        var taskResult = new TaskCompletionSource<string>();

        socket.BeginReceive(state.Buffer, 0, state.Buffer.Length, SocketFlags.None, ReceiveCallback, null);
        return taskResult.Task;

        void ReceiveCallback(IAsyncResult result) {
            try {
                var bytesReceived = socket.EndReceive(result);
                if (bytesReceived > 0) {
                    state.Response.Append(Encoding.ASCII.GetString(state.Buffer, 0, bytesReceived));
                    socket.BeginReceive(state.Buffer, 0, state.Buffer.Length, SocketFlags.None, ReceiveCallback,
                                        null);
                }
                else {
                    var content = Commons.ParseHttpResponse(state.Response.ToString());
                    var fileName = Commons.UriToFilename(state.Uri.AbsolutePath);
                    File.WriteAllText(fileName, content);
                    Console.WriteLine($"Downloaded content from {state.Url} into {fileName}");

                    socket.Close();
                    taskResult.SetResult(content);
                }
            }
            catch (Exception ex) {
                taskResult.SetException(new Exception($"Error receiving data from {state.Url}: {ex.Message}"));
                socket.Close();
            }
        }
    }

    private static Task<string> SendAsync(Socket socket, ConnectState state) {
        var taskResult = new TaskCompletionSource<string>();

        socket.BeginSend(state.RequestBytes, 0, state.RequestBytes.Length, SocketFlags.None, SendCallback, state);
        return taskResult.Task;

        void SendCallback(IAsyncResult result) {
            var sendState = result.AsyncState as ConnectState;
            Debug.Assert(sendState != null);
            try {
                socket.EndSend(result);
                var buffer = new byte[0x4000];
                var response = new StringBuilder();
                var receiveState = new ReceiveState(socket, sendState.Uri, sendState.Url, response, buffer);
                ReceiveAsync(socket, receiveState)
                   .ContinueWith(answer => {
                                     if (answer.IsFaulted) {
                                         taskResult.SetException(answer.Exception.InnerException!);
                                     }
                                     else {
                                         taskResult.SetResult(answer.Result);
                                     }
                                 });
            }
            catch (Exception ex) {
                taskResult.SetException(new Exception($"Error sending data to {sendState.Url}: {ex.Message}"));
                socket.Close();
            }
        }
    }

    private static Task<string> ConnectAsync(Socket socket, string host, int port, ConnectState state) {
        var taskResult = new TaskCompletionSource<string>();

        socket.BeginConnect(host, port, ConnectCallback, null);
        return taskResult.Task;

        void ConnectCallback(IAsyncResult result) {
            try {
                socket.EndConnect(result);
                SendAsync(socket, state).ContinueWith(answer => {
                                                          if (answer.IsFaulted) {
                                                              taskResult.SetException(answer.Exception
                                                                 .InnerException!);
                                                          }
                                                          else {
                                                              taskResult.SetResult(answer.Result);
                                                          }
                                                      });
            }
            catch (Exception ex) {
                taskResult.SetException(new Exception($"Error connecting to {state.Url}: {ex.Message}"));
                socket.Close();
            }
        }
    }

    private static Task DownloadFileAsync(string url) {
        var uri = new Uri(url);
        var port = Commons.GetHttpPort(uri);
        var requestHeaders = Commons.GetHttpRequest(uri);
        var requestBytes = Encoding.ASCII.GetBytes(requestHeaders);

        var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        var connectState = new ConnectState(socket, uri, url, requestBytes);

        return ConnectAsync(socket, uri.Host, port, connectState);
    }

    private static void DownloadFilesAsync() {
        Task.WhenAll(Commons.Urls.Select(DownloadFileAsync).ToArray()).ContinueWith(answer => {
            Console.WriteLine(answer.IsFaulted
                                  ? $"Errors occurred: {answer.Exception.Flatten().Message}"
                                  : "All downloads completed successfully.");
        });
    }

    public static void Main() {
        DownloadFilesAsync();
        Console.ReadLine();
    }
}