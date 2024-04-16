using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Printing;
using System.Text;
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
using System.Windows.Threading;

namespace ATCMD_HELPER
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        object locking_for_serialPort = new object();
        SerialPort serialPort = new SerialPort();
        System.Timers.Timer queued_logging_timer = new System.Timers.Timer();

        public MainWindow()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Command_layout_add_one("AT+MODE?\r");
            Command_layout_add_one("AT+PANDLST?\r");
            Command_layout_add_one("AT+PANDALIVE?\r");
            Command_layout_add_one("AT+BEARER=4\rOK\r");
            Command_layout_add_one("AT+MMID?\r");
            Command_layout_add_one("AT+EBR?\r");
            Command_layout_add_one("AT+METERID?\r");
            Command_layout_add_one("");
            Command_layout_add_one("");
            Command_layout_add_one("");
            Command_layout_add_one("");
            Command_layout_add_one("");

            queued_logging_timer = new System.Timers.Timer();
            queued_logging_timer.Interval = 100;
            queued_logging_timer.Elapsed += Queued_logging_timer_Elapsed;
            queued_logging_timer.Start();

            serialportbuadselection.ItemsSource = new object[]
            {
                9600,
                115200,
                460800
            };

            serialportbuadselection.SelectedIndex = 2;

            println("App Started");
        }

        void Command_layout_add_one(string cmd)
        {
            string _cmd = cmd;
            _cmd = _cmd.Replace("\r", "\\r");
            _cmd = _cmd.Replace("\n", "\\n");

            Grid grid = new Grid();

            ColumnDefinition cd1 = new ColumnDefinition();
            cd1.Width = new GridLength(5, GridUnitType.Star);
            grid.ColumnDefinitions.Add(cd1);

            ColumnDefinition cd2 = new ColumnDefinition();
            cd2.Width = new GridLength(2, GridUnitType.Star);
            grid.ColumnDefinitions.Add(cd2);

            TextBox tb = new TextBox();
            tb.Margin = new Thickness(2);
            tb.Text = _cmd;
            Grid.SetColumn(tb, 0);

            Button btn = new Button();
            btn.Margin = new Thickness(2);
            btn.Content = "send";
            btn.Click += (sender, e) =>
            {
                try
                {
                    //println((sender as Button).Parent.ToString());
                    Button? btn = sender as Button;
                    if (btn == null) return;
                    Grid? grid = btn.Parent as Grid;
                    if (grid == null) return;

                    string cmdstosent = null;

                    foreach (UIElement element in grid.Children)
                    {
                        if (element is TextBox textBox && Grid.GetRow(textBox) == Grid.GetRow(sender as Button))
                        {
                            cmdstosent = textBox.Text;
                            break;
                        }
                    }


                    if(cmdstosent != null)
                    {
                        if(cmdstosent.Length != 0)
                        {
                            cmdstosent = cmdstosent.Replace("\\r", "\r");
                            cmdstosent = cmdstosent.Replace("\\n", "\n");
                            sendmsg(cmdstosent);
                        }
                    }

                }
                catch (Exception ex)
                {
                    println(ex.ToString());
                }
            };
            Grid.SetColumn(btn, 1);

            grid.Children.Add(tb);
            grid.Children.Add(btn);

            stackpanel_commandlists.Children.Add(grid);
        }

        ConcurrentQueue<string> LogMessageQueue = new ConcurrentQueue<string>();
        private void Queued_logging_timer_Elapsed(object? sender, System.Timers.ElapsedEventArgs e)
        {
            string msg;
            if (LogMessageQueue.TryDequeue(out msg))
            {
                Dispatcher.Invoke(DispatcherPriority.Normal, new Action(delegate
                {
                    tb_logging.AppendText("[" + DateTime.Now.ToString("G") + "] " + msg + Environment.NewLine);
                    tb_logging.ScrollToEnd();
                }));
            }
        }

        void println(string s)
        {
            LogMessageQueue.Enqueue(s);
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (serialPort.IsOpen)
            {
                serialPort.Close();
                serialPort.Dispose();
            }
        }

        void closeserialport()
        {
            serialPort.DataReceived -= SerialPort_DataReceived;
            if (serialPort.IsOpen)
            {
                serialPort.Close();
                serialPort.Dispose();
            }
        }

        private void serialportselectionconfirm_Click(object sender, RoutedEventArgs e)
        {
            if (serialportselection.SelectedItem == null) return;
            if (serialportbuadselection.SelectedItem == null) return;

            Task.Factory.StartNew(async () =>
            {
                try
                {
                    closeserialport();

                    await Task.Delay(100);

                    serialPort = new SerialPort();
                    string? portname = null;
                    int rate = 0;

                    await Task.Factory.StartNew(() =>
                    {
                        Dispatcher.Invoke(() =>
                        {
                            portname = serialportselection.SelectedItem as string;
                            rate = (int)serialportbuadselection.SelectedValue;
                        });
                    });

                    if (portname == null) return;
                    if (rate == 0) return;

                    println("opening " + portname + ", " + rate);

                    serialPort.PortName = portname;
                    serialPort.BaudRate = rate;
                    serialPort.DataReceived += SerialPort_DataReceived;

                    serialPort.ErrorReceived += (sender, e) =>
                    {
                        println("serialPort ErrorReceived");
                    };
                    serialPort.Disposed += (sender, e) =>
                    {
                        println("serialPort Disposed");
                    };

                    serialPort.PinChanged += (sender, e) =>
                    {
                        println("serialPort PinChanged");
                    };


                    serialPort.Open();

                    if (!serialPort.IsOpen)
                    {
                        println("open failed " + portname);
                        return;
                    }

                    println("Serialport " + serialPort.PortName + " opened");
                }
                catch (Exception ex)
                {
                    println(ex.StackTrace.ToString());
                }
            });
        }

        private void SerialPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            lock (locking_for_serialPort)
            {
                SerialPort sp = (SerialPort)sender;
                string indata = sp.ReadExisting();
                indata = indata.Replace("\r", "\\r");
                indata = indata.Replace("\n", "\\n");
                println("RX: " + indata);
            }
        }

        void sendmsg(string s)
        {
            lock (locking_for_serialPort)
            {
                if (serialPort.IsOpen)
                {
                    serialPort.Write(s);

                    string s1 = s;
                    s1 = s1.Replace("\r", "\\r");
                    s1 = s1.Replace("\n", "\\n");
                    println(s1);
                }
                else
                {
                    println("serialport invalid");
                }
            }
        }

        private void serialportselection_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            refreshcomportlisttoui();
        }


        void refreshcomportlisttoui()
        {
            println("Refreshing Comport list");
            serialportselection.Items.Clear();
            SerialPort.GetPortNames().ToList().ForEach(port =>
            {
                serialportselection.Items.Add(port);
            });
            
        }

        private void serialportclose_Click(object sender, RoutedEventArgs e)
        {
            lock (locking_for_serialPort)
            {
                closeserialport();
            }
        }
    }
}
