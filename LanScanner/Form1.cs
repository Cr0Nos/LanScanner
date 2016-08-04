using System;
using System.ComponentModel;
using System.Data;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.Net;
using System.IO;
using System.Xml;
using System.Diagnostics;
using System.Web.Script.Serialization;
using System.Net.Sockets;
using System.Net.NetworkInformation;

namespace LanScanner
{
    public partial class Form1 : Form
    {
        private string dataJSON;
        private string tempFileName = "LanScanner.update";
        private static string _URL = "http://192.168.1.2/";
        private string versionRemoteFile = _URL + "verstion.xml";
        private string progRemoteFile = _URL + Application.ProductName + ".exe";
        private string receiver = _URL + "index.php";
        private string myIP = GetLocalIPAddress();
        private string myMAC = GetLocalMACAddress("-");
        private string myNAME = "desktop";

        public Form1()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            CheckUpdates();
            GetIPARPTable();
            dataGridView1.DataSource = ipData;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct MIB_IPNETROW
        {
            [MarshalAs(UnmanagedType.U4)]
            public int dwIndex;
            [MarshalAs(UnmanagedType.U4)]
            public int dwPhysAddrLen;
            [MarshalAs(UnmanagedType.U1)]
            public byte mac0;
            [MarshalAs(UnmanagedType.U1)]
            public byte mac1;
            [MarshalAs(UnmanagedType.U1)]
            public byte mac2;
            [MarshalAs(UnmanagedType.U1)]
            public byte mac3;
            [MarshalAs(UnmanagedType.U1)]
            public byte mac4;
            [MarshalAs(UnmanagedType.U1)]
            public byte mac5;
            [MarshalAs(UnmanagedType.U1)]
            public byte mac6;
            [MarshalAs(UnmanagedType.U1)]
            public byte mac7;
            [MarshalAs(UnmanagedType.U4)]
            public int dwAddr;
            [MarshalAs(UnmanagedType.U4)]
            public int dwType;
        }

        [DllImport("IpHlpApi.dll")]
        [return: MarshalAs(UnmanagedType.U4)]
        static extern int GetIpNetTable(IntPtr pIpNetTable,
            [MarshalAs(UnmanagedType.U4)]
            ref int pdwSize,
            bool bOrder);

        [DllImport("IpHlpApi.dll", SetLastError = true, CharSet = CharSet.Auto)]
        internal static extern int FreeMibTable(IntPtr plpNetTable);


        DataTable ipData = null;

        const int BUFFER_ERROR = 122;

        private void GetIPARPTable()
        {
            ipData = new DataTable();

            ipData.Columns.Add("IP адрес");
            ipData.Columns.Add("MAC адрес");
            ipData.Columns.Add("Тип адреса");

            int bytesNeeded = 0;
            
            int result = GetIpNetTable(IntPtr.Zero, ref bytesNeeded, false);

            if (result != BUFFER_ERROR)
                throw new Win32Exception(result);

            IntPtr buffer = IntPtr.Zero;

            try
            {
                buffer = Marshal.AllocCoTaskMem(bytesNeeded);
                
                result = GetIpNetTable(buffer, ref bytesNeeded, false);
                
                if (result != 0)
                    throw new Win32Exception(result);
                
                int entries = Marshal.ReadInt32(buffer);
                
                IntPtr currentBuffer = new IntPtr(buffer.ToInt64() + Marshal.SizeOf(typeof(int)));
                
                MIB_IPNETROW[] table = new MIB_IPNETROW[entries];

                for (int index = 0; index < entries; index++)
                    table[index] = (MIB_IPNETROW)Marshal.PtrToStructure(new IntPtr(currentBuffer.ToInt64() + (index * Marshal.SizeOf(typeof(MIB_IPNETROW)))), typeof(MIB_IPNETROW));

                dataJSON = "{";
                for (int i = 0; i < entries; i++)
                {
                    MIB_IPNETROW row = table[i];
                    
                    System.Net.IPAddress ip = new System.Net.IPAddress(BitConverter.GetBytes(table[i].dwAddr));
                    string type;
                    switch(table[i].dwType)
                    {
                        case 2:
                            {
                                type = "отключившийся";
                                break;
                            }
                        case 3:
                            {
                                type = "динамический";
                                break;
                            }
                        case 4:
                            {
                                type = "статический";
                                break;
                            }
                        default:
                            {
                                type = "неизвестно";
                                break;
                            }
                    }
                    
                    string MAC = 
                        row.mac0.ToString("X2") + '-' + row.mac1.ToString("X2") + '-' +
                        row.mac2.ToString("X2") + '-' + row.mac3.ToString("X2") + '-' +
                        row.mac4.ToString("X2") + '-' + row.mac5.ToString("X2");

                    ipData.Rows.Add(new object[] { ip.ToString(), MAC, type });
                    dataJSON += "\"" + MAC + "\":\"" + ip.ToString() + "\"";
                    if (i != entries - 1) dataJSON += ",";
                }
                dataJSON += "}";
            }
            finally
            {
                FreeMibTable(buffer);
            }
        }

