using System;
using System.Net;
using System.IO;
using System.Text;
using System.Net.Sockets;
using System.Diagnostics;
using System.Windows.Threading;

// ftp://ftp.neva.ru/Other/IPTel/LinkSYS-SPA942/spa9XX_user_web.pdf

namespace SETIlab3_2
{
    class FtpClient
    {
        public class FtpException : Exception
        {
            public FtpException(string message) : base(message) { }
            public FtpException(string message, Exception innerException) : base(message, innerException) { }
        }

        private MainWindow mainTreadLink;
        private static int BUFFER_SIZE = 512;
        private static Encoding ASCII = Encoding.ASCII;

        private string server = "localhost";
        private string remotePath = ".";
        private string username = "anonymous";
        private string password = "anonymous@anonymous.net";
        private string message = null;
        private string result = null;

        private int port = 21;
        private int bytes = 0;
        private int resultCode = 0;

        private int Count = 0;
        private long[] TimeArray;
        private long average = 0;

        private bool loggedin = false;
        private bool binMode = false;
        private long fileSize = 0;
        private long dowloadedSize = 0;
        private int dowloadPercent = 0;
        private int dowloadSpeed = -10;

        private int numberOfCycles = 50;
        private int numberMeasurements = 40;

        private Byte[] buffer = new Byte[BUFFER_SIZE];
        private Socket clientSocket = null;
        Socket cSocket = null;
        FileStream output = null;

        private int timeoutMSeconds = 10000;


        public FtpClient()
        {
        }


        public FtpClient(MainWindow mainThread, string server, string username, string password, int timeoutSeconds, int port)
        {
            this.mainTreadLink = mainThread;
            this.server = server;
            this.username = username;
            this.password = password;
            this.timeoutMSeconds = timeoutSeconds;
            this.port = port;
        }


        public int DowloadSpeed
        {
            get
            {
                return dowloadSpeed;
            }
            set
            {
                dowloadSpeed = value;
            }
        }


        public long FileSize
        {
            get
            {
                return this.fileSize;
            }
            set
            {
                this.fileSize = value;
            }
        }


        public long DowloadedSize
        {
            get
            {
                return this.dowloadedSize;
            }
            set
            {
                this.dowloadedSize = value;
            }
        }


        public int Port
        {
            get
            {
                return this.port;
            }
            set
            {
                this.port = value;
            }
        }


        public int Timeout
        {
            get
            {
                return this.timeoutMSeconds;
            }
            set
            {
                this.timeoutMSeconds = value;
            }
        }


        public string Server
        {
            get
            {
                return this.server;
            }
            set
            {
                this.server = value;
            }
        }


        public int RemotePort
        {
            get
            {
                return this.port;
            }
            set
            {
                this.port = value;
            }
        }


        public string RemotePath
        {
            get
            {
                return this.remotePath;
            }
            set
            {
                this.remotePath = value;
            }

        }


        public string Username
        {
            get
            {
                return this.username;
            }
            set
            {
                this.username = value;
            }
        }


        public string Password
        {
            get
            {
                return this.password;
            }
            set
            {
                this.password = value;
            }
        }


        public void Close()
        {

            if (this.clientSocket != null)
            {
                this.sendCommand("QUIT");
            }

            this.cleanup();
        }


        public long GetFileSize(string fileName)
        {
            if (!this.loggedin) this.Login();

            this.sendCommand("SIZE " + fileName);
            long size = 0;

            if (this.resultCode == 213)
                size = long.Parse(this.result.Substring(4));

            else
                throw new FtpException(this.result.Substring(4));

            return size;
        }


        public void ChangeDir(string dirName)
        {
            if (dirName == null || dirName.Equals(".") || dirName.Length == 0)
            {
                return;
            }

            if (!this.loggedin) this.Login();

            this.sendCommand("CWD " + dirName);

            if (this.resultCode != 250) throw new FtpException(result.Substring(4));

            this.sendCommand("PWD");

            if (this.resultCode != 257) throw new FtpException(result.Substring(4));

            this.remotePath = this.message.Split('"')[1];

            //updateStatusInMain("Текущая директория: " + this.remotePath, false);
        }


