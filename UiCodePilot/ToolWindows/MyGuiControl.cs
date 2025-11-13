
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows.Controls;
using UiCodePilot.UI;
using EnvDTE;

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
                case "getControlPlaneSessionInfo":
                    HandleGetControlPlaneSessionInfo(message);
                    break;
                case "getIdeSettings":
                    HandleGetIdeSettings(message);
                    break;
                case "config/listProfiles":
                    HandleListProfiles(message);
                    break;
                case "getOpenFiles":
                    HandleGetOpenFiles(message);
                    break;
                default:
                    // Для других типов сообщений можно добавить соответствующую обработку
                    System.Diagnostics.Debug.WriteLine($"Received message of type: {message.MessageType}");
                    break;
            }
        }

        /// <summary>
        /// Обработка запроса информации о сессии Control Plane
        /// </summary>
        /// <param name="message">Сообщение</param>
        private void HandleGetControlPlaneSessionInfo(Message message)
        {
            try
            {
                // Создаем объект с информацией о сессии Control Plane
                var sessionInfo = new
                {
                    isLoggedIn = false,
                    username = "",
                    email = "",
                    organizations = new object[] { }
                };

                // Отправляем ответ
                SendToWebview(message.MessageType, sessionInfo, message.MessageId);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error handling getControlPlaneSessionInfo: {ex.Message}");
                SendToWebview(message.MessageType, new { error = ex.Message }, message.MessageId);
            }
        }

        /// <summary>
        /// Обработка запроса настроек IDE
        /// </summary>
        /// <param name="message">Сообщение</param>
        private void HandleGetIdeSettings(Message message)
        {
            try
            {
                // Создаем объект с настройками IDE
                var ideSettings = new
                {
                    remoteConfigServerUrl = "",
                    remoteConfigSyncPeriod = 0,
                    userToken = "",
                    enableControlServerBeta = false,
                    pauseCodebaseIndexOnStart = false,
                    continueTestEnvironment = "none"
                };

                // Отправляем ответ
                SendToWebview(message.MessageType, ideSettings, message.MessageId);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error handling getIdeSettings: {ex.Message}");
                SendToWebview(message.MessageType, new { error = ex.Message }, message.MessageId);
            }
        }

        /// <summary>
        /// Обработка запроса списка профилей
        /// </summary>
        /// <param name="message">Сообщение</param>
        private void HandleListProfiles(Message message)
        {
            try
            {
                // Создаем объект со списком профилей
                var profiles = new object[] { };

                // Отправляем ответ
                SendToWebview(message.MessageType, profiles, message.MessageId);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error handling config/listProfiles: {ex.Message}");
                SendToWebview(message.MessageType, new { error = ex.Message }, message.MessageId);
            }
        }

        /// <summary>
        /// Обработка запроса списка открытых файлов
        /// </summary>
        /// <param name="message">Сообщение</param>
        private void HandleGetOpenFiles(Message message)
        {
            try
            {
                // Получаем список открытых файлов
                var openFiles = GetOpenFiles();

                // Отправляем ответ
                SendToWebview(message.MessageType, openFiles, message.MessageId);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error handling getOpenFiles: {ex.Message}");
                SendToWebview(message.MessageType, new { error = ex.Message }, message.MessageId);
            }
        }

        /// <summary>
        /// Получение списка открытых файлов
        /// </summary>
        /// <returns>Список открытых файлов</returns>
        private string[] GetOpenFiles()
        {
            try
            {
                // Получаем DTE (Development Tools Environment)
                var dte = Microsoft.VisualStudio.Shell.Package.GetGlobalService(typeof(EnvDTE.DTE)) as EnvDTE.DTE;
                if (dte == null)
                    return new string[0];

                // Получаем список открытых документов
                var documents = dte.Documents;
                if (documents == null)
                    return new string[0];

                // Создаем список путей к открытым файлам
                var openFiles = new List<string>();
                foreach (EnvDTE.Document document in documents)
                {
                    if (document != null && !string.IsNullOrEmpty(document.FullName))
                    {
                        // Преобразуем путь к файлу в URI
                        var fileUri = new Uri(document.FullName).ToString();
                        openFiles.Add(fileUri);
                    }
                }

                return openFiles.ToArray();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting open files: {ex.Message}");
                return new string[0];
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
