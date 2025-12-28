using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WatsonTcp;

namespace Test.Automated
{
    class Program
    {
        static TestFramework _framework;
        static string _hostname = "127.0.0.1";
        static int _portCounter = 10000;
        static object _portLock = new object();

        static int GetNextPort()
        {
            lock (_portLock)
            {
                return _portCounter++;
            }
        }

        static void SetupDefaultServerHandlers(WatsonTcpServer server)
        {
            // WatsonTcp requires either MessageReceived or StreamReceived to be set
            // Add a default empty handler if one hasn't been added yet
            server.Events.MessageReceived += (s, e) => { };
        }

        static void SetupDefaultClientHandlers(WatsonTcpClient client)
        {
            // WatsonTcp requires either MessageReceived or StreamReceived to be set
            // Add a default empty handler if one hasn't been added yet
            client.Events.MessageReceived += (s, e) => { };
        }

        static void SafeDispose(WatsonTcpServer server)
        {
            try
            {
                server?.Dispose();
            }
            catch (AggregateException)
            {
                // Can occur when background tasks are cancelled during disposal
            }
        }

        static void SafeDispose(WatsonTcpClient client)
        {
            try
            {
                client?.Dispose();
            }
            catch (AggregateException)
            {
                // Can occur when background tasks are cancelled during disposal
            }
        }

        static async Task Main(string[] args)
        {
            Console.WriteLine("===============================================");
            Console.WriteLine("WatsonTcp Automated Test Suite");
            Console.WriteLine("===============================================");
            Console.WriteLine();

            _framework = new TestFramework();

            // Run all tests
            await RunAllTests();

            // Display summary
            Console.WriteLine();
            Console.WriteLine("===============================================");
            Console.WriteLine("Test Summary");
            Console.WriteLine("===============================================");
            _framework.PrintSummary();

            Console.WriteLine();
        }

        static async Task RunAllTests()
        {
            // Basic connection tests
            await Test_BasicServerStartStop();
            await Test_BasicClientConnection();
            await Test_ClientServerConnection();

            // Message sending/receiving tests
            await Test_ClientSendServerReceive();
            await Test_ServerSendClientReceive();
            await Test_BidirectionalCommunication();
            await Test_EmptyMessage();

            // Metadata tests
            await Test_SendWithMetadata();
            await Test_ReceiveWithMetadata();

            // Sync request/response tests
            await Test_SyncRequestResponse();
            await Test_SyncRequestTimeout();

            // Event tests
            await Test_ServerConnectedEvent();
            await Test_ServerDisconnectedEvent();
            await Test_ClientConnectedEvent();
            await Test_ClientDisconnectedEvent();
            await Test_MessageReceivedEvent();

            // Stream tests
            await Test_StreamSendReceive();
            await Test_LargeStreamTransfer();

            // Statistics tests
            await Test_ClientStatistics();
            await Test_ServerStatistics();

            // Multiple client tests
            await Test_MultipleClients();
            await Test_ListClients();

            // Disconnection tests
            await Test_ClientDisconnect();
            await Test_ServerDisconnectClient();
            await Test_ServerStop();

            // Large data tests
            await Test_LargeMessageTransfer();
            await Test_ManyMessages();

            // Error condition tests
            await Test_SendToNonExistentClient();
            await Test_ConnectToNonExistentServer();

            // Concurrent operation tests
            await Test_ConcurrentClientConnections();
            await Test_ConcurrentMessageSends();

            // Client GUID tests
            await Test_SpecifyClientGuid();

            // Idle timeout tests
            await Test_IdleClientTimeout();

            // Authentication tests
            await Test_AuthenticationSuccess();
            await Test_AuthenticationFailure();
            await Test_AuthenticationCallback();
        }

        #region Basic-Connection-Tests

        static async Task Test_BasicServerStartStop()
        {
            string testName = "Basic Server Start/Stop";
            WatsonTcpServer server = null;
            try
            {
                int port = GetNextPort();
                server = new WatsonTcpServer(_hostname, port);
                SetupDefaultServerHandlers(server);
                SetupDefaultServerHandlers(server);
                server.Start();
                await Task.Delay(100);

                if (!server.IsListening)
                {
                    _framework.RecordFailure(testName, "Server not listening after Start()");
                    return;
                }

                server.Stop();
                await Task.Delay(200);

                if (server.IsListening)
                {
                    _framework.RecordFailure(testName, "Server still listening after Stop()");
                    return;
                }

                _framework.RecordSuccess(testName);
            }
            catch (Exception ex)
            {
                _framework.RecordFailure(testName, ex.Message);
            }
            finally
            {
                SafeDispose(server);
                await Task.Delay(100);
            }
        }

        static async Task Test_BasicClientConnection()
        {
            string testName = "Basic Client Connection";
            WatsonTcpServer server = null;
            WatsonTcpClient client = null;

            try
            {
                int port = GetNextPort();
                server = new WatsonTcpServer(_hostname, port);
                SetupDefaultServerHandlers(server);
                SetupDefaultServerHandlers(server);
                server.Start();
                await Task.Delay(100);

                client = new WatsonTcpClient(_hostname, port);
                SetupDefaultClientHandlers(client);
                SetupDefaultClientHandlers(client);
                client.Connect();
                await Task.Delay(200);

                if (!client.Connected)
                {
                    _framework.RecordFailure(testName, "Client not connected after Connect()");
                    return;
                }

                _framework.RecordSuccess(testName);
            }
            catch (Exception ex)
            {
                _framework.RecordFailure(testName, ex.Message);
            }
            finally
            {
                SafeDispose(client);
                SafeDispose(server);
                await Task.Delay(100);
            }
        }

        static async Task Test_ClientServerConnection()
        {
            string testName = "Client-Server Connection";
            WatsonTcpServer server = null;
            WatsonTcpClient client = null;

            try
            {
                int port = GetNextPort();
                server = new WatsonTcpServer(_hostname, port);
                SetupDefaultServerHandlers(server);
                server.Start();
                await Task.Delay(100);

                client = new WatsonTcpClient(_hostname, port);
                SetupDefaultClientHandlers(client);
                client.Connect();
                await Task.Delay(200);

                var clients = server.ListClients().ToList();
                if (clients.Count != 1)
                {
                    _framework.RecordFailure(testName, $"Expected 1 client, found {clients.Count}");
                    return;
                }

                _framework.RecordSuccess(testName);
            }
            catch (Exception ex)
            {
                _framework.RecordFailure(testName, ex.Message);
            }
            finally
            {
                SafeDispose(client);
                SafeDispose(server);
                await Task.Delay(100);
            }
        }

        #endregion

        #region Message-Send-Receive-Tests

        static async Task Test_ClientSendServerReceive()
        {
            string testName = "Client Send -> Server Receive";
            WatsonTcpServer server = null;
            WatsonTcpClient client = null;
            string receivedData = null;
            ManualResetEvent messageReceived = new ManualResetEvent(false);

            try
            {
                int port = GetNextPort();
                server = new WatsonTcpServer(_hostname, port);
                SetupDefaultServerHandlers(server);
                server.Events.MessageReceived += (s, e) =>
                {
                    receivedData = Encoding.UTF8.GetString(e.Data);
                    messageReceived.Set();
                };
                server.Start();
                await Task.Delay(100);

                client = new WatsonTcpClient(_hostname, port);
                SetupDefaultClientHandlers(client);
                client.Connect();
                await Task.Delay(200);

                string testData = "Hello from client!";
                await client.SendAsync(testData);

                if (!messageReceived.WaitOne(5000))
                {
                    _framework.RecordFailure(testName, "Server did not receive message");
                    return;
                }

                if (receivedData != testData)
                {
                    _framework.RecordFailure(testName, $"Expected '{testData}', received '{receivedData}'");
                    return;
                }

                _framework.RecordSuccess(testName);
            }
            catch (Exception ex)
            {
                _framework.RecordFailure(testName, ex.Message);
            }
            finally
            {
                SafeDispose(client);
                SafeDispose(server);
                await Task.Delay(100);
            }
        }

