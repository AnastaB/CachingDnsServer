using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace DnsServerGUI
{
    public class DnsForwarder : IDisposable
    {
        private UdpClient _udpClient;
        private IPEndPoint _remoteEndpoint;
        private bool _disposed;

        public DnsForwarder(string dnsServerIp, int dnsServerPort = 53)
        {
            _udpClient = new UdpClient();
            IPAddress ipAddress = IPAddress.Parse(dnsServerIp);
            _remoteEndpoint = new IPEndPoint(ipAddress, dnsServerPort);
            _disposed = false;
        }

        public async Task<byte[]> ForwardRequestAsync(byte[] requestData, int timeoutMs = 5000)
        {
            if (_disposed)
                throw new ObjectDisposedException("DnsForwarder");
            if (requestData == null)
                throw new ArgumentNullException("requestData");

            try
            {
                await _udpClient.SendAsync(requestData, requestData.Length, _remoteEndpoint);
                Task<UdpReceiveResult> receiveTask = _udpClient.ReceiveAsync();
                Task delayTask = Task.Delay(timeoutMs);
                Task completedTask = await Task.WhenAny(receiveTask, delayTask);

                if (completedTask == receiveTask)
                    return receiveTask.Result.Buffer;
                else
                    return null;
            }
            catch
            {
                throw;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing && _udpClient != null)
                {
                    _udpClient.Close();
                    _udpClient.Dispose();
                    _udpClient = null;
                }
                _disposed = true;
            }
        }

        ~DnsForwarder()
        {
            Dispose(false);
        }
    }
}