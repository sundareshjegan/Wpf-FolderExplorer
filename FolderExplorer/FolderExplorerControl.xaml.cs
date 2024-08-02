#region usings
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;

using System.Reflection;
using System.Security.Principal;

#endregion

namespace FolderExplorer
{
    public partial class FolderExplorerControl : Window
    {
        public FolderExplorerControl()
        {
            InitializeComponent();
            InitializeQuickAccessTags();
            LoadDrives();

            DataContext = this;

            PathSuggestions = new ObservableCollection<string>();
            PathSuggestionsListBox.ItemsSource = PathSuggestions;

            PathTB.GotFocus += PathTB_GotFocus;
            PathTB.LostFocus += PathTB_LostFocus;
            TitleBar.MouseLeftButtonDown += TitleBar_MouseLeftButtonDown;
        }

        private void InitializeQuickAccessTags()
        {
            DesktopItem.Tag = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            DownloadsItem.Tag = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
            PicturesItem.Tag = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
            DocumentsItem.Tag = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            VideosItem.Tag = Environment.GetFolderPath(Environment.SpecialFolder.MyVideos);

            //Console.WriteLine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + "\\Downloads");
            SelectedPath = DesktopItem.Tag.ToString();
            LoadFolders(SelectedPath);
            QuickAccessListView.SelectedIndex = 0;

            CurrentAccessLevel = $"FolderExplorer User - {Environment.UserName}";
        }

        public static readonly DependencyProperty CurrentPathProperty = DependencyProperty.Register("CurrentPath", typeof(string), typeof(FolderExplorerControl), new PropertyMetadata(string.Empty));

        public string SelectedPath
        {
            get { return (string)GetValue(CurrentPathProperty); }
            set { SetValue(CurrentPathProperty, value); }
        }

        public static readonly DependencyProperty CurrentAccessLevelProperty = DependencyProperty.Register("CurrentAccessLevel", typeof(string), typeof(FolderExplorerControl), new PropertyMetadata(string.Empty));
        public string CurrentAccessLevel
        {
            get { return (string)GetValue(CurrentAccessLevelProperty); }
            set { SetValue(CurrentAccessLevelProperty, value); }
        }

        public ObservableCollection<string> PathSuggestions { get; set; }

        private bool _ignoreSelectionChanged, _isTyping;
        private DirectoryItem _clipboardItem;
        private bool _isCutOperation;

        public event EventHandler<string> OnOpenClicked;

        #region Title bar and Resize

        private const int WM_SYSCOMMAND = 0x112;
        private const int SC_SIZE = 0xF000;