        private void SendData()
        {
            var httpWebRequest = (HttpWebRequest)WebRequest.Create(receiver);
            httpWebRequest.ContentType = "application/json";
            httpWebRequest.Method = "POST";

            using (var streamWriter = new StreamWriter(httpWebRequest.GetRequestStream()))
            {
                //string data = "{\"device\":\"desktop\"," +
                //              "\"mac\":\""+ myMAC +"\"," +
                //              "\"ip\":\"" + myIP + "\"," +
                //              "\"devices\":\"" + dataJSON + "\"}";

                string data = new JavaScriptSerializer().Serialize(new
                {
                    device = myNAME,
                    mac = myMAC,
                    ip = myIP,
                    devices = dataJSON
                });

                streamWriter.Write(data);
                streamWriter.Flush();
                streamWriter.Close();
            }

            var httpResponse = (HttpWebResponse)httpWebRequest.GetResponse();
            using (var streamReader = new StreamReader(httpResponse.GetResponseStream()))
            {
                var result = streamReader.ReadToEnd();
            }
        }

        public static string GetLocalIPAddress()
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    return ip.ToString();
                }
            }
            return "Local IP Address Not Found!";
        }

        public static string GetLocalMACAddress(string sep)
        {
            NetworkInterface[] nics = NetworkInterface.GetAllNetworkInterfaces();
            string sMacAddress = string.Empty;
            foreach (NetworkInterface adapter in nics)
            {
                if (sMacAddress == string.Empty)
                {
                    sMacAddress = adapter.GetPhysicalAddress().ToString();
                }
            }

            string result = string.Empty;
            for(int i = 0, j = 0; i < sMacAddress.Length; i++)
            {
                result += sMacAddress[i];
                j++;
                if (j == 2 && i != sMacAddress.Length - 1)
                {
                    j = 0;
                    result += sep;
                }
            }

            return result;
        }

        private void CheckUpdates()
        {
            XmlDocument versionFile = new XmlDocument();
            versionFile.Load(_URL + "version.xml");

            Version newVersion = new Version(versionFile.GetElementsByTagName("version")[0].InnerText);
            Version thisVersion = new Version(Application.ProductVersion);

            if (thisVersion < newVersion) Download();
        }

        private void Download()
        {
            try
            {
                if (File.Exists(tempFileName)) File.Delete(tempFileName);

                WebClient client = new WebClient();
                client.DownloadProgressChanged += new DownloadProgressChangedEventHandler(download_ProgressChanged);
                client.DownloadFileCompleted += new AsyncCompletedEventHandler(download_Completed);
                client.DownloadFileAsync(new Uri(progRemoteFile), tempFileName);
                
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }

        private void download_ProgressChanged(object sender, DownloadProgressChangedEventArgs e)
        {
            try
            {
                downloadProgress1.Value = e.ProgressPercentage;
            }
            catch (Exception) { }
        }

        private void download_Completed(object sender, AsyncCompletedEventArgs e)
        {
            try
            {
                Process.Start("LanScannerUpdater.exe", tempFileName + " " + Application.ProductName + ".exe");
                Process.GetCurrentProcess().Kill();
            }
            catch (Exception) { }
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            SendData();
            CheckUpdates();
        }
    }
}
