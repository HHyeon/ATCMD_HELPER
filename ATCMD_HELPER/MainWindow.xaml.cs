using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Ports;
using System.Linq;
using System.Printing;
using System.Security.RightsManagement;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
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
        System.Timers.Timer commands_interval_send_timer = new System.Timers.Timer();

        public MainWindow()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Command_layout_add_one("AT+MODE?\r");
            Command_layout_add_one("AT+MODE:0\r");
            Command_layout_add_one("AT+MODE:1\r");
            Command_layout_add_one("AT+METERID?\r");
            Command_layout_add_one("AT+MMID?\r");
            Command_layout_add_one("AT+MAC?\r");
            Command_layout_add_one("AT+PANDLST?\r");
            Command_layout_add_one("AT+DEVLST?\r");
            Command_layout_add_one("AT+CHAN?\r");
            Command_layout_add_one("AT+FUN:1\r");
            Command_layout_add_one("AT+NWK?\r");
            Command_layout_add_one("AT+PING:1024,10,200,3038FFF530000066\r");
            Command_layout_add_one("AT+WHITEDV?\r");
            Command_layout_add_one("AT+WHITEDV:1,1,3038FFF530000066?\r");
            Command_layout_add_one("AT+WHITECD?\r");
            Command_layout_add_one("AT+WHITECD:3038FFF530000066\r");
            Command_layout_add_one("");
            Command_layout_add_one("");
            Command_layout_add_one("");
            Command_layout_add_one("");
            Command_layout_add_one("");
            Command_layout_add_one("");
            Command_layout_add_one("");
            Command_layout_add_one("");
            Command_layout_add_one("");

            queued_logging_timer = new System.Timers.Timer();
            queued_logging_timer.Interval = 100;
            queued_logging_timer.Elapsed += Queued_logging_timer_Elapsed;
            queued_logging_timer.Start();

            commands_interval_send_timer = new System.Timers.Timer();
            commands_interval_send_timer.Interval = 1000;
            commands_interval_send_timer.Elapsed += Commands_interval_send_timer_Elapsed;
            commands_interval_send_timer.Start();

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

            for(int i=0;i<2;i++)
            {
                ColumnDefinition cd2 = new ColumnDefinition();
                cd2.Width = new GridLength(50);
                grid.ColumnDefinitions.Add(cd2);
            }

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
                    string? cmdstosent = null;

                    foreach (UIElement element in grid.Children)
                    {
                        if (element is TextBox textBox)
                        {
                            if(Grid.GetColumn(textBox) == 0)
                            {
                                cmdstosent = textBox.Text;
                            }
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

            TextBox textBox1 = new TextBox();
            textBox1.Margin = new Thickness(2);
            textBox1.Text = "0";
            textBox1.HorizontalContentAlignment = HorizontalAlignment.Center;
            textBox1.VerticalContentAlignment = VerticalAlignment.Center;
            Grid.SetColumn(textBox1, 2);

            grid.Children.Add(tb);
            grid.Children.Add(btn);
            grid.Children.Add(textBox1);

            stackpanel_commandlists.Children.Add(grid);
        }

        uint secondly_accumulatly_number = 0;
        private void Commands_interval_send_timer_Elapsed(object? sender, System.Timers.ElapsedEventArgs e)
        {
            
            Dispatcher.Invoke(() =>
            {
                secondly_accumulatly_number++;

                foreach (UIElement element in stackpanel_commandlists.Children)
                {
                    try
                    {
                        Grid? grid = element as Grid;
                        if (grid == null) return;
                        string? cmdstosent = null;
                        string? strintv = null;

                        foreach (UIElement element2 in grid.Children)
                        {
                            if (element2 is TextBox textBox)
                            {
                                int cidx = Grid.GetColumn(textBox);
                                if (cidx == 0)
                                {
                                    cmdstosent = textBox.Text;
                                }
                                else if (cidx == 2)
                                {
                                    strintv = textBox.Text;
                                }
                            }
                        }

                        int intv;
                        if (strintv != null && cmdstosent != null && int.TryParse(strintv, out intv))
                        {
                            if(intv > 0)
                            {
                                if(secondly_accumulatly_number % intv == 0)
                                {
                                    if (cmdstosent.Length != 0)
                                    {
                                        cmdstosent = cmdstosent.Replace("\\r", "\r");
                                        cmdstosent = cmdstosent.Replace("\\n", "\n");
                                        sendmsg(cmdstosent);
                                    }
                                }
                            }
                        }
                        else
                        {
                            println("Something Parsing Error");
                        }

                    }
                    catch(Exception e)
                    {
                        println(e.ToString());
                    }
                }
            });



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
                    println("serialport invalid - try open");

                    Task.Factory.StartNew(async () =>
                    {
                        await Task.Factory.StartNew(() =>
                        {
                            Dispatcher.Invoke(() =>
                            {
                                serialportselectionconfirm_Click(null, null);
                            });
                        });

                        await Task.Delay(500);

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
                            println("serialport invalid even retry");
                        }

                    });

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
