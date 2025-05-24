using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace DnsServerGUI
{
    public class DnsServer
    {
        private readonly UdpClient _listener;
        private readonly DnsForwarder _forwarder;
        private readonly DnsCache _cache;
        private readonly BlacklistFilter _filter;
        private readonly Logger _logger;

        public DnsServer(int listenPort, string forwarderIp, DnsCache cache, BlacklistFilter filter, Logger logger)
        {
            _listener = new UdpClient(listenPort);
            _forwarder = new DnsForwarder(forwarderIp);
            _cache = cache;
            _filter = filter;
            _logger = logger;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.Log("INFO", "system", "DNS-сервер запущен на порту " + ((IPEndPoint)_listener.Client.LocalEndPoint).Port);
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    Task<UdpReceiveResult> recvTask = _listener.ReceiveAsync();
                    Task cancelTask = Task.Delay(-1, cancellationToken);
                    Task completed = await Task.WhenAny(recvTask, cancelTask);
                    if (completed != recvTask)
                        break;
                    UdpReceiveResult result = recvTask.Result;
                    _ = Task.Run(() => HandleRequestAsync(result.RemoteEndPoint, result.Buffer), cancellationToken);
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                _logger.Log("ERROR", "system", "Ошибка в StartAsync: " + ex);
            }
            finally
            {
                _logger.Log("INFO", "system", "DNS-сервер остановлен");
                _listener.Close();
            }
        }

        private async Task HandleRequestAsync(IPEndPoint client, byte[] data)
        {
            DnsHeader reqHeader = null;
            DnsQuestion question = null;
            string domain = null;

            try
            {
                _logger.Log("REQUEST", client.ToString(), "Получен запрос");
                Tuple<DnsHeader, DnsQuestion> parseResult = DnsPacketParser.ParseQuery(data);
                reqHeader = parseResult.Item1;
                question = parseResult.Item2;
                domain = question.Name;
                if (string.IsNullOrEmpty(domain))
                {
                    _logger.Log("ERROR", domain, "Некорректное доменное имя в запросе");
                    return;
                }
            }
            catch (Exception ex)
            {
                _logger.Log("ERROR", "parser", ex.Message);
                return;
            }

            try
            {
                if (HandleBlocked(client, reqHeader, question))
                    return;

                if (HandleCached(client, reqHeader, question))
                    return;

                await HandleForwarded(client, reqHeader, question, data);
            }
            catch (Exception ex)
            {
                _logger.Log("ERROR", domain, "Ошибка обработки запроса: " + ex);
            }
        }

        private bool HandleBlocked(IPEndPoint client, DnsHeader reqHeader, DnsQuestion question)
        {
            string domain = question.Name;
            try
            {
                if (_filter.IsBlocked(domain))
                {
                    _logger.Log("BLOCKED", domain, "Заблокирован домен");
                    byte[] response = DnsPacketParser.BuildResponse(reqHeader, question, new List<DnsResourceRecord>(), true, true);
                    _listener.Send(response, response.Length, client);
                    return true;
                }
            }
            catch (Exception ex)
            {
                _logger.Log("ERROR", domain, "Ошибка в HandleBlocked: " + ex);
                return true;
            }
            return false;
        }

        private bool HandleCached(IPEndPoint client, DnsHeader reqHeader, DnsQuestion question)
        {
            string domain = question.Name;
            try
            {
                if (_cache.TryGet(domain, out DnsResourceRecord[] cachedRecords))
                {
                    _logger.Log("CACHED", domain, "Ответ из кэша");
                    List<DnsResourceRecord> answers = new List<DnsResourceRecord>(cachedRecords);
                    byte[] response = DnsPacketParser.BuildResponse(reqHeader, question, answers, false, false);
                    _listener.Send(response, response.Length, client);
                    return true;
                }
            }
            catch (Exception ex)
            {
                _logger.Log("ERROR", domain, "Ошибка в HandleCached: " + ex);
            }
            return false;
        }

        private async Task HandleForwarded(IPEndPoint client, DnsHeader reqHeader, DnsQuestion question, byte[] requestData)
        {
            string domain = question.Name;
            try
            {
                _logger.Log("FORWARD", domain, "Пересылаем внешний запрос");
                byte[] extResponse = await _forwarder.ForwardRequestAsync(requestData);

                if (extResponse == null)
                {
                    _logger.Log("TIMEOUT", domain, "Таймаут внешнего DNS");
                    byte[] nxdomain = DnsPacketParser.BuildResponse(reqHeader, question, new List<DnsResourceRecord>(), true, true);
                    _listener.Send(nxdomain, nxdomain.Length, client);
                    return;
                }

                Tuple<DnsHeader, List<DnsQuestion>, List<DnsResourceRecord>> parseResp = DnsPacketParser.ParseResponse(extResponse);
                List<DnsResourceRecord> answers = parseResp.Item3;

                _cache.Add(domain, answers.ToArray());

                _listener.Send(extResponse, extResponse.Length, client);
            }
            catch (Exception ex)
            {
                _logger.Log("ERROR", domain, "Ошибка форвардинга: " + ex);
                try
                {
                    byte[] nxdomain = DnsPacketParser.BuildResponse(reqHeader, question, new List<DnsResourceRecord>(), true, true);
                    _listener.Send(nxdomain, nxdomain.Length, client);
                }
                catch (Exception inner)
                {
                    _logger.Log("ERROR", domain, "Ошибка при отправке NXDOMAIN: " + inner);
                }
            }
        }
    }
}