        static async Task Test_ServerSendClientReceive()
        {
            string testName = "Server Send -> Client Receive";
            WatsonTcpServer server = null;
            WatsonTcpClient client = null;
            string receivedData = null;
            ManualResetEvent messageReceived = new ManualResetEvent(false);

            try
            {
                int port = GetNextPort();
                server = new WatsonTcpServer(_hostname, port);
                SetupDefaultServerHandlers(server);
                server.Start();
                await Task.Delay(100);

                client = new WatsonTcpClient(_hostname, port);
                SetupDefaultClientHandlers(client);
                client.Events.MessageReceived += (s, e) =>
                {
                    receivedData = Encoding.UTF8.GetString(e.Data);
                    messageReceived.Set();
                };
                client.Connect();
                await Task.Delay(200);

                string testData = "Hello from server!";
                var clients = server.ListClients().ToList();
                await server.SendAsync(clients[0].Guid, testData);

                if (!messageReceived.WaitOne(5000))
                {
                    _framework.RecordFailure(testName, "Client did not receive message");
                    return;
                }

                if (receivedData != testData)
                {
                    _framework.RecordFailure(testName, $"Expected '{testData}', received '{receivedData}'");
                    return;
                }

                _framework.RecordSuccess(testName);
            }
            catch (Exception ex)
            {
                _framework.RecordFailure(testName, ex.Message);
            }
            finally
            {
                SafeDispose(client);
                SafeDispose(server);
                await Task.Delay(100);
            }
        }

        static async Task Test_BidirectionalCommunication()
        {
            string testName = "Bidirectional Communication";
            WatsonTcpServer server = null;
            WatsonTcpClient client = null;
            string serverReceived = null;
            string clientReceived = null;
            ManualResetEvent serverGotMessage = new ManualResetEvent(false);
            ManualResetEvent clientGotMessage = new ManualResetEvent(false);

            try
            {
                int port = GetNextPort();
                server = new WatsonTcpServer(_hostname, port);
                SetupDefaultServerHandlers(server);
                server.Events.MessageReceived += (s, e) =>
                {
                    serverReceived = Encoding.UTF8.GetString(e.Data);
                    serverGotMessage.Set();
                };
                server.Start();
                await Task.Delay(100);

                client = new WatsonTcpClient(_hostname, port);
                SetupDefaultClientHandlers(client);
                client.Events.MessageReceived += (s, e) =>
                {
                    clientReceived = Encoding.UTF8.GetString(e.Data);
                    clientGotMessage.Set();
                };
                client.Connect();
                await Task.Delay(200);

                // Client -> Server
                string clientMsg = "From client";
                await client.SendAsync(clientMsg);
                if (!serverGotMessage.WaitOne(5000))
                {
                    _framework.RecordFailure(testName, "Server did not receive message");
                    return;
                }

                // Server -> Client
                string serverMsg = "From server";
                var clients = server.ListClients().ToList();
                await server.SendAsync(clients[0].Guid, serverMsg);
                if (!clientGotMessage.WaitOne(5000))
                {
                    _framework.RecordFailure(testName, "Client did not receive message");
                    return;
                }

                if (serverReceived != clientMsg || clientReceived != serverMsg)
                {
                    _framework.RecordFailure(testName, $"Data mismatch: server got '{serverReceived}', client got '{clientReceived}'");
                    return;
                }

                _framework.RecordSuccess(testName);
            }
            catch (Exception ex)
            {
                _framework.RecordFailure(testName, ex.Message);
            }
            finally
            {
                SafeDispose(client);
                SafeDispose(server);
                await Task.Delay(100);
            }
        }

        static async Task Test_EmptyMessage()
        {
            string testName = "Empty Message with Metadata";
            WatsonTcpServer server = null;
            WatsonTcpClient client = null;
            Dictionary<string, object> receivedMetadata = null;
            ManualResetEvent messageReceived = new ManualResetEvent(false);

            try
            {
                int port = GetNextPort();
                server = new WatsonTcpServer(_hostname, port);
                SetupDefaultServerHandlers(server);
                server.Events.MessageReceived += (s, e) =>
                {
                    receivedMetadata = e.Metadata;
                    messageReceived.Set();
                };
                server.Start();
                await Task.Delay(100);

                client = new WatsonTcpClient(_hostname, port);
                SetupDefaultClientHandlers(client);
                client.Connect();
                await Task.Delay(200);

                var metadata = new Dictionary<string, object> { { "test", "value" } };
                await client.SendAsync("", metadata);

                if (!messageReceived.WaitOne(5000))
                {
                    _framework.RecordFailure(testName, "Server did not receive message");
                    return;
                }

                if (receivedMetadata == null || !receivedMetadata.ContainsKey("test"))
                {
                    _framework.RecordFailure(testName, "Metadata not received correctly");
                    return;
                }

                _framework.RecordSuccess(testName);
            }
            catch (Exception ex)
            {
                _framework.RecordFailure(testName, ex.Message);
            }
            finally
            {
                SafeDispose(client);
                SafeDispose(server);
                await Task.Delay(100);
            }
        }

        #endregion

        #region Metadata-Tests

        static async Task Test_SendWithMetadata()
        {
            string testName = "Send With Metadata";
            WatsonTcpServer server = null;
            WatsonTcpClient client = null;
            Dictionary<string, object> receivedMetadata = null;
            string receivedData = null;
            ManualResetEvent messageReceived = new ManualResetEvent(false);

            try
            {
                int port = GetNextPort();
                server = new WatsonTcpServer(_hostname, port);
                SetupDefaultServerHandlers(server);
                server.Events.MessageReceived += (s, e) =>
                {
                    receivedData = Encoding.UTF8.GetString(e.Data);
                    receivedMetadata = e.Metadata;
                    messageReceived.Set();
                };
                server.Start();
                await Task.Delay(100);

                client = new WatsonTcpClient(_hostname, port);
                SetupDefaultClientHandlers(client);
                client.Connect();
                await Task.Delay(200);

                var metadata = new Dictionary<string, object>
                {
                    { "key1", "value1" },
                    { "key2", 42 },
                    { "key3", true }
                };

                await client.SendAsync("Test data", metadata);

                if (!messageReceived.WaitOne(5000))
                {
                    _framework.RecordFailure(testName, "Server did not receive message");
                    return;
                }

                if (receivedData != "Test data")
                {
                    _framework.RecordFailure(testName, $"Data mismatch: got '{receivedData}'");
                    return;
                }

                if (receivedMetadata == null || receivedMetadata.Count != 3)
                {
                    _framework.RecordFailure(testName, "Metadata not received correctly");
                    return;
                }

                _framework.RecordSuccess(testName);
            }
            catch (Exception ex)
            {
                _framework.RecordFailure(testName, ex.Message);
            }
            finally
            {
                SafeDispose(client);
                SafeDispose(server);
                await Task.Delay(100);
            }
        }

