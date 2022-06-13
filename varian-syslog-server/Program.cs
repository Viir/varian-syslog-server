
using System.CommandLine;

class Program
{
    static async Task<int> Main(string[] args)
    {
        var tcpPortDefault = 55020;

        var tcpPortOption =
        new System.CommandLine.Option<int>(
            name: "--port",
            description: "The TCP port to listen on for clients.");

        tcpPortOption.SetDefaultValue(tcpPortDefault);

        var rootCommand = new System.CommandLine.RootCommand("Syslog server writing received messages into a file.");
        rootCommand.AddOption(tcpPortOption);

        rootCommand.SetHandler(Run, tcpPortOption);

        return await rootCommand.InvokeAsync(args);
    }

    static void Run(int tcpPort)
    {
        var outputFileName = DateTimeOffset.UtcNow.ToString("yyyy-MM-ddTHH-mm-ss") + ".log";

        object writeLock = new object();

        var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, tcpPort);

        listener.Start();

        Console.WriteLine("Listening for connections at " + listener.LocalEndpoint.ToString());
        Console.WriteLine("Writing to file " + outputFileName);

        while (true)
        {
            var client = listener.AcceptTcpClient();

            Console.WriteLine("Connected new client: " + client.Client.RemoteEndPoint);

            Task.Run(() => forwardForClient(client));
        }

        void forwardForClient(System.Net.Sockets.TcpClient tcpClient)
        {
            string currentLine = "";

            var stream = tcpClient.GetStream();

            int i;

            var bytes = new byte[0x1000];

            while ((i = stream.Read(bytes, 0, bytes.Length)) != 0)
            {
                var newString = System.Text.Encoding.ASCII.GetString(bytes, 0, i);

                var newStringLines = newString.Split((char)10, (char)13);

                if (newStringLines.Length == 1)
                {
                    currentLine += newString;
                }
                else
                {
                    completedLine(currentLine + newStringLines[0]);

                    foreach (var line in newStringLines.Skip(1).SkipLast(1))
                        if (0 < line.Length)
                            completedLine(line);

                    currentLine = newStringLines.Last();
                }
            }

            tcpClient.Close();
        }

        void completedLine(string line)
        {
            Console.WriteLine(nameof(completedLine) + ": " + line);

            lock (writeLock)
            {
                File.AppendAllText(outputFileName, line + "\n");
            }
        }
    }
}

