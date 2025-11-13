
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using Newtonsoft.Json;
using System;
using System.Diagnostics;
using System.Windows.Controls;
using UiCodePilot.UI;

namespace MyGui
{
    /// <summary>
    /// GUI контрол с WebView2 для отображения веб-интерфейса
    /// </summary>
    public class MyGuiControl : UserControl
    {
        private WebView2 _webView;
        private WebView _webViewHandler;
        private bool _isInitialized;

        public MyGuiControl()
        {
            Debug.WriteLine("Debug Start");
            InitializeWebView();
        }


        private async void InitializeWebView()
        {
            try
            {
                // Создаем WebView2
                _webView = new WebView2();
                this.Content = _webView;

                // Настраиваем папку для пользовательских данных
                string userDataFolder = System.IO.Path.Combine(
                    System.IO.Path.GetTempPath(),
                "MyVsExtention",
                    "WebView2Data");

                // Создаем окружение WebView2
                var environment = await CoreWebView2Environment.CreateAsync(
                    browserExecutableFolder: null,
                    userDataFolder: userDataFolder
                );

                // Инициализируем WebView2
                await _webView.EnsureCoreWebView2Async(environment);

                Debug.WriteLine("WebMessageReceived");
                // Регистрируем обработчик сообщений от WebView
                _webView.CoreWebView2.WebMessageReceived += OnWebMessageReceived;

                Debug.WriteLine("AddScriptToExecuteOnDocumentCreatedAsync");
                // Регистрируем JavaScript-функцию для отправки сообщений в VS2022
                await _webView.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(@"
                    window.postVS2022Message = (messageType, data, messageId) => {
                        window.chrome.webview.postMessage({
                            messageType: messageType,
                            data: data,
                            messageId: messageId
                        });
                    };

                    // Устанавливаем флаг IDE
                    localStorage.setItem('ide', 'vs2022');
                ");

                Debug.WriteLine("_isInitialized");

                // Устанавливаем флаг инициализации
                _isInitialized = true;

                // Загружаем GUI с localhost
                _webView.Source = new Uri("http://localhost:5173/");

                Debug.WriteLine("and _webViewHandler");
                // Создаем обработчик WebView
                //_webViewHandler = new WebView(_webView, null);
                Debug.WriteLine("and constructor");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error initializing WebView2: {ex.Message}");
                await VS.MessageBox.ShowWarningAsync("UiCodePilot", $"Ошибка инициализации WebView2: {ex.Message}");
            }
        }

        /// <summary>
        /// Обработчик сообщений от WebView
        /// </summary>
        /// <param name="sender">Отправитель</param>
        /// <param name="e">Аргументы события</param>
        private void OnWebMessageReceived(object sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            try
            {
                // Получаем сообщение
                var json = e.WebMessageAsJson;
                var message = JsonConvert.DeserializeObject<Message>(json);

                if (message == null)
                    return;

                // Обрабатываем сообщение
                HandleMessage(message);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error handling web message: {ex.Message}");
            }
        }

        /// <summary>
        /// Обработка сообщения от WebView
        /// </summary>
        /// <param name="message">Сообщение</param>
        private void HandleMessage(Message message)
        {
            // Здесь можно добавить обработку различных типов сообщений
            switch (message.MessageType)
            {
                case "ping":
                    SendToWebview("pong", null, message.MessageId);
                    break;
                default:
                    // Для других типов сообщений можно добавить соответствующую обработку
                    Debug.WriteLine($"Received message of type: {message.MessageType}");
                    break;
            }
        }

        /// <summary>
        /// Отправка сообщения в WebView
        /// </summary>
        /// <param name="messageType">Тип сообщения</param>
        /// <param name="data">Данные сообщения</param>
        /// <param name="messageId">Идентификатор сообщения</param>
        public void SendToWebview(string messageType, object data, string messageId)
        {
            if (!_isInitialized)
                return;

            try
            {
                // Создаем сообщение
                var message = new Message
                {
                    MessageId = messageId,
                    MessageType = messageType,
                    Data = data
                };

                // Сериализуем сообщение в JSON
                var json = JsonConvert.SerializeObject(message);

                // Отправляем сообщение в WebView
                _webView.CoreWebView2.PostWebMessageAsJson(json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error sending message to webview: {ex.Message}");
            }
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