        static async Task Test_ReceiveWithMetadata()
        {
            string testName = "Receive With Metadata";
            WatsonTcpServer server = null;
            WatsonTcpClient client = null;
            Dictionary<string, object> receivedMetadata = null;
            ManualResetEvent messageReceived = new ManualResetEvent(false);

            try
            {
                int port = GetNextPort();
                server = new WatsonTcpServer(_hostname, port);
                SetupDefaultServerHandlers(server);
                server.Start();
                await Task.Delay(100);

                client = new WatsonTcpClient(_hostname, port);
                SetupDefaultClientHandlers(client);
                client.Events.MessageReceived += (s, e) =>
                {
                    receivedMetadata = e.Metadata;
                    messageReceived.Set();
                };
                client.Connect();
                await Task.Delay(200);

                var metadata = new Dictionary<string, object> { { "server", "data" } };
                var clients = server.ListClients().ToList();
                await server.SendAsync(clients[0].Guid, "Server message", metadata);

                if (!messageReceived.WaitOne(5000))
                {
                    _framework.RecordFailure(testName, "Client did not receive message");
                    return;
                }

                if (receivedMetadata == null || !receivedMetadata.ContainsKey("server"))
                {
                    _framework.RecordFailure(testName, "Metadata not received correctly");
                    return;
                }

                _framework.RecordSuccess(testName);
            }
            catch (Exception ex)
            {
                _framework.RecordFailure(testName, ex.Message);
            }
            finally
            {
                SafeDispose(client);
                SafeDispose(server);
                await Task.Delay(100);
            }
        }

        #endregion

        #region Sync-Request-Response-Tests

        static async Task Test_SyncRequestResponse()
        {
            string testName = "Sync Request/Response";
            WatsonTcpServer server = null;
            WatsonTcpClient client = null;

            try
            {
                int port = GetNextPort();
                server = new WatsonTcpServer(_hostname, port);
                SetupDefaultServerHandlers(server);
                server.Callbacks.SyncRequestReceivedAsync = async (req) =>
                {
                    await Task.Delay(10);
                    return new SyncResponse(req, "Response from server");
                };
                server.Start();
                await Task.Delay(100);

                client = new WatsonTcpClient(_hostname, port);
                SetupDefaultClientHandlers(client);
                client.Connect();
                await Task.Delay(200);

                SyncResponse response = await client.SendAndWaitAsync(5000, "Request from client");

                if (response == null)
                {
                    _framework.RecordFailure(testName, "No response received");
                    return;
                }

                string responseData = Encoding.UTF8.GetString(response.Data);
                if (responseData != "Response from server")
                {
                    _framework.RecordFailure(testName, $"Expected 'Response from server', got '{responseData}'");
                    return;
                }

                _framework.RecordSuccess(testName);
            }
            catch (Exception ex)
            {
                _framework.RecordFailure(testName, ex.Message);
            }
            finally
            {
                SafeDispose(client);
                SafeDispose(server);
                await Task.Delay(100);
            }
        }

        static async Task Test_SyncRequestTimeout()
        {
            string testName = "Sync Request Timeout";
            WatsonTcpServer server = null;
            WatsonTcpClient client = null;

            try
            {
                int port = GetNextPort();
                server = new WatsonTcpServer(_hostname, port);
                SetupDefaultServerHandlers(server);
                server.Callbacks.SyncRequestReceivedAsync = async (req) =>
                {
                    // Delay longer than timeout
                    await Task.Delay(3000);
                    return new SyncResponse(req, "Too late");
                };
                server.Start();
                await Task.Delay(100);

                client = new WatsonTcpClient(_hostname, port);
                SetupDefaultClientHandlers(client);
                client.Connect();
                await Task.Delay(200);

                bool timedOut = false;
                try
                {
                    SyncResponse response = await client.SendAndWaitAsync(1000, "Request");
                }
                catch (TimeoutException)
                {
                    timedOut = true;
                }

                if (!timedOut)
                {
                    _framework.RecordFailure(testName, "Request did not timeout as expected");
                    return;
                }

                _framework.RecordSuccess(testName);
            }
            catch (Exception ex)
            {
                _framework.RecordFailure(testName, ex.Message);
            }
            finally
            {
                SafeDispose(client);
                SafeDispose(server);
                await Task.Delay(100);
            }
        }

        #endregion

        #region Event-Tests

        static async Task Test_ServerConnectedEvent()
        {
            string testName = "Server Connected Event";
            WatsonTcpServer server = null;
            WatsonTcpClient client = null;
            bool eventFired = false;
            ManualResetEvent connectionEvent = new ManualResetEvent(false);

            try
            {
                int port = GetNextPort();
                server = new WatsonTcpServer(_hostname, port);
                SetupDefaultServerHandlers(server);
                server.Start();
                await Task.Delay(100);

                client = new WatsonTcpClient(_hostname, port);
                SetupDefaultClientHandlers(client);
                client.Events.ServerConnected += (s, e) =>
                {
                    eventFired = true;
                    connectionEvent.Set();
                };
                client.Connect();

                if (!connectionEvent.WaitOne(5000))
                {
                    _framework.RecordFailure(testName, "ServerConnected event did not fire");
                    return;
                }

                if (!eventFired)
                {
                    _framework.RecordFailure(testName, "Event flag not set");
                    return;
                }

                _framework.RecordSuccess(testName);
            }
            catch (Exception ex)
            {
                _framework.RecordFailure(testName, ex.Message);
            }
            finally
            {
                SafeDispose(client);
                SafeDispose(server);
                await Task.Delay(100);
            }
        }

        static async Task Test_ServerDisconnectedEvent()
        {
            string testName = "Server Disconnected Event";
            WatsonTcpServer server = null;
            WatsonTcpClient client = null;
            bool eventFired = false;
            ManualResetEvent disconnectionEvent = new ManualResetEvent(false);

            try
            {
                int port = GetNextPort();
                server = new WatsonTcpServer(_hostname, port);
                SetupDefaultServerHandlers(server);
                server.Start();
                await Task.Delay(100);

                client = new WatsonTcpClient(_hostname, port);
                SetupDefaultClientHandlers(client);
                client.Events.ServerDisconnected += (s, e) =>
                {
                    eventFired = true;
                    disconnectionEvent.Set();
                };
                client.Connect();
                await Task.Delay(200);

                // Disconnect from server side
                var clients = server.ListClients().ToList();
                await server.DisconnectClientAsync(clients[0].Guid);

                if (!disconnectionEvent.WaitOne(5000))
                {
                    _framework.RecordFailure(testName, "ServerDisconnected event did not fire");
                    return;
                }

                if (!eventFired)
                {
                    _framework.RecordFailure(testName, "Event flag not set");
                    return;
                }

                _framework.RecordSuccess(testName);
            }
            catch (Exception ex)
            {
                _framework.RecordFailure(testName, ex.Message);
            }
            finally
            {
                SafeDispose(client);
                SafeDispose(server);
                await Task.Delay(100);
            }
        }

