using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace BrucesBlocks
{
    /// <summary>
    /// Interaction logic for BlockList.xaml
    /// </summary>
    internal partial class BlockList : UserControl
    {
        private int groupLevel = 0;
        private BlockList Owner = null;
        private BlockList GroupList = null;
        private SolidColorBrush SelectedBrush, UnselectedBrush;

        public BlockList() : this(null) { }

        private BlockList(BlockList Owner)
        {
            InitializeComponent();

            this.Owner = Owner;
            if (Owner != null)
                groupLevel = Owner.groupLevel + 1;

            UpdateBrushes();
            Clear();
        }

        public void UpdateBrushes()
        {
            bool selected = (bdrBar.Background == SelectedBrush);
            SelectedBrush = (SolidColorBrush)FindResource("SelectedBrush");
            UnselectedBrush = (SolidColorBrush)FindResource("UnselectedBrush");
            bdrBar.Background = selected ? SelectedBrush : UnselectedBrush;
            if (dp != null)
                dp.UpdateBrushes();
            if (GroupList != null)
                GroupList.UpdateBrushes();
        }

        public void Clear()
        {
            listBlocks.Items.Clear();
            SplitterOpen = false;
        }

        public void Add(IBlock[] blocks)
        {
            foreach (var block in blocks)
            {
                listBlocks.Items.Add(block.BlockItem);
                block.BlockGroup = ParentGroup;
            }
        }

        public void Select(IBlock block)
        {
            // Push block groups on the stack up to the root:
            Stack<IBlockGroup> groupStack = new Stack<IBlockGroup>();
            IBlockGroup group = block.BlockGroup;
            while (group != null)
            {
                groupStack.Push(group);
                group = group.BlockGroup;
            }

            // Pop block groups off the stack and open them:
            ListBoxItem item;
            BlockList bl = this;
            while (groupStack.Count > 0)
            {
                group = groupStack.Pop();
                item = group.BlockItem;
                if (bl.listBlocks.Items.Contains(item))
                {
                    if (!bl.SplitterOpen && bl.listBlocks.SelectedItem == item)
                        bl.listBlocks.SelectedItem = null;
                    bl.listBlocks.SelectedItem = item;
                    bl.listBlocks.ScrollIntoView(item);
                }
                bl = bl.GroupList;
            }

            // Select the item:
            item = block.BlockItem;
            if (bl != null && bl.listBlocks.Items.Contains(item))
            {
                bl.listBlocks.SelectedItem = item;
                bl.listBlocks.ScrollIntoView(item);
                if (bl.GroupList != null)
                    bl.GroupList.listBlocks.UnselectAll();
            }
        }

        public void UnselectAll()
        {
            listBlocks.UnselectAll();
        }

        public IBlock[] Blocks
        {
            get
            {
                List<IBlock> blockList = new List<IBlock>();
                if (dp != null)
                {
                    foreach (var item in dp.Children.OfType<ListBoxItem>().OrderBy(DragPanel.GetOrder))
                        blockList.Add((IBlock)item.Tag);
                }
                else
                {
                    foreach (var item in listBlocks.Items)
                        blockList.Add((IBlock)((ListBoxItem)item).Tag);
                }
                return blockList.ToArray();
            }
            set
            {
                Clear();
                Add(value);
            }
        }

        public IBlock[] SelectedBlocks
        {
            get
            {
                if (dp != null)
                {
                    List<IBlock> blockList = new List<IBlock>();
                    foreach (var item in dp.Children.OfType<ListBoxItem>().OrderBy(DragPanel.GetOrder))
                    {
                        if (item.IsSelected)
                            blockList.Add((IBlock)item.Tag);
                    }
                    return blockList.ToArray();
                }
                else
                {
                    return new IBlock[0];
                }
            }
        }

        public IBlock[] AdjacentSelectedBlocks
        {
            get
            {
                if (dp != null)
                {
                    List<IBlock> blockList = new List<IBlock>();
                    bool started = false;
                    foreach (var item in dp.Children.OfType<ListBoxItem>().OrderBy(DragPanel.GetOrder))
                    {
                        if (item.IsSelected)
                        {
                            blockList.Add((IBlock)item.Tag);
                            started = true;
                        }
                        else if (started)
                        {
                            break;
                        }
                    }
                    return blockList.ToArray();
                }
                else
                {
                    return new IBlock[0];
                }
            }
        }

        private IBlockGroup parentGroup = null;

        public IBlockGroup ParentGroup
        {
            get { return parentGroup; }
            set
            {
                if (parentGroup != value)
                {
                    parentGroup = value;
                    listBlocks.Items.Clear();
                    if (GroupList != null)
                        GroupList.ParentGroup = null;

                    if (parentGroup != null)
                    {
                        foreach (IBlock block in parentGroup.Blocks)
                            listBlocks.Items.Add(block.BlockItem);
                    }
                }
            }
        }

        private bool splitterOpen = true;

        private bool SplitterOpen
        {
            get { return splitterOpen; }
            set
            {
                if (splitterOpen != value)
                {
                    splitterOpen = value;
                    if (splitterOpen)
                    {
                        bdrBar.Background = SelectedBrush;
                        BlockList owner = Owner;
                        while (owner != null)
                        {
                            owner.bdrBar.Background = UnselectedBrush;
                            owner = owner.Owner;
                        }
                        splitter.Visibility = Visibility.Visible;
                        rowTop.Height = new GridLength(rowTopHeight, GridUnitType.Pixel);
                        rowBottom.Height = new GridLength(1, GridUnitType.Star);
                    }
                    else
                    {
                        splitter.Visibility = Visibility.Collapsed;
                        rowTop.Height = new GridLength(1, GridUnitType.Star);
                        rowBottom.Height = new GridLength(0, GridUnitType.Pixel);
                        bdrBar.Background = UnselectedBrush;
                        if (GroupList != null)
                        {
                            GroupList.SplitterOpen = false;
                            GroupList.ParentGroup = null;
                        }
                    }
                }
            }
        }

        public BlockList GetSelectedBlockList(bool forAdd = false)
        {
            if (GroupList == null || GroupList.ParentGroup == null)
                return this;
            if (GroupList.listBlocks.SelectedItems.Count == 0)
                return forAdd ? GroupList : this;
            return GroupList.GetSelectedBlockList(forAdd);
        }

        public void OnMouseDoubleClick()
        {
            if (listBlocks.SelectedItems.Count == 1)
                OnSelectionChanged(null, null);
        }

        public static event EventHandler OrderChanged;
        public static event EventHandler SelectionChanged;

        private DragPanel dp;

        private void DragPanel_Loaded(object sender, RoutedEventArgs e)
        {
            dp = (DragPanel)sender;
            dp.OrderChanged += OnOrderChanged;
        }

        private void OnOrderChanged(object sender, EventArgs e)
        {
            if (ParentGroup != null)
                ParentGroup.Blocks = Blocks;

            if (OrderChanged != null)
                OrderChanged(this, new EventArgs());
        }

        private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (Owner != null && Owner.listBlocks.SelectedItems.Count > 1 && ParentGroup != null)
                Owner.listBlocks.SelectedItem = ParentGroup.BlockItem;

            bool groupOpen = false;
            if (listBlocks.SelectedItems.Count == 1)
            {
                IBlock block = (IBlock)((ListBoxItem)listBlocks.SelectedItem).Tag;
                if (block is IBlockGroup)
                {
                    if (GroupList == null)
                    {
                        GroupList = new BlockList(this);
                        ccGroupList.Content = GroupList;
                    }
                    GroupList.ParentGroup = (IBlockGroup)block;
                    bdrBar.Background = SelectedBrush;
                    BlockList owner = Owner;
                    while (owner != null)
                    {
                        owner.bdrBar.Background = UnselectedBrush;
                        owner = owner.Owner;
                    }
                    SplitterOpen = true;
                    groupOpen = true;
                }
            }

            if (!groupOpen && GroupList != null)
            {
                GroupList.ParentGroup = null;
                bdrBar.Background = UnselectedBrush;
                if (Owner != null)
                    Owner.bdrBar.Background = SelectedBrush;
            }

            if (SelectionChanged != null)
                SelectionChanged(this, new EventArgs());
        }

        private void CloseGroupList_Click(object sender, RoutedEventArgs e)
        {
            SplitterOpen = false;
            if (Owner != null && listBlocks.Items.Count > 0)
                Owner.bdrBar.Background = SelectedBrush;
        }

        private double rowTopHeight = 140;

        private void Splitter_DragCompleted(object sender, DragCompletedEventArgs e)
        {
            rowTopHeight = rowTop.Height.Value;
        }

        private void OnMouseDown(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            listBlocks.UnselectAll();
            listBlocks.Focus();
        }

        private void OnMouseDownGroupList(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            if (GroupList != null)
            {
                GroupList.listBlocks.UnselectAll();
                GroupList.listBlocks.Focus();
            }
        }

        private void OnMouseDownBar(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            if (GroupList != null)
            {
                if (GroupList.GroupList != null && GroupList.listBlocks.Items.Count > 0)
                {
                    GroupList.GroupList.ParentGroup = null;
                    GroupList.bdrBar.Background = UnselectedBrush;
                    bdrBar.Background = SelectedBrush;
                }
            }
        }
    }
}
