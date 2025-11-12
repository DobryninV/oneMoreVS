Нужно реализовать плагин такой же но для VS
# Создание расширения для Visual Studio, интегрированного с существующим core компонентом

Для создания расширения для Visual Studio, которое будет работать с существующим core компонентом (написанным на TypeScript), необходимо выполнить следующие шаги:

## 1. Архитектурный подход

Поскольку расширения для Visual Studio разрабатываются на C#, а core компонент написан на TypeScript, необходимо использовать межпроцессное взаимодействие, аналогично подходу, используемому для IntelliJ. Основные варианты:

1. **Запуск core как отдельного процесса** с обменом сообщениями через:
   - Стандартные потоки ввода/вывода (stdin/stdout)
   - TCP-соединение
   - Named Pipes (предпочтительно для Windows)

## 2. Шаги по созданию расширения

### 2.1. Настройка среды разработки

1. **Установите Visual Studio** с поддержкой разработки расширений:
   - Visual Studio 2022 (рекомендуется)
   - Установите рабочую нагрузку "Разработка расширений для Visual Studio"

2. **Установите SDK для разработки расширений**:
   - Visual Studio SDK
   - .NET Framework (последняя версия)

### 2.2. Создание проекта расширения

1. **Создайте новый проект расширения**:
   - В Visual Studio выберите "Создать новый проект"
   - Выберите шаблон "VSIX Project" (Visual Studio Extension)
   - Назовите проект (например, "ContinueVS")

2. **Добавьте необходимые ссылки**:
   - Microsoft.VisualStudio.Shell.xx.0
   - Microsoft.VisualStudio.Shell.Interop.xx.0
   - System.ComponentModel.Composition

### 2.3. Реализация взаимодействия с core

#### Создание менеджера процессов для core

```csharp
public class CoreProcessManager
{
    private Process _coreProcess;
    private StreamWriter _processInput;
    private StreamReader _processOutput;
    private readonly string _corePath;
    
    public CoreProcessManager(string corePath)
    {
        _corePath = corePath;
    }
    
    public void StartCoreProcess()
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = _corePath,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        
        _coreProcess = new Process { StartInfo = startInfo };
        _coreProcess.Start();
        
        _processInput = _coreProcess.StandardInput;
        _processOutput = _coreProcess.StandardOutput;
        
        // Запустите асинхронное чтение вывода
        Task.Run(() => ReadOutputAsync());
    }
    
    private async Task ReadOutputAsync()
    {
        string line;
        while ((line = await _processOutput.ReadLineAsync()) != null)
        {
            if (!string.IsNullOrEmpty(line))
            {
                ProcessMessage(line);
            }
        }
    }
    
    private void ProcessMessage(string message)
    {
        // Десериализация JSON сообщения
        var jsonMessage = JsonConvert.DeserializeObject<dynamic>(message);
        string messageType = jsonMessage.messageType;
        string messageId = jsonMessage.messageId;
        
        // Обработка сообщения в зависимости от типа
        // ...
    }
    
    public async Task<string> SendRequestAsync(string messageType, object data)
    {
        string messageId = Guid.NewGuid().ToString();
        var message = new
        {
            messageType,
            messageId,
            data
        };
        
        string jsonMessage = JsonConvert.SerializeObject(message);
        await _processInput.WriteLineAsync(jsonMessage);
        await _processInput.FlushAsync();
        
        // Ожидание ответа с соответствующим messageId
        // ...
        
        return result;
    }
}
```

#### Реализация интерфейса IDE

```csharp
public class VisualStudioIde : IIde
{
    private readonly DTE2 _dte;
    
    public VisualStudioIde(DTE2 dte)
    {
        _dte = dte;
    }
    
    public async Task<string> ReadFile(string filepath)
    {
        // Реализация чтения файла в VS
        // ...
    }
    
    public async Task WriteFile(string path, string contents)
    {
        // Реализация записи в файл в VS
        // ...
    }
    
    public async Task OpenFile(string path)
    {
        // Открытие файла в редакторе VS
        // ...
    }
    
    // Реализация других методов интерфейса IDE
    // ...
}
```

### 2.4. Сборка и упаковка core компонента

1. **Соберите core компонент**:
   - Используйте скрипт, аналогичный `build_intellij.sh`, но адаптированный для Windows
   - Убедитесь, что core компонент собран и готов к использованию

