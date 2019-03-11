using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;

namespace BrucesBlocks
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        // Local variables:
        private string path = "";
        private bool edited = false;
        private bool inSelectionChanged = false;
        private IBlockManager BlockManager;
        private BlockList BlockList;
        private DispatcherTimer AddTimer;

        /// <summary>Initializes a new instance of the <see cref="MainWindow"/> class.</summary>
        public MainWindow()
        {
            InitializeComponent();

            BlockManager = new BlockManager();
            foreach (IBlock block in BlockManager.BlockTypes)
                listAdd.Items.Add(block.BlockItem);

            BlockList = new BlockList();
            BlockList.MouseDoubleClick += OnMouseDoubleClick;
            ccBlockList.Content = BlockList;

            BlockList.OrderChanged += ListBlocks_OrderChanged;
            BlockList.SelectionChanged += ListBlocks_SelectionChanged;
            listCode.SelectionChanged += ListCode_SelectionChanged;

            AddTimer = new DispatcherTimer();
            AddTimer.Tick += AddTimer_Tick;
            AddTimer.Interval = TimeSpan.FromMilliseconds(200);

            StateChanged += MainWindow_StateChanged;
        }

        private bool OfferToSaveChanges()
        {
            if (Edited && path.Length > 0)
            {
                var result = MessageBox.Show("The file has been edited but not saved.  Do you want to Save your changes?",
                    "Block Editor", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
                if (result == MessageBoxResult.Yes)
                    Save();
                return (result != MessageBoxResult.Cancel);
            }
            return true;
        }

        /// <summary>Clears the editor contents.</summary>
        public void New()
        {
            if (OfferToSaveChanges())
            {
                path = "";
                tbPath.Text = "(no file selected)";
                Load(null);
                Edited = false;
                gridAdd.Visibility = Visibility.Visible;
            }
        }

        /// <summary>Opens a file.</summary>
        public void Open()
        {
            if (OfferToSaveChanges())
            {
                var dlg = new OpenFileDialog();
                dlg.Filter = "Text Files|*.txt|All Files|*.*";
                if (dlg.ShowDialog() == true)
                    Open(dlg.FileName);
            }
        }

        private void Open(string path)
        {
            this.path = path;
            tbPath.Text = path;
            IBlock[] blocks;
            BlockManager.ReadFile(path, out blocks);
            Load(blocks);
            Edited = false;
            gridAdd.Visibility = Visibility.Hidden;
        }

        /// <summary>Saves the file.</summary>
        public void Save()
        {
            if (string.IsNullOrEmpty(path))
            {
                SaveAs();
            }
            else
            {
                BlockManager.WriteFile(path, BlockList.Blocks);
                Edited = false;
            }
        }

        /// <summary>Saves the file under a new name.</summary>
        public void SaveAs()
        {
            var dlg = new SaveFileDialog();
            dlg.Filter = "Text Files|*.txt|All Files|*.*";
            dlg.DefaultExt = ".txt";
            if (dlg.ShowDialog() == true)
            {
                path = dlg.FileName;
                tbPath.Text = path;
                Save();
            }
        }

        /// <summary>Indicates that editor contents have been modified and not yet saved.</summary>
        public bool Edited
        {
            get
            {
                return edited;
            }
            private set
            {
                if (edited != value)
                {
                    edited = value;
                    btnSave.IsEnabled = edited;
                    if (edited && !tbPath.Text.EndsWith("*"))
                        tbPath.Text += "*";
                    else if (!edited && tbPath.Text.EndsWith("*"))
                        tbPath.Text = tbPath.Text.Substring(0, tbPath.Text.Length - 1);
                }
            }
        }

        private void GroupSelectedItems()
        {
            var sbl = BlockList.GetSelectedBlockList();
            int count = sbl.listBlocks.SelectedItems.Count;
            if (count == 0)
            {
                System.Media.SystemSounds.Beep.Play();
                return;
            }

            string prompt;
            if (count == 1)
                prompt = "Group Selected Block?";
            else if (count == sbl.listBlocks.Items.Count)
                prompt = "Group All Blocks?";
            else
                prompt = string.Format("Group {0} Selected Blocks?", count);

            if (MessageBox.Show(prompt, "Block Editor", MessageBoxButton.OKCancel, MessageBoxImage.Question) == MessageBoxResult.OK)
            {
                inSelectionChanged = true;
                int index = sbl.listBlocks.SelectedItemIndex;
                IBlockGroup group = BlockManager.NewBlockGroup();

                IBlock[] blocks = sbl.AdjacentSelectedBlocks;
                foreach (IBlock block in blocks)
                {
                    group.Add(block);
                    sbl.listBlocks.Items.Remove(block.BlockItem);
                    sbl.listBlocks.ResetItemIndex(block.BlockItem);
                }

                sbl.listBlocks.Insert(index, group.BlockItem);
                sbl.listBlocks.SelectedItem = group.BlockItem;
                listCode.UnselectAll();
                if (sbl.ParentGroup != null)
                    sbl.ParentGroup.Blocks = sbl.Blocks;

                Edited = true;
                btnDelete.IsEnabled = btnCut.IsEnabled = btnCopy.IsEnabled = btnGroup.IsEnabled = btnUngroup.IsEnabled = true;
                inSelectionChanged = false;
            }
        }

        private void UngroupSelectedGroup()
        {
            var sbl = BlockList.GetSelectedBlockList();
            ListBoxItem item = (ListBoxItem)sbl.listBlocks.SelectedItem;
            IBlockGroup group = item.Tag as IBlockGroup;
            if (group == null)
            {
                System.Media.SystemSounds.Beep.Play();
                return;
            }

            if (MessageBox.Show("Ungroup Selected Group?", "Block Editor", MessageBoxButton.OKCancel, MessageBoxImage.Question) == MessageBoxResult.OK)
            {
                inSelectionChanged = true;
                int index = sbl.listBlocks.SelectedItemIndex;
                sbl.listBlocks.Items.Remove(item);
                sbl.Insert(index, group.Blocks);
                group.Blocks = new IBlock[] { };

                listCode.UnselectAll();
                if (sbl.ParentGroup != null)
                    sbl.ParentGroup.Blocks = sbl.Blocks;
                sbl.Focus();

                tbLine.Text = string.Format("{0} lines", listCode.Items.Count);
                Edited = true;
                btnDelete.IsEnabled = btnCut.IsEnabled = btnCopy.IsEnabled = btnGroup.IsEnabled = btnUngroup.IsEnabled = false;
                inSelectionChanged = false;
            }
        }

        private void DeleteSelectedItems(bool confirm = true)
        {
            var sbl = BlockList.GetSelectedBlockList();
            int count = sbl.listBlocks.SelectedItems.Count;
            if (count == 0)
            {
                System.Media.SystemSounds.Beep.Play();
                return;
            }

            string prompt = "";
            if (confirm)
            {
                if (count == 1)
                    prompt = "Delete Selected Block?";
                else if (count == sbl.listBlocks.Items.Count)
                    prompt = "Delete All Blocks?";
                else
                    prompt = string.Format("Delete {0} Selected Blocks?", count);
            }

            if (!confirm || MessageBox.Show(prompt, "Block Editor", MessageBoxButton.OKCancel, MessageBoxImage.Question) == MessageBoxResult.OK)
            {
                inSelectionChanged = true;
                ListBoxItem[] items = new ListBoxItem[count];
                sbl.listBlocks.SelectedItems.CopyTo(items, 0);
                foreach (var item in items)
                {
                    sbl.listBlocks.Items.Remove(item);
                    RemoveCode((IBlock)item.Tag);
                }

                if (sbl.ParentGroup != null)
                    sbl.ParentGroup.Blocks = sbl.Blocks;
                sbl.Focus();

                tbLine.Text = string.Format("{0} lines", listCode.Items.Count);
                Edited = true;
                btnDelete.IsEnabled = btnCut.IsEnabled = btnCopy.IsEnabled = btnGroup.IsEnabled = btnUngroup.IsEnabled = false;
                inSelectionChanged = false;
            }
        }

        private bool Copy()
        {
            var sbl = BlockList.GetSelectedBlockList();
            IBlock[] blocks = sbl.SelectedBlocks;
            if (blocks.Length == 0)
            {
                System.Media.SystemSounds.Beep.Play();
                return false;
            }

            Clipboard.SetText(BlockManager.ToString(blocks));
            return true;
        }

        private void Cut()
        {
            if (Copy())
                DeleteSelectedItems(false);
        }

        private void Paste()
        {
            IBlock[] blocks = BlockManager.NewBlocks(Clipboard.GetText());
            if (blocks.Length == 0)
            {
                System.Media.SystemSounds.Beep.Play();
                return;
            }

            inSelectionChanged = true;
            foreach (IBlock block in blocks)
                AddBlock(block);

            tbLine.Text = string.Format("{0} lines", listCode.Items.Count);
            Edited = true;
            btnDelete.IsEnabled = btnCut.IsEnabled = btnCopy.IsEnabled = btnGroup.IsEnabled = btnUngroup.IsEnabled = false;
            inSelectionChanged = false;
        }

        private void Load(IBlock[] blocks)
        {
            BlockList.Clear();
            listCode.Items.Clear();

            if (blocks != null)
            {
                BlockList.Add(blocks);
                AddCode(blocks);
            }

            tbLine.Text = string.Format("{0} lines", listCode.Items.Count);
            Edited = false;
            btnDelete.IsEnabled = btnCut.IsEnabled = btnCopy.IsEnabled = btnGroup.IsEnabled = btnUngroup.IsEnabled = false;
        }

        private void OnEdited(object sender, EventArgs e)
        {
            Edited = true;
        }

        private void ListBlocks_OrderChanged(object sender, EventArgs e)
        {
            var sbl = BlockList.GetSelectedBlockList();
            if (sbl.listBlocks.SelectedItem == null)
            {
                System.Media.SystemSounds.Beep.Play();
                return;
            }

            inSelectionChanged = true;
            listCode.Items.Clear();
            AddCode(BlockList.Blocks);

            ListBoxItem item = (ListBoxItem)sbl.listBlocks.SelectedItem;
            IBlock block = (IBlock)item.Tag;
            if (block is IBlockGroup)
            {
                listCode.UnselectAll();
            }
            else
            {
                item = block.CodeItem;
                listCode.SelectedItem = item;
                listCode.ScrollIntoView(item);
            }

            Edited = true;
            inSelectionChanged = false;
        }

        private void AddCode(IBlock[] blocks)
        {
            foreach (IBlock block in blocks)
            {
                if (block is IBlockGroup)
                    AddCode(((IBlockGroup)block).Blocks);
                else
                    listCode.Items.Add(block.CodeItem);
            }
        }

        private void InsertCode(IBlock[] blocks, ref int index)
        {
            foreach (IBlock block in blocks)
            {
                if (block is IBlockGroup)
                {
                    InsertCode((IBlockGroup)block, ref index);
                }
                else
                {
                    listCode.Items.Insert(index, block.CodeItem);
                    ++index;
                }
            }
        }

        private void InsertCode(IBlockGroup group, ref int index)
        {
            foreach (IBlock block in group.Blocks)
            {
                if (block is IBlockGroup)
                {
                    InsertCode((IBlockGroup)block, ref index);
                }
                else
                {
                    listCode.Items.Insert(index, block.CodeItem);
                    ++index;
                }
            }
        }

        private void RemoveCode(IBlock block)
        {
            if (block is IBlockGroup)
            {
                IBlockGroup group = (IBlockGroup)block;
                foreach (var block2 in group.Blocks)
                    RemoveCode(block2);
            }
            else
            {
                listCode.Items.Remove(block.CodeItem);
            }
        }

        private void ListBlocks_SelectionChanged(object sender, EventArgs e)
        {
            if (inSelectionChanged) return;  // ListCode_SelectionChanged has selected a block
            inSelectionChanged = true;

            var sbl = BlockList.GetSelectedBlockList();
            if (sbl.listBlocks.SelectedItem != null)
            {
                ListBoxItem item = (ListBoxItem)sbl.listBlocks.SelectedItem;
                IBlock block = (IBlock)item.Tag;
                if (block is IBlockGroup)
                {
                    item = block.CodeItem;
                    if (item != null)
                    {
                        listCode.SelectedItem = item;
                        listCode.ScrollIntoView(item);
                    }
                    else
                    {
                        listCode.UnselectAll();
                    }
                }
                else
                {
                    item = block.CodeItem;
                    listCode.SelectedItem = item;
                    listCode.ScrollIntoView(item);
                }

                btnDelete.IsEnabled = btnCut.IsEnabled = btnCopy.IsEnabled = btnGroup.IsEnabled = true;
                btnUngroup.IsEnabled = ((block is IBlockGroup) && sbl.listBlocks.SelectedItems.Count == 1);
            }
            else
            {
                listCode.UnselectAll();
                sbl.Focus();
                btnDelete.IsEnabled = btnCut.IsEnabled = btnCopy.IsEnabled = btnGroup.IsEnabled = btnUngroup.IsEnabled = false;
            }

            inSelectionChanged = false;
        }

        private void ListCode_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (listCode.SelectedIndex >= 0)
                tbLine.Text = string.Format("Line {0} of {1}", listCode.SelectedIndex + 1, listCode.Items.Count);
            else
                tbLine.Text = string.Format("{0} lines", listCode.Items.Count);

            if (inSelectionChanged) return;  // ListBlocks_SelectionChanged has selected a line of code
            inSelectionChanged = true;

            if (listCode.SelectedItem != null)
            {
                ListBoxItem item = (ListBoxItem)listCode.SelectedItem;
                IBlock block = (IBlock)item.Tag;
                BlockList.Select(block);
                btnDelete.IsEnabled = btnCut.IsEnabled = btnCopy.IsEnabled = btnGroup.IsEnabled = true;
                btnUngroup.IsEnabled = (block is IBlockGroup);
            }
            else
            {
                BlockList.UnselectAll();
                btnDelete.IsEnabled = btnCut.IsEnabled = btnCopy.IsEnabled = btnGroup.IsEnabled = btnUngroup.IsEnabled = false;
            }

            inSelectionChanged = false;
        }

        private void ListAdd_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (listAdd.SelectedItem == null)
                return;

            inSelectionChanged = true;
            ListBoxItem item = (ListBoxItem)listAdd.SelectedItem;
            IBlock block = BlockManager.NewBlock((IBlock)item.Tag);
            AddBlock(block);
            AddTimer.Start();
            tbLine.Text = string.Format("{0} lines", listCode.Items.Count);
            Edited = true;
            btnDelete.IsEnabled = btnCut.IsEnabled = btnCopy.IsEnabled = btnGroup.IsEnabled = btnUngroup.IsEnabled = false;
            inSelectionChanged = false;
        }

        private void AddBlock(IBlock block)
        {
            var sbl = BlockList.GetSelectedBlockList(true);
            sbl.listBlocks.UnselectAll();
            listCode.UnselectAll();

            ListBoxItem item = block.BlockItem;
            sbl.listBlocks.Items.Add(item);
            sbl.listBlocks.ScrollIntoView(item);

            if (sbl.ParentGroup != null)
                sbl.ParentGroup.Add(block);

            if (block is IBlockGroup)
            {
                IBlockGroup group = (IBlockGroup)block;
                if (sbl.ParentGroup != null)
                {
                    bool found = false;
                    ListBoxItem item2 = InsertCodeItemBefore(BlockList.Blocks, sbl.ParentGroup, ref found);
                    if (item2 != null)
                    {
                        int index = listCode.Items.IndexOf(item2);
                        InsertCode(group, ref index);
                    }
                    else
                    {
                        AddCode(group.Blocks);
                    }
                }
                else
                {
                    AddCode(group.Blocks);
                }
            }
            else
            {
                item = block.CodeItem;
                if (sbl.ParentGroup != null)
                {
                    bool found = false;
                    ListBoxItem item2 = InsertCodeItemBefore(BlockList.Blocks, sbl.ParentGroup, ref found);
                    if (item2 != null)
                    {
                        int index = listCode.Items.IndexOf(item2);
                        listCode.Items.Insert(index, item);
                    }
                    else
                    {
                        listCode.Items.Add(item);
                    }
                }
                else
                {
                    listCode.Items.Add(item);
                }
            }
        }

        private ListBoxItem InsertCodeItemBefore(IBlock[] blocks, IBlockGroup group, ref bool found)
        {
            foreach (IBlock block in blocks)
            {
                if (block is IBlockGroup)
                {
                    IBlockGroup group2 = (IBlockGroup)block;
                    if (group2 == group)
                    {
                        found = true;
                        continue;
                    }
                    ListBoxItem item = InsertCodeItemBefore(group2.Blocks, group, ref found);
                    if (item != null)
                        return item;
                }
                else
                {
                    if (found && block.BlockGroup != group)
                        return block.CodeItem;
                    if (block.BlockGroup == group)
                        found = true;
                }
            }

            return null;
        }

        private void AddTimer_Tick(object sender, EventArgs e)
        {
            listAdd.UnselectAll();
            AddTimer.Stop();  // one-shot
        }

        private void ButtonBase_Click(object sender, RoutedEventArgs e)
        {
            ButtonBase button = e.OriginalSource as ButtonBase;
            if (button == null || e.Handled) return;

            switch (button.Name)
            {
                case "btnNew": New(); break;
                case "btnOpen": Open(); break;
                case "btnSave": Save(); break;
                case "btnSaveAs": SaveAs(); break;

                case "btnAdd":
                    gridAdd.Visibility = Visibility.Visible;
                    break;
                case "btnAddClose":
                case "btnAddFinished":
                    gridAdd.Visibility = Visibility.Hidden;
                    break;

                case "btnDelete": DeleteSelectedItems(); break;
                case "btnGroup": GroupSelectedItems(); break;
                case "btnUngroup": UngroupSelectedGroup(); break;
                case "btnCut": Cut(); break;
                case "btnCopy": Copy(); break;
                case "btnPaste": Paste(); break;

                case "btnLight":
                    ((App)Application.Current).SetSkin("Light");
                    BlockList.UpdateBrushes();
                    btnLight.IsEnabled = false;
                    btnDark.IsEnabled = true;
                    break;
                case "btnDark":
                    ((App)Application.Current).SetSkin("Dark");
                    BlockList.UpdateBrushes();
                    btnLight.IsEnabled = true;
                    btnDark.IsEnabled = false;
                    break;

                case "btnAbout":
                    MessageBox.Show("Created by Bruce Greene\nwww.MotionCommander.com", "Block Editor", 
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    break;

                case "btnMinimize": WindowState = WindowState.Minimized; break;
                case "btnMaximize": ToggleMaximized(); break;
                case "btnClose": Close(); break;
            }
        }

        private void ToggleMaximized()
        {
            if (WindowState == WindowState.Maximized)
            {
                WindowState = WindowState.Normal;
                ResizeMode = ResizeMode.CanResize;
            }
            else
            {
                if (WindowState != WindowState.Normal)
                    WindowState = WindowState.Normal;
                ResizeMode = ResizeMode.NoResize;
                WindowState = WindowState.Maximized;
            }
        }

        private void Window_StateChanged(object sender, EventArgs e)
        {
            if (WindowState == WindowState.Maximized && ResizeMode != ResizeMode.NoResize)
            {
                WindowState = WindowState.Normal;
                ResizeMode = ResizeMode.NoResize;
                WindowState = WindowState.Maximized;
            }
        }

        private void MainWindow_StateChanged(object sender, EventArgs e)
        {
            if (WindowState == WindowState.Maximized)
            {
                tbMaximize.Text = "\uf2d2";
                tbMaximize.ToolTip = "Restore";
            }
            else
            {
                tbMaximize.Text = "\uf2d0";
                tbMaximize.ToolTip = "Maximize";
            }
        }

        private void OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            bool ctrl = (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl));
            var element = Keyboard.FocusedElement;
            bool listBoxFocus = (element is ListBoxItem) || (element is ListBox);

            if (listBoxFocus)  // block editor cut/copy/paste/delete
            {
                if (ctrl)
                {
                    switch (e.Key)
                    {
                        case Key.X: Cut(); e.Handled = true; break;
                        case Key.C: Copy(); e.Handled = true; break;
                        case Key.V: Paste(); e.Handled = true; break;
                    }
                }
                else
                {
                    switch (e.Key)
                    {
                        case Key.Delete:
                            DeleteSelectedItems();
                            e.Handled = true;
                            break;
                    }
                }
            }

            if (ctrl)  // global functionality
            {
                switch (e.Key)
                {
                    case Key.N:
                        e.Handled = true;
                        if (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift))  // Ctrl+Shift+N opens file in Notepad
                        {
                            if (Edited)
                                Save();
                            if (File.Exists(path))
                                System.Diagnostics.Process.Start("notepad.exe", path);
                        }
                        else
                        {
                            New();
                        }
                        break;
                    case Key.O:
                        e.Handled = true;
                        Open();
                        break;
                    case Key.S:
                        e.Handled = true;
                        Save();
                        break;
                }
            }
            else
            {
                switch (e.Key)
                {
                    case Key.Insert:
                        gridAdd.Visibility = Visibility.Visible;
                        e.Handled = true;
                        break;
                    case Key.Escape:
                        gridAdd.Visibility = Visibility.Hidden;
                        BlockList.UnselectAll();
                        break;
                }
            }
        }

        private void Page_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files.Length > 0 && OfferToSaveChanges())
                    Open(files[0]);
            }
        }

        private void OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            gridAdd.Visibility = Visibility.Hidden;

            var sbl = BlockList.GetSelectedBlockList();
            sbl.OnMouseDoubleClick();
        }

        private void Caption_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            DragMove();
        }

        private void Caption_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            ToggleMaximized();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (!OfferToSaveChanges())
                e.Cancel = true;
        }
    }
}