        static async Task Test_ClientConnectedEvent()
        {
            string testName = "Client Connected Event";
            WatsonTcpServer server = null;
            WatsonTcpClient client = null;
            bool eventFired = false;
            Guid? connectedGuid = null;
            ManualResetEvent connectionEvent = new ManualResetEvent(false);

            try
            {
                int port = GetNextPort();
                server = new WatsonTcpServer(_hostname, port);
                SetupDefaultServerHandlers(server);
                server.Events.ClientConnected += (s, e) =>
                {
                    eventFired = true;
                    connectedGuid = e.Client.Guid;
                    connectionEvent.Set();
                };
                server.Start();
                await Task.Delay(100);

                client = new WatsonTcpClient(_hostname, port);
                SetupDefaultClientHandlers(client);
                client.Connect();

                if (!connectionEvent.WaitOne(5000))
                {
                    _framework.RecordFailure(testName, "ClientConnected event did not fire");
                    return;
                }

                if (!eventFired || connectedGuid == null)
                {
                    _framework.RecordFailure(testName, "Event not fired or GUID not captured");
                    return;
                }

                _framework.RecordSuccess(testName);
            }
            catch (Exception ex)
            {
                _framework.RecordFailure(testName, ex.Message);
            }
            finally
            {
                SafeDispose(client);
                SafeDispose(server);
                await Task.Delay(100);
            }
        }

        static async Task Test_ClientDisconnectedEvent()
        {
            string testName = "Client Disconnected Event";
            WatsonTcpServer server = null;
            WatsonTcpClient client = null;
            bool eventFired = false;
            ManualResetEvent disconnectionEvent = new ManualResetEvent(false);

            try
            {
                int port = GetNextPort();
                server = new WatsonTcpServer(_hostname, port);
                SetupDefaultServerHandlers(server);
                server.Events.ClientDisconnected += (s, e) =>
                {
                    eventFired = true;
                    disconnectionEvent.Set();
                };
                server.Start();
                await Task.Delay(100);

                client = new WatsonTcpClient(_hostname, port);
                SetupDefaultClientHandlers(client);
                client.Connect();
                await Task.Delay(200);

                client.Disconnect();

                if (!disconnectionEvent.WaitOne(5000))
                {
                    _framework.RecordFailure(testName, "ClientDisconnected event did not fire");
                    return;
                }

                if (!eventFired)
                {
                    _framework.RecordFailure(testName, "Event flag not set");
                    return;
                }

                _framework.RecordSuccess(testName);
            }
            catch (Exception ex)
            {
                _framework.RecordFailure(testName, ex.Message);
            }
            finally
            {
                SafeDispose(client);
                SafeDispose(server);
                await Task.Delay(100);
            }
        }

        static async Task Test_MessageReceivedEvent()
        {
            string testName = "Message Received Event";
            WatsonTcpServer server = null;
            WatsonTcpClient client = null;
            int messageCount = 0;
            ManualResetEvent messageEvent = new ManualResetEvent(false);

            try
            {
                int port = GetNextPort();
                server = new WatsonTcpServer(_hostname, port);
                SetupDefaultServerHandlers(server);
                server.Events.MessageReceived += (s, e) =>
                {
                    messageCount++;
                    messageEvent.Set();
                };
                server.Start();
                await Task.Delay(100);

                client = new WatsonTcpClient(_hostname, port);
                SetupDefaultClientHandlers(client);
                client.Connect();
                await Task.Delay(200);

                await client.SendAsync("Test message");

                if (!messageEvent.WaitOne(5000))
                {
                    _framework.RecordFailure(testName, "MessageReceived event did not fire");
                    return;
                }

                if (messageCount != 1)
                {
                    _framework.RecordFailure(testName, $"Expected 1 message, got {messageCount}");
                    return;
                }

                _framework.RecordSuccess(testName);
            }
            catch (Exception ex)
            {
                _framework.RecordFailure(testName, ex.Message);
            }
            finally
            {
                SafeDispose(client);
                SafeDispose(server);
                await Task.Delay(100);
            }
        }

        #endregion

        #region Stream-Tests

        static async Task Test_StreamSendReceive()
        {
            string testName = "Stream Send/Receive";
            WatsonTcpServer server = null;
            WatsonTcpClient client = null;
            long receivedLength = 0;
            ManualResetEvent streamReceived = new ManualResetEvent(false);

            try
            {
                int port = GetNextPort();
                server = new WatsonTcpServer(_hostname, port);
                // Don't call SetupDefaultServerHandlers here - we're adding StreamReceived handler
                server.Events.StreamReceived += (s, e) =>
                {
                    receivedLength = e.ContentLength;
                    byte[] buffer = new byte[e.ContentLength];
                    _ = e.DataStream.Read(buffer, 0, (int)e.ContentLength);
                    streamReceived.Set();
                };
                server.Start();
                await Task.Delay(100);

                client = new WatsonTcpClient(_hostname, port);
                SetupDefaultClientHandlers(client);
                client.Connect();
                await Task.Delay(200);

                byte[] data = Encoding.UTF8.GetBytes("Stream data test");
                using (MemoryStream ms = new MemoryStream(data))
                {
                    await client.SendAsync(data.Length, ms);
                }

                if (!streamReceived.WaitOne(5000))
                {
                    _framework.RecordFailure(testName, "Stream not received");
                    return;
                }

                if (receivedLength != data.Length)
                {
                    _framework.RecordFailure(testName, $"Expected {data.Length} bytes, got {receivedLength}");
                    return;
                }

                _framework.RecordSuccess(testName);
            }
            catch (Exception ex)
            {
                _framework.RecordFailure(testName, ex.Message);
            }
            finally
            {
                SafeDispose(client);
                SafeDispose(server);
                await Task.Delay(100);
            }
        }

        static async Task Test_LargeStreamTransfer()
        {
            string testName = "Large Stream Transfer (10MB)";
            WatsonTcpServer server = null;
            WatsonTcpClient client = null;
            long receivedLength = 0;
            bool dataVerified = false;
            ManualResetEvent streamReceived = new ManualResetEvent(false);

            try
            {
                int port = GetNextPort();
                server = new WatsonTcpServer(_hostname, port);
                // Don't call SetupDefaultServerHandlers here - we're adding StreamReceived handler
                server.Events.StreamReceived += (s, e) =>
                {
                    receivedLength = e.ContentLength;
                    byte[] buffer = new byte[8192];
                    long totalRead = 0;
                    bool valid = true;

                    while (totalRead < e.ContentLength)
                    {
                        int bytesRead = e.DataStream.Read(buffer, 0, buffer.Length);
                        if (bytesRead <= 0) break;

                        // Verify data integrity
                        for (int i = 0; i < bytesRead; i++)
                        {
                            byte expected = (byte)((totalRead + i) % 256);
                            if (buffer[i] != expected)
                            {
                                valid = false;
                                break;
                            }
                        }

                        totalRead += bytesRead;
                        if (!valid) break;
                    }

                    dataVerified = valid;
                    streamReceived.Set();
                };
                server.Start();
                await Task.Delay(100);

                client = new WatsonTcpClient(_hostname, port);
                SetupDefaultClientHandlers(client);
                client.Connect();
                await Task.Delay(200);

                // Send 10MB of data
                int dataSize = 10 * 1024 * 1024;
                using (MemoryStream ms = new MemoryStream())
                {
                    for (long i = 0; i < dataSize; i++)
                    {
                        ms.WriteByte((byte)(i % 256));
                    }

                    ms.Seek(0, SeekOrigin.Begin);
                    await client.SendAsync(dataSize, ms);
                }

                if (!streamReceived.WaitOne(30000))
                {
                    _framework.RecordFailure(testName, "Stream not received within timeout");
                    return;
                }

                if (receivedLength != dataSize)
                {
                    _framework.RecordFailure(testName, $"Expected {dataSize} bytes, got {receivedLength}");
                    return;
                }

                if (!dataVerified)
                {
                    _framework.RecordFailure(testName, "Data integrity check failed");
                    return;
                }

                _framework.RecordSuccess(testName);
            }
            catch (Exception ex)
            {
                _framework.RecordFailure(testName, ex.Message);
            }
            finally
            {
                SafeDispose(client);
                SafeDispose(server);
                await Task.Delay(100);
            }
        }