        public void Login()
        {
            if (this.loggedin) this.Close();

            mainTreadLink.Dispatcher.Invoke(new UpdateStatus(mainTreadLink.updateStatus), 
                new object[] { "Открытие соединения с " + this.server, false, 0 ,-1 });

            IPAddress addr = null;
            IPEndPoint ep = null;

            try
            {
                this.clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                clientSocket.ReceiveTimeout = timeoutMSeconds;
                addr = Dns.Resolve(this.server).AddressList[0];
                ep = new IPEndPoint(addr, this.port);
                this.clientSocket.Connect(ep);
            }
            catch (Exception ex)
            {
                //
                if (this.clientSocket != null && this.clientSocket.Connected) this.clientSocket.Close();

                throw new FtpException("Не удается подключиться к удаленному серверу", ex);
            }

            this.readResponse();

            if (this.resultCode != 220)
            {
                this.Close();
                throw new FtpException(this.result.Substring(4));
            }

            this.sendCommand("USER " + username);

            if (!(this.resultCode == 331 || this.resultCode == 230))
            {
                this.cleanup();
                throw new FtpException(this.result.Substring(4));
            }

            if (this.resultCode != 230)
            {
                this.sendCommand("PASS " + password);

                if (!(this.resultCode == 230 || this.resultCode == 202))
                {
                    this.cleanup();
                    throw new FtpException(this.result.Substring(4));
                }
            }

            this.loggedin = true;

            mainTreadLink.Dispatcher.Invoke(new UpdateStatus(mainTreadLink.updateStatus),
               new object[] { "Соединено с " + this.server, false, 30, -10 });

            this.ChangeDir(this.remotePath);
        }


        public bool BinaryMode
        {
            get
            {
                return this.binMode;
            }
            set
            {
                if (this.binMode == value) return;

                if (value)
                    sendCommand("TYPE I");

                else
                    sendCommand("TYPE A");

                if (this.resultCode != 200) throw new FtpException(result.Substring(4));
            }
        }


        public void Download(string remFileName, string locFileName, Boolean resume)
        {
            if (!this.loggedin) this.Login();

            this.BinaryMode = true;

            mainTreadLink.Dispatcher.Invoke(new UpdateStatus(mainTreadLink.updateStatus),
              new object[] { "Открытие соединения передачи файла", false, 70, -10 });

            if (locFileName.Equals(""))
            {
                locFileName = remFileName;
            }


            if (!File.Exists(locFileName))
                output = File.Create(locFileName);

            else
                output = new FileStream(locFileName, FileMode.Open);

            cSocket = createDataSocket();
            cSocket.ReceiveTimeout = timeoutMSeconds;

            long offset = 0;

            if (resume)
            {
                offset = output.Length;

                if (offset > 0)
                {
                    this.sendCommand("REST " + offset);
                    if (this.resultCode != 350)
                    {
                        offset = 0;
                        //mainTreadLink.Dispatcher.Invoke(new UpdateStatus(mainTreadLink.updateStatus),
                        //    new object[] { "Возобновление не поддерживается: " + result.Substring(4), false, offset });
                       
                    }
                    else
                    {
                        //mainTreadLink.Dispatcher.Invoke(new UpdateStatus(mainTreadLink.updateStatus),
                        //    new object[] { ""Возобновление на смещении: " + offset, false, offset });
                        output.Seek(offset, SeekOrigin.Begin);
                    }
                }
            }

            dowloadedSize = offset;
            fileSize = GetFileSize(remFileName);

            this.sendCommand("RETR " + remFileName);

            if (this.resultCode != 150 && this.resultCode != 125)
            {
                throw new FtpException(this.result.Substring(4));
            }

            // DateTime timeout = DateTime.Now.AddSeconds(this.timeoutSeconds);
            Count = 0;
            TimeArray = new long[numberMeasurements];
            Stopwatch stopWatch = new Stopwatch();

            mainTreadLink.Dispatcher.Invoke(new UpdateStatus(mainTreadLink.updateStatus),
             new object[] { "Передача файла начата!", false, 100, -10 });

            while (true)
            {
                try
                {
                    this.bytes = cSocket.Receive(buffer, buffer.Length, 0);
                }
                catch (SocketException se)
                {
                    output.Close();
                    cSocket.Close();
                    throw new FtpException("Подключение потеряно, время ожидания истекло", se);
                }
                output.Write(this.buffer, 0, this.bytes);
                
                dowloadedSize += bytes;

                if ((Count% numberOfCycles) == 0 )
                {
                    dowloadPercent = (int)(dowloadedSize * 100 / fileSize);
                    stopWatch.Stop();
                    TimeArray[Count/numberOfCycles] = (int)stopWatch.Elapsed.Ticks;
                    stopWatch.Reset();
                    stopWatch.Start();
                    mainTreadLink.Dispatcher.Invoke(new UpdateStatus(mainTreadLink.updateStatus),
                           new object[] { dowloadPercent.ToString() + " %", false, dowloadPercent, dowloadSpeed });

                    if (Count == (numberOfCycles * (TimeArray.Length-1)))
                    {
                        if (dowloadPercent == 100)
                            dowloadSpeed = -1;
                        else
                        {
                            average = 0;
                            for (int i = 0; i < TimeArray.Length; i++)
                                average += TimeArray[i];
                            average /= TimeArray.Length; 
                            dowloadSpeed = (int)((average * ((fileSize - DowloadedSize) / (BUFFER_SIZE * numberOfCycles))) / 10000000);
                        }
                        Count = 0;
                    }


                    else
                        Count++;
                }
                else
                    Count++;




                if (this.bytes <= 0)
                {
                    break;
                }
            }

            output.Close();

            if (cSocket.Connected) cSocket.Close();

            this.readResponse();

            if (this.resultCode != 226 && this.resultCode != 250)
                throw new FtpException(this.result.Substring(4));
        }


