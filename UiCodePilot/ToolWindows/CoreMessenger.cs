using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace UiCodePilot.ToolWindows
{
    /// <summary>
    /// Класс для обмена сообщениями с Core
    /// </summary>
    public class CoreMessenger
    {
        private TcpClient _tcpClient;
        private StreamWriter _tcpWriter;
        private StreamReader _tcpReader;
        private System.Threading.Thread _readThread;
        private bool _isConnected;
        private Dictionary<string, TaskCompletionSource<object>> _pendingRequests = new Dictionary<string, TaskCompletionSource<object>>();
        private Dictionary<string, Action<Message>> _messageTypeHandlers = new Dictionary<string, Action<Message>>();
        private object _configCache;

        /// <summary>
        /// Событие, возникающее при получении сообщения от Core
        /// </summary>
        public event EventHandler<Message> MessageReceived;

        /// <summary>
        /// Конструктор
        /// </summary>
        public CoreMessenger()
        {
            // Регистрируем обработчики сообщений
            RegisterMessageHandlers();
            
            // Подключаемся к Core
            ConnectToCore();
        }

        /// <summary>
        /// Регистрация обработчиков сообщений
        /// </summary>
        private void RegisterMessageHandlers()
        {
            // Обработчик для ping-сообщений
            _messageTypeHandlers["ping"] = (message) => {
                SendToCore("pong", null, message.MessageId);
            };

            // Обработчик для сообщений о конфигурации
            _messageTypeHandlers["configUpdate"] = (message) => {
                _configCache = message.Data;
                Debug.WriteLine("Received config update");
            };

            // Обработчик для сообщений о профилях
            _messageTypeHandlers["didChangeAvailableProfiles"] = (message) => {
                Debug.WriteLine("Profiles changed");
            };
        }

        /// <summary>
        /// Подключение к Core через TCP
        /// </summary>
        private void ConnectToCore()
        {
            try
            {
                // Адрес и порт для подключения к Core
                string host = "127.0.0.1";
                int port = 3000;

                // Создаем TCP-клиент
                _tcpClient = new TcpClient();
                
                // Пытаемся подключиться с таймаутом
                var connectTask = _tcpClient.ConnectAsync(host, port);
                if (!Task.WaitAll(new Task[] { connectTask }, 5000))
                {
                    throw new TimeoutException($"Connection to {host}:{port} timed out");
                }
                
                // Создаем потоки для чтения и записи
                NetworkStream stream = _tcpClient.GetStream();
                _tcpWriter = new StreamWriter(stream) { AutoFlush = true };
                _tcpReader = new StreamReader(stream);
                
                // Запускаем поток для чтения ответов
                _readThread = new System.Threading.Thread(ReadTcpMessages);
                _readThread.IsBackground = true;
                _readThread.Start();
                
                _isConnected = true;
                
                Debug.WriteLine($"Connected to Core via TCP at {host}:{port}");

                // Запрашиваем конфигурацию после подключения
                RequestConfig();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error connecting to Core: {ex.Message}");
                _isConnected = false;
            }
        }

        /// <summary>
        /// Запрос конфигурации от Core
        /// </summary>
        private async void RequestConfig()
        {
            try
            {
                // Запрашиваем список профилей
                var profiles = await RequestFromCore("config/listProfiles", null);
                Debug.WriteLine($"Received profiles: {JsonConvert.SerializeObject(profiles)}");

                // Запрашиваем текущую конфигурацию
                _configCache = await RequestFromCore("config/getSerializedProfileInfo", new { profileId = "default" });
                Debug.WriteLine($"Received config: {JsonConvert.SerializeObject(_configCache)}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error requesting config: {ex.Message}");
            }
        }

        /// <summary>
        /// Получение кэшированной конфигурации
        /// </summary>
        /// <returns>Конфигурация</returns>
        public object GetConfig()
        {
            return _configCache;
        }

        /// <summary>
        /// Чтение сообщений из TCP-соединения
        /// </summary>
        private void ReadTcpMessages()
        {
            try
            {
                while (_isConnected && _tcpClient.Connected)
                {
                    string line = _tcpReader.ReadLine();
                    if (string.IsNullOrEmpty(line))
                        continue;

                    // Обрабатываем полученное сообщение
                    ProcessReceivedMessage(line);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error reading from TCP: {ex.Message}");
                _isConnected = false;
                
                // Пробуем переподключиться
                Task.Run(() => {
                    System.Threading.Thread.Sleep(5000); // Ждем 5 секунд перед повторным подключением
                    ConnectToCore();
                });
            }
        }

        /// <summary>
        /// Обработка полученного сообщения
        /// </summary>
        /// <param name="json">JSON-строка с сообщением</param>
        private void ProcessReceivedMessage(string json)
        {
            try
            {
                // Парсим JSON
                var message = JsonConvert.DeserializeObject<Message>(json);
                if (message == null)
                    return;

                Debug.WriteLine($"Received message: {message.MessageType}, ID: {message.MessageId}");

                // Если есть ожидающий запрос, завершаем его
                if (_pendingRequests.TryGetValue(message.MessageId, out var tcs))
                {
                    tcs.SetResult(message.Data);
                    _pendingRequests.Remove(message.MessageId);
                }
                else
                {
                    // Если есть обработчик для данного типа сообщения, вызываем его
                    if (_messageTypeHandlers.TryGetValue(message.MessageType, out var handler))
                    {
                        handler(message);
                    }

                    // Вызываем событие получения сообщения
                    MessageReceived?.Invoke(this, message);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error processing message: {ex.Message}");
            }
        }

        /// <summary>
        /// Отправка сообщения в Core
        /// </summary>
        /// <param name="messageType">Тип сообщения</param>
        /// <param name="data">Данные сообщения</param>
        /// <param name="messageId">Идентификатор сообщения</param>
        public void SendToCore(string messageType, object data, string messageId = null)
        {
            if (!_isConnected || !_tcpClient.Connected)
            {
                Debug.WriteLine("Not connected to Core");
                
                // Пробуем переподключиться
                ConnectToCore();
                
                if (!_isConnected || !_tcpClient.Connected)
                {
                    Debug.WriteLine("Failed to reconnect to Core");
                    return;
                }
            }

            try
            {
                // Создаем сообщение
                var message = new Message
                {
                    MessageId = messageId ?? Guid.NewGuid().ToString(),
                    MessageType = messageType,
                    Data = data
                };

                // Сериализуем сообщение в JSON
                var json = JsonConvert.SerializeObject(message);

                // Отправляем сообщение в Core
                _tcpWriter.WriteLine(json);
                
                Debug.WriteLine($"Sent message to Core: {messageType}, ID: {message.MessageId}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error sending message to Core: {ex.Message}");
                
                // Помечаем соединение как разорванное
                _isConnected = false;
                
                // Пробуем переподключиться
                Task.Run(() => {
                    System.Threading.Thread.Sleep(5000); // Ждем 5 секунд перед повторным подключением
                    ConnectToCore();
                });
            }
        }

        /// <summary>
        /// Отправка запроса в Core и ожидание ответа
        /// </summary>
        /// <param name="messageType">Тип сообщения</param>
        /// <param name="data">Данные сообщения</param>
        /// <returns>Ответ от Core</returns>
        public async Task<object> RequestFromCore(string messageType, object data)
        {
            if (!_isConnected)
            {
                // Пробуем переподключиться
                ConnectToCore();
                if (!_isConnected)
                {
                    throw new InvalidOperationException("Not connected to Core");
                }
            }

            var messageId = Guid.NewGuid().ToString();
            var tcs = new TaskCompletionSource<object>();
            _pendingRequests[messageId] = tcs;

            SendToCore(messageType, data, messageId);

            // Ожидаем ответ с таймаутом
            var timeoutTask = Task.Delay(10000);
            var completedTask = await Task.WhenAny(tcs.Task, timeoutTask);

            if (completedTask == timeoutTask)
            {
                _pendingRequests.Remove(messageId);
                throw new TimeoutException($"Request to Core timed out: {messageType}");
            }

            return await tcs.Task;
        }
    }

    /// <summary>
    /// Класс сообщения
    /// </summary>
    public class Message
    {
        /// <summary>
        /// Идентификатор сообщения
        /// </summary>
        [JsonProperty("messageId")]
        public string MessageId { get; set; }

        /// <summary>
        /// Тип сообщения
        /// </summary>
        [JsonProperty("messageType")]
        public string MessageType { get; set; }

        /// <summary>
        /// Данные сообщения
        /// </summary>
        [JsonProperty("data")]
        public object Data { get; set; }
    }
}