        #endregion

        #region Statistics-Tests

        static async Task Test_ClientStatistics()
        {
            string testName = "Client Statistics";
            WatsonTcpServer server = null;
            WatsonTcpClient client = null;

            try
            {
                int port = GetNextPort();
                server = new WatsonTcpServer(_hostname, port);
                SetupDefaultServerHandlers(server);
                server.Start();
                await Task.Delay(100);

                client = new WatsonTcpClient(_hostname, port);
                SetupDefaultClientHandlers(client);
                client.Connect();
                await Task.Delay(200);

                long initialSent = client.Statistics.SentBytes;
                long initialReceived = client.Statistics.ReceivedBytes;

                // Send data
                await client.SendAsync("Test message");
                await Task.Delay(200);

                if (client.Statistics.SentBytes <= initialSent)
                {
                    _framework.RecordFailure(testName, "SentBytes did not increase");
                    return;
                }

                // Receive data
                var clients = server.ListClients().ToList();
                await server.SendAsync(clients[0].Guid, "Response");
                await Task.Delay(200);

                if (client.Statistics.ReceivedBytes <= initialReceived)
                {
                    _framework.RecordFailure(testName, "ReceivedBytes did not increase");
                    return;
                }

                _framework.RecordSuccess(testName);
            }
            catch (Exception ex)
            {
                _framework.RecordFailure(testName, ex.Message);
            }
            finally
            {
                SafeDispose(client);
                SafeDispose(server);
                await Task.Delay(100);
            }
        }

        static async Task Test_ServerStatistics()
        {
            string testName = "Server Statistics";
            WatsonTcpServer server = null;
            WatsonTcpClient client = null;

            try
            {
                int port = GetNextPort();
                server = new WatsonTcpServer(_hostname, port);
                SetupDefaultServerHandlers(server);
                server.Start();
                await Task.Delay(100);

                long initialSent = server.Statistics.SentBytes;
                long initialReceived = server.Statistics.ReceivedBytes;

                client = new WatsonTcpClient(_hostname, port);
                SetupDefaultClientHandlers(client);
                client.Connect();
                await Task.Delay(200);

                // Receive data
                await client.SendAsync("Client message");
                await Task.Delay(200);

                if (server.Statistics.ReceivedBytes <= initialReceived)
                {
                    _framework.RecordFailure(testName, "ReceivedBytes did not increase");
                    return;
                }

                // Send data
                var clients = server.ListClients().ToList();
                await server.SendAsync(clients[0].Guid, "Server message");
                await Task.Delay(200);

                if (server.Statistics.SentBytes <= initialSent)
                {
                    _framework.RecordFailure(testName, "SentBytes did not increase");
                    return;
                }

                _framework.RecordSuccess(testName);
            }
            catch (Exception ex)
            {
                _framework.RecordFailure(testName, ex.Message);
            }
            finally
            {
                SafeDispose(client);
                SafeDispose(server);
                await Task.Delay(100);
            }
        }

        #endregion

        #region Multiple-Client-Tests

        static async Task Test_MultipleClients()
        {
            string testName = "Multiple Clients";
            WatsonTcpServer server = null;
            List<WatsonTcpClient> clients = new List<WatsonTcpClient>();

            try
            {
                int port = GetNextPort();
                server = new WatsonTcpServer(_hostname, port);
                SetupDefaultServerHandlers(server);
                server.Start();
                await Task.Delay(100);

                // Connect 5 clients
                for (int i = 0; i < 5; i++)
                {
                    var client = new WatsonTcpClient(_hostname, port);
                SetupDefaultClientHandlers(client);
                    client.Connect();
                    clients.Add(client);
                    await Task.Delay(100);
                }

                var serverClients = server.ListClients().ToList();
                if (serverClients.Count != 5)
                {
                    _framework.RecordFailure(testName, $"Expected 5 clients, found {serverClients.Count}");
                    return;
                }

                // Send message to each client
                for (int i = 0; i < 5; i++)
                {
                    string msg = $"Message {i}";
                    await server.SendAsync(serverClients[i].Guid, msg);
                }

                await Task.Delay(300);

                _framework.RecordSuccess(testName);
            }
            catch (Exception ex)
            {
                _framework.RecordFailure(testName, ex.Message);
            }
            finally
            {
                foreach (var client in clients)
                {
                    SafeDispose(client);
                }
                SafeDispose(server);
                await Task.Delay(100);
            }
        }

        static async Task Test_ListClients()
        {
            string testName = "List Clients";
            WatsonTcpServer server = null;
            WatsonTcpClient client1 = null;
            WatsonTcpClient client2 = null;

            try
            {
                int port = GetNextPort();
                server = new WatsonTcpServer(_hostname, port);
                SetupDefaultServerHandlers(server);
                server.Start();
                await Task.Delay(100);

                client1 = new WatsonTcpClient(_hostname, port);
                SetupDefaultClientHandlers(client1);
                client1.Connect();
                await Task.Delay(100);

                client2 = new WatsonTcpClient(_hostname, port);
                SetupDefaultClientHandlers(client2);
                client2.Connect();
                await Task.Delay(100);

                var clients = server.ListClients().ToList();
                if (clients.Count != 2)
                {
                    _framework.RecordFailure(testName, $"Expected 2 clients, got {clients.Count}");
                    return;
                }

                // Verify IsClientConnected
                foreach (var client in clients)
                {
                    if (!server.IsClientConnected(client.Guid))
                    {
                        _framework.RecordFailure(testName, $"Client {client.Guid} not reported as connected");
                        return;
                    }
                }

                _framework.RecordSuccess(testName);
            }
            catch (Exception ex)
            {
                _framework.RecordFailure(testName, ex.Message);
            }
            finally
            {
                SafeDispose(client1);
                SafeDispose(client2);
                SafeDispose(server);
                await Task.Delay(100);
            }
        }

        #endregion

        #region Disconnection-Tests

        static async Task Test_ClientDisconnect()
        {
            string testName = "Client Disconnect";
            WatsonTcpServer server = null;
            WatsonTcpClient client = null;

            try
            {
                int port = GetNextPort();
                server = new WatsonTcpServer(_hostname, port);
                SetupDefaultServerHandlers(server);
                server.Start();
                await Task.Delay(100);

                client = new WatsonTcpClient(_hostname, port);
                SetupDefaultClientHandlers(client);
                client.Connect();
                await Task.Delay(200);

                if (!client.Connected)
                {
                    _framework.RecordFailure(testName, "Client not connected");
                    return;
                }

                client.Disconnect();
                await Task.Delay(200);

                if (client.Connected)
                {
                    _framework.RecordFailure(testName, "Client still connected after Disconnect()");
                    return;
                }

                _framework.RecordSuccess(testName);
            }
            catch (Exception ex)
            {
                _framework.RecordFailure(testName, ex.Message);
            }
            finally
            {
                SafeDispose(client);
                SafeDispose(server);
                await Task.Delay(100);
            }
        }