        private void readResponse()
        {
            this.message = "";
            this.result = this.readLine();

            if (this.result.Length > 3)
                this.resultCode = int.Parse(this.result.Substring(0, 3));
            else
                this.result = null;
        }


        private string readLine()
        {
            while (true)
            {
                try
                {
                    this.bytes = clientSocket.Receive(this.buffer, this.buffer.Length, 0);
                }
                catch (SocketException)
                {
                    return "";
                }
                this.message += ASCII.GetString(this.buffer, 0, this.bytes);

                if (this.bytes < this.buffer.Length)
                {
                    break;
                }
            }

            string[] msg = this.message.Split('\n');

            if (this.message.Length > 2)
                this.message = msg[msg.Length - 2];

            else
                this.message = msg[0];


            if (this.message.Length > 4 && !this.message.Substring(3, 1).Equals(" ")) return this.readLine();

            return message;
        }


        private void sendCommand(String command)
        {
            Byte[] cmdBytes = Encoding.ASCII.GetBytes((command + "\r\n").ToCharArray());
            clientSocket.Send(cmdBytes, cmdBytes.Length, 0);
            this.readResponse();
        }


        private Socket createDataSocket()
        { 
            this.sendCommand("PASV");
            if (this.resultCode != 227) throw new FtpException(this.result.Substring(4));

            int index1 = this.result.IndexOf('(');
            int index2 = this.result.IndexOf(')');

            string ipData = this.result.Substring(index1 + 1, index2 - index1 - 1);

            int[] parts = new int[6];

            int len = ipData.Length;
            int partCount = 0;
            string buf = "";

            for (int i = 0; i < len && partCount <= 6; i++)
            {
                char ch = char.Parse(ipData.Substring(i, 1));

                if (char.IsDigit(ch))
                    buf += ch;

                else if (ch != ',')
                    throw new FtpException("Неверный PASV ответ: " + result);

                if (ch == ',' || i + 1 == len)
                {
                    try
                    {
                        parts[partCount++] = int.Parse(buf);
                        buf = "";
                    }
                    catch (Exception ex)
                    {
                        throw new FtpException("Неверный PASV ответ (не поддерживается?): " + this.result, ex);
                    }
                }
            }

            string ipAddress = parts[0] + "." + parts[1] + "." + parts[2] + "." + parts[3];

            int port = (parts[4] << 8) + parts[5];

            Socket socket = null;
            IPEndPoint ep = null;

            try
            {
                socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                ep = new IPEndPoint(Dns.Resolve(ipAddress).AddressList[0], port);
                socket.Connect(ep);
            }
            catch (Exception ex)
            {
                // doubtfull....
                if (socket != null && socket.Connected) socket.Close();

                throw new FtpException("Невозможно подключиться к удаленному серверу", ex);
            }

            return socket;
        }


        private void cleanup()
        {
            if (output != null)
            {
                output.Close();
                output = null;
            }

            if (cSocket != null)
            {
                cSocket.Close();
                cSocket = null;
            }

            if (this.clientSocket != null)
            {
                this.clientSocket.Close();
                this.clientSocket = null;
            }
            dowloadSpeed = -1;
            this.loggedin = false;
        }


        ~FtpClient()
        {
            this.cleanup();
        }
    }
}
