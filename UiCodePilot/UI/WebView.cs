using System;
using System.Windows.Controls;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using Newtonsoft.Json;
using UiCodePilot.Messenger;
using UiCodePilot.Protocol;

namespace UiCodePilot.UI
{
    /// <summary>
    /// WebView для отображения GUI и обмена сообщениями
    /// </summary>
    public class WebView
    {
        private readonly WebView2 _browser;
        private readonly VS2022Messenger _messenger;
        private bool _isInitialized;

        /// <summary>
        /// Конструктор
        /// </summary>
        /// <param name="browser">Элемент управления WebView2</param>
        /// <param name="messenger">Мессенджер для обмена сообщениями</param>
        public WebView(WebView2 browser, VS2022Messenger messenger)
        {
            _browser = browser ?? throw new ArgumentNullException(nameof(browser));
            _messenger = messenger ?? throw new ArgumentNullException(nameof(messenger));
            
            // Инициализируем WebView2
            InitializeAsync();
        }

        /// <summary>
        /// Асинхронная инициализация WebView2
        /// </summary>
        private async void InitializeAsync()
        {
            try
            {
                // Инициализируем WebView2
                await _browser.EnsureCoreWebView2Async();
                
                // Регистрируем обработчик сообщений от WebView
                _browser.CoreWebView2.WebMessageReceived += OnWebMessageReceived;
                
                // Регистрируем JavaScript-функцию для отправки сообщений в VS2022
                await _browser.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(@"
                    window.postVS2022Message = (messageType, data, messageId) => {
                        window.chrome.webview.postMessage({
                            messageType: messageType,
                            data: data,
                            messageId: messageId
                        });
                    };
                ");
                
                // Устанавливаем флаг инициализации
                _isInitialized = true;
                
                // Отправляем сообщение об инициализации
                await _messenger.SendMessage("webview_initialized", null);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error initializing WebView2: {ex.Message}");
            }
        }

        /// <summary>
        /// Обработчик сообщений от WebView
        /// </summary>
        /// <param name="sender">Отправитель</param>
        /// <param name="e">Аргументы события</param>
        private async void OnWebMessageReceived(object sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            try
            {
                // Получаем сообщение
                var json = e.WebMessageAsJson;
                var message = JsonConvert.DeserializeObject<Message>(json);
                
                if (message == null)
                    return;
                
                // Отправляем сообщение в Core
                await _messenger.SendMessage(message.MessageType, message.Data, message.MessageId);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error handling web message: {ex.Message}");
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
                _browser.CoreWebView2.PostWebMessageAsJson(json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error sending message to webview: {ex.Message}");
            }
        }

        /// <summary>
        /// Открытие инструментов разработчика
        /// </summary>
        public void OpenDevtools()
        {
            if (!_isInitialized)
                return;
            
            try
            {
                _browser.CoreWebView2.OpenDevToolsWindow();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error opening devtools: {ex.Message}");
            }
        }

        /// <summary>
        /// Навигация по URL
        /// </summary>
        /// <param name="url">URL</param>
        public void Navigate(string url)
        {
            if (!_isInitialized)
                return;
            
            try
            {
                _browser.CoreWebView2.Navigate(url);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error navigating to URL: {ex.Message}");
            }
        }
    }
}