2. **Включите собранный core в расширение**:
   - Добавьте собранные файлы core как ресурсы проекта
   - Настройте копирование этих файлов в выходную директорию при сборке

```xml
<!-- В .csproj файле -->
<ItemGroup>
  <Content Include="core\**">
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
  </Content>
</ItemGroup>
```

### 2.5. Инициализация расширения

```csharp
[PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
[ProvideMenuResource("Menus.ctmenu", 1)]
[Guid(ContinueVSPackage.PackageGuidString)]
public sealed class ContinueVSPackage : AsyncPackage
{
    public const string PackageGuidString = "12345678-1234-1234-1234-123456789012";
    
    private CoreProcessManager _coreManager;
    private VisualStudioIde _vsIde;
    
    protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
    {
        await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
        
        // Получение DTE сервиса
        var dte = await GetServiceAsync(typeof(DTE)) as DTE2;
        
        // Инициализация IDE интерфейса
        _vsIde = new VisualStudioIde(dte);
        
        // Определение пути к core компоненту
        string extensionDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        string corePath = Path.Combine(extensionDirectory, "core", "continue-core.exe");
        
        // Инициализация и запуск core процесса
        _coreManager = new CoreProcessManager(corePath);
        _coreManager.StartCoreProcess();
        
        // Регистрация команд меню
        await ContinueVSCommand.InitializeAsync(this);
    }
}
```

### 2.6. Реализация команд и UI

1. **Создайте команды для взаимодействия с core**:
   - Команда для открытия чата
   - Команда для включения/отключения автодополнения
   - Другие необходимые команды

2. **Реализуйте UI компоненты**:
   - Панель инструментов
   - Окно чата
   - Визуализация автодополнения в редакторе

```csharp
[Guid("12345678-1234-1234-1234-123456789013")]
public class ContinueChatToolWindow : ToolWindowPane
{
    public ContinueChatToolWindow() : base(null)
    {
        Caption = "Continue Chat";
        
        // Создание контента окна (например, WebView для отображения GUI)
        Content = new WebBrowser();
    }
}
```

### 2.7. Тестирование и отладка

1. **Запустите экспериментальный экземпляр Visual Studio**:
   - Нажмите F5 в проекте расширения
   - Visual Studio запустит новый экземпляр с загруженным расширением

