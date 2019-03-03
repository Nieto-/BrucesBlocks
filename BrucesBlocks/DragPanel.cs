using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace BrucesBlocks
{
    internal sealed class DragPanel : Panel
    {
        #region Drag to Reorder

        private UIElement draggingObject;
        private Vector draggingObjectDelta;
        private int draggingObjectOrder;
        private Color DropShadowColor;

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
            SetPosition(draggingObject, newPosition);
            if (draggingObject.Effect == null)
            {
                draggingObject.Effect = new DropShadowEffect()
                {
                    ShadowDepth = 2,
                    BlurRadius = 4,
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
                SetPosition(draggingObject, GetDesiredPosition(draggingObject));
                draggingObject.Effect = null;
                draggingObject = null;

                if (OrderChanged != null && newOrder != draggingObjectOrder)
                    OrderChanged(this, new EventArgs());
            }
        }

        /// <summary>Informs the owner that user has dragged to reorder.</summary>
        public event EventHandler OrderChanged;

        /// <summary>Called by the owner after style has changed.</summary>
        public void UpdateBrushes()
        {
            DropShadowColor = (Color)FindResource("DropShadowColor");
        }

        private UIElement GetChildOfThis(UIElement element)
        {
            // Move up the tree until the element's parent is this Panel.
            UIElement parent = (UIElement)VisualTreeHelper.GetParent(element);
            while (parent != this && parent != null)
            {
                element = parent;
                parent = (UIElement)VisualTreeHelper.GetParent(element);
            }
            return element;
        }

        #endregion Drag to Reorder

        #region Measure and Arrange

        private int items = 0, columns = 0, rows = 0;
        private double itemWidth = 0, columnWidth = 0, rowHeight = 0;

        protected override Size MeasureOverride(Size availableSize)
        {
            if (Children.Count == 0)
            {
                items = columns = rows = 0;
                return new Size(0, 0);
            }

            // Newly added elements will have Order -1 and must be assigned an Order.
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
                // Note that Order is completely different from Children collection order!
                foreach (UIElement child in Children)
                {
                    if (GetOrder(child) == -1)
                        SetOrder(child, ++maxOrder);
                }
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

            // Elements have been added or deleted. Determine new max width and height.
            if (items != Children.Count)
            {
                items = Children.Count;
                itemWidth = rowHeight = 0;
                Size sizeInf = new Size(double.PositiveInfinity, double.PositiveInfinity);
                foreach (UIElement child in Children)
                {
                    child.Measure(sizeInf);
                    Size size = child.DesiredSize;
                    itemWidth = Math.Max(itemWidth, size.Width);
                    rowHeight = Math.Max(rowHeight, size.Height);
                }
            }

            columnWidth = itemWidth;

            if (Layout == ELayout.Grid)
            {
                columns = items;
                rows = 1;
                if ((columnWidth * columns) > availableSize.Width)
                {
                    columns = Math.Max(1, (int)(availableSize.Width / columnWidth));
                    rows = items / columns;  // number of full rows
                    if (items % columns > 0) ++rows;  // partial row
                }
            }
            else if (Layout == ELayout.Row)
            {
                columns = items;
                rows = 1;
            }
            else // ELayout.Column
            {
                columns = 1;
                rows = items;
            }

            if (!double.IsPositiveInfinity(availableSize.Width) && availableSize.Width > (columnWidth * columns))
                columnWidth = availableSize.Width / columns;  // span available width

            int index = 0;
            foreach (UIElement child in Children.OfType<UIElement>().OrderBy(GetOrder))
            {
                if (child != draggingObject)
                {
                    Rect pos = GridPosition(index);
                    SetDesiredPosition(child, pos);
                }
                ++index;
            }

            return new Size(columnWidth * columns, rowHeight * rows);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            foreach (UIElement child in Children)
            {
                Rect position = GetPosition(child);
                if (double.IsNaN(position.Top))
                    position = GetDesiredPosition(child);
                child.Arrange(position);
            }

            return new Size(columnWidth * columns, rowHeight * rows);
        }

        private Rect GridPosition(int index)  // grid position from index
        {
            int col = index % columns;
            int row = index / columns;
            return new Rect(columnWidth * col, rowHeight * row, columnWidth, rowHeight);
        }

        private int GridIndex(Point point)  // the grid index that contains the point
        {
            int col = Math.Min((int)(point.X / columnWidth), columns - 1);
            int row = Math.Min((int)(point.Y / rowHeight), rows - 1);
            return Math.Min((row * columns) + col, items - 1);
        }

        #endregion Measure and Arrange

        #region Attached Properties

        public enum ELayout { Grid, Row, Column }  // three layout options

        public static readonly DependencyProperty LayoutProperty = DependencyProperty.Register("Layout", typeof(ELayout), typeof(DragPanel), 
            new FrameworkPropertyMetadata(ELayout.Grid, FrameworkPropertyMetadataOptions.AffectsMeasure));

        public ELayout Layout
        {
            get { return (ELayout)GetValue(LayoutProperty); }
            set { SetValue(LayoutProperty, value); }
        }

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

        public static readonly DependencyProperty DesiredPositionProperty = DependencyProperty.RegisterAttached("DesiredPosition", typeof(Rect), typeof(DragPanel),
                new FrameworkPropertyMetadata(new Rect(double.NaN, double.NaN, double.NaN, double.NaN), OnDesiredPositionChanged));

        public static Rect GetDesiredPosition(DependencyObject obj)
        {
            return (Rect)obj.GetValue(DesiredPositionProperty);
        }

        public static void SetDesiredPosition(DependencyObject obj, Rect value)
        {
            obj.SetValue(DesiredPositionProperty, value);
        }

        private static void OnDesiredPositionChanged(DependencyObject obj, DependencyPropertyChangedEventArgs e)
        {
            SetPosition(obj, (Rect)e.NewValue);
        }

        #endregion Attached Properties
    }
}