        static async Task Test_ServerDisconnectClient()
        {
            string testName = "Server Disconnect Client";
            WatsonTcpServer server = null;
            WatsonTcpClient client = null;
            bool clientDisconnected = false;
            ManualResetEvent disconnectionEvent = new ManualResetEvent(false);

            try
            {
                int port = GetNextPort();
                server = new WatsonTcpServer(_hostname, port);
                SetupDefaultServerHandlers(server);
                server.Start();
                await Task.Delay(100);

                client = new WatsonTcpClient(_hostname, port);
                SetupDefaultClientHandlers(client);
                client.Events.ServerDisconnected += (s, e) =>
                {
                    clientDisconnected = true;
                    disconnectionEvent.Set();
                };
                client.Connect();
                await Task.Delay(200);

                var clients = server.ListClients().ToList();
                await server.DisconnectClientAsync(clients[0].Guid);

                if (!disconnectionEvent.WaitOne(5000))
                {
                    _framework.RecordFailure(testName, "Client did not detect disconnection");
                    return;
                }

                if (!clientDisconnected)
                {
                    _framework.RecordFailure(testName, "Client disconnect event not fired");
                    return;
                }

                var remainingClients = server.ListClients().ToList();
                if (remainingClients.Count != 0)
                {
                    _framework.RecordFailure(testName, $"Expected 0 clients, found {remainingClients.Count}");
                    return;
                }

                _framework.RecordSuccess(testName);
            }
            catch (Exception ex)
            {
                _framework.RecordFailure(testName, ex.Message);
            }
            finally
            {
                SafeDispose(client);
                SafeDispose(server);
                await Task.Delay(100);
            }
        }

        static async Task Test_ServerStop()
        {
            string testName = "Server Stop Disconnects Clients";
            WatsonTcpServer server = null;
            WatsonTcpClient client = null;
            bool clientDisconnected = false;
            ManualResetEvent disconnectionEvent = new ManualResetEvent(false);

            try
            {
                int port = GetNextPort();
                server = new WatsonTcpServer(_hostname, port);
                SetupDefaultServerHandlers(server);
                server.Start();
                await Task.Delay(100);

                client = new WatsonTcpClient(_hostname, port);
                SetupDefaultClientHandlers(client);
                client.Events.ServerDisconnected += (s, e) =>
                {
                    clientDisconnected = true;
                    disconnectionEvent.Set();
                };
                client.Connect();
                await Task.Delay(200);

                server.Stop();
                await Task.Delay(500); // Give time for disconnection to propagate

                // Try to send a message to force client to detect disconnection
                try
                {
                    await client.SendAsync("test");
                }
                catch
                {
                    // Expected - send might fail after server stops
                }

                await Task.Delay(500);

                if (!disconnectionEvent.WaitOne(10000))
                {
                    _framework.RecordFailure(testName, "Client did not detect server stop");
                    return;
                }

                if (!clientDisconnected)
                {
                    _framework.RecordFailure(testName, "Client disconnect event not fired");
                    return;
                }

                _framework.RecordSuccess(testName);
            }
            catch (Exception ex)
            {
                _framework.RecordFailure(testName, ex.Message);
            }
            finally
            {
                SafeDispose(client);
                try
                {
                    SafeDispose(server);
                }
                catch (AggregateException)
                {
                    // Expected - server.Stop() was called, disposal may throw cancelled task exception
                }
                await Task.Delay(100);
            }
        }

        #endregion

        #region Large-Data-Tests

        static async Task Test_LargeMessageTransfer()
        {
            string testName = "Large Message Transfer (1MB)";
            WatsonTcpServer server = null;
            WatsonTcpClient client = null;
            byte[] receivedData = null;
            ManualResetEvent messageReceived = new ManualResetEvent(false);

            try
            {
                int port = GetNextPort();
                server = new WatsonTcpServer(_hostname, port);
                SetupDefaultServerHandlers(server);
                server.Events.MessageReceived += (s, e) =>
                {
                    receivedData = e.Data;
                    messageReceived.Set();
                };
                server.Start();
                await Task.Delay(100);

                client = new WatsonTcpClient(_hostname, port);
                SetupDefaultClientHandlers(client);
                client.Connect();
                await Task.Delay(200);

                // Send 1MB of data
                byte[] largeData = new byte[1024 * 1024];
                for (int i = 0; i < largeData.Length; i++)
                {
                    largeData[i] = (byte)(i % 256);
                }

                await client.SendAsync(largeData);

                if (!messageReceived.WaitOne(30000))
                {
                    _framework.RecordFailure(testName, "Large message not received");
                    return;
                }

                if (receivedData.Length != largeData.Length)
                {
                    _framework.RecordFailure(testName, $"Expected {largeData.Length} bytes, got {receivedData.Length}");
                    return;
                }

                // Verify data integrity
                for (int i = 0; i < largeData.Length; i++)
                {
                    if (largeData[i] != receivedData[i])
                    {
                        _framework.RecordFailure(testName, $"Data mismatch at byte {i}");
                        return;
                    }
                }

                _framework.RecordSuccess(testName);
            }
            catch (Exception ex)
            {
                _framework.RecordFailure(testName, ex.Message);
            }
            finally
            {
                SafeDispose(client);
                SafeDispose(server);
                await Task.Delay(100);
            }
        }

        static async Task Test_ManyMessages()
        {
            string testName = "Many Messages (100 messages)";
            WatsonTcpServer server = null;
            WatsonTcpClient client = null;
            int receivedCount = 0;
            ManualResetEvent allReceived = new ManualResetEvent(false);

            try
            {
                int port = GetNextPort();
                server = new WatsonTcpServer(_hostname, port);
                SetupDefaultServerHandlers(server);
                server.Events.MessageReceived += (s, e) =>
                {
                    receivedCount++;
                    if (receivedCount >= 100)
                    {
                        allReceived.Set();
                    }
                };
                server.Start();
                await Task.Delay(100);

                client = new WatsonTcpClient(_hostname, port);
                SetupDefaultClientHandlers(client);
                client.Connect();
                await Task.Delay(200);

                // Send 100 messages
                for (int i = 0; i < 100; i++)
                {
                    await client.SendAsync($"Message {i}");
                }

                if (!allReceived.WaitOne(30000))
                {
                    _framework.RecordFailure(testName, $"Only received {receivedCount}/100 messages");
                    return;
                }

                _framework.RecordSuccess(testName);
            }
            catch (Exception ex)
            {
                _framework.RecordFailure(testName, ex.Message);
            }
            finally
            {
                SafeDispose(client);
                SafeDispose(server);
                await Task.Delay(100);
            }
        }

        #endregion

        #region Error-Condition-Tests

