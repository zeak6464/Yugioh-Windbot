using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;

namespace YGOSharp.Network
{
    public class AsyncBinaryClient
    {
        public event Action Connected;
        public event Action<Exception> Disconnected;
        public event Action<byte[]> PacketReceived;

        protected int MaxPacketLength = 0xFFFF;
        protected int HeaderSize = 2;
        protected bool IsHeaderSizeIncluded = false;

        private NetworkClient _client;

        private List<byte> _receiveBuffer = new List<byte>();
        private byte[] _lengthBuffer = new byte[16];

        private int _pendingLength;

        public bool IsConnected
        {
            get { return _client.IsConnected; }
        }

        public IPAddress RemoteIPAddress
        {
            get { return _client.RemoteIPAddress; }
        }

        public AsyncBinaryClient(NetworkClient client)
        {
            _client = client;

            client.Connected += Client_Connected;
            client.Disconnected += Client_Disconnected;
            client.DataReceived += Client_DataReceived;

            if (_client.IsConnected)
            {
                _client.BeginReceive();
            }
        }

        public void Connect(IPAddress address, int port)
        {
            _client.BeginConnect(address, port);
        }

        public void Initialize(Socket socket)
        {
            _client.Initialize(socket);
        }

        public void Send(byte[] packet)
        {
            if (packet.Length > MaxPacketLength)
            {
                throw new Exception("Tried to send a too large packet");
            }

            int packetLength = packet.Length;
            if (IsHeaderSizeIncluded) packetLength += HeaderSize;

            byte[] header;
            if (HeaderSize == 2)
            {
                header = BitConverter.GetBytes((ushort)packetLength);
            }
            else if (HeaderSize == 4)
            {
                header = BitConverter.GetBytes(packetLength);
            }
            else
            {
                throw new Exception("Unsupported header size: " + HeaderSize);
            }
            byte[] data = new byte[packet.Length + HeaderSize];
            Array.Copy(header, 0, data, 0, header.Length);
            Array.Copy(packet, 0, data, header.Length, packet.Length);
            _client.BeginSend(data);
        }

        public void Close(Exception error = null)
        {
            _client.Close(error);
        }

        private void Client_Connected()
        {
            if (Connected != null)
                Connected();
        }

        private void Client_Disconnected(Exception ex)
        {
            if (Disconnected != null)
                Disconnected(ex);
        }

        private void Client_DataReceived(byte[] data)
        {
            _receiveBuffer.AddRange(data);
            ExtractPackets();
        }

        private void ExtractPackets()
        {
            bool hasExtracted;
            do
            {
                if (_pendingLength == 0)
                {
                    hasExtracted = ExtractPendingLength();
                }
                else
                {
                    hasExtracted = ExtractPendingPacket();
                }
            }
            while (hasExtracted);
        }

        private bool ExtractPendingLength()
        {
            if (_receiveBuffer.Count >= HeaderSize)
            {
                _receiveBuffer.CopyTo(0, _lengthBuffer, 0, HeaderSize);
                if (HeaderSize == 2)
                {
                    _pendingLength = BitConverter.ToUInt16(_lengthBuffer, 0);
                }
                else if (HeaderSize == 4)
                {
                    _pendingLength = BitConverter.ToInt32(_lengthBuffer, 0);
                }
                else
                {
                    throw new Exception("Unsupported header size: " + HeaderSize);
                }
                _receiveBuffer.RemoveRange(0, HeaderSize);

                if (IsHeaderSizeIncluded) _pendingLength -= HeaderSize;

                if (_pendingLength < 0 || _pendingLength > MaxPacketLength)
                {
                    _client.Close(new Exception("Tried to receive a too large packet"));
                    return false;
                }

                return true;
            }
            return false;
        }

        private bool ExtractPendingPacket()
        {
            if (_receiveBuffer.Count >= _pendingLength)
            {
                byte[] packet = new byte[_pendingLength];

                _receiveBuffer.CopyTo(0, packet, 0, _pendingLength);
                _receiveBuffer.RemoveRange(0, _pendingLength);
                _pendingLength = 0;

                if (PacketReceived != null)
                    PacketReceived(packet);
                return true;
            }
            return false;
        }
    }
}
