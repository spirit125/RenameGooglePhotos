using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
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
using FolderBrowserEx;
using System.Text.Json;
using static System.Windows.Forms.Design.AxImporter;
using Microsoft.VisualBasic;
using System.IO;

namespace RenameGooglePhotos
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private class JsonTimeClass
        {
            public string? timestamp { get; set; }
            public string? formatted { get; set; }
            public DateTime? GetDateTime()
            {
                if (double.TryParse(this.timestamp, out double dblTime))
                {
                    return UnixTimeStampToDateTime(dblTime);
                }
                else
                {
                    return null;
                }
            }

            public static DateTime UnixTimeStampToDateTime(double unixTimeStamp)
            {
                // Unix timestamp is seconds past epoch
                DateTime dateTime = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
                dateTime = dateTime.AddSeconds(unixTimeStamp).ToLocalTime();
                return dateTime;
            }

            public static DateTime JavaTimeStampToDateTime(double javaTimeStamp)
            {
                // Java timestamp is milliseconds past epoch
                DateTime dateTime = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
                dateTime = dateTime.AddMilliseconds(javaTimeStamp).ToLocalTime();
                return dateTime;
            }
        }

        private class JsonClass
        {
            public string? title { get; set; }
            public string? description { get; set; }
            public string? imageViews { get; set; }
            public JsonTimeClass? creationTime { get; set; }
            public JsonTimeClass? photoTakenTime { get; set; }
        }

        private Dictionary<string, string> _dicFileName = new Dictionary<string, string>();

        public MainWindow()
        {
            InitializeComponent();
        }

        private void btnToPath_Click(object sender, RoutedEventArgs e)
        {
            FolderBrowserDialog folderBrowserDialog = new FolderBrowserDialog();
            folderBrowserDialog.Title = "Select a folder";
            folderBrowserDialog.InitialFolder = tbxToPath.Text;// @"C:\";
            folderBrowserDialog.AllowMultiSelect = false;
            if (folderBrowserDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                tbxToPath.Text = folderBrowserDialog.SelectedFolder;
                string result = folderBrowserDialog.SelectedFolder;
            }
        }

        private List<string> GetAllFilePath(string StrFolderPath, string StrSearchPattern)
        {
            var arrSearch = new List<string>();
            var arrSearchPattern = StrSearchPattern.Split(",", StringSplitOptions.RemoveEmptyEntries);
            foreach ( var SearchPattern in arrSearchPattern)
            {
                arrSearch.AddRange(System.IO.Directory.GetFiles(StrFolderPath, SearchPattern, System.IO.SearchOption.AllDirectories));
            }
            return arrSearch.Distinct().ToList();//去除重複資料
        }

        private void BtnStart_Click(object sender, RoutedEventArgs e)
        {
            _dicFileName.Clear();
            string StrFolderPath = tbxToPath.Text;
            string StrSearchPattern = tbxSearchPattern.Text;
            if (!System.IO.Directory.Exists(StrFolderPath))
            {
                MessageBox.Show("路徑不存在");
                return;
            }

            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            var AllFiles = GetAllFilePath(StrFolderPath, StrSearchPattern);
            foreach (var file in AllFiles)
            {
                System.IO.FileInfo fileInfo = new System.IO.FileInfo(file);
                var dtJsonPhotoTime = GetJsonPhotoTakenTime(fileInfo);
                DateTime? dtPhotoTime = null;
                if (dtJsonPhotoTime == null)
                {
                    AddLogMsg(sb, GetRelativePath(file, StrFolderPath, false) + "，取不到json檔");
                    var dtFilePhotoTime = GetPhotoTime(file);
                    if (dtFilePhotoTime != null)
                    {
                        dtPhotoTime = dtFilePhotoTime;
                        AddLogMsg(sb, GetRelativePath(file, StrFolderPath, false) + "，拍攝日期：" + dtFilePhotoTime.Value.ToString("yyyy/MM/dd_HH:mm:ss"));
                    };
                }
                else {
                    dtPhotoTime = dtJsonPhotoTime;
                    AddLogMsg(sb, GetRelativePath(file, StrFolderPath, false) + "，(json檔記錄)拍攝日期：" + dtJsonPhotoTime.Value.ToString("yyyy/MM/dd_HH:mm:ss"));
                }
                if (dtPhotoTime != null)
                {
                    string NewFileNamePath = GetNewFileName(fileInfo, dtPhotoTime.Value);
                    AddLogMsg(sb, GetRelativePath(file, StrFolderPath, false) + "，新的檔名："+ GetRelativePath(NewFileNamePath, StrFolderPath, false));
                    _dicFileName.Add(NewFileNamePath, file);
                    fileInfo.MoveTo(NewFileNamePath);
                }
            }

            tbxMemo.Text =sb.ToString();
            MessageBox.Show("完成");
        }

        private void AddLogMsg(System.Text.StringBuilder sb, string aMsg)
        {
            sb.AppendLine(aMsg);
        }

        //從json檔取得拍攝時間
        private DateTime? GetJsonPhotoTakenTime(System.IO.FileInfo fileInfo)
        {
            var JsonFilePath = GetJsonFilePath(fileInfo);
            if (JsonFilePath == null)
            {
                return null;
            }
            else
            {
                var json = System.IO.File.ReadAllText(JsonFilePath);
                var JsonObj = JsonSerializer.Deserialize<JsonClass>(json);
                if (JsonObj == null)
                {
                    return null;
                }
                else
                {
                    if (JsonObj.photoTakenTime == null)
                    {
                        return null;
                    }
                    else
                    {
                        return JsonObj.photoTakenTime.GetDateTime();
                    }
                }
            }
        }

        //取得圖檔對應的json檔路徑
        private string? GetJsonFilePath(System.IO.FileInfo fileInfo)
        {
            string JsonPath;
            //DSC_0003.JPG 取出 DSC_0003.JPG.json
            JsonPath = fileInfo.FullName + ".json";
            if (System.IO.File.Exists(JsonPath))
            {
                return JsonPath;
            }
            //IMG_0106-已編輯.JPG 取出 IMG_0106.JPG.json
            JsonPath = fileInfo.DirectoryName + "\\" + fileInfo.Name.Replace("-已編輯", "") + ".json";
            if (System.IO.File.Exists(JsonPath))
            {
                return JsonPath;
            }
            //DSC_0003(1).JPG 取出 DSC_0003.JPG(1).json
            var n1 = fileInfo.Name.LastIndexOf("(");
            var n2 = fileInfo.Name.LastIndexOf(")");
            if (n1 != -1 && n2 != -1 && n2 > n1)
            {
                JsonPath = fileInfo.DirectoryName + "\\" + fileInfo.Name.Substring(0, n1) + fileInfo.Name.Substring(n2 + 1) + fileInfo.Name.Substring(n1, n2 - n1+1) + ".json";
                if (System.IO.File.Exists(JsonPath))
                {
                    return JsonPath;
                }
            }
            return null;
        }

        //取得檔案拍攝日期
        private static DateTime? GetPhotoTime(string file)
        {
            try
            {
                using (var image = System.Drawing.Image.FromFile(file))
                {
                    if (image.PropertyItems.Any(p => p.Id == 0x9003))
                    {
                        var propItem = image.GetPropertyItem(0x9003); //Id 为 0x9003 表示拍照的时间
                        if (propItem != null && propItem.Value != null)
                        {
                            var propItemValue = propItem.Value;
                            var dateTimeStr = System.Text.Encoding.ASCII.GetString(propItemValue).Trim('\0');
                            var dt = DateTime.ParseExact(dateTimeStr, "yyyy:MM:dd HH:mm:ss", CultureInfo.InvariantCulture);
                            return dt;
                        }
                    }
                };
            }
            catch { }
            return null;
        }

        //某個資料夾為基準推算檔案的相對路徑
        private static string GetRelativePath(string fullPath, string basePath,
            bool limitSubfolder = false)
        {
            if (!basePath.EndsWith(@"\")) basePath += @"\";
            Uri fp = new Uri(fullPath);
            Uri bp = new Uri(basePath);
            var relPath = bp.MakeRelativeUri(fp).ToString().Replace("/", @"\");
            if (relPath.Contains(@"..\") && limitSubfolder)
                throw new ApplicationException("path must be under basePath!");
            return relPath;
        }

        //路徑\DSC_0003(1).JPG 回傳 路徑\20170624_102755[DSC_0003(1)].JPG
        private string GetNewFileName(System.IO.FileInfo fileInfo, DateTime dtPhotoTime)
        {
            var strDate = dtPhotoTime.ToString("yyyyMMdd_HHmmss");
            var n = fileInfo.Name.LastIndexOf(".");
            return fileInfo.DirectoryName + "\\" + strDate + "[" + fileInfo.Name.Substring(0, n) + "]" + fileInfo.Name.Substring(n);
        }

        //恢復檔案名稱
        private void BtnRecover_Click(object sender, RoutedEventArgs e)
        {
            foreach(var file in _dicFileName.Keys)
            {
                System.IO.FileInfo fileInfo = new System.IO.FileInfo(file);
                if (fileInfo.Exists) {
                    fileInfo.MoveTo(_dicFileName[file]);
                }
            }
            _dicFileName.Clear();
            MessageBox.Show("完成");
        }
    }
}