        static async Task Test_SendToNonExistentClient()
        {
            string testName = "Send To Non-Existent Client";
            WatsonTcpServer server = null;

            try
            {
                int port = GetNextPort();
                server = new WatsonTcpServer(_hostname, port);
                SetupDefaultServerHandlers(server);
                server.Start();
                await Task.Delay(100);

                Guid nonExistentGuid = Guid.NewGuid();

                try
                {
                    bool result = await server.SendAsync(nonExistentGuid, "Test data");

                    if (result)
                    {
                        _framework.RecordFailure(testName, "Send succeeded for non-existent client");
                        return;
                    }
                }
                catch (Exception sendEx)
                {
                    // Exception is also acceptable - either false return or exception means client not found
                    if (!sendEx.Message.Contains("Unable to find client") && !sendEx.Message.Contains("not found"))
                    {
                        _framework.RecordFailure(testName, $"Unexpected exception: {sendEx.Message}");
                        return;
                    }
                }

                _framework.RecordSuccess(testName);
            }
            catch (Exception ex)
            {
                _framework.RecordFailure(testName, ex.Message);
            }
            finally
            {
                SafeDispose(server);
                await Task.Delay(100);
            }
        }

        static async Task Test_ConnectToNonExistentServer()
        {
            string testName = "Connect To Non-Existent Server";
            WatsonTcpClient client = null;

            try
            {
                int port = GetNextPort();
                client = new WatsonTcpClient(_hostname, port);
                SetupDefaultClientHandlers(client);

                bool connectionFailed = false;
                try
                {
                    client.Connect();
                    await Task.Delay(500);
                }
                catch
                {
                    connectionFailed = true;
                }

                if (!connectionFailed && client.Connected)
                {
                    _framework.RecordFailure(testName, "Client connected to non-existent server");
                    return;
                }

                _framework.RecordSuccess(testName);
            }
            catch (Exception)
            {
                // Expected behavior - connection failure is expected
                _framework.RecordSuccess(testName);
            }
            finally
            {
                SafeDispose(client);
                await Task.Delay(100);
            }
        }

        #endregion

        #region Concurrent-Operation-Tests

        static async Task Test_ConcurrentClientConnections()
        {
            string testName = "Concurrent Client Connections";
            WatsonTcpServer server = null;
            List<WatsonTcpClient> clients = new List<WatsonTcpClient>();

            try
            {
                int port = GetNextPort();
                server = new WatsonTcpServer(_hostname, port);
                SetupDefaultServerHandlers(server);
                server.Start();
                await Task.Delay(100);

                // Connect 10 clients concurrently
                var connectTasks = new List<Task>();
                for (int i = 0; i < 10; i++)
                {
                    var client = new WatsonTcpClient(_hostname, port);
                SetupDefaultClientHandlers(client);
                    clients.Add(client);
                    connectTasks.Add(Task.Run(() => client.Connect()));
                }

                await Task.WhenAll(connectTasks);
                await Task.Delay(500);

                var serverClients = server.ListClients().ToList();
                if (serverClients.Count != 10)
                {
                    _framework.RecordFailure(testName, $"Expected 10 clients, found {serverClients.Count}");
                    return;
                }

                _framework.RecordSuccess(testName);
            }
            catch (Exception ex)
            {
                _framework.RecordFailure(testName, ex.Message);
            }
            finally
            {
                foreach (var client in clients)
                {
                    SafeDispose(client);
                }
                SafeDispose(server);
                await Task.Delay(100);
            }
        }

        static async Task Test_ConcurrentMessageSends()
        {
            string testName = "Concurrent Message Sends";
            WatsonTcpServer server = null;
            WatsonTcpClient client = null;
            int receivedCount = 0;
            object lockObj = new object();
            ManualResetEvent allReceived = new ManualResetEvent(false);

            try
            {
                int port = GetNextPort();
                server = new WatsonTcpServer(_hostname, port);
                SetupDefaultServerHandlers(server);
                server.Events.MessageReceived += (s, e) =>
                {
                    lock (lockObj)
                    {
                        receivedCount++;
                        if (receivedCount >= 50)
                        {
                            allReceived.Set();
                        }
                    }
                };
                server.Start();
                await Task.Delay(100);

                client = new WatsonTcpClient(_hostname, port);
                SetupDefaultClientHandlers(client);
                client.Connect();
                await Task.Delay(200);

                // Send 50 messages concurrently
                var sendTasks = new List<Task>();
                for (int i = 0; i < 50; i++)
                {
                    int index = i;
                    sendTasks.Add(Task.Run(async () => await client.SendAsync($"Concurrent message {index}")));
                }

                await Task.WhenAll(sendTasks);

                if (!allReceived.WaitOne(30000))
                {
                    _framework.RecordFailure(testName, $"Only received {receivedCount}/50 messages");
                    return;
                }

                _framework.RecordSuccess(testName);
            }
            catch (Exception ex)
            {
                _framework.RecordFailure(testName, ex.Message);
            }
            finally
            {
                SafeDispose(client);
                SafeDispose(server);
                await Task.Delay(100);
            }
        }

        #endregion

        #region Client-GUID-Tests

        static async Task Test_SpecifyClientGuid()
        {
            string testName = "Specify Client GUID";
            WatsonTcpServer server = null;
            WatsonTcpClient client = null;
            Guid? receivedGuid = null;
            ManualResetEvent clientConnected = new ManualResetEvent(false);

            try
            {
                int port = GetNextPort();
                server = new WatsonTcpServer(_hostname, port);
                SetupDefaultServerHandlers(server);
                server.Events.ClientConnected += (s, e) =>
                {
                    receivedGuid = e.Client.Guid;
                    clientConnected.Set();
                };
                server.Start();
                await Task.Delay(100);

                Guid customGuid = Guid.NewGuid();
                client = new WatsonTcpClient(_hostname, port);
                SetupDefaultClientHandlers(client);
                client.Settings.Guid = customGuid;
                client.Connect();

                if (!clientConnected.WaitOne(5000))
                {
                    _framework.RecordFailure(testName, "Client connection event not fired");
                    return;
                }

                if (receivedGuid != customGuid)
                {
                    _framework.RecordFailure(testName, $"Expected GUID {customGuid}, got {receivedGuid}");
                    return;
                }

                _framework.RecordSuccess(testName);
            }
            catch (Exception ex)
            {
                _framework.RecordFailure(testName, ex.Message);
            }
            finally
            {
                SafeDispose(client);
                SafeDispose(server);
                await Task.Delay(100);
            }
        }

        #endregion

        #region Idle-Timeout-Tests

        static async Task Test_IdleClientTimeout()
        {
            string testName = "Idle Client Timeout";
            WatsonTcpServer server = null;
            WatsonTcpClient client = null;
            bool clientDisconnected = false;
            ManualResetEvent disconnectionEvent = new ManualResetEvent(false);

            try
            {
                int port = GetNextPort();
                server = new WatsonTcpServer(_hostname, port);
                SetupDefaultServerHandlers(server);
                server.Settings.IdleClientTimeoutSeconds = 2;
                server.Start();
                await Task.Delay(100);

                client = new WatsonTcpClient(_hostname, port);
                SetupDefaultClientHandlers(client);
                client.Events.ServerDisconnected += (s, e) =>
                {
                    clientDisconnected = true;
                    disconnectionEvent.Set();
                };
                client.Connect();
                await Task.Delay(200);

                // Wait for idle timeout (2 seconds + buffer)
                if (!disconnectionEvent.WaitOne(5000))
                {
                    _framework.RecordFailure(testName, "Client not disconnected after idle timeout");
                    return;
                }

                if (!clientDisconnected)
                {
                    _framework.RecordFailure(testName, "Client disconnect event not fired");
                    return;
                }

                _framework.RecordSuccess(testName);
            }
            catch (Exception ex)
            {
                _framework.RecordFailure(testName, ex.Message);
            }
            finally
            {
                SafeDispose(client);
                SafeDispose(server);
                await Task.Delay(100);
            }
        }

