using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO.Pipes;
using System.IO;
using System.Threading;

namespace batocera_services
{
    public class NamedPipeServer : IDisposable
    {
        public static readonly string PIPENAME = "batocera-marquee";

        public static bool IsServerRunning()
        {
            string pipeFullName = @"\\.\pipe\" + PIPENAME;

            try
            {
                // Check if the named pipe exists
                return File.Exists(pipeFullName);
            }
            catch (Exception)
            {
                // Handle any exceptions here, but this code should not throw exceptions
                return false;
            }
        }

        private readonly Action<string> messageHandler;         
        private EventWaitHandle shutdownEvent = new ManualResetEvent(false);

        public NamedPipeServer(Action<string> messageHandler)
        {
            this.messageHandler = messageHandler;
            Start();
        }

        public void Start()
        {
            new Thread(ServerLoop).Start();
        }

        public void Stop()
        {
            shutdownEvent.Set();
        }

        private void ServerLoop()
        {
            while (true)
            {
                var stream = new NamedPipeServerStream(PIPENAME, PipeDirection.In, -1, PipeTransmissionMode.Message, PipeOptions.Asynchronous);

                try
                {
                    var result = stream.BeginWaitForConnection(new AsyncCallback(this.HandleConnection), stream);

                    if (WaitHandle.WaitAny(new[] { result.AsyncWaitHandle, shutdownEvent }) == 1)
                    {
                        stream.Close();
                        stream.Dispose();
                        return;
                    }
                }
                catch
                {
                    stream.Close();
                    stream.Dispose();
                    continue;
                }
            }
        }

        private void HandleConnection(IAsyncResult iar)
        {
            try
            {
                using (var stream = (NamedPipeServerStream)iar.AsyncState)
                {
                    stream.EndWaitForConnection(iar);

                    using (var reader = new StreamReader(stream))
                    {
                        var request = reader.ReadToEnd();
                        if (messageHandler != null)
                            messageHandler.Invoke(request);
                    }
                }
            }
            catch { }
        }
        
        public void Dispose()
        {
            Stop();
        }
    }

    public class NamedPipeClient
    {
        public static void SendArguments(string[] args)
        {
            // Set the name for the named pipe (must match the server's pipe name)
            string pipeName = NamedPipeServer.PIPENAME;

            try
            {
                using (NamedPipeClientStream clientPipe = new NamedPipeClientStream(".", pipeName, PipeDirection.Out))
                {
                    clientPipe.Connect(150);

                    // Send the command-line arguments to the server as a single string
                    string argumentString = string.Join(" ", args.Select(a => a.Contains(" ") ? "\"" + a + "\"" : a).ToArray());

                    using (StreamWriter writer = new StreamWriter(clientPipe))
                    {
                        writer.Write(argumentString);
                        writer.Flush();
                        clientPipe.WaitForPipeDrain();
                    }
                }
            }
            catch
            {
                // Handle any exceptions here
            }
        }
    }
}
