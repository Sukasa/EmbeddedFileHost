using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace SDBrowser
{
    public class EmbeddedFileHost
    {
        private Socket UdpHost;
        private Thread RunThread;
        private byte[] Buffer;

        public string Status { get; set; }
        public string FriendlyName { get; set; }
        public string RootDir { get; set; }

        public void Init()
        {
            RunThread = new Thread(Run);
            RunThread.Start();
        }

        private void Run()
        {
            Buffer = new byte[512];
            EndPoint Endpoint = new IPEndPoint(IPAddress.Any, 15000);

            UdpHost = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            UdpHost.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            UdpHost.Bind(Endpoint);

            while (true)
            {
                if (UdpHost.Poll(-1, SelectMode.SelectRead))
                {
                    int PacketSize = UdpHost.ReceiveFrom(Buffer, ref Endpoint);

                    string Packet = new string(Encoding.UTF8.GetChars(Buffer, 0, PacketSize));
                    string Param = null;

                    int Split = Packet.IndexOf(' ');
                    if (Split > -1)
                    {
                        Param = Packet.Substring(Split);
                        Packet = Packet.Substring(0, Split);
                    }

                    byte[] Data;
                    switch (Packet)
                    {
                        case "KNOCKKNOCK":
                            Data = Encoding.UTF8.GetBytes("HIHI");
                            UdpHost.SendTo(Data, Data.Length, SocketFlags.None, Endpoint);

                            // Send Status/etc
                            Data = Encoding.UTF8.GetBytes("IAM " + FriendlyName);
                            UdpHost.SendTo(Data, Data.Length, SocketFlags.None, Endpoint);

                            Data = Encoding.UTF8.GetBytes("CURRENT EmbeddedFileHost/1.0 " + Status);
                            UdpHost.SendTo(Data, Data.Length, SocketFlags.None, Endpoint);

                            Data = Encoding.UTF8.GetBytes("ROOT " + RootDir);
                            UdpHost.SendTo(Data, Data.Length, SocketFlags.None, Endpoint);
                            break;
                        case "DIR":
                            StringBuilder SB = new StringBuilder("LIST ");
                            foreach (String Node in Directory.EnumerateFileSystemEntries(Param))
                            {
                                if (Directory.Exists(Node))
                                {
                                    SB.Append(Node);
                                    SB.AppendLine("#DIR");
                                }
                                else if (File.Exists(Node))
                                {
                                    FileInfo F = new FileInfo(Node);

                                    SB.Append(Node);
                                    SB.Append("#");
                                    SB.AppendLine(F.Length.ToString());
                                }
                            }
                            Data = Encoding.UTF8.GetBytes(SB.ToString());
                            UdpHost.SendTo(Data, Data.Length, SocketFlags.None, Endpoint);
                            break;
                        case "GET":
                            {
                                FileStream FS = File.OpenRead(Param);
                                Data = Encoding.UTF8.GetBytes("READY " + FS.Length.ToString());
                                UdpHost.SendTo(Data, Data.Length, SocketFlags.None, Endpoint);
                                int Amt;

                                while ((Amt = FS.Read(Buffer, 0, 512)) > 0)
                                {
                                    UdpHost.SendTo(Buffer, Amt, SocketFlags.None, Endpoint);
                                }
                                FS.Close();
                                FS.Dispose();
                                FS = null;
                            }
                            break;
                        case "PUT":
                            {
                                String[] Params = Param.Split(' ');
                                int AmtToGet = int.Parse(Params[1]);
                                int AmtGotten = 0;
                                if (File.Exists(Params[0]))
                                    File.Delete(Params[0]);
                                FileStream FS = File.OpenWrite(Params[0]);
                                Data = Encoding.UTF8.GetBytes("READY " + Buffer.Length);
                                UdpHost.SendTo(Data, Data.Length, SocketFlags.None, Endpoint);

                                while (AmtGotten < AmtToGet)
                                {
                                    int Amt = UdpHost.ReceiveFrom(Buffer, ref Endpoint);
                                    FS.Write(Buffer, 0, Amt);
                                    AmtGotten += Amt;
                                }
                                FS.Close();
                                FS.Dispose();
                                FS = null;
                            }
                            break;
                        case "MKDIR":
                            Directory.CreateDirectory(Param);
                            Data = Encoding.UTF8.GetBytes("DONE");
                            UdpHost.SendTo(Data, Data.Length, SocketFlags.None, Endpoint);
                            break;
                        case "DELETE":
                            File.Delete(Param);
                            Data = Encoding.UTF8.GetBytes("DONE");
                            UdpHost.SendTo(Data, Data.Length, SocketFlags.None, Endpoint);
                            break;
                        case "DONE":
                            break;
                    }
                    Data = null;
                }
                else
                {
                    Thread.Sleep(10);
                }
            }

        }
    }
}