        #endregion

        #region Authentication-Tests

        static async Task Test_AuthenticationSuccess()
        {
            string testName = "Authentication Success (via Settings)";
            WatsonTcpServer server = null;
            WatsonTcpClient client = null;
            bool authSucceeded = false;
            ManualResetEvent authEvent = new ManualResetEvent(false);

            try
            {
                int port = GetNextPort();
                string presharedKey = "1234567890123456"; // Must be 16 bytes

                server = new WatsonTcpServer(_hostname, port);
                server.Events.MessageReceived += (s, e) => { }; // Required handler
                server.Settings.PresharedKey = presharedKey;
                server.Events.AuthenticationSucceeded += (s, e) =>
                {
                    authSucceeded = true;
                    authEvent.Set();
                };
                server.Start();
                await Task.Delay(200);

                client = new WatsonTcpClient(_hostname, port);
                client.Events.MessageReceived += (s, e) => { }; // Required handler
                // Set the preshared key in client settings - should now auto-authenticate
                client.Settings.PresharedKey = presharedKey;
                client.Connect();
                await Task.Delay(2000); // Wait longer for full auth handshake

                if (!authEvent.WaitOne(5000))
                {
                    _framework.RecordFailure(testName, "Authentication event did not fire");
                    return;
                }

                if (!authSucceeded)
                {
                    _framework.RecordFailure(testName, "Server did not record successful authentication");
                    return;
                }

                _framework.RecordSuccess(testName);
            }
            catch (Exception ex)
            {
                _framework.RecordFailure(testName, ex.Message);
            }
            finally
            {
                SafeDispose(client);
                SafeDispose(server);
                await Task.Delay(100);
            }
        }

        static async Task Test_AuthenticationFailure()
        {
            string testName = "Authentication Failure (Wrong Key)";
            WatsonTcpServer server = null;
            WatsonTcpClient client = null;
            bool authFailed = false;
            bool clientDisconnected = false;
            ManualResetEvent authEvent = new ManualResetEvent(false);

            try
            {
                int port = GetNextPort();
                string serverKey = "1234567890123456"; // Must be 16 bytes
                string wrongKey = "wrongkey12345678";

                server = new WatsonTcpServer(_hostname, port);
                server.Events.MessageReceived += (s, e) => { }; // Required handler
                server.Settings.PresharedKey = serverKey;
                server.Events.AuthenticationFailed += (s, e) =>
                {
                    authFailed = true;
                    authEvent.Set();
                };
                server.Start();
                await Task.Delay(200);

                client = new WatsonTcpClient(_hostname, port);
                client.Events.MessageReceived += (s, e) => { }; // Required handler
                client.Events.ServerDisconnected += (s, e) =>
                {
                    if (e.Reason == DisconnectReason.AuthFailure)
                    {
                        clientDisconnected = true;
                    }
                };
                // Set WRONG preshared key
                client.Settings.PresharedKey = wrongKey;
                client.Connect();
                await Task.Delay(1000); // Wait for auth failure

                if (!authEvent.WaitOne(5000))
                {
                    _framework.RecordFailure(testName, "Authentication failure event did not fire on server");
                    return;
                }

                if (!authFailed)
                {
                    _framework.RecordFailure(testName, "Server did not record authentication failure");
                    return;
                }

                if (!clientDisconnected)
                {
                    _framework.RecordFailure(testName, "Client was not disconnected after auth failure");
                    return;
                }

                _framework.RecordSuccess(testName);
            }
            catch (Exception ex)
            {
                _framework.RecordFailure(testName, ex.Message);
            }
            finally
            {
                SafeDispose(client);
                SafeDispose(server);
                await Task.Delay(100);
            }
        }

        static async Task Test_AuthenticationCallback()
        {
            string testName = "Authentication Callback (Fallback)";
            WatsonTcpServer server = null;
            WatsonTcpClient client = null;
            bool authSucceeded = false;
            bool callbackCalled = false;
            ManualResetEvent authEvent = new ManualResetEvent(false);

            try
            {
                int port = GetNextPort();
                string presharedKey = "callback12345678"; // Must be 16 bytes

                server = new WatsonTcpServer(_hostname, port);
                server.Events.MessageReceived += (s, e) => { }; // Required handler
                server.Settings.PresharedKey = presharedKey;
                server.Events.AuthenticationSucceeded += (s, e) =>
                {
                    authSucceeded = true;
                    authEvent.Set();
                };
                server.Start();
                await Task.Delay(200);

                client = new WatsonTcpClient(_hostname, port);
                client.Events.MessageReceived += (s, e) => { }; // Required handler
                // Don't set Settings.PresharedKey - use callback instead
                client.Callbacks.AuthenticationRequested = () =>
                {
                    callbackCalled = true;
                    return presharedKey;
                };
                client.Connect();
                await Task.Delay(1000); // Wait for callback and auth

                if (!authEvent.WaitOne(5000))
                {
                    _framework.RecordFailure(testName, "Authentication via callback did not succeed");
                    return;
                }

                if (!authSucceeded)
                {
                    _framework.RecordFailure(testName, "Server did not record successful authentication");
                    return;
                }

                if (!callbackCalled)
                {
                    _framework.RecordFailure(testName, "Callback was not called");
                    return;
                }

                _framework.RecordSuccess(testName);
            }
            catch (Exception ex)
            {
                _framework.RecordFailure(testName, ex.Message);
            }
            finally
            {
                SafeDispose(client);
                SafeDispose(server);
                await Task.Delay(100);
            }
        }

        #endregion
    }

    #region Test-Framework

    class TestFramework
    {
        private List<TestResult> _results = new List<TestResult>();
        private object _lock = new object();

        public void RecordSuccess(string testName)
        {
            lock (_lock)
            {
                _results.Add(new TestResult { TestName = testName, Passed = true });
                Console.WriteLine($"[PASS] {testName}");
            }
        }

        public void RecordFailure(string testName, string reason)
        {
            lock (_lock)
            {
                _results.Add(new TestResult { TestName = testName, Passed = false, FailureReason = reason });
                Console.WriteLine($"[FAIL] {testName}");
                Console.WriteLine($"       Reason: {reason}");
            }
        }

        public void PrintSummary()
        {
            Console.WriteLine();
            foreach (var result in _results)
            {
                string status = result.Passed ? "PASS" : "FAIL";
                Console.WriteLine($"[{status}] {result.TestName}");
            }

            Console.WriteLine();
            int passed = _results.Count(r => r.Passed);
            int failed = _results.Count(r => !r.Passed);
            int total = _results.Count;

            Console.WriteLine($"Total: {total} tests");
            Console.WriteLine($"Passed: {passed}");
            Console.WriteLine($"Failed: {failed}");
            Console.WriteLine();

            if (failed == 0)
            {
                Console.WriteLine("===============================================");
                Console.WriteLine("OVERALL RESULT: PASS");
                Console.WriteLine("===============================================");
            }
            else
            {
                Console.WriteLine("===============================================");
                Console.WriteLine("OVERALL RESULT: FAIL");
                Console.WriteLine("===============================================");
            }
        }
    }

    class TestResult
    {
        public string TestName { get; set; }
        public bool Passed { get; set; }
        public string FailureReason { get; set; }
    }

    #endregion
}