        private void TitleBar_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                this.DragMove();
            }
        }

        private void TitleBar_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2 && e.ChangedButton == MouseButton.Left)
            {
                if (WindowState == WindowState.Normal)
                {
                    WindowState = WindowState.Maximized;
                }
                else
                {
                    WindowState = WindowState.Normal;
                }
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            SelectedPath = "";
            this.Close();
        }

        private void Border_MouseMove(object sender, MouseEventArgs e)
        {
            const int resizeBorderThickness = 5;
            Point mousePosition = e.GetPosition(this);

            if (mousePosition.X <= resizeBorderThickness && mousePosition.Y <= resizeBorderThickness)
            {
                Cursor = Cursors.SizeNWSE;
                if (e.LeftButton == MouseButtonState.Pressed)
                {
                    ResizeWindow(ResizeDirection.TopLeft);
                }
            }
            else if (mousePosition.X >= this.ActualWidth - resizeBorderThickness && mousePosition.Y <= resizeBorderThickness)
            {
                Cursor = Cursors.SizeNESW;
                if (e.LeftButton == MouseButtonState.Pressed)
                {
                    ResizeWindow(ResizeDirection.TopRight);
                }
            }
            else if (mousePosition.X <= resizeBorderThickness && mousePosition.Y >= this.ActualHeight - resizeBorderThickness)
            {
                Cursor = Cursors.SizeNESW;
                if (e.LeftButton == MouseButtonState.Pressed)
                {
                    ResizeWindow(ResizeDirection.BottomLeft);
                }
            }
            else if (mousePosition.X >= this.ActualWidth - resizeBorderThickness && mousePosition.Y >= this.ActualHeight - resizeBorderThickness)
            {
                Cursor = Cursors.SizeNWSE;
                if (e.LeftButton == MouseButtonState.Pressed)
                {
                    ResizeWindow(ResizeDirection.BottomRight);
                }
            }
            else if (mousePosition.Y <= resizeBorderThickness)
            {
                Cursor = Cursors.SizeNS;
                if (e.LeftButton == MouseButtonState.Pressed)
                {
                    ResizeWindow(ResizeDirection.Top);
                }
            }
            else if (mousePosition.Y >= this.ActualHeight - resizeBorderThickness)
            {
                Cursor = Cursors.SizeNS;
                if (e.LeftButton == MouseButtonState.Pressed)
                {
                    ResizeWindow(ResizeDirection.Bottom);
                }
            }
            else if (mousePosition.X <= resizeBorderThickness)
            {
                Cursor = Cursors.SizeWE;
                if (e.LeftButton == MouseButtonState.Pressed)
                {
                    ResizeWindow(ResizeDirection.Left);
                }
            }
            else if (mousePosition.X >= this.ActualWidth - resizeBorderThickness)
            {
                Cursor = Cursors.SizeWE;
                if (e.LeftButton == MouseButtonState.Pressed)
                {
                    ResizeWindow(ResizeDirection.Right);
                }
            }
            else
            {
                Cursor = Cursors.Arrow;
            }
        }

        private void ResizeWindow(ResizeDirection direction)
        {
            HwndSource hwndSource = (HwndSource)PresentationSource.FromVisual(this);
            if (hwndSource != null)
            {
                SendMessage(hwndSource.Handle, WM_SYSCOMMAND, (IntPtr)(SC_SIZE + direction), IntPtr.Zero);
            }
        }

        private enum ResizeDirection
        {
            Left = 1,
            Right = 2,
            Top = 3,
            TopLeft = 4,
            TopRight = 5,
            Bottom = 6,
            BottomLeft = 7,
            BottomRight = 8
        }

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        #endregion

        private void LoadDrives()
        {

            Debug.WriteLine("LoadDrives method called.");
            var drives = Directory.GetLogicalDrives();
            var rootDirectories = new ObservableCollection<DirectoryItem>();

            foreach (var drive in drives)
            {
                var item = new DirectoryItem
                {
                    Name = "Local Disk (" + drive.Replace("\\", "") + ")",
                    Path = drive,
                    IsDrive = true,
                    HasSubDirectories = true
                };
                item.SubDirectories.Add(new DirectoryItem { Name = "Loading...", Path = string.Empty });
                rootDirectories.Add(item);
            }

            DriveTreeView.ItemsSource = rootDirectories;
        }
        
        private void LoadFolders(string path)
        {
            Debug.WriteLine($"LoadFolders method called with path: {path}");
            var folders = new ObservableCollection<DirectoryItem>();
            try
            {
                if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
                {
                    var dirs = Directory.GetDirectories(path, "*", SearchOption.TopDirectoryOnly);
                    foreach (var dir in dirs)
                    {
                        try
                        {
                            var subDirs = Directory.GetDirectories(dir);
                            folders.Add(new DirectoryItem
                            {
                                Name = Path.GetFileName(dir),
                                Path = dir,
                                HasSubDirectories = subDirs.Any()
                            });
                        }
                        catch (UnauthorizedAccessException)
                        {
                            Debug.WriteLine($"Access denied to directory: {dir}");
                            folders.Add(new DirectoryItem
                            {
                                Name = Path.GetFileName(dir) + " [Access Denied]",
                                Path = dir,
                                HasSubDirectories = false
                            });
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"Error loading folders for {dir}: {ex.Message}");
                        }
                    }
                }
                else
                {
                    Debug.WriteLine($"Invalid or empty path: {path}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading folders for {path}: {ex.Message}");
            }
            FolderListView.ItemsSource = folders;
            SelectedPath = path; // Update the SelectedPath to the current path
        }

        private ObservableCollection<DirectoryItem> GetSubDirectories(string path)
        {
            Debug.WriteLine($"GetSubDirectories method called with path: {path}");
            var directories = new ObservableCollection<DirectoryItem>();

            try
            {
                if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
                {
                    var dirs = Directory.GetDirectories(path, "*", SearchOption.TopDirectoryOnly);
                    foreach (var dir in dirs)
                    {
                        try
                        {
                            // Check for subdirectories and handle access restrictions
                            var subDirs = Directory.GetDirectories(dir);
                            var item = new DirectoryItem
                            {
                                Name = Path.GetFileName(dir),
                                Path = dir,
                                HasSubDirectories = subDirs.Any()
                            };

                            if (item.HasSubDirectories)
                            {
                                item.SubDirectories.Add(new DirectoryItem { Name = "Loading...", Path = string.Empty });
                            }

                            directories.Add(item);
                        }
                        catch (UnauthorizedAccessException)
                        {
                            Debug.WriteLine($"Access denied to directory: {dir}");
                            // Handle access denied, for example, by adding a placeholder or skipping
                            directories.Add(new DirectoryItem
                            {
                                Name = Path.GetFileName(dir) + " [Access Denied]",
                                Path = dir,
                                HasSubDirectories = false
                            });
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"Error getting subdirectories for {dir}: {ex.Message}");
                        }
                    }
                }
                else
                {
                    Debug.WriteLine($"Invalid or empty path: {path}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting subdirectories for {path}: {ex.Message}");
            }

            return directories;
        }

        #region PathTB to display the current path and suggestions
        private void PathTB_GotFocus(object sender, RoutedEventArgs e)
        {
            _isTyping = true;
        }

        private void PathTB_LostFocus(object sender, RoutedEventArgs e)
        {
            _isTyping = false;
            PathSuggestionsPopup.IsOpen = false;
        }

        private void PathTB_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!_isTyping) return;

            var input = PathTB.Text;
            if (string.IsNullOrEmpty(input))
            {
                PathSuggestionsPopup.IsOpen = false;
                return;
            }

            var suggestions = GetPathSuggestions(input);
            if (suggestions.Any())
            {
                PathSuggestions.Clear();
                foreach (var suggestion in suggestions)
                {
                    PathSuggestions.Add(suggestion);
                }
                PathSuggestionsPopup.IsOpen = true;
            }
            else
            {
                PathSuggestions.Clear();
                PathSuggestions.Add("No paths found");
                //PathSuggestionsPopup.IsOpen = false;
            }
        }

        private void PathTB_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && sender is TextBox textBox)
            {
                GoButtonClick(sender, e);
                FolderListView.Focus();
            }
        }

        private IEnumerable<string> GetPathSuggestions(string input)
        {
            var suggestions = new List<string>();
            try
            {
                if (input.Length == 2 && char.IsLetter(input[0]) && input[1] == ':')
                {
                    suggestions.AddRange(Directory.GetLogicalDrives().Where(d => d.StartsWith(input, StringComparison.OrdinalIgnoreCase)));
                }
                else
                {
                    var directoryName = Path.GetDirectoryName(input);
                    if (Directory.Exists(directoryName))
                    {
                        suggestions.AddRange(Directory.GetDirectories(directoryName).Where(d => d.StartsWith(input, StringComparison.OrdinalIgnoreCase)));
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting path suggestions: {ex.Message}");
            }
            return suggestions;
        }

        private void PathSuggestionsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (PathSuggestionsListBox.SelectedItem is string selectedPath && PathSuggestionsListBox.SelectedItem.ToString() != "No paths found")
            {
                PathTB.Text = selectedPath;
                PathSuggestionsPopup.IsOpen = false;
            }
        }
        #endregion

        private void DriveTreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (DriveTreeView.SelectedItem is DirectoryItem selectedDirectory)
            {
                Debug.WriteLine($"DriveTreeView_SelectedItemChanged: {selectedDirectory.Path}");
                SelectedPath = selectedDirectory.Path;
                LoadFolders(SelectedPath);
            }
        }

        private void ExpandTreeViewItem(DirectoryItem currentItem, string[] parts, int index)
        {
            if (index >= parts.Length)
            {
                var treeViewItemFinal = GetTreeViewItemFromObject(DriveTreeView, currentItem);
                if (treeViewItemFinal != null)
                {
                    treeViewItemFinal.IsSelected = true;
                    treeViewItemFinal.BringIntoView();
                }
                return;
            }

            var treeViewItemCurrent = GetTreeViewItemFromObject(DriveTreeView, currentItem);
            if (treeViewItemCurrent != null)
            {
                treeViewItemCurrent.IsExpanded = true;
            }

            var nextItem = currentItem.SubDirectories.FirstOrDefault(d => d.Name.Equals(parts[index], StringComparison.OrdinalIgnoreCase));
            if (nextItem == null)
            {
                nextItem = new DirectoryItem
                {
                    Name = parts[index],
                    Path = Path.Combine(currentItem.Path, parts[index]),
                };
                currentItem.SubDirectories.Add(nextItem);
            }

            ExpandTreeViewItem(nextItem, parts, index + 1);
        }

        private TreeViewItem GetTreeViewItemFromObject(ItemsControl parent, object item)
        {
            TreeViewItem foundTreeViewItem = null;
            for (int i = 0; i < parent.Items.Count; i++)
            {
                //var currentTreeViewItem = parent.ItemContainerGenerator.ContainerFromIndex(i) as TreeViewItem;
                if (!(parent.ItemContainerGenerator.ContainerFromIndex(i) is TreeViewItem currentTreeViewItem))
                {
                    parent.UpdateLayout();
                    currentTreeViewItem = parent.ItemContainerGenerator.ContainerFromIndex(i) as TreeViewItem;
                }

                if (currentTreeViewItem != null && currentTreeViewItem.DataContext == item)
                {
                    foundTreeViewItem = currentTreeViewItem;
                    break;
                }

                if (currentTreeViewItem != null)
                {
                    foundTreeViewItem = GetTreeViewItemFromObject(currentTreeViewItem, item);
                    if (foundTreeViewItem != null)
                    {
                        break;
                    }
                }
            }
            return foundTreeViewItem;
        }

        private void TreeViewItem_Expanded(object sender, RoutedEventArgs e)
        {
            if (e.OriginalSource is TreeViewItem item && item.Header is DirectoryItem directoryItem)
            {
                Debug.WriteLine($"TreeViewItem_Expanded: {directoryItem.Path}");
                if (directoryItem.SubDirectories.Count == 1 && directoryItem.SubDirectories[0].Path == string.Empty)
                {
                    directoryItem.SubDirectories.Clear();

                    try
                    {
                        var subDirs = GetSubDirectories(directoryItem.Path);
                        foreach (var subDir in subDirs)
                        {
                            directoryItem.SubDirectories.Add(subDir);
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error expanding tree view item for {directoryItem.Path}: {ex.Message}");
                    }
                }
            }
        }

        private void FolderListView_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            _isTyping = false;

            if (FolderListView.SelectedItem is DirectoryItem selectedDirectory)
            {
                Debug.WriteLine($"FolderListView_MouseDoubleClick: {selectedDirectory.Path}");
                SelectedPath = selectedDirectory.Path;
                _ignoreSelectionChanged = true;
                LoadFolders(SelectedPath);
            }
        }

        private void FolderListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _isTyping = false;

            if (_ignoreSelectionChanged)
            {
                _ignoreSelectionChanged = false;
                return;
            }

            if (FolderListView.SelectedItem is DirectoryItem selectedDirectory)
            {
                Debug.WriteLine($"FolderListView_SelectionChanged: {selectedDirectory.Path}");
                SelectedPath = selectedDirectory.Path;
            }
        }

        private void GoButtonClick(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine($"GoButtonClick: CurrentPath is {SelectedPath}");
            if (Directory.Exists(SelectedPath))
            {
                LoadFolders(SelectedPath);
                //UpdateTreeView(SelectedPath);
            }
            else
            {
                MessageBox.Show("The path you entered is not valid. Please enter a valid path.", "Invalid Path", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void RefreshButtonClick(object sender, RoutedEventArgs e)
        {
            LoadDrives();
            LoadFolders(SelectedPath);
        }

        protected override void OnPreviewKeyDown(KeyEventArgs e)
        {
            base.OnPreviewKeyDown(e);

            if (e.Key == Key.Back)
            {
                // Check if the focused control is not a TextBox
                if (!(Keyboard.FocusedElement is TextBox))
                {
                    // Call the GoBack method
                    BackButton_Click(this, e);

                    // Optionally, mark the event as handled to prevent further processing
                    e.Handled = true;
                }
            }
            if (e.Key == Key.Enter)
            {
                if (!(Keyboard.FocusedElement is TextBox))
                {
                    GoButtonClick(this, e);
                    e.Handled = true;
                }
            }
            if (e.Key == Key.Delete)
            {
                DeleteFolder_Click(this, e);
                e.Handled = true;
            }
            if (Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift) && e.Key == Key.N)
            {
                NewFolderBtn_Click(this, e);
                e.Handled = true;
            }
            if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.C)
            {
                CopyFolder_Click(this, e);
                e.Handled = true;
            }

            // Handle Ctrl+V key combination
            if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.V)
            {
                // Call the paste method
                PasteFolder_Click(this, e);
                e.Handled = true;
            }
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine($"BackButton_Click: CurrentPath before back is {SelectedPath}");
            if (!string.IsNullOrEmpty(SelectedPath))
            {
                var parentDirectory = Directory.GetParent(SelectedPath);
                if (parentDirectory != null)
                {
                    SelectedPath = parentDirectory.FullName;
                    LoadFolders(SelectedPath);
                }
            }
            Debug.WriteLine($"BackButton_Click: CurrentPath after back is {SelectedPath}");
        }

        private void QuickAccessListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (QuickAccessListView.SelectedItem is ListViewItem selectedItem)
            {
                string path = string.Empty;

                // Get the tag value which contains the folder path
                if (selectedItem.Tag is Environment.SpecialFolder specialFolder)
                {
                    path = Environment.GetFolderPath(specialFolder);
                }
                else if (selectedItem.Tag is string customPath)
                {
                    path = customPath;
                }

                if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
                {
                    SelectedPath = path;
                    LoadFolders(SelectedPath);
                }
            }
        }

        private void QuickAccessListView_LostFocus(object sender, RoutedEventArgs e)
        {
            QuickAccessListView.SelectedItem = null;
        }

        public bool IsRunningAsAdmin()
        {
            WindowsIdentity identity = WindowsIdentity.GetCurrent();
            WindowsPrincipal principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }

        public void RestartAsAdmin(string path)
        {
            var exeName = Assembly.GetExecutingAssembly().Location;
            var startInfo = new ProcessStartInfo(exeName)
            {
                UseShellExecute = true,
                Verb = "runas",
            };

            try
            {
                Process.Start(startInfo);
            }
            catch (Exception ex)
            {
                MessageBox.Show("This operation requires elevated privileges. Please run the application as an administrator.");
            }

            Application.Current.Shutdown();
        }

        private void NewFolderBtn_Click(object sender, RoutedEventArgs e)
        {
            if (FolderListView.SelectedItem != null)
            {
                BackButton_Click(sender, e);
            }
            if (!string.IsNullOrEmpty(SelectedPath) && Directory.Exists(SelectedPath))
            {
                string newFolderName = "New Folder";
                string newFolderPath = System.IO.Path.Combine(SelectedPath, newFolderName);
                int folderNumber = 1;

                // Ensure unique folder name
                while (Directory.Exists(newFolderPath))
                {
                    newFolderName = $"New Folder ({folderNumber++})";
                    newFolderPath = System.IO.Path.Combine(SelectedPath, newFolderName);
                }

                try
                {
                    Directory.CreateDirectory(newFolderPath);
                }
                catch (UnauthorizedAccessException)
                {
                    MessageBoxResult result = MessageBox.Show($"You need to be an administrator to proceed this operation!\n\n Proceeding will restart the application?", "Destination Access Denied", MessageBoxButton.YesNo, MessageBoxImage.Question);

                    if (result == MessageBoxResult.Yes)
                    {
                        string currentPath = SelectedPath;
                        if (!IsRunningAsAdmin())
                        {
                            RestartAsAdmin(currentPath);
                            return;
                        }
                    }
                    else
                    {
                        return;
                    }
                }
                var newFolder = new DirectoryItem
                {
                    Name = newFolderName,
                    Path = newFolderPath,
                    IsEditing = true
                };

                if (FolderListView.ItemsSource is ObservableCollection<DirectoryItem> folderList)
                {
                    folderList.Add(newFolder);
                    var parentFolder = folderList.FirstOrDefault(item => item.Path == SelectedPath);
                    if (parentFolder != null)
                    {
                        parentFolder.HasSubDirectories = true;
                    }
                }
                var parentDirectory = FindDirectoryItem(SelectedPath);
                if (parentDirectory != null)
                {
                    parentDirectory.SubDirectories.Add(newFolder);
                }
                // Focus the TextBox and select the text
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    var listViewItem = FolderListView.ItemContainerGenerator.ContainerFromItem(newFolder) as ListViewItem;
                    if (listViewItem != null)
                    {
                        var textBox = FindVisualChild<TextBox>(listViewItem);
                        if (textBox != null)
                        {
                            textBox.Focus();
                            textBox.SelectAll();
                        }
                    }
                }), System.Windows.Threading.DispatcherPriority.Input);
            }
        }

        private DirectoryItem FindDirectoryItem(string path)
        {
            return FindDirectoryItem(DriveTreeView.ItemsSource as ObservableCollection<DirectoryItem>, path);
        }

        private DirectoryItem FindDirectoryItem(ObservableCollection<DirectoryItem> items, string path)
        {
            foreach (var item in items)
            {
                if (item.Path.Equals(path, StringComparison.OrdinalIgnoreCase))
                {
                    return item;
                }

                var foundItem = FindDirectoryItem(item.SubDirectories, path);
                if (foundItem != null)
                {
                    return foundItem;
                }
            }
            return null;
        }

        private childItem FindVisualChild<childItem>(DependencyObject obj) where childItem : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(obj); i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(obj, i);
                if (child != null && child is childItem)
                    return (childItem)child;
                else
                {
                    childItem childOfChild = FindVisualChild<childItem>(child);
                    if (childOfChild != null)
                        return childOfChild;
                }
            }
            return null;
        }

        private void FolderTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                CompleteEditing(sender as TextBox);
            }
        }

        private void FolderTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            CompleteEditing(sender as TextBox);
        }

        private void CompleteEditing(TextBox textBox)
        {
            if (textBox == null) return;

            if (textBox.DataContext is DirectoryItem item)
            {
                string newFolderName = textBox.Text;
                string newFolderPath = System.IO.Path.Combine(SelectedPath, newFolderName);

                if (newFolderName.Equals(item.Name, StringComparison.OrdinalIgnoreCase))
                {
                    // No change in folder name
                    item.IsEditing = false;
                    return;
                }

                if (!Directory.Exists(newFolderPath))
                {
                    try
                    {
                        Directory.Move(item.Path, newFolderPath);
                        item.Path = newFolderPath;
                        item.Name = newFolderName;
                        item.IsEditing = false;

                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error renaming folder: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                    RefreshTreeView();
                    LoadFolders(SelectedPath);
                }
                else
                {
                    MessageBox.Show("A folder with this name already exists.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    textBox.Focus();
                    textBox.SelectAll();
                }
            }
        }

        private void OpenButton_Click(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine($"Open Button Clicked: CurrentPath is {SelectedPath}");
            if (Directory.Exists(SelectedPath))
            {
                MessageBox.Show($"Selected Path: {SelectedPath}");
                OnOpenClicked?.Invoke(this, SelectedPath);
            }
            else
            {
                //MessageBox.Show("The path you entered is not valid. Please enter a valid path.", "Invalid Path", MessageBoxButton.OK, MessageBoxImage.Warning);
                MessageBox.Show("You can't open this location using this program. Please try a different location.", "Invalid Path", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            Close();
        }

        private void CutFolder_Click(object sender, RoutedEventArgs e)
        {
            if (FolderListView.SelectedItem is DirectoryItem selectedFolder)
            {
                _clipboardItem = selectedFolder;
                _isCutOperation = true;
            }
        }

        private void CopyFolder_Click(object sender, RoutedEventArgs e)
        {
            if (FolderListView.SelectedItem is DirectoryItem selectedFolder)
            {
                _clipboardItem = selectedFolder;
                _isCutOperation = false;
            }
        }

        private void PasteFolder_Click(object sender, RoutedEventArgs e)
        {
            if (FolderListView.SelectedItem != null)
            {
                BackButton_Click(this, e);
            }
            if (_clipboardItem != null && !string.IsNullOrEmpty(SelectedPath))
            {
                string destinationPath = System.IO.Path.Combine(SelectedPath, _clipboardItem.Name);

                if (_isCutOperation)
                {
                    try
                    {
                        destinationPath = GetUniqueCopyPath(destinationPath);
                        CopyDirectory(_clipboardItem.Path, destinationPath);
                        Directory.Delete(_clipboardItem.Path, true);

                        // Update TreeView and ListView
                        var sourceParent = FindParentDirectory(_clipboardItem.Path);
                        if (sourceParent != null)
                        {
                            sourceParent.SubDirectories.Remove(_clipboardItem);
                        }
                        LoadFolders(SelectedPath);
                        RefreshTreeView();
                        _clipboardItem = null;
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error moving folder: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                else
                {
                    try
                    {
                        // Ensure unique folder name for copy operation
                        destinationPath = GetUniqueCopyPath(destinationPath);
                        CopyDirectory(_clipboardItem.Path, destinationPath);
                        //RefreshTreeView();
                        LoadFolders(SelectedPath);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error copying folder: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }

        private DirectoryItem FindDirectoryItem(DirectoryItem currentItem, string path)
        {
            // Check if the current item matches the path
            if (currentItem.Path == path)
            {
                return currentItem;
            }

            // Recursively search in subdirectories
            foreach (var subItem in currentItem.SubDirectories)
            {
                var foundItem = FindDirectoryItem(subItem, path);
                if (foundItem != null)
                {
                    return foundItem;
                }
            }

            return null;
        }

        private string GetUniqueCopyPath(string originalPath)
        {
            string directory = Path.GetDirectoryName(originalPath);
            string originalName = Path.GetFileName(originalPath);
            string newPath = originalPath;
            int copyNumber = 1;

            while (Directory.Exists(newPath))
            {
                newPath = Path.Combine(directory, $"{originalName} - copy({copyNumber++})");
            }

            return newPath;
        }

        private void DeleteFolder_Click(object sender, RoutedEventArgs e)
        {
            if (FolderListView.SelectedItem is DirectoryItem selectedFolder)
            {
                MessageBoxResult result = MessageBox.Show($"Do you want to delete the folder\n{selectedFolder.Name}?\n\n This action cannot be redone!", "Confirmation", MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        var parentPath = Path.GetDirectoryName(selectedFolder.Path);
                        Directory.Delete(selectedFolder.Path, true);

                        var parentDirectory = FindDirectoryItem(parentPath);
                        if (parentDirectory != null)
                        {
                            parentDirectory.SubDirectories.Remove(selectedFolder);
                        }

                        // Update the SelectedPath to the parent directory path
                        RefreshTreeView();
                        LoadFolders(parentPath);
                        SelectedPath = parentPath;
                    }
                    catch (Exception ex)
                    {
                        //MessageBoxResult msg = MessageBox.Show($"You need to be an administrator to proceed this operation!\n\n Proceeding will restart the application?", "Destination Access Denied", MessageBoxButton.YesNo, MessageBoxImage.Question);

                        //if (msg == MessageBoxResult.Yes)
                        //{
                        //    string currentPath = SelectedPath;
                        //    if (!IsRunningAsAdmin())
                        //    {
                        //        RestartAsAdmin(currentPath);
                        //        return;
                        //    }
                        //}
                        //else
                        //{
                        //    return;
                        //}
                        MessageBox.Show("Error in Deleting Folder "+ ex.Message);
                    }
                }
            }
        }

        private DirectoryItem FindParentDirectory(string path)
        {
            var parts = path.Split(new[] { Path.DirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length > 1)
            {
                var parentPath = string.Join(Path.DirectorySeparatorChar.ToString(), parts.Take(parts.Length - 1));
                return FindDirectoryItem(parentPath);
            }
            return null;
        }

        private void CopyDirectory(string sourceDir, string destinationDir)
        {
            var dir = new DirectoryInfo(sourceDir);
            var dirs = dir.GetDirectories();
            Directory.CreateDirectory(destinationDir);

            foreach (var file in dir.GetFiles())
            {
                string targetFilePath = Path.Combine(destinationDir, file.Name);
                file.CopyTo(targetFilePath, true); // Overwrite files if they already exist
            }

            foreach (var subDir in dirs)
            {
                string newDestinationDir = Path.Combine(destinationDir, subDir.Name);
                CopyDirectory(subDir.FullName, newDestinationDir);
            }
        }

        private void OpenCmd_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Create a ProcessStartInfo object
                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    WorkingDirectory = SelectedPath,
                    UseShellExecute = true
                };

                // Start the process
                Process.Start(psi);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error opening Command Prompt: " + ex.Message);
            }
        }

        #region changes in tree view
        private HashSet<string> GetExpandedNodes()
        {
            var expandedNodes = new HashSet<string>();
            foreach (DirectoryItem driveItem in DriveTreeView.ItemsSource)
            {
                var treeViewItem = (TreeViewItem)DriveTreeView.ItemContainerGenerator.ContainerFromItem(driveItem);
                if (treeViewItem != null && treeViewItem.IsExpanded)
                {
                    expandedNodes.Add(driveItem.Path);
                    GetExpandedNodesRecursive(treeViewItem, expandedNodes);
                }
            }
            return expandedNodes;
        }

        private void GetExpandedNodesRecursive(TreeViewItem treeViewItem, HashSet<string> expandedNodes)
        {
            foreach (DirectoryItem directoryItem in treeViewItem.Items)
            {
                var subItem = (TreeViewItem)treeViewItem.ItemContainerGenerator.ContainerFromItem(directoryItem);
                if (subItem != null && subItem.IsExpanded)
                {
                    expandedNodes.Add(directoryItem.Path);
                    GetExpandedNodesRecursive(subItem, expandedNodes);
                }
            }
        }

        private void RestoreExpandedNodes(HashSet<string> expandedNodes)
        {
            foreach (DirectoryItem driveItem in DriveTreeView.ItemsSource)
            {
                if (expandedNodes.Contains(driveItem.Path))
                {
                    var treeViewItem = (TreeViewItem)DriveTreeView.ItemContainerGenerator.ContainerFromItem(driveItem);
                    if (treeViewItem != null)
                    {
                        treeViewItem.IsExpanded = true;
                        RestoreExpandedNodesRecursive(treeViewItem, expandedNodes);
                    }
                }
            }
        }

        private void RestoreExpandedNodesRecursive(TreeViewItem treeViewItem, HashSet<string> expandedNodes)
        {
            foreach (DirectoryItem directoryItem in treeViewItem.Items)
            {
                if (expandedNodes.Contains(directoryItem.Path))
                {
                    var subItem = (TreeViewItem)treeViewItem.ItemContainerGenerator.ContainerFromItem(directoryItem);
                    if (subItem != null)
                    {
                        subItem.IsExpanded = true;
                        RestoreExpandedNodesRecursive(subItem, expandedNodes);
                    }
                }
            }
        }

        private void RefreshTreeView()
        {
            var expandedNodes = GetExpandedNodes();
            RestoreExpandedNodes(expandedNodes);
        }
        #endregion
    }

    public class DirectoryItem : INotifyPropertyChanged
    {
        private string _name;
        private string _path;
        private ObservableCollection<DirectoryItem> _subDirectories;
        private bool _isDrive;
        private bool _isEditing;
        private bool _hasSubDirectories;

        public string Name
        {
            get { return _name; }
            set { _name = value; OnPropertyChanged(nameof(Name)); }
        }

        public string Path
        {
            get { return _path; }
            set { _path = value; OnPropertyChanged(nameof(Path)); }
        }

        public ObservableCollection<DirectoryItem> SubDirectories
        {
            get { return _subDirectories; }
            set { _subDirectories = value; OnPropertyChanged(nameof(SubDirectories)); }
        }

        public bool IsDrive
        {
            get { return _isDrive; }
            set { _isDrive = value; OnPropertyChanged(nameof(IsDrive)); }
        }

        public bool IsEditing
        {
            get { return _isEditing; }
            set { _isEditing = value; OnPropertyChanged(nameof(IsEditing)); }
        }

        public bool HasSubDirectories
        {
            get { return _hasSubDirectories; }
            set { _hasSubDirectories = value; OnPropertyChanged(nameof(HasSubDirectories)); }
        }

        public DirectoryItem()
        {
            SubDirectories = new ObservableCollection<DirectoryItem>();
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class PathToFolderNameConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string path && !string.IsNullOrEmpty(path))
            {
                path = path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

                // Check if the path is a logical drive (e.g., C:\ or D:\)
                if (Path.GetPathRoot(path) == path)
                {
                    // Logical drive, return the drive letter
                    return Path.GetPathRoot(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                }

                // Return the folder name of a given path
                return Path.GetFileName(path);
            }

            return string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    [ValueConversion(typeof(bool), typeof(Visibility))]
    public class BooleanToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                // Check if we want to invert the logic
                bool invert = parameter != null && bool.Parse(parameter.ToString());

                if (invert)
                {
                    return boolValue ? Visibility.Collapsed : Visibility.Visible;
                }
                else
                {
                    return boolValue ? Visibility.Visible : Visibility.Collapsed;
                }
            }
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Visibility visibility)
            {
                bool invert = parameter != null && bool.Parse(parameter.ToString());

                if (invert)
                {
                    return visibility != Visibility.Visible;
                }
                else
                {
                    return visibility == Visibility.Visible;
                }
            }
            return false;
        }
    }
}
