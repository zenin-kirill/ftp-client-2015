using Microsoft.Win32;
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
using System.Drawing;
using System.Windows.Forms;
using System.IO;

namespace SETIlab3_2
{
    public delegate void UpdateStatus(string strMessage, bool isReady, int percentsDowloded, int dowloadSpeed);

    public partial class MainWindow : Window
    {

        public class MWException : Exception
        {
            public MWException(string message) : base(message) { }
            public MWException(string message, Exception innerException) : base(message, innerException) { }
        }
        FtpClient client;
        static string remoteAddresString;
        static string remotePathString;
        static string localFileString;
        static string localPathString;

        const int timeout = 5000;
        const int port = 21;
        Thread load;
        DateTime waitingTime;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void inButton_Click(object sender, RoutedEventArgs e)
        {
            if (inButton.Content.Equals("Остановить"))
            {
                inButton.IsEnabled = false;
                load.Suspend();
                Dispatcher.Invoke(new UpdateStatus(updateStatus), new object[] { "Остановка загрузки...", false, -1, -1 });
                load.Resume();
                load.Abort();
            }
            else
            {
                try
                {
                    string[] words = inTextBox.Text.Split(new char[] { '/' });
                    if (outTextBox.Text == "" || !Directory.Exists(outTextBox.Text))
                        throw new MWException("Неверно выбран каталог загрузки");
                    if (!words[0].Equals("ftp:") || !words[1].Equals(""))
                        throw new MWException("Неверно введен адрес файла");

                    string login;
                    string pass;

                    if (LoginTextBox.IsEnabled == true)
                    {
                        if (LoginTextBox.Text == "")
                            throw new MWException("Пожалуйста, введите или отключите логин");
                        login = LoginTextBox.Text;
                    }
                    else
                        login = "anonymous";

                    if (passwordBox.IsEnabled == true)
                    {
                        if (passwordBox.Password == "")
                            throw new MWException("Пожалуйста, введите или отключите пароль");
                        pass = passwordBox.Password;
                    }
                    else
                        pass = "anonymous@anonymous.net";

                    inButton.BorderBrush = Brushes.Black;
                    statusLabel.Foreground = Brushes.Black;

                    remoteAddresString = words[2];
                    localFileString = words[(words.Length) - 1];
                    localPathString = outTextBox.Text + localFileString;
                    var tempList = new List<string>(words);
                    for (int i = 0; i < 3; i++)
                        tempList.RemoveAt(0);

                    remotePathString = string.Join("/", tempList.ToArray());
                    client = new FtpClient(this, remoteAddresString, login, pass, timeout, port);
                    load = new Thread(reciveFile);
                    load.Start();

                }
                catch (MWException mwe)
                {
                    statusLabel.Foreground = Brushes.Red;
                    inButton.BorderBrush = Brushes.Red;
                    statusLabel.Content = mwe.Message;
                }
            }
        }



