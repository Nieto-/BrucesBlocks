using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace BrucesBlocks
{
    /// <summary>A ListBox that supports drag-to-order.</summary>
    /// <remarks>
    /// The key thing to be aware of about this class is that an item's order is maintained by
    /// the DragPanel Order attached property, NOT the item's index within the Items collection.
    /// </remarks>
    public class DragListBox : ListBox
    {
        public DragListBox()
        {
            // The following code is equivalent to this xaml:
            // <ListBox.ItemsPanel>
            //     <ItemsPanelTemplate>
            //         <local:DragPanel Loaded="DragPanel_Loaded" />
            //     </ItemsPanelTemplate>
            // </ListBox.ItemsPanel>
            var factory = new FrameworkElementFactory(typeof(DragPanel));
            factory.SetValue(Panel.IsItemsHostProperty, true);
            factory.AddHandler(LoadedEvent, (RoutedEventHandler)DragPanel_Loaded);
            var template = new ItemsPanelTemplate();
            template.VisualTree = factory;
            ItemsPanel = template;
        }

        /// <summary>Items in drag-to-order Order.</summary>
        public ListBoxItem[] ItemsInOrder
        {
            get { return Items.OfType<ListBoxItem>().OrderBy(DragPanel.GetOrder).ToArray(); }
        }

        /// <summary>Selected Items in drag-to-order Order.</summary>
        public ListBoxItem[] SelectedItemsInOrder
        {
            get { return Items.OfType<ListBoxItem>().Where(child => child.IsSelected).OrderBy(DragPanel.GetOrder).ToArray(); }
        }

        /// <summary>Order index of the first selected item.</summary>
        public int SelectedItemIndex
        {
            get
            {
                int index = 0;
                foreach (ListBoxItem item in Items.OfType<ListBoxItem>().OrderBy(DragPanel.GetOrder))
                {
                    if (item.IsSelected)
                        return index;
                    ++index;
                }
                return -1;
            }
        }

        /// <summary>Inserts a ListBoxItem at the specified Order index.</summary>
        public void Insert(int index, ListBoxItem item)
        {
            int index2 = 0;
            foreach (ListBoxItem item2 in Items.OfType<ListBoxItem>().OrderBy(DragPanel.GetOrder))
            {
                ++index2;
                if (index2 > index)
                    DragPanel.SetOrder(item2, index2);
            }

            DragPanel.SetOrder(item, index);
            Items.Add(item);
        }

        /// <summary>Resets a ListBoxItem Order index.</summary>
        public void ResetItemIndex(ListBoxItem item)
        {
            DragPanel.SetOrder(item, -1);
        }

        private DragPanel dp;

        private void DragPanel_Loaded(object sender, RoutedEventArgs e)
        {
            dp = (DragPanel)sender;
            dp.Layout = Layout;
            dp.DropShadowColor = DropShadowColor;
            dp.OrderChanged += OnDpOrderChanged;
        }

        private void OnDpOrderChanged(object sender, EventArgs e)
        {
            RaiseEvent(new RoutedEventArgs(OrderChangedEvent));
        }

        #region Dependency Properties

        public enum ELayout { None, RowMajor, ColumnMajor, SingleRow, SingleColumn }

        public static readonly DependencyProperty LayoutProperty = DependencyProperty.Register("Layout", typeof(ELayout), typeof(DragListBox),
            new FrameworkPropertyMetadata(ELayout.None, OnLayoutChanged));

        private static void OnLayoutChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            DragListBox dlb = (DragListBox)d;
            switch (e.NewValue)
            {
                case ELayout.RowMajor:
                case ELayout.SingleColumn:
                    dlb.VerticalAlignment = VerticalAlignment.Top;
                    dlb.HorizontalAlignment = HorizontalAlignment.Stretch;
                    dlb.SetValue(ScrollViewer.HorizontalScrollBarVisibilityProperty, ScrollBarVisibility.Disabled);
                    dlb.SetValue(ScrollViewer.VerticalScrollBarVisibilityProperty, ScrollBarVisibility.Auto);
                    break;

                case ELayout.ColumnMajor:
                case ELayout.SingleRow:
                    dlb.VerticalAlignment = VerticalAlignment.Stretch;
                    dlb.HorizontalAlignment = HorizontalAlignment.Left;
                    dlb.SetValue(ScrollViewer.HorizontalScrollBarVisibilityProperty, ScrollBarVisibility.Auto);
                    dlb.SetValue(ScrollViewer.VerticalScrollBarVisibilityProperty, ScrollBarVisibility.Disabled);
                    break;
            }
            if (dlb.dp != null)
            {
                dlb.dp.Layout = (ELayout)e.NewValue;
                dlb.dp.InvalidateMeasure();
            }
        }

        public ELayout Layout
        {
            get { return (ELayout)GetValue(LayoutProperty); }
            set { SetValue(LayoutProperty, value); }
        }

        public static readonly DependencyProperty DropShadowColorProperty = DependencyProperty.Register("DropShadowColor", typeof(Color), typeof(DragListBox),
            new FrameworkPropertyMetadata(Colors.Black, OnDropShadowColorChanged));

        private static void OnDropShadowColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            DragListBox dlb = (DragListBox)d;
            if (dlb.dp != null)
                dlb.dp.DropShadowColor = (Color)e.NewValue;
        }

        public Color DropShadowColor
        {
            get { return (Color)GetValue(DropShadowColorProperty); }
            set { SetValue(DropShadowColorProperty, value); }
        }

        public static readonly RoutedEvent OrderChangedEvent = EventManager.RegisterRoutedEvent("OrderChanged", RoutingStrategy.Bubble, 
            typeof(RoutedEventHandler), typeof(DragListBox));

        public event RoutedEventHandler OrderChanged
        {
            add { AddHandler(OrderChangedEvent, value); }
            remove { RemoveHandler(OrderChangedEvent, value); }
        }

        #endregion Dependency Properties

        /// <summary>Private ListBox ItemsPanelTemplate class.</summary>
        private class DragPanel : Panel
        {
            public ELayout Layout = ELayout.RowMajor;
            public Color DropShadowColor = Colors.Black;
            public event EventHandler OrderChanged;

            #region Drag to Reorder

            private UIElement draggingObject;
            private Vector draggingObjectDelta;
            private int draggingObjectOrder;

            protected override void OnPreviewMouseLeftButtonDown(MouseButtonEventArgs e)
            {
                StartDragging(e);
            }

            protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
            {
                StopDragging();
            }

            protected override void OnMouseMove(MouseEventArgs e)
            {
                if (draggingObject != null)
                {
                    if (e.LeftButton == MouseButtonState.Released)
                        StopDragging();
                    else
                        DoDragging(e);
                }
            }

            protected override void OnMouseLeave(MouseEventArgs e)
            {
                StopDragging();
                base.OnMouseLeave(e);
            }

            private void StartDragging(MouseEventArgs e)
            {
                Point mousePosition = e.GetPosition(this);
                draggingObject = GetChildOfThis((UIElement)e.OriginalSource);
                draggingObjectOrder = GetOrder(draggingObject);
                draggingObject.SetValue(ZIndexProperty, 100);
                Rect position = GetPosition(draggingObject);
                draggingObjectDelta = position.TopLeft - mousePosition;
            }

            private void DoDragging(MouseEventArgs e)
            {
                e.Handled = true;
                Point mousePosition = e.GetPosition(this);
                int index = GridIndex(mousePosition);
                SetOrder(draggingObject, index);
                Point topLeft = mousePosition + draggingObjectDelta;
                Rect newPosition = new Rect(topLeft, GetPosition(draggingObject).Size);
                // Constrain dragging to within the Panel boundaries.
                if (newPosition.Width >= ActualWidth || newPosition.Left < 0)
                    newPosition.Offset(-newPosition.Left, 0);
                if (newPosition.Height >= ActualHeight || newPosition.Top < 0)
                    newPosition.Offset(0, -newPosition.Top);
                if (newPosition.Width < ActualWidth && newPosition.Right > ActualWidth)
                    newPosition.Offset(ActualWidth - newPosition.Right, 0);
                if (newPosition.Height < ActualHeight && newPosition.Bottom > ActualHeight)
                    newPosition.Offset(0, ActualHeight - newPosition.Bottom);
                SetPosition(draggingObject, newPosition);
                if (draggingObject.Effect == null)
                {
                    draggingObject.Effect = new DropShadowEffect()
                    {
                        ShadowDepth = 1,
                        BlurRadius = 6,
                        Color = DropShadowColor
                    };
                }
            }

            private void StopDragging()
            {
                if (draggingObject != null)
                {
                    int newOrder = GetOrder(draggingObject);
                    draggingObject.ClearValue(ZIndexProperty);
                    InvalidateMeasure();
                    draggingObject.Effect = null;
                    draggingObject = null;

                    if (OrderChanged != null && newOrder != draggingObjectOrder)
                        OrderChanged(this, new EventArgs());
                }
            }

            private UIElement GetChildOfThis(UIElement element)
            {
                // Travel up the tree until the element's parent is this Panel.
                UIElement parent = VisualTreeHelper.GetParent(element) as UIElement;
                while (parent != this && parent != null)
                {
                    element = parent;
                    parent = VisualTreeHelper.GetParent(element) as UIElement;
                }
                return element;
            }

            #endregion Drag to Reorder

            #region Measure and Arrange

            private int items = 0, columns = 0, rows = 0;
            private double itemWidth = 0, itemHeight = 0;
            private double columnWidth = 0, rowHeight = 0;

            protected override Size MeasureOverride(Size availableSize)
            {
                if (Children.Count == 0)
                {
                    items = columns = rows = 0;
                    return new Size(0, 0);
                }

                if (draggingObject != null)
                {
                    // Slide the other elements aside while an element is being dragged.
                    int orderDO = GetOrder(draggingObject);
                    int order = 0;
                    foreach (UIElement child in Children.OfType<UIElement>().OrderBy(GetOrder))
                    {
                        if (order == orderDO) ++order;
                        if (child != draggingObject)
                        {
                            if (GetOrder(child) != order)
                                SetOrder(child, order);
                            ++order;
                        }
                    }
                }
                else
                {
                    // New elements will have Order -1 and must be assigned an Order.
                    int maxOrder = -1;
                    bool setOrder = false;
                    foreach (UIElement child in Children)
                    {
                        int order = GetOrder(child);
                        maxOrder = Math.Max(maxOrder, order);
                        if (order == -1)
                            setOrder = true;
                    }

                    if (setOrder)
                    {
                        // New elements are added to the end of the Order.
                        // Note that Order is completely different from Children index!
                        foreach (UIElement child in Children)
                        {
                            if (GetOrder(child) == -1)
                                SetOrder(child, ++maxOrder);
                        }
                    }

                    if (items != Children.Count)
                    {
                        // Elements have been added or deleted. Determine new max width and height.
                        items = Children.Count;
                        itemWidth = itemHeight = 0;
                        Size sizeInf = new Size(double.PositiveInfinity, double.PositiveInfinity);
                        foreach (UIElement child in Children)
                        {
                            child.Measure(sizeInf);
                            Size size = child.DesiredSize;
                            itemWidth = Math.Max(itemWidth, size.Width);
                            itemHeight = Math.Max(itemHeight, size.Height);
                        }
                    }
                }

                columnWidth = itemWidth;
                rowHeight = itemHeight;

                switch (Layout)
                {
                    case ELayout.RowMajor:
                        columns = Math.Max(1, Math.Min(items, (int)(availableSize.Width / itemWidth)));
                        rows = items / columns;  // number of full rows
                        if (items % columns > 0) ++rows;  // partial row
                        if (!double.IsPositiveInfinity(availableSize.Width) && availableSize.Width > (columnWidth * columns))
                            columnWidth = availableSize.Width / columns;  // span available width
                        break;

                    case ELayout.ColumnMajor:
                        rows = Math.Max(1, Math.Min(items, (int)(availableSize.Height / itemHeight)));
                        columns = items / rows;  // number of full columns
                        if (items % rows > 0) ++columns;  // partial column
                        if (!double.IsPositiveInfinity(availableSize.Height) && availableSize.Height > (rowHeight * rows))
                            rowHeight = availableSize.Height / rows;  // span available height
                        break;

                    case ELayout.SingleRow:
                        columns = items;
                        rows = 1;
                        if (!double.IsPositiveInfinity(availableSize.Height) && availableSize.Height > (rowHeight * rows))
                            rowHeight = availableSize.Height / rows;  // span available height
                        break;

                    case ELayout.SingleColumn:
                        rows = items;
                        columns = 1;
                        if (!double.IsPositiveInfinity(availableSize.Width) && availableSize.Width > (columnWidth * columns))
                            columnWidth = availableSize.Width / columns;  // span available width
                        break;
                }

                int index = 0;
                foreach (UIElement child in Children.OfType<UIElement>().OrderBy(GetOrder))
                {
                    if (child != draggingObject)
                        SetPosition(child, GridPosition(index));
                    ++index;
                }

                return new Size(columnWidth * columns, rowHeight * rows);
            }

            protected override Size ArrangeOverride(Size finalSize)
            {
                foreach (UIElement child in Children)
                {
                    child.Arrange(GetPosition(child));
                    //((FrameworkElement)child).ToolTip = string.Format("{0}:{1}", GetOrder(child), Children.IndexOf(child));  // diagnostic info
                }

                return new Size(columnWidth * columns, rowHeight * rows);
            }

            private Rect GridPosition(int index)  // grid position from index
            {
                int row, col;
                switch (Layout)
                {
                    case ELayout.RowMajor:
                        col = index % columns;
                        row = index / columns;
                        return new Rect(columnWidth * col, rowHeight * row, columnWidth, rowHeight);

                    case ELayout.ColumnMajor:
                        row = index % rows;
                        col = index / rows;
                        return new Rect(columnWidth * col, rowHeight * row, columnWidth, rowHeight);

                    case ELayout.SingleRow:
                        return new Rect(columnWidth * index, 0, columnWidth, rowHeight);

                    case ELayout.SingleColumn:
                    default:
                        return new Rect(0, rowHeight * index, columnWidth, rowHeight);
                }
            }

            private int GridIndex(Point point)  // the grid index that contains the point
            {
                int row, col;
                switch (Layout)
                {
                    case ELayout.RowMajor:
                        col = Math.Min((int)(point.X / columnWidth), columns - 1);
                        row = Math.Min((int)(point.Y / rowHeight), rows - 1);
                        return Math.Min((row * columns) + col, items - 1);

                    case ELayout.ColumnMajor:
                        col = Math.Min((int)(point.X / columnWidth), columns - 1);
                        row = Math.Min((int)(point.Y / rowHeight), rows - 1);
                        return Math.Min((col * rows) + row, items - 1);

                    case ELayout.SingleRow:
                        return Math.Min((int)(point.X / columnWidth), items - 1);

                    case ELayout.SingleColumn:
                    default:
                        return Math.Min((int)(point.Y / rowHeight), items - 1);
                }
            }

            #endregion Measure and Arrange

            #region Attached Properties

            public static readonly DependencyProperty OrderProperty = DependencyProperty.RegisterAttached("Order", typeof(int), typeof(DragPanel),
                new FrameworkPropertyMetadata(-1, FrameworkPropertyMetadataOptions.AffectsParentMeasure));

            public static int GetOrder(DependencyObject obj)
            {
                return (int)obj.GetValue(OrderProperty);
            }

            public static void SetOrder(DependencyObject obj, int value)
            {
                obj.SetValue(OrderProperty, value);
            }

            public static readonly DependencyProperty PositionProperty = DependencyProperty.RegisterAttached("Position", typeof(Rect), typeof(DragPanel),
                new FrameworkPropertyMetadata(new Rect(double.NaN, double.NaN, double.NaN, double.NaN), FrameworkPropertyMetadataOptions.AffectsParentArrange));

            public static Rect GetPosition(DependencyObject obj)
            {
                return (Rect)obj.GetValue(PositionProperty);
            }

            public static void SetPosition(DependencyObject obj, Rect value)
            {
                obj.SetValue(PositionProperty, value);
            }

            #endregion Attached Properties
        }
    }
}