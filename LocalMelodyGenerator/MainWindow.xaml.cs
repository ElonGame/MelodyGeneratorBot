using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Telegram;
using MelodyGeneratorBot;
using Newtonsoft.Json.Linq;

namespace LocalMelodyGenerator
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private Task _botTask;
        private User _botInfo;
        private CancellationTokenSource _cts;

        public MainWindow()
        {
            InitializeComponent();
        }

        private async void StartButton_Click(object sender, RoutedEventArgs e)
        {
            if (_botTask == null || _botTask.IsCompleted)
            {
                StartBot();
                StartButton.Content = "Stop";
            }
            else
            {
                EndBot();
                StartButton.Content = "Start";
            }
        }

        private async void StartBot()
        {
            var apiToken = this.ApiTokenTextBox.Password;
            _cts = new CancellationTokenSource();
            var bot = new MelodyGenerationBot(apiToken);
            _botTask = Task.Run(async delegate ()
            {
                var botapi = new BotApi(apiToken);
                try
                {
                    _botInfo = await botapi.GetMe();
                }
                catch
                {
                    StartButton.Dispatcher.Invoke(() => StartButton.Content = "Start");
                }

                this.LogScrollViewer.Dispatcher.Invoke(delegate ()
                {
                    TextBlock tb = new TextBlock();
                    var msg = "The bot has been started";
                    if (_botInfo == null)
                        msg = "The bot cannot be started";
                    tb.Text = $"{DateTime.Now.ToLongTimeString()} {msg}";
                    LogPanel.Children.Add(tb);
                });
                if (_botInfo == null)
                    return;
                int maxUpdate = 0;
                while (!_cts.Token.IsCancellationRequested)
                {
                    Update[] updates = null;
                    try
                    {
                        updates = await botapi.GetUpdates(maxUpdate);
                        if (updates.Length > 0)
                            maxUpdate = updates.Select(u => u.Id).Max() + 1;
                    }
                    catch (Exception)
                    {
                        break;
                    }
                    foreach (var update in updates)
                    {
                        bot.ProcessUpdate(update);
                        this.LogPanel.Dispatcher.Invoke(delegate ()
                        {
                            var notification = new TextBlock
                            {
                                Text = $"{DateTime.Now.ToLongTimeString()} Update {update.Id} processed"
                            };
                            LogPanel.Children.Add(notification);
                            //Update contents
                            var spoiler = new Expander();

                            var updateJson = new TextBox
                            {
                                Text = JObject.FromObject(update).ToString(),
                                FontFamily = new FontFamily("Consolas"),
                                IsReadOnly = true
                            };
                            spoiler.Content = updateJson;
                            spoiler.Header = "Update JSON";
                            LogPanel.Children.Add(spoiler);
                        });
                    }
                }
                this.LogScrollViewer.Dispatcher.Invoke(delegate ()
                {
                    TextBlock tb = new TextBlock();
                    tb.Text = $"{DateTime.Now.ToLongTimeString()} The bot has been stopped";
                    LogPanel.Children.Add(tb);
                });

            }, _cts.Token);

        }

        private async void EndBot()
        {
            _cts?.Cancel();
            if (_botTask != null)
                await _botTask;
        }

        private async void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            EndBot();
        }
    }
}