        public void updateStatus(string status, bool isReady, int percentsDowloded, int dowloadSpeed)
        {
            statusLabel.Content = status;

            if (dowloadSpeed != -1)
            {
                if (dowloadSpeed >= 0)
                {
                    if (timeLabel.Visibility == Visibility.Hidden)
                        timeLabel.Visibility = Visibility.Visible;
                    waitingTime = new DateTime().AddSeconds((double)dowloadSpeed);
                    timeLabel.Content = "Приблизительное время ожидания: " + waitingTime.Hour + " ч. " 
                        + waitingTime.Minute + " мин. " + waitingTime.Second + " сек.";
                }
                else
                    if (dowloadSpeed == -10)
                {
                    if (timeLabel.Visibility == Visibility.Hidden)
                        timeLabel.Visibility = Visibility.Visible;
                    timeLabel.Content = "Подготовка...";
                }
            }
            else
            {
                if (timeLabel.Visibility == Visibility.Visible)
                    timeLabel.Visibility = Visibility.Hidden;
            }


            if (isReady)
            {
                if (!inButton.Content.Equals("Загрузить"))
                {
                    if (inButton.IsEnabled == false)
                        inButton.IsEnabled = true;
                    inButton.BorderBrush = Brushes.LightGreen;
                    inButton.Content = "Загрузить";
                }
            }
            else
                if (!inButton.Content.Equals("Остановить"))
                {
                    inButton.BorderBrush = Brushes.LightPink;
                    inButton.Content = "Остановить";
                }

            if (!isReady)
            {
                loginCheckBox.IsEnabled = false;
                LoginTextBox.IsEnabled = false;
                passwordCheckBox.IsEnabled = false;
                passwordBox.IsEnabled = false;
            }
            else
            {
                loginCheckBox.IsEnabled = true;
                if (loginCheckBox.IsChecked == true)
                {
                    LoginTextBox.IsEnabled = true;
                    passwordCheckBox.IsEnabled = true;
                }

                if (passwordCheckBox.IsChecked == true)
                {
                    passwordBox.IsEnabled = true;
                }
                
                
            }

            if (inTextBox.IsEnabled != isReady)
                inTextBox.IsEnabled = isReady;

            if (outButton.IsEnabled != isReady)
                outButton.IsEnabled = isReady;

            if (outTextBox.IsEnabled != isReady)
                outTextBox.IsEnabled = isReady;

            if (percentsDowloded >= 0 && percentsDowloded <= 100)
            {
                statusBar.Visibility = Visibility.Visible;
                statusBar.Value = percentsDowloded;
            }
            else
                if (statusBar.Visibility == Visibility.Visible)
                    statusBar.Visibility = Visibility.Hidden;
        }

        void reciveFile()
        {

            try
            {
                client.Download(remotePathString, localPathString, true);
                Dispatcher.Invoke(new UpdateStatus(updateStatus), new object[] { "Загрузка успешно выполнена!", true, -1, -1 });
            }
            catch (FtpClient.FtpException ftpe)
            {
                Dispatcher.Invoke(new UpdateStatus(updateStatus), new object[] { ftpe.Message.ToString(), true, -1, -1 });
            }
            catch(ThreadAbortException)
            {
                client.Close();
                Dispatcher.Invoke(new UpdateStatus(updateStatus), new object[] {"Загрузка прервана пользователем", true, -1, -1 });
            }
           
        }

        private void outButton_Click(object sender, RoutedEventArgs e)
        {

            FolderBrowserDialog dialog = new FolderBrowserDialog();

            dialog.Description = "Выберите папку для загрузки...";
            //dialog.ShowNewFolderButton = false;
            dialog.RootFolder = Environment.SpecialFolder.MyComputer;

            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                if (dialog.SelectedPath.Substring(dialog.SelectedPath.Length-1, 1).Equals("\\"))
                    outTextBox.Text = dialog.SelectedPath;
                else
                    outTextBox.Text = dialog.SelectedPath + "\\";
            }          
        }

        private void outTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
           
            if ((outTextBox.Text != "") && (inTextBox.Text.Length >= 6))
                if  (inTextBox.Text.Substring(0, 6).Equals("ftp://"))       
                    inButton.BorderBrush = Brushes.LightGreen;
                else
                    inButton.BorderBrush = Brushes.LightPink;
            else
                inButton.BorderBrush = Brushes.LightPink;
        }

        private void inTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if ((outTextBox.Text != "") && (inTextBox.Text.Length >= 6))
                if (inTextBox.Text.Substring(0, 6).Equals("ftp://"))
                    inButton.BorderBrush = Brushes.LightGreen;
                else
                    inButton.BorderBrush = Brushes.LightPink;
            else
                inButton.BorderBrush = Brushes.LightPink;
        }

        private void checkBox_Checked(object sender, RoutedEventArgs e)
        {
            LoginTextBox.IsEnabled = true;
            passwordCheckBox.IsEnabled = true;
            if (LoginTextBox.Text.Equals("anonymous"))
                LoginTextBox.Text = "";
        }

        private void loginCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            LoginTextBox.IsEnabled = false;
            passwordCheckBox.IsChecked = false;
            passwordCheckBox.IsEnabled = false;
        }

        private void passwordCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            passwordBox.IsEnabled = true;
        }

        private void passwordCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            passwordBox.IsEnabled = false;
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            load.Abort();
        }
    }
}
