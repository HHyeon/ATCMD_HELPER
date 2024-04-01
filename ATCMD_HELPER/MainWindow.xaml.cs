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
            queued_logging_timer = new System.Timers.Timer();
            queued_logging_timer.Interval = 100;
            queued_logging_timer.Elapsed += Queued_logging_timer_Elapsed;
            queued_logging_timer.Start();

            println("App Started");
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

            Task.Factory.StartNew(async () =>
            {
                try
                {
                    closeserialport();

                    await Task.Delay(100);

                    serialPort = new SerialPort();
                    string? portname = null;

                    await Task.Factory.StartNew(() =>
                    {
                        Dispatcher.Invoke(() =>
                        {
                            portname = serialportselection.SelectedItem as string;
                        });
                    });

                    if (portname == null) return;

                    println("opening " + portname);

                    serialPort.PortName = portname;
                    serialPort.BaudRate = 460800;
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
                    println(ex.ToString());
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
                println(indata);
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

        private void action0_Click(object sender, RoutedEventArgs e)
        {
            sendmsg("AT+MODE?\r");
        }

        private void action1_Click(object sender, RoutedEventArgs e)
        {
            sendmsg("AT+PANDLST?\r");
        }

        private void action2_Click(object sender, RoutedEventArgs e)
        {
            sendmsg("AT+PANDALIVE?\r");
        }

        private void action3_Click(object sender, RoutedEventArgs e)
        {
            sendmsg("AT+BEARER=4\rOK\r");
        }

        private void serialportselection_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            refreshcomportlisttoui();
        }


        void refreshcomportlisttoui()
        {
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
