using System.Collections.ObjectModel;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Linq;
using System.Drawing;

namespace NetStatAnalyzer
{  
    public class NetStatEntry
    {
        public string Protocol { get; set; }
        public string LocalAddress { get; set; }
        public string ForeignAddress { get; set; }
        public string State { get; set; }
        public int PID { get; set; }
        public string ProcessName { get; set; }
        public BitmapImage ProcessIcon { get; set; }
    }
    public partial class MainWindow : Window
    {
        public ObservableCollection<NetStatEntry> Entries { get; set; } = new ObservableCollection<NetStatEntry>();

        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;
            LoadNetStatData();
        }

        private void LoadNetStatData()
        {
            try
            {
                Entries.Clear();
                var netStatOutput = RunCommand("netstat", "-ano");
                var states = new HashSet<string>();

                foreach (var line in netStatOutput)
                {
                    var entry = ParseNetStatLine(line);
                    if (entry != null)
                    {
                        entry.ProcessName = GetProcessName(entry.PID);
                        entry.ProcessIcon = GetProcessIcon(entry.PID);
                        Entries.Add(entry);

                        //Adicionando o estado único ao conjunto
                        states.Add(entry.State); 
                    }
                }

                //Preenchendo o ComboBox com os estados únicos encontrados
                StateComboBox.ItemsSource = states.OrderBy(state => state).ToList();

                //Nenhum estado selecionado inicialmente
                StateComboBox.SelectedItem = null;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading NetStat data: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            LoadNetStatData();
        }

        private string[] RunCommand(string command, string args)
        {
            try
            {
                using Process process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = command,
                        Arguments = args,
                        RedirectStandardOutput = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };

                process.Start();
                string output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();

                return output.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error running command '{command}': {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return Array.Empty<string>();
            }
        }

        private NetStatEntry ParseNetStatLine(string line)
        {
            try
            {
                string[] parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 5 || !int.TryParse(parts[^1], out int pid))
                    return null;

                return new NetStatEntry
                {
                    Protocol = parts[0],
                    LocalAddress = parts[1],
                    ForeignAddress = parts[2],
                    State = parts.Length > 4 ? parts[3] : "N/A",
                    PID = pid
                };
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error parsing line '{line}': {ex.Message}");
                return null;
            }
        }

        private string GetProcessName(int pid)
        {
            try
            {
                var process = Process.GetProcessById(pid);
                return process.ProcessName;
            }
            catch
            {
                return "Unknown";
            }
        }

        private BitmapImage GetProcessIcon(int pid)
        {
            try
            {
                var process = Process.GetProcessById(pid);
                string path = process.MainModule?.FileName;

                if (!string.IsNullOrEmpty(path) && File.Exists(path))
                {
                    var icon = GetIcon(path);
                    if (icon != null)
                    {
                        var bitmapImage = new BitmapImage();
                        using (var memoryStream = new MemoryStream())
                        {
                            //Converte o ícone para Bitmap
                            using (var bitmap = icon.ToBitmap())
                            {
                                //Salva o bitmap em MemoryStream no formato PNG
                                bitmap.Save(memoryStream, System.Drawing.Imaging.ImageFormat.Png);
                            }
                            memoryStream.Seek(0, SeekOrigin.Begin);
                            bitmapImage.BeginInit();
                            bitmapImage.StreamSource = memoryStream;
                            bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                            bitmapImage.EndInit();
                        }
                        return bitmapImage;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error retrieving icon for PID {pid}: {ex.Message}");
            }

            return null;
        }

        private System.Drawing.Icon GetIcon(string filePath)
        {
            try
            {
                return System.Drawing.Icon.ExtractAssociatedIcon(filePath);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error extracting icon: {ex.Message}");
                return null;
            }
        }

        private void OpenFileLocation_Click(object sender, RoutedEventArgs e)
        {
            if (DataGrid.SelectedItem is NetStatEntry entry)
            {
                try
                {
                    var process = Process.GetProcessById(entry.PID);
                    string path = process.MainModule?.FileName;
                    if (!string.IsNullOrEmpty(path) && File.Exists(path))
                    {
                        Process.Start("explorer.exe", $"/select,\"{path}\"");
                    }
                }
                catch
                {
                    MessageBox.Show("Unable to open file location.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void FilterData_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string protocolFilter = ProtocolTextBox.Text.ToLower();

                //Obtendo o estado selecionado
                string stateFilter = StateComboBox.SelectedItem as string ?? string.Empty;
                string ipFilter = IPTextBox.Text;

                var filtered = Entries.Where(entry =>
                    (string.IsNullOrEmpty(protocolFilter) || entry.Protocol.ToLower().Contains(protocolFilter)) &&
                    (string.IsNullOrEmpty(stateFilter) || entry.State.ToLower().Contains(stateFilter.ToLower())) &&
                    (string.IsNullOrEmpty(ipFilter) || entry.LocalAddress.StartsWith(ipFilter))
                ).ToList();

                DataGrid.ItemsSource = filtered;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error filtering data: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void StateComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            FilterData_Click(sender, e);
        }
       
        private void ReapplyLoad_Click(object sender, RoutedEventArgs e)
        {
            LoadNetStatData();
        }
    }
}