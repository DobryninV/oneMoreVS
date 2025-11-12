
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using System.Windows.Controls;

public class MyGuiControl : UserControl
{
    private WebView2 webView;

    public MyGuiControl()
    {
        InitializeWebView();
    }

    private async void InitializeWebView()
    {
        try
        {

            webView = new WebView2();
            this.Content = webView;

            string userDataFolder = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "MyVsExtention",
                "WebView2Data");

            var environment = await CoreWebView2Environment.CreateAsync(
                   browserExecutableFolder: null,
                   userDataFolder: userDataFolder
                );

            // Инициализация WebView2
            await webView.EnsureCoreWebView2Async(environment);



            // Загрузка HTML контента
            string htmlContent = @"
            <!DOCTYPE html>
            <html>
            <head>
                <title>Моё GUI</title>
                <style>
                    body { 
                        font-family: Arial, sans-serif; 
                        margin: 20px;
                        background-color: #f0f0f0;
                    }
                    h1 { color: #333; }
                </style>
            </head>
            <body>
                <h1>Моё GUI приложение</h1>
                <p>Это мой HTML контент!</p>
                <button onclick='showMessage()'>Нажми меня</button>
                <script>
                    function showMessage() {
                        alert('Привет из WebView2!');
                    }
                </script>
            </body>
            </html>";

            //webView.NavigateToString(htmlContent);

            // Или загрузка из файла
            // string htmlPath = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location), "Resources", "mygui.html");
             webView.Source = new Uri("http://localhost:5173/");

        } catch
        {
            await VS.MessageBox.ShowWarningAsync("uiCodePilot", "Crash");
        }
    }
}