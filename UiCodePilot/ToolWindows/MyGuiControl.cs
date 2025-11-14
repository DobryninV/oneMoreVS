
using EnvDTE;
using EnvDTE80;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
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
        private CoreMessenger _coreMessenger;

        public MyGuiControl()
        {
            Debug.WriteLine("Debug Start");
            InitializeWebView();
            InitializeCore();
        }

        /// <summary>
        /// Инициализация Core
        /// </summary>
        private void InitializeCore()
        {
            try
            {
                // Создаем экземпляр CoreMessenger для обмена сообщениями с Core
                _coreMessenger = new CoreMessenger();
                Debug.WriteLine("Core initialized");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error initializing Core: {ex.Message}");
            }
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
                _webView.CoreWebView2.Settings.IsWebMessageEnabled = true;

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
                    //localStorage.setItem('ide', 'vs2022');
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
            Debug.WriteLine($"$ {sender.ToString()}");
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
            Debug.WriteLine($"!!!! Message {message.MessageType} {message.Data}");
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
                case "config/openProfile":
                    HandleOpenProfile(message);
                    break;
                case "config/getSerializedProfileInfo":
                    HandleGetSerializedProfileInfo(message);
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
        private async void HandleGetControlPlaneSessionInfo(Message message)
        {
            try
            {
                if (_coreMessenger != null)
                {
                    // Получаем информацию о сессии Control Plane из Core
                    var result = await _coreMessenger.RequestFromCore("getControlPlaneSessionInfo", message.Data);
                    
                    // Отправляем ответ
                    SendToWebview(message.MessageType, result, message.MessageId);
                }
                else
                {
                    // Если Core не инициализирован, возвращаем заглушку
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
        private async void HandleGetIdeSettings(Message message)
        {
            try
            {
                if (_coreMessenger != null)
                {
                    // Получаем настройки IDE из Core
                    var result = await _coreMessenger.RequestFromCore("getIdeSettings", message.Data);
                    
                    // Отправляем ответ
                    SendToWebview(message.MessageType, result, message.MessageId);
                }
                else
                {
                    // Если Core не инициализирован, возвращаем заглушку
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
        private async void HandleListProfiles(Message message)
        {
            try
            {
                if (_coreMessenger != null)
                {
                    // Получаем список профилей из Core
                    var result = await _coreMessenger.RequestFromCore("config/listProfiles", message.Data);
                    
                    // Отправляем ответ
                    SendToWebview(message.MessageType, result, message.MessageId);
                }
                else
                {
                    // Если Core не инициализирован, возвращаем заглушку
                    var profiles = new object[] { };

                    // Отправляем ответ
                    SendToWebview(message.MessageType, profiles, message.MessageId);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error handling config/listProfiles: {ex.Message}");
                SendToWebview(message.MessageType, new { error = ex.Message }, message.MessageId);
            }
        }

        /// <summary>
        /// Обработка запроса открытия профиля
        /// </summary>
        /// <param name="message">Сообщение</param>
        private async void HandleOpenProfile(Message message)
        {
            try
            {
                if (_coreMessenger != null)
                {
                    // Получаем результат открытия профиля из Core
                    var result = await _coreMessenger.RequestFromCore("config/openProfile", message.Data);
                    
                    // Отправляем ответ
                    SendToWebview(message.MessageType, result, message.MessageId);
                }
                else
                {
                    // Если Core не инициализирован, возвращаем заглушку
                    var result = new
                    {
                        success = true
                    };

                    // Отправляем ответ
                    SendToWebview(message.MessageType, result, message.MessageId);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error handling config/openProfile: {ex.Message}");
                SendToWebview(message.MessageType, new { error = ex.Message }, message.MessageId);
            }
        }

        /// <summary>
        /// Обработка запроса сериализованной информации о профиле
        /// </summary>
        /// <param name="message">Сообщение</param>
        private async void HandleGetSerializedProfileInfo(Message message)
        {
            try
            {
                if (_coreMessenger != null)
                {
                    // Получаем информацию о профиле из Core
                    var result = await _coreMessenger.RequestFromCore("config/getSerializedProfileInfo", message.Data);
                    
                    // Отправляем ответ
                    SendToWebview(message.MessageType, result, message.MessageId);
                }
                else
                {
                    // Если Core не инициализирован, возвращаем заглушку
                    dynamic requestData = message.Data;
                    string profileId = requestData?.profileId?.ToString();

                    // Создаем объект с информацией о профиле
                    var profileInfo = new
                    {
                        id = profileId ?? "default",
                        name = "Default Profile",
                        description = "Default profile for VS2022",
                        profileType = "local",
                        config = new
                        {
                            models = new object[] { },
                            ui = new
                            {
                                showSessionTabs = true
                            }
                        }
                    };

                    // Отправляем ответ
                    SendToWebview(message.MessageType, profileInfo, message.MessageId);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error handling config/getSerializedProfileInfo: {ex.Message}");
                SendToWebview(message.MessageType, new { error = ex.Message }, message.MessageId);
            }
        }

        /// <summary>
        /// Обработка запроса списка открытых файлов
        /// </summary>
        /// <param name="message">Сообщение</param>
        private async void HandleGetOpenFiles(Message message)
        {
            try
            {
                if (_coreMessenger != null)
                {
                    // Сначала пробуем получить список открытых файлов из Core
                    try
                    {
                        var result = await _coreMessenger.RequestFromCore("getOpenFiles", message.Data);
                        
                        // Отправляем ответ
                        SendToWebview(message.MessageType, result, message.MessageId);
                        return;
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error getting open files from Core: {ex.Message}. Falling back to local implementation.");
                    }
                }
                
                // Если Core не инициализирован или произошла ошибка, используем локальную реализацию
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

                Debug.WriteLine($"PostWebMessageAsJson: {json}");

                //_webView.CoreWebView2.PostWebMessag

                // Отправляем сообщение в WebView
                _webView.CoreWebView2.PostWebMessageAsJson(json);
                // window.postMessage("message", {"messageId":"2cc027ff-401b-4e2c-aa0c-4f02f1bd4bf3","messageType":"getOpenFiles","data":["file:///C:/Users/harit/source/repos/CodePilot/CodePilot/CodePilotPackage.cs"]})

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

    /// <summary>
    /// Класс для обмена сообщениями с Core
    /// </summary>
    public class CoreMessenger
    {
        private System.Diagnostics.ProcessStartInfo _startProps;
        private System.Diagnostics.Process _coreProcess;
        private Dictionary<string, TaskCompletionSource<object>> _pendingRequests = new Dictionary<string, TaskCompletionSource<object>>();

        /// <summary>
        /// Конструктор
        /// </summary>
        public CoreMessenger()
        {
            StartCoreProcess();
        }

        /// <summary>
        /// Запуск процесса Core
        /// </summary>
        private void StartCoreProcess()
        {
            try
            {

                Debug.WriteLine($"!!!!!!!!!!!! SGewnmdsf {System.Reflection.Assembly.GetExecutingAssembly().Location}");
                // Путь к исполняемому файлу Core
                string corePath = System.IO.Path.Combine(
                    "D:/Prog/CodePilotVS/binary/bin/win32 - x64/continue-binary.exe",
                    "win32 - x64",
                    "continue-binary.exe");

                // Создаем процесс
                _startProps = new System.Diagnostics.ProcessStartInfo();
                _startProps.FileName = "D:\\Prog\\CodePilotVS\\binary\\bin\\win32-x64\\continue-binary.exe";
                Debug.WriteLine($"!!!!!!!!!!!! _coreProcess StartInfo");
                
                // Устанавливаем рабочую директорию для процесса Core
                _startProps.WorkingDirectory = "D:\\Prog\\CodePilotVS";
                Debug.WriteLine($"Working directory: {_startProps.WorkingDirectory}");
                
                // Настраиваем процесс
                _startProps.UseShellExecute = false;
                _startProps.RedirectStandardInput = true;
                _startProps.RedirectStandardOutput = true;
                _startProps.RedirectStandardError = true; // Перенаправляем поток ошибок
                _startProps.CreateNoWindow = true;
                
                // Добавляем переменные окружения, которые могут быть необходимы для работы Core
                _startProps.EnvironmentVariables["NODE_ENV"] = "production";
                _startProps.EnvironmentVariables["CONTINUE_BINARY_PATH"] = "D:\\Prog\\CodePilotVS\\binary\\bin\\win32-x64\\continue-binary.exe";

                Debug.WriteLine($"!!!!!!!!!!!! _coreProcess OnCoreOutputDataReceived");
                // Обработчик вывода
                

                Debug.WriteLine($"!!!!!!!!!!!! _coreProcess Start");
                // Запускаем процесс
                _coreProcess = System.Diagnostics.Process.Start(_startProps);
                _coreProcess.OutputDataReceived += OnCoreOutputDataReceived;
                _coreProcess.ErrorDataReceived += OnCoreErrorDataReceived; // Добавляем обработчик ошибок
                Debug.WriteLine($"!!!!!!!!!!!! _coreProcess BeginOutputReadLine");
                _coreProcess.BeginOutputReadLine();
                _coreProcess.BeginErrorReadLine(); // Начинаем асинхронное чтение потока ошибок

                Debug.WriteLine("Core process started");
                _coreProcess.Exited += OnExited;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error starting Core process: {ex.Message}");
            }
        }

        private void OnExited(object lol, EventArgs e)
        {
            Debug.WriteLine($"!!!!!! Exited !!!! {lol} !!!! {e}");
        }

        /// <summary>
        /// Обработчик ошибок Core
        /// </summary>
        /// <param name="sender">Отправитель</param>
        /// <param name="e">Аргументы события</param>
        private void OnCoreErrorDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (string.IsNullOrEmpty(e.Data))
                return;

            Debug.WriteLine($"Core ERROR: {e.Data}");
        }

        /// <summary>
        /// Обработчик вывода Core
        /// </summary>
        /// <param name="sender">Отправитель</param>
        /// <param name="e">Аргументы события</param>
        private void OnCoreOutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (string.IsNullOrEmpty(e.Data))
                return;

            // Выводим все сообщения для отладки
            Debug.WriteLine($"Core OUTPUT: {e.Data}");

            try
            {
                // Парсим JSON
                var message = JsonConvert.DeserializeObject<Message>(e.Data);
                if (message == null)
                {
                    Debug.WriteLine("Failed to parse message as JSON");
                    return;
                }

                Debug.WriteLine($"Received message from Core: {message.MessageType} (ID: {message.MessageId})");

                // Если есть ожидающий запрос, завершаем его
                if (_pendingRequests.TryGetValue(message.MessageId, out var tcs))
                {
                    Debug.WriteLine($"Completing request: {message.MessageId}");
                    tcs.SetResult(message.Data);
                    _pendingRequests.Remove(message.MessageId);
                }
                else
                {
                    Debug.WriteLine($"No pending request found for message ID: {message.MessageId}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error handling Core output: {ex.Message}");
                Debug.WriteLine($"Raw output: {e.Data}");
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
            if (_coreProcess == null)
            {
                Debug.WriteLine("Core process is null, trying to restart...");
                StartCoreProcess();
                if (_coreProcess == null)
                {
                    Debug.WriteLine("Failed to restart Core process");
                    return;
                }
            }
            
            if (_coreProcess.HasExited)
            {
                Debug.WriteLine($"Core process has exited with code {_coreProcess.ExitCode}, trying to restart...");
                StartCoreProcess();
                if (_coreProcess == null || _coreProcess.HasExited)
                {
                    Debug.WriteLine("Failed to restart Core process");
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
                
                Debug.WriteLine($"Sending to Core: {messageType} (ID: {message.MessageId})");

                // Отправляем сообщение в Core
                _coreProcess.StandardInput.WriteLine(json);
                _coreProcess.StandardInput.Flush();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error sending message to Core: {ex.Message}");
                Debug.WriteLine($"Stack trace: {ex.StackTrace}");
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
}