2. **Отладка взаимодействия с core**:
   - Добавьте логирование в обе части (C# и core)
   - Используйте отладчик Visual Studio для C# кода
   - Проверьте корректность обмена сообщениями

### 2.8. Упаковка и публикация

1. **Создайте VSIX пакет**:
   - Правый клик на проект -> Собрать
   - VSIX файл будет создан в выходной директории

2. **Опубликуйте расширение**:
   - Загрузите VSIX в Visual Studio Marketplace
   - Или распространяйте VSIX файл напрямую

## 3. Особенности реализации

### 3.1. Обработка сообщений

Для обработки сообщений от core можно использовать подход, аналогичный IntelliJ:

```csharp
private void HandleMessage(string jsonMessage)
{
    var message = JsonConvert.DeserializeObject<dynamic>(jsonMessage);
    string messageType = message.messageType;
    string messageId = message.messageId;
    dynamic data = message.data;
    
    switch (messageType)
    {
        case "readFile":
            string filepath = data.filepath;
            string content = _vsIde.ReadFile(filepath).Result;
            SendResponse(messageId, messageType, content);
            break;
            
        case "writeFile":
            string path = data.path;
            string contents = data.contents;
            _vsIde.WriteFile(path, contents).Wait();
            SendResponse(messageId, messageType, null);
            break;
            
        // Обработка других типов сообщений
    }
}

private void SendResponse(string messageId, string messageType, object data)
{
    var response = new
    {
        messageId,
        messageType,
        data = new
        {
            done = true,
            content = data,
            status = "success"
        }
    };
    
    string jsonResponse = JsonConvert.SerializeObject(response);
    _processInput.WriteLineAsync(jsonResponse).Wait();
    _processInput.FlushAsync().Wait();
}
```

### 3.2. Автодополнение кода

Для реализации автодополнения кода в Visual Studio:

```csharp
[Export(typeof(ICompletionSource))]
[ContentType("code")]
[Name("ContinueCompletionSource")]
public class ContinueCompletionSource : ICompletionSource
{
    private readonly CoreProcessManager _coreManager;
    
    [ImportingConstructor]
    public ContinueCompletionSource([Import] CoreProcessManager coreManager)
    {
        _coreManager = coreManager;
    }
    
    public void AugmentCompletionSession(ICompletionSession session, IList<CompletionSet> completionSets)
    {
        // Получение текущего документа и позиции курсора
        ITextView textView = session.TextView;
        ITextSnapshot snapshot = textView.TextSnapshot;
        SnapshotPoint triggerPoint = session.GetTriggerPoint(snapshot).Value;
        
        // Получение контекста для автодополнения
        string documentText = snapshot.GetText();
        int position = triggerPoint.Position;
        
        // Запрос автодополнения от core
        var completionRequest = new
        {
            document = documentText,
            position = position,
            // Другие параметры
        };
        
        string completionResult = _coreManager.SendRequestAsync("autocomplete/complete", completionRequest).Result;
        
        // Создание и добавление предложений автодополнения
        // ...
    }
    
    // Другие методы интерфейса
}
```

## 4. Заключение

Создание расширения для Visual Studio, интегрированного с существующим core компонентом, требует реализации межпроцессного взаимодействия между C# кодом расширения и TypeScript кодом core. Ключевые моменты:

1. **Используйте межпроцессное взаимодействие** через stdin/stdout, TCP или Named Pipes
2. **Реализуйте интерфейс IDE** для Visual Studio, аналогичный существующим реализациям для VSCode и IntelliJ
3. **Обеспечьте сериализацию/десериализацию сообщений** в том же формате, что используется в существующих расширениях
4. **Интегрируйте UI компоненты** с Visual Studio, используя стандартные механизмы расширений VS

Этот подход позволит повторно использовать существующий core компонент и обеспечить согласованное поведение расширения во всех поддерживаемых IDE.

# Доработка TypeScript кода core и gui для работы с Visual Studio

Да, для обеспечения работы с Visual Studio потребуются определенные доработки в TypeScript коде core и gui компонентов. Вот необходимые изменения:

## 1. Доработки в core компоненте

### 1.1. Поддержка нового протокола взаимодействия

Необходимо добавить поддержку взаимодействия с Visual Studio через межпроцессное взаимодействие:

```typescript
// Добавление нового типа мессенджера для Visual Studio
export class VisualStudioMessenger<
  ToProtocol extends IProtocol,
  FromProtocol extends IProtocol
> extends IPCMessengerBase<ToProtocol, FromProtocol> {
  // Реализация, аналогичная IpcMessenger, но с учетом особенностей VS
  // ...
}
```

### 1.2. Расширение интерфейса IDE

Необходимо расширить интерфейс IDE для поддержки специфичных для Visual Studio функций:

```typescript
// В protocol/ide.ts
export interface IDE {
  // Существующие методы...
  
  // Методы, специфичные для Visual Studio
  getVisualStudioVersion?(): Promise<string>;
  getVisualStudioSettings?(): Promise<any>;
  // Другие методы, необходимые для VS
}
```

### 1.3. Адаптация для Windows

Поскольку Visual Studio работает преимущественно на Windows, необходимо убедиться, что core корректно работает в этой среде:

```typescript
// Проверка и адаптация путей для Windows
export function normalizePath(path: string): string {
  if (process.platform === 'win32') {
    // Обработка путей для Windows
    return path.replace(/\\/g, '/');
  }
  return path;
}
```

### 1.4. Модификация системы запуска

Необходимо модифицировать систему запуска core для поддержки запуска из Visual Studio:

```typescript
// В binary/src/index.ts
if (process.env.VISUAL_STUDIO_INTEGRATION === "true") {
  // Настройка для интеграции с Visual Studio
  setupCoreLogging();
  messenger = new VisualStudioMessenger<ToCoreProtocol, FromCoreProtocol>();
} else if (process.env.CONTINUE_DEVELOPMENT === "true") {
  // Существующий код для режима разработки
  messenger = new TcpMessenger<ToCoreProtocol, FromCoreProtocol>();
  // ...
} else {
  // Существующий код для обычного режима
  setupCoreLogging();
  messenger = new IpcMessenger<ToCoreProtocol, FromCoreProtocol>();
}
```

## 2. Доработки в gui компоненте

### 2.1. Адаптация WebView для Visual Studio

Visual Studio имеет свою реализацию WebView, поэтому необходимо адаптировать gui компонент:

```typescript
// В gui/src/utils/platform.ts
export enum Platform {
  VSCode,
  IntelliJ,
  VisualStudio,
  // ...
}

export function getCurrentPlatform(): Platform {
  // Определение текущей платформы
  if (window.vsCodeApi) {
    return Platform.VSCode;
  } else if (window.intellijApi) {
    return Platform.IntelliJ;
  } else if (window.visualStudioApi) {
    return Platform.VisualStudio;
  }
  // ...
}

// Адаптер для Visual Studio
export function createPlatformAdapter(platform: Platform) {
  switch (platform) {
    case Platform.VSCode:
      return new VSCodeAdapter();
    case Platform.IntelliJ:
      return new IntelliJAdapter();
    case Platform.VisualStudio:
      return new VisualStudioAdapter();
    // ...
  }
}
```

### 2.2. Создание адаптера для Visual Studio

```typescript
// В gui/src/adapters/visualStudioAdapter.ts
export class VisualStudioAdapter implements IPlatformAdapter {
  postMessage(message: any): void {
    // Отправка сообщения в Visual Studio
    window.visualStudioApi.postMessage(message);
  }
  
  onMessage(callback: (message: any) => void): void {
    // Обработка сообщений от Visual Studio
    window.addEventListener('message', (event) => {
      callback(event.data);
    });
  }
  
  // Другие методы адаптера
}
```

### 2.3. Адаптация стилей для Visual Studio

```css
/* В gui/src/styles/platforms/visualStudio.css */
.vs-theme {
  /* Стили, соответствующие Visual Studio */
  --background-color: #1E1E1E;
  --text-color: #D4D4D4;
  /* Другие переменные стилей */
}
```

## 3. Создание скрипта сборки для Visual Studio

Необходимо создать скрипт сборки, аналогичный `build_intellij.sh` и `build_vscode.sh`, но адаптированный для Visual Studio:

```powershell
# build_visualstudio.ps1
param (
    [string]$Platform = "win32",
    [string]$Arch = "x64"
)

$Target = "${Platform}-${Arch}"

# Установка переменных окружения
$env:VISUAL_STUDIO_INTEGRATION = "true"

# Сборка core
Write-Host "Installing core Node.js dependencies"
Push-Location core
npm ci
Pop-Location

# Сборка GUI
Write-Host "Installing and building GUI Node.js dependencies"
Push-Location gui
npm ci --prefer-offline
npm run build
Pop-Location

# Сборка binary
Write-Host "Installing and building Binary Node.js dependencies"
Push-Location binary
npm ci --prefer-offline
npm run build
Pop-Location

# Копирование файлов в директорию расширения Visual Studio
$VSExtensionDir = "extensions/visualstudio"
if (-not (Test-Path $VSExtensionDir)) {
    New-Item -ItemType Directory -Path $VSExtensionDir
}

# Копирование собранных файлов
Copy-Item -Path "binary/dist" -Destination "$VSExtensionDir/core" -Recurse -Force
Copy-Item -Path "gui/dist" -Destination "$VSExtensionDir/gui" -Recurse -Force

Write-Host "Build completed successfully. Files are ready for Visual Studio extension."
```

## 4. Необходимые изменения в протоколе

### 4.1. Добавление специфичных для Visual Studio сообщений

```typescript
// В core/protocol/index.ts
export type ToIdeProtocol = {
  // Существующие типы сообщений...
  
  // Типы сообщений, специфичные для Visual Studio
  "visualStudio/getVersion": [undefined, string];
  "visualStudio/getSettings": [undefined, any];
  // Другие типы сообщений
};
```

### 4.2. Адаптация обработки сообщений

```typescript
// В core/protocol/messenger/messageIde.ts
// Добавление методов для Visual Studio
getVisualStudioVersion(): Promise<string> {
  return this.request("visualStudio/getVersion", undefined);
}

getVisualStudioSettings(): Promise<any> {
  return this.request("visualStudio/getSettings", undefined);
}
```

## 5. Заключение

Для интеграции с Visual Studio потребуются следующие основные доработки в TypeScript коде:

1. **В core компоненте**:
   - Добавление нового типа мессенджера для Visual Studio
   - Расширение интерфейса IDE для поддержки специфичных функций
   - Адаптация для работы в Windows
   - Модификация системы запуска

2. **В gui компоненте**:
   - Адаптация WebView для Visual Studio
   - Создание адаптера для взаимодействия с Visual Studio
   - Адаптация стилей для соответствия теме Visual Studio

3. **Общие изменения**:
   - Создание скрипта сборки для Visual Studio
   - Расширение протокола сообщений для поддержки специфичных для VS функций

Эти изменения позволят существующему TypeScript коду core и gui компонентов работать с расширением для Visual Studio, сохраняя при этом совместимость с другими IDE.

# Интеграция GUI в расширение для Visual Studio

Интеграция GUI компонента, написанного на TypeScript/React, в расширение для Visual Studio требует особого подхода, поскольку расширения VS разрабатываются на C#. Вот подробное руководство по интеграции:

## 1. Подготовка GUI для интеграции

### 1.1. Адаптация сборки GUI

Сначала необходимо адаптировать сборку GUI для использования в Visual Studio:

```typescript
// В gui/vite.config.ts или webpack.config.js
export default defineConfig({
  // Существующая конфигурация...
  
  build: {
    // Существующие настройки...
    
    // Добавить специальную конфигурацию для VS
    rollupOptions: {
      output: {
        // Обеспечить совместимость с WebView в VS
        format: 'iife',
        entryFileNames: 'assets/[name].js',
        chunkFileNames: 'assets/[name]-[hash].js',
        assetFileNames: 'assets/[name]-[hash].[ext]'
      }
    }
  },
  
  // Добавить переменную окружения для определения платформы
  define: {
    'process.env.PLATFORM': JSON.stringify(process.env.PLATFORM || 'vscode')
  }
});
```

### 1.2. Создание адаптера для Visual Studio

```typescript
// В gui/src/adapters/visualStudioAdapter.ts
export class VisualStudioAdapter {
  private static instance: VisualStudioAdapter;
  private messageHandlers: Map<string, (data: any) => void> = new Map();
  
  private constructor() {
    // Инициализация обработчика сообщений от VS
    window.addEventListener('message', this.handleMessage.bind(this));
  }
  
  public static getInstance(): VisualStudioAdapter {
    if (!VisualStudioAdapter.instance) {
      VisualStudioAdapter.instance = new VisualStudioAdapter();
    }
    return VisualStudioAdapter.instance;
  }
  
  private handleMessage(event: MessageEvent) {
    const { type, data } = event.data;
    const handler = this.messageHandlers.get(type);
    if (handler) {
      handler(data);
    }
  }
  
  public registerMessageHandler(type: string, handler: (data: any) => void) {
    this.messageHandlers.set(type, handler);
  }
  
  public postMessage(type: string, data: any) {
    // Отправка сообщения в VS
    window.external.notify(JSON.stringify({ type, data }));
  }
}

// Инициализация адаптера при загрузке страницы
window.vsAdapter = VisualStudioAdapter.getInstance();
```

### 1.3. Модификация точки входа GUI

```typescript
// В gui/src/index.tsx
import React from 'react';
import ReactDOM from 'react-dom';
import App from './App';
import { VisualStudioAdapter } from './adapters/visualStudioAdapter';

// Определение текущей платформы
const platform = process.env.PLATFORM || 'vscode';

// Инициализация соответствующего адаптера
let adapter;
if (platform === 'visualstudio') {
  adapter = VisualStudioAdapter.getInstance();
  // Регистрация глобального объекта для отладки
  window.vsAdapter = adapter;
}

// Рендеринг приложения с передачей адаптера
ReactDOM.render(
  <React.StrictMode>
    <App platform={platform} adapter={adapter} />
  </React.StrictMode>,
  document.getElementById('root')
);
```

## 2. Интеграция GUI в расширение Visual Studio

### 2.1. Создание WebView контрола в C#

```csharp
using Microsoft.Web.WebView2.Wpf;
using System.Windows.Controls;
using System.IO;
using System.Reflection;

public class ContinueWebView : UserControl
{
    private WebView2 webView;
    private string guiPath;
    
    public ContinueWebView()
    {
        InitializeComponent();
        InitializeWebView();
    }
    
    private void InitializeComponent()
    {
        webView = new WebView2();
        Content = webView;
    }
    
    private async void InitializeWebView()
    {
        // Путь к GUI файлам (относительно сборки расширения)
        string assemblyLocation = Assembly.GetExecutingAssembly().Location;
        string assemblyDirectory = Path.GetDirectoryName(assemblyLocation);
        guiPath = Path.Combine(assemblyDirectory, "gui");
        
        // Инициализация WebView2
        await webView.EnsureCoreWebView2Async();
        
        // Настройка обработчиков событий
        webView.CoreWebView2.WebMessageReceived += CoreWebView2_WebMessageReceived;
        
        // Загрузка GUI
        string indexPath = Path.Combine(guiPath, "index.html");
        webView.CoreWebView2.Navigate(new Uri(indexPath).AbsoluteUri);
        
        // Установка переменной окружения для определения платформы
        webView.CoreWebView2.ExecuteScriptAsync("window.process = { env: { PLATFORM: 'visualstudio' } };");
    }
    
    private void CoreWebView2_WebMessageReceived(object sender, Microsoft.Web.WebView2.Core.CoreWebView2WebMessageReceivedEventArgs e)
    {
        string message = e.WebMessageAsJson;
        // Десериализация и обработка сообщения от GUI
        dynamic jsonMessage = JsonConvert.DeserializeObject(message);
        string type = jsonMessage.type;
        dynamic data = jsonMessage.data;
        
        // Обработка сообщения в зависимости от типа
        HandleWebViewMessage(type, data);
    }
    
    public void SendMessageToWebView(string type, object data)
    {
        // Сериализация и отправка сообщения в GUI
        var message = new { type, data };
        string jsonMessage = JsonConvert.SerializeObject(message);
        webView.CoreWebView2.PostWebMessageAsJson(jsonMessage);
    }
    
    private void HandleWebViewMessage(string type, dynamic data)
    {
        // Обработка сообщений от GUI
        switch (type)
        {
            case "request":
                // Обработка запроса к core
                HandleCoreRequest(data);
                break;
            
            case "uiEvent":
                // Обработка UI события
                HandleUIEvent(data);
                break;
            
            // Другие типы сообщений
        }
    }
    
    private void HandleCoreRequest(dynamic data)
    {
        // Перенаправление запроса к core через CoreMessenger
        // ...
    }
    
    private void HandleUIEvent(dynamic data)
    {
        // Обработка UI события
        // ...
    }
}
```

### 2.2. Создание Tool Window для размещения WebView

```csharp
[Guid("12345678-1234-1234-1234-123456789012")]
public class ContinueChatToolWindow : ToolWindowPane
{
    private ContinueWebView webView;
    
    public ContinueChatToolWindow() : base(null)
    {
        Caption = "Continue Chat";
        
        // Создание WebView контрола
        webView = new ContinueWebView();
        Content = webView;
    }
    
    // Метод для доступа к WebView из других частей расширения
    public ContinueWebView WebView => webView;
}
```

### 2.3. Регистрация Tool Window в пакете расширения

```csharp
[PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
[ProvideMenuResource("Menus.ctmenu", 1)]
[ProvideToolWindow(typeof(ContinueChatToolWindow))]
[Guid(ContinueVSPackage.PackageGuidString)]
public sealed class ContinueVSPackage : AsyncPackage
{
    public const string PackageGuidString = "87654321-4321-4321-4321-210987654321";
    
    private CoreProcessManager _coreManager;
    
    protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
    {
        await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
        
        // Инициализация core
        // ...
        
        // Регистрация команд
        await ContinueVSCommand.InitializeAsync(this);
    }
    
    // Метод для получения Tool Window
    public async Task<ContinueChatToolWindow> GetChatToolWindowAsync()
    {
        var window = await FindToolWindowAsync(typeof(ContinueChatToolWindow), 0, true, CancellationToken.None) as ContinueChatToolWindow;
        if (window == null)
        {
            throw new NotSupportedException("Cannot create Continue Chat tool window");
        }
        return window;
    }
}
```

### 2.4. Создание команды для открытия Tool Window

```csharp
internal sealed class OpenChatWindowCommand
{
    private readonly AsyncPackage package;
    
    private OpenChatWindowCommand(AsyncPackage package, OleMenuCommandService commandService)
    {
        this.package = package;
        
        var menuCommandID = new CommandID(new Guid("12345678-1234-1234-1234-123456789012"), 0x0100);
        var menuItem = new MenuCommand(Execute, menuCommandID);
        commandService.AddCommand(menuItem);
    }
    
    public static async Task InitializeAsync(AsyncPackage package)
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(package.DisposalToken);
        
        OleMenuCommandService commandService = await package.GetServiceAsync(typeof(IMenuCommandService)) as OleMenuCommandService;
        new OpenChatWindowCommand(package, commandService);
    }
    
    private void Execute(object sender, EventArgs e)
    {
        ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
        {
            var continuePackage = package as ContinueVSPackage;
            var window = await continuePackage.GetChatToolWindowAsync();
            
            var windowFrame = (IVsWindowFrame)window.Frame;
            Microsoft.VisualStudio.ErrorHandler.ThrowOnFailure(windowFrame.Show());
        });
    }
}
```

## 3. Связывание GUI с Core через C# прослойку

### 3.1. Создание прослойки для передачи сообщений

```csharp
public class CoreGuiMessenger
{
    private readonly CoreProcessManager coreManager;
    private readonly ContinueWebView webView;
    
    public CoreGuiMessenger(CoreProcessManager coreManager, ContinueWebView webView)
    {
        this.coreManager = coreManager;
        this.webView = webView;
        
        // Подписка на сообщения от core
        coreManager.MessageReceived += OnCoreMessageReceived;
    }
    
    private void OnCoreMessageReceived(object sender, CoreMessageEventArgs e)
    {
        // Перенаправление сообщения от core в GUI
        webView.SendMessageToWebView("coreMessage", new
        {
            messageType = e.MessageType,
            messageId = e.MessageId,
            data = e.Data
        });
    }
    
    public async Task SendGuiMessageToCore(string messageType, string messageId, object data)
    {
        // Перенаправление сообщения от GUI к core
        await coreManager.SendMessageAsync(messageType, messageId, data);
    }
}
```

### 3.2. Инициализация прослойки в пакете расширения

```csharp
protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
{
    await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
    
    // Инициализация core
    string extensionDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
    string corePath = Path.Combine(extensionDirectory, "core", "continue-core.exe");
    _coreManager = new CoreProcessManager(corePath);
    _coreManager.StartCoreProcess();
    
    // Получение WebView
    var chatWindow = await GetChatToolWindowAsync();
    var webView = chatWindow.WebView;
    
    // Создание прослойки для связи GUI и core
    _coreGuiMessenger = new CoreGuiMessenger(_coreManager, webView);
    
    // Регистрация команд
    await ContinueVSCommand.InitializeAsync(this);
}
```

## 4. Упаковка GUI в расширение

### 4.1. Настройка проекта для включения GUI файлов

```xml
<!-- В .csproj файле -->
<ItemGroup>
  <Content Include="gui\**">
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    <IncludeInVSIX>true</IncludeInVSIX>
  </Content>
</ItemGroup>
```

### 4.2. Создание скрипта для сборки и копирования GUI

```powershell
# build_vs_gui.ps1
param (
    [string]$OutputDir = "extensions/visualstudio/gui"
)

# Установка переменной окружения для сборки GUI для VS
$env:PLATFORM = "visualstudio"

# Сборка GUI
Write-Host "Building GUI for Visual Studio..."
Push-Location gui
npm ci --prefer-offline
npm run build
Pop-Location

# Создание выходной директории, если она не существует
if (-not (Test-Path $OutputDir)) {
    New-Item -ItemType Directory -Path $OutputDir -Force
}

# Копирование собранных файлов GUI
Write-Host "Copying GUI files to $OutputDir..."
Copy-Item -Path "gui/dist/*" -Destination $OutputDir -Recurse -Force

Write-Host "GUI build completed successfully."
```

### 4.3. Интеграция скрипта в процесс сборки расширения

```xml
<!-- В .csproj файле -->
<Target Name="BuildGUI" BeforeTargets="PrepareForBuild">
  <Exec Command="powershell -ExecutionPolicy Bypass -File build_vs_gui.ps1 -OutputDir $(MSBuildProjectDirectory)\gui" />
</Target>
```

## 5. Особенности работы с WebView2 в Visual Studio