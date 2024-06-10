using System;
using System.IO;
using System.Drawing;
using System.Windows.Forms;
using System.Collections;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace FileExplorer
{
    public partial class Form1 : Form
    {
        private bool showAllFolders = false;
        private int sortColumn = -1;
        private ContextMenuStrip contextMenuStrip1;
        private ContextMenuStrip recycleBinContextMenuStrip;
        private string copiedFilePath = null;
        private string currentPath;
        private ComboBox formatFilterComboBox;
        private Stack<string> folderHistoryBack = new Stack<string>();
        private Stack<string> folderHistoryForward = new Stack<string>();
        private bool filterFilesOnly = false;

        public Form1()
        {
            InitializeComponent();
            InitializeImageList();
            LoadDrives();
            InitializeContextMenu();
            InitializeRecycleBinContextMenu();
            InitializeFormatFilterComboBox();
            InitializeDragAndDrop();
            treeView1.NodeMouseClick += new TreeNodeMouseClickEventHandler(treeView1_NodeMouseClick);
            treeView1.AfterSelect += new TreeViewEventHandler(treeView1_AfterSelect);
            ApplyStyles();

        }
        private void ApplyStyles()
        {
            this.BackColor = Color.WhiteSmoke;
            treeView1.BackColor = Color.White;
            listView1.BackColor = Color.White;
            detailsTextBox.BackColor = Color.White;
            detailsTextBox.Font = new Font("Arial", 10);
            toggleFoldersButton.BackColor = Color.LightGray;
            toggleFoldersButton.FlatStyle = FlatStyle.Flat;
            searchTextBox.BorderStyle = BorderStyle.FixedSingle;
            searchButton.BackColor = Color.LightGray;
            searchButton.FlatStyle = FlatStyle.Flat;
            backButton.FlatStyle = FlatStyle.Flat;
            forwardButton.FlatStyle = FlatStyle.Flat;
        }

        private void InitializeImageList()
        {
            imageList1 = new ImageList();
            imageList1.ImageSize = new Size(16, 16);
            imageList1.ColorDepth = ColorDepth.Depth32Bit;

            imageList1.Images.Add("folder", Properties.Resources.folder_icon);
            imageList1.Images.Add("file_icon", Properties.Resources.file_icon);
            imageList1.Images.Add("txt_icon", Properties.Resources.txt_icon);
            imageList1.Images.Add("image_icon", Properties.Resources.image_icon);
            imageList1.Images.Add("video_icon", Properties.Resources.video_icon);
            imageList1.Images.Add("program_icon", Properties.Resources.program_icon);
            imageList1.Images.Add("disk_icon", Properties.Resources.disk_icon);
            imageList1.Images.Add("bin_icon", Properties.Resources.bin_icon);
            imageList1.Images.Add("disk_icon", Properties.Resources.disk_icon);
            imageList1.Images.Add("bin_icon", Properties.Resources.bin_icon);
            imageList1.Images.Add("desk_icon", Properties.Resources.desk_icon);
            imageList1.Images.Add("down_icon", Properties.Resources.down_icon);
            listView1.SmallImageList = imageList1;
            treeView1.ImageList = imageList1;
        }

        private void InitializeFormatFilterComboBox()
        {
            formatFilterComboBox = new ComboBox();
            formatFilterComboBox.Items.AddRange(new string[] { "Wszystkie pliki", ".txt", ".jpg", ".jpeg", ".png", ".doc", ".docx", ".mp3", ".mp4" });
            formatFilterComboBox.SelectedIndex = 0;
            formatFilterComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
            formatFilterComboBox.SelectedIndexChanged += FormatFilterComboBox_SelectedIndexChanged;

            topPanel.Controls.Add(formatFilterComboBox);
            formatFilterComboBox.Location = new Point(searchButton.Left - 45, searchButton.Bottom + 5);
            formatFilterComboBox.Size = new Size(120, 23);
            formatFilterComboBox.Anchor = AnchorStyles.Top;
        }

        private void FormatFilterComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            filterFilesOnly = formatFilterComboBox.SelectedIndex != 0;
            if (!string.IsNullOrEmpty(currentPath) && Directory.Exists(currentPath))
            {
                LoadFilesAndDirectories(currentPath);
            }
        }

        private void LoadDrives()
        {
            treeView1.Nodes.Clear();
            foreach (var drive in Directory.GetLogicalDrives())
            {
                try
                {
                    var driveNode = new TreeNode(drive)
                    {
                        Tag = drive,
                        ImageKey = "disk_icon",
                        SelectedImageKey = "disk_icon"
                    };
                    treeView1.Nodes.Add(driveNode);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Błąd załadowania dysku {drive}: {ex.Message}");
                }
            }
            LoadSpecialFolders();
        }

        private void LoadSpecialFolders()
        {
            try
            {
                string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                if (!IsNodeExists(desktopPath))
                {
                    var desktopNode = new TreeNode("Pulpit")
                    {
                        Tag = desktopPath,
                        ImageKey = "desk_icon",
                        SelectedImageKey = "desk_icon"
                    };
                    treeView1.Nodes.Add(desktopNode);
                }

                string downloadsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
                if (!IsNodeExists(downloadsPath))
                {
                    var downloadsNode = new TreeNode("Pobrane pliki")
                    {
                        Tag = downloadsPath,
                        ImageKey = "down_icon",
                        SelectedImageKey = "down_icon"
                    };
                    treeView1.Nodes.Add(downloadsNode);
                }

                string recycleBinTag = "Kosz";
                if (!IsNodeExists(recycleBinTag))
                {
                    var binNode = new TreeNode("Kosz")
                    {
                        Tag = recycleBinTag,
                        ImageKey = "bin_icon",
                        SelectedImageKey = "bin_icon"
                    };
                    treeView1.Nodes.Add(binNode);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Nie można załadować: {ex.Message}");
            }
        }

        private bool IsNodeExists(string tag)
        {
            foreach (TreeNode node in treeView1.Nodes)
            {
                if (node.Tag != null && node.Tag.ToString() == tag)
                {
                    return true;
                }
            }
            return false;
        }

        private void LoadDirectories(TreeNode parentNode)
        {
            try
            {
                string path = parentNode.Tag as string;

                if (path == "RecycleBin")
                {
                    LoadRecycleBin();
                    return;
                }

                parentNode.Nodes.Clear();
                var directories = Directory.GetDirectories(path);
                foreach (var directory in directories)
                {
                    var dirInfo = new DirectoryInfo(directory);
                    if (showAllFolders || !dirInfo.Attributes.HasFlag(FileAttributes.Hidden))
                    {
                        var dirNode = new TreeNode(dirInfo.Name);
                        dirNode.Tag = directory;
                        dirNode.ImageKey = "folder";
                        dirNode.SelectedImageKey = "folder";
                        parentNode.Nodes.Add(dirNode);
                    }
                }
            }
            catch (UnauthorizedAccessException)
            {
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Nie udało się załadować: {ex.Message}");
            }
        }

        private void LoadFilesAndDirectories(string path, bool addToHistory = true)
        {
            try
            {
                if (addToHistory)
                {
                    if (!string.IsNullOrEmpty(currentPath))
                    {
                        folderHistoryBack.Push(currentPath);
                    }
                    folderHistoryForward.Clear();
                }

                listView1.Clear();
                listView1.Columns.Add("Nazwa", 200);
                listView1.Columns.Add("Typ", 100);
                listView1.Columns.Add("Format", 100);
                listView1.Columns.Add("Rozmiar", 100);

                var directories = Directory.GetDirectories(path);
                if (!filterFilesOnly)
                {
                    foreach (var directory in directories)
                    {
                        var dirInfo = new DirectoryInfo(directory);
                        if (showAllFolders || !dirInfo.Attributes.HasFlag(FileAttributes.Hidden))
                        {
                            var listItem = new ListViewItem(new[] { dirInfo.Name, "", "", "" }, "folder");
                            listItem.Tag = directory;
                            listView1.Items.Add(listItem);
                        }
                    }
                }

                var files = Directory.GetFiles(path);
                string selectedFormat = formatFilterComboBox.SelectedItem.ToString();
                foreach (var file in files)
                {
                    var fileInfo = new FileInfo(file);
                    if (selectedFormat == "Wszystkie pliki" || fileInfo.Extension.Equals(selectedFormat, StringComparison.OrdinalIgnoreCase))
                    {
                        string iconKey = GetIconKey(fileInfo.Extension);
                        var listItem = new ListViewItem(new[] { fileInfo.Name, "Plik", fileInfo.Extension, FormatSize(fileInfo.Length) }, iconKey);
                        listItem.Tag = file;
                        listView1.Items.Add(listItem);
                    }
                }

                ShowDirectoryDetails(path); 
                UpdateCurrentPath(path);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Nie udało się załadować: {ex.Message}");
            }
        }

        private string GetIconKey(string extension)
        {
            switch (extension.ToLower())
            {
                case ".txt":
                    return "txt_icon";
                case ".jpg":
                case ".jpeg":
                case ".png":
                case ".bmp":
                    return "image_icon";
                case ".avi":
                case ".mp4":
                case ".mkv":
                    return "video_icon";
                case ".exe":
                case ".dll":
                    return "program_icon";
                default:
                    return "file_icon";
            }
        }

        private void treeView1_NodeMouseClick(object sender, TreeNodeMouseClickEventArgs e)
        {
            if (e.Button == MouseButtons.Right && e.Node.Tag.ToString() == "Kosz")
            {
                recycleBinContextMenuStrip.Show(treeView1, e.Location);
            }
            else if (e.Button == MouseButtons.Left)
            {
                treeView1.SelectedNode = e.Node;

                string path = e.Node.Tag as string;
                if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
                {
                    if (e.Node.IsExpanded)
                    {
                        e.Node.Collapse();
                    }
                    else
                    {
                        e.Node.Expand();
                    }
                    LoadDirectories(e.Node);
                    LoadFilesAndDirectories(path);
                }

                pictureBox1.Image = null;
                detailsTextBox.Clear();
            }
        }


        private void treeView1_AfterSelect(object sender, TreeViewEventArgs e)
        {
            TreeNode selectedNode = e.Node;
            string path = selectedNode.Tag as string;

            if (!string.IsNullOrEmpty(path))
            {
                if (Directory.Exists(path))
                {
                    LoadFilesAndDirectories(path);
                    pictureBox1.Image = null;
                    detailsTextBox.Clear();
                }
                else if (path == "Kosz")
                {
                    LoadRecycleBin();
                }
            }
        }

        private void listView1_DoubleClick(object sender, EventArgs e)
        {
            if (listView1.SelectedItems.Count == 1)
            {
                var selectedItem = listView1.SelectedItems[0];
                var path = selectedItem.Tag as string;

                if (Directory.Exists(path))
                {
                    LoadFilesAndDirectories(path);
                }
                else if (File.Exists(path))
                {
                    OpenFile(path);
                    var extension = Path.GetExtension(path).ToLower();
                    if (extension == ".jpg" || extension == ".jpeg" || extension == ".png" || extension == ".bmp")
                    {
                        DisplayImageFileContent(path);
                    }
                }
            }
        }

        private void listView1_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (listView1.SelectedItems.Count == 1)
            {
                var selectedItem = listView1.SelectedItems[0];
                var path = selectedItem.Tag as string;

                if (File.Exists(path))
                {
                    FileInfo fileInfo = new FileInfo(path);
                    detailsTextBox.Clear();
                    detailsTextBox.AppendText($"Nazwa: {fileInfo.Name}\n");
                    detailsTextBox.AppendText($"Rozmiar: {FormatSize(fileInfo.Length)}\n");
                    detailsTextBox.AppendText($"Ścieżka: {fileInfo.DirectoryName}\n");
                    detailsTextBox.AppendText($"Utworzono: {fileInfo.CreationTime}\n");
                    detailsTextBox.AppendText($"Zmodyfikowano: {fileInfo.LastWriteTime}\n");
                    detailsTextBox.AppendText($"Format: {fileInfo.Extension}\n");

                    if (fileInfo.Extension.ToLower() == ".txt" || fileInfo.Extension.ToLower() == ".csv")
                    {
                        DisplayTextFileContent(path);
                    }
                    else if (fileInfo.Extension.ToLower() == ".jpg" || fileInfo.Extension.ToLower() == ".jpeg" ||
                             fileInfo.Extension.ToLower() == ".png" || fileInfo.Extension.ToLower() == ".bmp")
                    {
                        DisplayImageFileContent(path);
                    }
                    else
                    {
                        detailsTextBox.Visible = true;
                        detailsTextBox.Dock = DockStyle.Fill;
                        pictureBox1.Image = null;
                        pictureBox1.Visible = false;
                    }
                }
            }
        }

        private void DisplayTextFileContent(string path)
        {
            try
            {
                using (StreamReader reader = new StreamReader(path))
                {
                    detailsTextBox.Text = reader.ReadToEnd();
                }
                detailsTextBox.Visible = true;
                detailsTextBox.Dock = DockStyle.Fill;
                pictureBox1.Image = null;
                pictureBox1.Visible = false;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Nie udało się załadować pliku: {ex.Message}");
            }
        }

        private void DisplayImageFileContent(string path)
        {
            try
            {
                Image image = Image.FromFile(path);
                pictureBox1.Image = image;
                pictureBox1.Visible = true;
                pictureBox1.Dock = DockStyle.Fill;
                detailsTextBox.Visible = false;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Nie udało się załadować zdjęcia: {ex.Message}");
            }
        }

        private void toggleFoldersButton_Click(object sender, EventArgs e)
        {
            showAllFolders = !showAllFolders;
            if (showAllFolders)
            {
                toggleFoldersButton.Text = "Ukryj ukryte foldery";
            }
            else
            {
                toggleFoldersButton.Text = "Pokaż ukryte foldery";
            }
            LoadDrives();
        }

        private void listView1_ColumnClick(object sender, ColumnClickEventArgs e)
        {
            if (e.Column != sortColumn)
            {
                sortColumn = e.Column;
                listView1.Sorting = SortOrder.Ascending;
            }
            else
            {
                if (listView1.Sorting == SortOrder.Ascending)
                {
                    listView1.Sorting = SortOrder.Descending;
                }
                else
                {
                    listView1.Sorting = SortOrder.Ascending;
                }
            }
            listView1.Sort();
            this.listView1.ListViewItemSorter = new ListViewItemComparer(e.Column, listView1.Sorting);
        }

        public class ListViewItemComparer : IComparer
        {
            private int col;
            private SortOrder order;

            public ListViewItemComparer()
            {
                col = 0;
                order = SortOrder.Ascending;
            }

            public ListViewItemComparer(int column, SortOrder order)
            {
                col = column;
                this.order = order;
            }

            public int Compare(object x, object y)
            {
                int returnVal = -1;

                if (col == 3)
                {
                    long sizeX = ParseFileSize(((ListViewItem)x).SubItems[col].Text);
                    long sizeY = ParseFileSize(((ListViewItem)y).SubItems[col].Text);
                    returnVal = sizeX.CompareTo(sizeY);
                }
                else
                {
                    returnVal = String.Compare(((ListViewItem)x).SubItems[col].Text, ((ListViewItem)y).SubItems[col].Text);
                }

                if (order == SortOrder.Descending)
                {
                    returnVal *= -1;
                }

                return returnVal;
            }
            private long ParseFileSize(string text)
            {
                if (string.IsNullOrEmpty(text))
                {
                    return 0;
                }

                text = text.Trim();

                double size;
                if (text.EndsWith("KB"))
                {
                    size = double.Parse(text.Replace(" KB", "")) * 1024;
                }
                else if (text.EndsWith("MB"))
                {
                    size = double.Parse(text.Replace(" MB", "")) * 1024 * 1024;
                }
                else if (text.EndsWith("GB"))
                {
                    size = double.Parse(text.Replace(" GB", "")) * 1024 * 1024 * 1024;
                }
                else
                {
                    size = double.Parse(text.Replace(" B", ""));
                }

                return (long)size;
            }
        }
        private void ShowDirectoryDetails(string path)
        {
            try
            {
                detailsTextBox.Clear();

                if (Directory.Exists(path))
                {
                    DirectoryInfo dirInfo = new DirectoryInfo(path);
                    detailsTextBox.AppendText($"Adres: {dirInfo.FullName}\n");
                    detailsTextBox.AppendText($"Utworzono: {dirInfo.CreationTime}\n");
                    detailsTextBox.AppendText($"Modyfikowano: {dirInfo.LastWriteTime}\n");

                    DriveInfo driveInfo = new DriveInfo(dirInfo.Root.FullName);
                    detailsTextBox.AppendText($"\nDysk: {driveInfo.Name}\n");
                    detailsTextBox.AppendText($"Całkowity rozmiar: {FormatSize(driveInfo.TotalSize)}\n");
                    detailsTextBox.AppendText($"Wolne Miejsce: {FormatSize(driveInfo.AvailableFreeSpace)}\n");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Nie udało się załadować: {ex.Message}");
            }
        }

        private string FormatSize(long size)
        {
            if (size >= 1024 * 1024 * 1024)
                return $"{size / (1024 * 1024 * 1024.0):0.0} GB";
            else if (size >= 1024 * 1024)
                return $"{size / (1024 * 1024.0):0.0} MB";
            else
                return $"{size / 1024.0:0.0} KB";
        }

        private void UpdateCurrentPath(string path)
        {
            currentPath = path;
            richTextBox1.Clear();
            richTextBox1.AppendText(path + "\n\n");
        }

        private void OpenFile(string filePath)
        {
            try
            {
                Process.Start(new ProcessStartInfo(filePath) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Nie udało się otworzyć pliku: {ex.Message}");
            }
        }

        private async void searchButton_Click(object sender, EventArgs e)
        {
            string searchQuery = searchTextBox.Text;
            if (!string.IsNullOrWhiteSpace(searchQuery))
            {
                await SearchFilesAndDirectoriesAsync(searchQuery);
            }
        }

        private async Task SearchFilesAndDirectoriesAsync(string query)
        {
            try
            {
                listView1.Items.Clear();
                foreach (var drive in Directory.GetLogicalDrives())
                {
                    await Task.Run(() => SearchInDirectory(drive, query));
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Nie znaleziono pliku: {ex.Message}");
            }
        }

        private void SearchInDirectory(string directory, string query)
        {
            try
            {
                var dirInfo = new DirectoryInfo(directory);
                var directories = dirInfo.GetDirectories();
                foreach (var dir in directories)
                {
                    if (dir.Name.StartsWith(query, StringComparison.OrdinalIgnoreCase))
                    {
                        var listItem = new ListViewItem(new[] { dir.Name, "Ścieżka", "", "" }, "folder");
                        listItem.Tag = dir.FullName;
                        Invoke(new Action(() => listView1.Items.Add(listItem)));
                    }
                    SearchInDirectory(dir.FullName, query);
                }

                var files = dirInfo.GetFiles();
                foreach (var file in files)
                {
                    if (file.Name.StartsWith(query, StringComparison.OrdinalIgnoreCase))
                    {
                        string iconKey = GetIconKey(file.Extension);
                        var listItem = new ListViewItem(new[] { file.Name, "Plik", file.Extension, FormatSize(file.Length) }, iconKey);
                        listItem.Tag = file.FullName;
                        Invoke(new Action(() => listView1.Items.Add(listItem)));
                    }
                }
            }
            catch (UnauthorizedAccessException)
            {
                
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Nie znaleziono '{directory}': {ex.Message}");
            }
        }

        private void searchTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                searchButton.PerformClick();
            }
        }

        private void LoadRecycleBin()
        {
            listView1.Items.Clear();
            SHQUERYRBINFO queryInfo = new SHQUERYRBINFO();
            queryInfo.cbSize = Marshal.SizeOf(typeof(SHQUERYRBINFO));
            int result = SHQueryRecycleBin(null, ref queryInfo);
            if (result == 0)
            {
                IShellFolder desktopFolder;
                SHGetDesktopFolder(out desktopFolder);
                IntPtr pidlRecycleBin;
                SHGetSpecialFolderLocation(IntPtr.Zero, CSIDL_BITBUCKET, out pidlRecycleBin);
                IShellFolder recycleBinFolder;
                desktopFolder.BindToObject(pidlRecycleBin, IntPtr.Zero, typeof(IShellFolder).GUID, out recycleBinFolder);
                IEnumIDList enumIDList;
                recycleBinFolder.EnumObjects(IntPtr.Zero, SHCONTF.FOLDERS | SHCONTF.NONFOLDERS, out enumIDList);
                IntPtr pidl;
                uint fetched;
                while (enumIDList.Next(1, out pidl, out fetched) == 0)
                {
                    string displayName = GetDisplayName(recycleBinFolder, pidl);
                    string iconKey = GetIconKey(Path.GetExtension(displayName));
                    listView1.Items.Add(new ListViewItem(displayName, iconKey));
                    Marshal.FreeCoTaskMem(pidl);
                }
                Marshal.FreeCoTaskMem(pidlRecycleBin);
                Marshal.ReleaseComObject(enumIDList);
                Marshal.ReleaseComObject(recycleBinFolder);
                Marshal.ReleaseComObject(desktopFolder);
            }
            else
            {
                MessageBox.Show($"Nie udało się załadować kosza, kod błędu: {result}");
            }
        }

        private string GetDisplayName(IShellFolder folder, IntPtr pidl)
        {
            STRRET strret;
            folder.GetDisplayNameOf(pidl, SHGDN.INFOLDER, out strret);
            return StrRetToString(ref strret, pidl);
        }

        private string GetOriginalLocation(IShellFolder folder, IntPtr pidl)
        {
            STRRET strret;
            folder.GetDisplayNameOf(pidl, SHGDN.FORPARSING, out strret);
            return StrRetToString(ref strret, pidl);
        }

        private string StrRetToString(ref STRRET pstr, IntPtr pidl)
        {
            switch (pstr.uType)
            {
                case 0: // STRRET_WSTR
                    return Marshal.PtrToStringUni(pstr.pOleStr);
                case 1: // STRRET_OFFSET
                    IntPtr ptr = IntPtr.Add(pidl, (int)pstr.uOffset);
                    return Marshal.PtrToStringAnsi(ptr);
                case 2: // STRRET_CSTR
                    return pstr.cStr;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        private static extern int SHQueryRecycleBin(string pszRootPath, ref SHQUERYRBINFO pSHQueryRBInfo);

        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        private static extern int SHGetDesktopFolder(out IShellFolder ppshf);

        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        private static extern int SHGetSpecialFolderLocation(IntPtr hwndOwner, int nFolder, out IntPtr ppidl);

        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        private static extern uint SHEmptyRecycleBin(IntPtr hwnd, string pszRootPath, uint dwFlags);

        private const uint SHERB_NOCONFIRMATION = 0x00000001;
        private const uint SHERB_NOPROGRESSUI = 0x00000002;
        private const uint SHERB_NOSOUND = 0x00000004;

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct SHQUERYRBINFO
        {
            public int cbSize;
            public long i64Size;
            public long i64NumItems;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct STRRET
        {
            public uint uType;
            public IntPtr pOleStr;
            public uint uOffset;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string cStr;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct SHFILEINFO
        {
            public IntPtr hIcon;
            public int iIcon;
            public uint dwAttributes;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string szDisplayName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
            public string szTypeName;
        }

        [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("000214E6-0000-0000-C000-000000000046")]
        private interface IShellFolder
        {
            void ParseDisplayName(IntPtr hwnd, IntPtr pbc, [MarshalAs(UnmanagedType.LPWStr)] string pszDisplayName, ref uint pchEaten, out IntPtr ppidl, ref uint pdwAttributes);
            void EnumObjects(IntPtr hwnd, SHCONTF grfFlags, out IEnumIDList ppenumIDList);
            void BindToObject(IntPtr pidl, IntPtr pbc, [MarshalAs(UnmanagedType.LPStruct)] Guid riid, out IShellFolder ppv);
            void BindToStorage(IntPtr pidl, IntPtr pbc, [MarshalAs(UnmanagedType.LPStruct)] Guid riid, out IntPtr ppv);
            void CompareIDs(IntPtr lParam, IntPtr pidl1, IntPtr pidl2);
            void CreateViewObject(IntPtr hwndOwner, [MarshalAs(UnmanagedType.LPStruct)] Guid riid, out IntPtr ppv);
            void GetAttributesOf(uint cidl, [MarshalAs(UnmanagedType.LPArray)] IntPtr[] apidl, ref uint rgfInOut);
            void GetUIObjectOf(IntPtr hwndOwner, uint cidl, [MarshalAs(UnmanagedType.LPArray)] IntPtr[] apidl, [MarshalAs(UnmanagedType.LPStruct)] Guid riid, ref uint rgfReserved, out IntPtr ppv);
            void GetDisplayNameOf(IntPtr pidl, SHGDN uFlags, out STRRET pName);
            void SetNameOf(IntPtr hwnd, IntPtr pidl, [MarshalAs(UnmanagedType.LPWStr)] string pszName, uint uFlags, out IntPtr ppidlOut);
        }

        [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("000214F2-0000-0000-C000-000000000046")]
        private interface IEnumIDList
        {
            [PreserveSig]
            int Next(uint celt, out IntPtr rgelt, out uint pceltFetched);
            void Skip(uint celt);
            void Reset();
            void Clone(out IEnumIDList ppenum);
        }

        [Flags]
        private enum SHCONTF
        {
            FOLDERS = 0x0020,
            NONFOLDERS = 0x0040,
            INCLUDEHIDDEN = 0x0080,
            INIT_ON_FIRST_NEXT = 0x0100,
            NETPRINTERSRCH = 0x0200,
            SHAREABLE = 0x0400,
            STORAGE = 0x0800,
        }

        private enum SHGDN
        {
            NORMAL = 0x0000,
            INFOLDER = 0x0001,
            FORPARSING = 0x8000,
        }

        private const int CSIDL_BITBUCKET = 0x000A;

        private void InitializeContextMenu()
        {
            contextMenuStrip1 = new ContextMenuStrip();
            contextMenuStrip1.Opening += (sender, e) =>
            {
                e.Cancel = listView1.SelectedItems.Count == 0;
            };

            var renameMenuItem = new ToolStripMenuItem("Zmień nazwę");
            renameMenuItem.Click += RenameMenuItem_Click;
            contextMenuStrip1.Items.Add(renameMenuItem);

            var copyMenuItem = new ToolStripMenuItem("Kopiuj");
            copyMenuItem.Click += CopyMenuItem_Click;
            contextMenuStrip1.Items.Add(copyMenuItem);

            var pasteMenuItem = new ToolStripMenuItem("Wklej");
            pasteMenuItem.Click += PasteMenuItem_Click;
            contextMenuStrip1.Items.Add(pasteMenuItem);

            var moveToTrashMenuItem = new ToolStripMenuItem("Przenieś do kosza");
            moveToTrashMenuItem.Click += MoveToTrashMenuItem_Click;
            contextMenuStrip1.Items.Add(moveToTrashMenuItem);

            listView1.ContextMenuStrip = contextMenuStrip1;
        }

        private void listView1_MouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                var item = listView1.HitTest(e.X, e.Y).Item;
                if (item != null)
                {
                    listView1.SelectedItems.Clear();
                    item.Selected = true;
                    contextMenuStrip1.Show(listView1, e.Location);
                }
            }
        }

        private void InitializeRecycleBinContextMenu()
        {
            recycleBinContextMenuStrip = new ContextMenuStrip();

            var emptyRecycleBinMenuItem = new ToolStripMenuItem("Opróżnij kosz");
            emptyRecycleBinMenuItem.Click += EmptyRecycleBinMenuItem_Click;
            recycleBinContextMenuStrip.Items.Add(emptyRecycleBinMenuItem);
        }

        private void EmptyRecycleBinMenuItem_Click(object sender, EventArgs e)
        {
            EmptyRecycleBin();
        }

        private void RenameMenuItem_Click(object sender, EventArgs e)
        {
            if (listView1.SelectedItems.Count == 1)
            {
                var selectedItem = listView1.SelectedItems[0];
                var path = selectedItem.Tag as string;

                if (File.Exists(path) || Directory.Exists(path))
                {
                    string newName = Prompt.ShowDialog("Podaj nową nazwę", "Zmień nazwę");
                    if (!string.IsNullOrWhiteSpace(newName))
                    {
                        string newPath = Path.Combine(Path.GetDirectoryName(path), newName);

                        try
                        {
                            if (File.Exists(path))
                            {
                                File.Move(path, newPath);
                            }
                            else if (Directory.Exists(path))
                            {
                                Directory.Move(path, newName);
                            }

                            selectedItem.Text = newName;
                            selectedItem.Tag = newPath;
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show($"Błąd zmiany nazwy: {ex.Message}");
                        }
                    }
                }
            }
        }

        private void CopyMenuItem_Click(object sender, EventArgs e)
        {
            if (listView1.SelectedItems.Count == 1)
            {
                var selectedItem = listView1.SelectedItems[0];
                copiedFilePath = selectedItem.Tag as string;
            }
        }

        private void PasteMenuItem_Click(object sender, EventArgs e)
        {
            if (!string.IsNullOrEmpty(copiedFilePath) && Directory.Exists(currentPath))
            {
                try
                {
                    string targetPath = Path.Combine(currentPath, Path.GetFileName(copiedFilePath));
                    File.Copy(copiedFilePath, targetPath, true);
                    LoadFilesAndDirectories(currentPath);
                    MessageBox.Show("Plik został wklejony pomyślnie.");
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Błąd wklejania pliku: {ex.Message}");
                }
            }
        }

        private void MoveToTrashMenuItem_Click(object sender, EventArgs e)
        {
            if (listView1.SelectedItems.Count == 1)
            {
                var selectedItem = listView1.SelectedItems[0];
                var path = selectedItem.Tag as string;

                if (File.Exists(path))
                {
                    try
                    {
                        MoveToTrash(path);
                        LoadFilesAndDirectories(currentPath);
                        MessageBox.Show("Plik został przeniesiony do kosza.");
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Błąd przenoszenia pliku do kosza: {ex.Message}");
                    }
                }
            }
        }

        private void MoveToTrash(string path)
        {
            SHFILEOPSTRUCT shf = new SHFILEOPSTRUCT
            {
                wFunc = WinAPI.FO_DELETE,
                pFrom = path + '\0' + '\0',
                fFlags = WinAPI.FOF_ALLOWUNDO | WinAPI.FOF_NOCONFIRMATION
            };
            WinAPI.SHFileOperation(ref shf);
        }

        private void EmptyRecycleBin()
        {
            try
            {
                uint result = SHEmptyRecycleBin(IntPtr.Zero, null, SHERB_NOCONFIRMATION | SHERB_NOPROGRESSUI | SHERB_NOSOUND);
                if (result != 0)
                {
                    throw new Exception("Operacja nie powiodła się");
                }
                MessageBox.Show("Kosz został opróżniony.");
                LoadRecycleBin();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd opróżniania kosza: {ex.Message}");
            }
        }

        private void InitializeDragAndDrop()
        {
            listView1.AllowDrop = true;
            treeView1.AllowDrop = true;

            listView1.ItemDrag += ListView1_ItemDrag;
            listView1.DragEnter += ListView1_DragEnter;
            listView1.DragOver += ListView1_DragOver;
            listView1.DragDrop += ListView1_DragDrop;

            treeView1.ItemDrag += TreeView1_ItemDrag;
            treeView1.DragEnter += TreeView1_DragEnter;
            treeView1.DragOver += TreeView1_DragOver;
            treeView1.DragDrop += TreeView1_DragDrop;
        }

        private void ListView1_ItemDrag(object sender, ItemDragEventArgs e)
        {
            List<string> paths = new List<string>();
            foreach (ListViewItem item in listView1.SelectedItems)
            {
                paths.Add(item.Tag as string);
            }
            listView1.DoDragDrop(paths, DragDropEffects.Move);
        }

        private void ListView1_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(typeof(List<string>)))
            {
                e.Effect = DragDropEffects.Move;
            }
        }

        private void ListView1_DragOver(object sender, DragEventArgs e)
        {
            e.Effect = DragDropEffects.Move;
        }

        private void ListView1_DragDrop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(typeof(List<string>)))
            {
                List<string> sourcePaths = e.Data.GetData(typeof(List<string>)) as List<string>;
                string destinationPath = currentPath;

                foreach (string sourcePath in sourcePaths)
                {
                    string newFilePath = Path.Combine(destinationPath, Path.GetFileName(sourcePath));
                    if (File.Exists(sourcePath))
                    {
                        File.Move(sourcePath, newFilePath);
                    }
                    else if (Directory.Exists(sourcePath))
                    {
                        Directory.Move(sourcePath, newFilePath);
                    }
                }
                LoadFilesAndDirectories(destinationPath);
            }
        }

        private void TreeView1_ItemDrag(object sender, ItemDragEventArgs e)
        {
            if (e.Item is TreeNode)
            {
                TreeNode draggedNode = (TreeNode)e.Item;
                if (draggedNode.Tag is string path && Directory.Exists(path))
                {
                    treeView1.DoDragDrop(draggedNode, DragDropEffects.Move);
                }
            }
        }
        private void TreeView1_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(typeof(TreeNode)) || e.Data.GetDataPresent(typeof(List<string>)))
            {
                e.Effect = DragDropEffects.Move;
            }
        }

        private void TreeView1_DragOver(object sender, DragEventArgs e)
        {
            e.Effect = DragDropEffects.Move;
        }
        private void TreeView1_DragDrop(object sender, DragEventArgs e)
        {
            Point pt = treeView1.PointToClient(new Point(e.X, e.Y));
            TreeNode destinationNode = treeView1.GetNodeAt(pt);

            if (destinationNode != null && Directory.Exists(destinationNode.Tag as string))
            {
                string destinationPath = destinationNode.Tag as string;

                if (e.Data.GetDataPresent(typeof(TreeNode)))
                {
                    TreeNode draggedNode = (TreeNode)e.Data.GetData(typeof(TreeNode));
                    string sourcePath = draggedNode.Tag as string;

                    try
                    {
                        string newDirectoryPath = Path.Combine(destinationPath, Path.GetFileName(sourcePath));
                        if (!Directory.Exists(newDirectoryPath))
                        {
                            Directory.Move(sourcePath, newDirectoryPath);
                            draggedNode.Remove();
                            TreeNode newNode = new TreeNode(Path.GetFileName(newDirectoryPath))
                            {
                                Tag = newDirectoryPath,
                                ImageKey = "folder",
                                SelectedImageKey = "folder"
                            };
                            destinationNode.Nodes.Add(newNode);
                            destinationNode.Expand();
                        }
                        else
                        {
                            MessageBox.Show("Folder o tej nazwie już istnieje w miejscu docelowym.");
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Błąd przenoszenia folderu: {ex.Message}");
                    }
                }
                else if (e.Data.GetDataPresent(typeof(List<string>)))
                {
                    List<string> sourcePaths = e.Data.GetData(typeof(List<string>)) as List<string>;

                    foreach (string sourcePath in sourcePaths)
                    {
                        try
                        {
                            string newFilePath = Path.Combine(destinationPath, Path.GetFileName(sourcePath));
                            if (File.Exists(sourcePath))
                            {
                                if (!File.Exists(newFilePath))
                                {
                                    File.Move(sourcePath, newFilePath);
                                }
                                else
                                {
                                    MessageBox.Show($"Plik o nazwie {Path.GetFileName(sourcePath)} już istnieje w miejscu docelowym.");
                                }
                            }
                            else if (Directory.Exists(sourcePath))
                            {
                                if (!Directory.Exists(newFilePath))
                                {
                                    Directory.Move(sourcePath, newFilePath);
                                }
                                else
                                {
                                    MessageBox.Show($"Folder o nazwie {Path.GetFileName(sourcePath)} już istnieje w miejscu docelowym.");
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show($"Błąd przenoszenia pliku lub folderu: {ex.Message}");
                        }
                    }
                    LoadDirectories(destinationNode);
                    LoadFilesAndDirectories(destinationPath);
                }
            }
        }

        private void backButton_Click(object sender, EventArgs e)
        {
            if (folderHistoryBack.Count > 0)
            {
                string previousPath = folderHistoryBack.Pop();
                folderHistoryForward.Push(currentPath);
                LoadFilesAndDirectories(previousPath, false);
            }
        }

        private void forwardButton_Click(object sender, EventArgs e)
        {
            if (folderHistoryForward.Count > 0)
            {
                string nextPath = folderHistoryForward.Pop();
                folderHistoryBack.Push(currentPath);
                LoadFilesAndDirectories(nextPath, false);
            }
        }
    }

    public static class Prompt
    {
        public static string ShowDialog(string text, string caption)
        {
            Form prompt = new Form()
            {
                Width = 500,
                Height = 150,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                Text = caption,
                StartPosition = FormStartPosition.CenterScreen
            };
            Label textLabel = new Label() { Left = 50, Top = 20, Text = text };
            TextBox textBox = new TextBox() { Left = 50, Top = 50, Width = 400 };
            Button confirmation = new Button() { Text = "OK", Left = 350, Width = 100, Top = 70, DialogResult = DialogResult.OK };
            confirmation.Click += (sender, e) => { prompt.Close(); };
            prompt.Controls.Add(textBox);
            prompt.Controls.Add(confirmation);
            prompt.Controls.Add(textLabel);
            prompt.AcceptButton = confirmation;

            return prompt.ShowDialog() == DialogResult.OK ? textBox.Text : "";
        }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    public struct SHFILEOPSTRUCT
    {
        public IntPtr hwnd;
        [MarshalAs(UnmanagedType.U4)]
        public int wFunc;
        public string pFrom;
        public string pTo;
        public short fFlags;
        [MarshalAs(UnmanagedType.Bool)]
        public bool fAnyOperationsAborted;
        public IntPtr hNameMappings;
        public string lpszProgressTitle;
    }

    public class WinAPI
    {
        public const int FO_DELETE = 3;
        public const int FOF_ALLOWUNDO = 0x40;
        public const int FOF_NOCONFIRMATION = 0x10;

        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        public static extern int SHFileOperation(ref SHFILEOPSTRUCT FileOp);
    }
}
