using Newtonsoft.Json;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace MyGui
{
public partial class MyGuiControl
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

        /// <summary>
        /// Конструктор
        /// </summary>
        public CoreMessenger()
        {
            ConnectToCore();
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
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error connecting to Core: {ex.Message}");
                _isConnected = false;
            }
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

                // Если есть ожидающий запрос, завершаем его
                if (_pendingRequests.TryGetValue(message.MessageId, out var tcs))
                {
                    tcs.SetResult(message.Data);
                    _pendingRequests.Remove(message.MessageId);
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
                Debug.WriteLine($"RequestFromCore 7");
                if (!_isConnected || !_tcpClient.Connected)
            {
                Debug.WriteLine("Not connected to Core");
                return;
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

                    Debug.WriteLine($"RequestFromCore 8");

                    // Отправляем сообщение в Core
                    _tcpWriter.WriteLine(json);
                    Debug.WriteLine($"RequestFromCore 9");
                }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error sending message to Core: {ex.Message}");
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
                Debug.WriteLine($"RequestFromCore 1");
                if (!_isConnected)
            {
                    Debug.WriteLine($"RequestFromCore 2");
                    // Пробуем переподключиться
                    ConnectToCore();
                if (!_isConnected)
                {
                        Debug.WriteLine($"RequestFromCore 3");
                        throw new InvalidOperationException("Not connected to Core");
                }
            }

                Debug.WriteLine($"RequestFromCore 4");
                var messageId = Guid.NewGuid().ToString();
                Debug.WriteLine($"RequestFromCore 5");
                var tcs = new TaskCompletionSource<object>();
            _pendingRequests[messageId] = tcs;
                Debug.WriteLine($"RequestFromCore 6");

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
    }
}
