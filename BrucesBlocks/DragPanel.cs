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
            Point startPosition = e.GetPosition(this);
            draggingObject = GetChild((UIElement)e.OriginalSource);
            draggingObjectOrder = GetOrder(draggingObject);
            draggingObject.SetValue(ZIndexProperty, 100);
            Rect position = GetPosition(draggingObject);
            draggingObjectDelta = position.TopLeft - startPosition;
            draggingObject.BeginAnimation(PositionProperty, null);
            SetPosition(draggingObject, position);
        }

        private void DoDragging(MouseEventArgs e)
        {
            e.Handled = true;
            Point mousePosition = e.GetPosition(this);
            int index = GetIndex(mousePosition);
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

        public event EventHandler OrderChanged;

        public void UpdateBrushes()
        {
            DropShadowColor = (Color)FindResource("DropShadowColor");
        }

        private UIElement GetChild(UIElement element)
        {
            UIElement child = element;
            UIElement parent = (UIElement)VisualTreeHelper.GetParent(child);
            while (parent != null && parent != this)
            {
                child = parent;
                parent = (UIElement)VisualTreeHelper.GetParent(child);
            }
            return child;
        }

        #endregion Drag to Reorder

        #region Measure and Arrange

        private int items = 0, columns = 0, rows = 0;
        private double itemWidth = 0, columnWidth = 0, rowHeight = 0;

        protected override Size MeasureOverride(Size availableSize)
        {
            if (Children.Count > 0)
            {
                int next = Children.OfType<UIElement>().Max(ch => GetOrder(ch)) + 1;
                foreach (UIElement child in Children.OfType<UIElement>().Where(child => GetOrder(child) == -1))
                {
                    SetOrder(child, next);
                    ++next;
                }
            }

            if (draggingObject != null)
            {
                int s = GetOrder(draggingObject);
                int i = 0;
                foreach (UIElement child in Children.OfType<UIElement>().OrderBy(GetOrder))
                {
                    if (i == s) ++i;
                    if (child == draggingObject) continue;
                    int current = GetOrder(child);
                    if (i != current)
                        SetOrder(child, i);
                    ++i;
                }
            }

            if (items != Children.Count)
            {
                items = Children.Count;
                itemWidth = rowHeight = 0;
                Size infSize = new Size(double.PositiveInfinity, double.PositiveInfinity);
                foreach (UIElement child in Children)
                {
                    child.Measure(infSize);
                    itemWidth = Math.Max(itemWidth, child.DesiredSize.Width);
                    rowHeight = Math.Max(rowHeight, child.DesiredSize.Height);
                }
            }

            columnWidth = itemWidth;

            if (items > 0)
            {
                columns = items;
                rows = 1;
                if ((columnWidth * columns) > availableSize.Width)
                {
                    columns = Math.Max(1, (int)(availableSize.Width / columnWidth));
                    rows = items / columns;  // number of full rows
                    if (items % columns > 0) ++rows;  // partial row
                }

                if ((columnWidth * columns) < availableSize.Width)
                    columnWidth = availableSize.Width / columns;  // span available width
            }

            int index = -1;
            foreach (UIElement child in Children.OfType<UIElement>().OrderBy(GetOrder))
            {
                ++index;
                if (child == draggingObject) continue;

                Rect pos = GetPosition(index);
                SetDesiredPosition(child, pos);
            }

            return new Size(columnWidth * columns, rowHeight * rows);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            foreach (UIElement child in Children.OfType<UIElement>().OrderBy(GetOrder))
            {
                Rect position = GetPosition(child);
                if (double.IsNaN(position.Top))
                    position = GetDesiredPosition(child);
                child.Arrange(position);
            }

            return new Size(columnWidth * columns, rowHeight * rows);
        }

        private Rect GetPosition(int index)
        {
            int col = index % columns;
            int row = index / columns;
            double x = columnWidth * col;
            double y = rowHeight * row;
            return new Rect(new Point(x, y), new Size(columnWidth, rowHeight));
        }

        private int GetIndex(Point position)
        {
            int col = Math.Min((int)(position.X / columnWidth), columns - 1);
            int row = Math.Min((int)(position.Y / rowHeight), rows - 1);
            return Math.Min((row * columns) + col, items - 1);
        }

        #endregion Measure and Arrange

        #region Attached Properties

        public static readonly DependencyProperty OrderProperty;
        public static readonly DependencyProperty PositionProperty;
        public static readonly DependencyProperty DesiredPositionProperty;

        static DragPanel()
        {
            PositionProperty = DependencyProperty.RegisterAttached("Position", typeof(Rect), typeof(DragPanel),
                new FrameworkPropertyMetadata(new Rect(double.NaN, double.NaN, double.NaN, double.NaN), FrameworkPropertyMetadataOptions.AffectsParentArrange));

            DesiredPositionProperty = DependencyProperty.RegisterAttached("DesiredPosition", typeof(Rect), typeof(DragPanel),
                new FrameworkPropertyMetadata(new Rect(double.NaN, double.NaN, double.NaN, double.NaN), OnDesiredPositionChanged));

            OrderProperty = DependencyProperty.RegisterAttached("Order", typeof(int), typeof(DragPanel),
                new FrameworkPropertyMetadata(-1, FrameworkPropertyMetadataOptions.AffectsParentMeasure));
        }

        public static int GetOrder(DependencyObject obj)
        {
            return (int)obj.GetValue(OrderProperty);
        }

        public static void SetOrder(DependencyObject obj, int value)
        {
            obj.SetValue(OrderProperty, value);
        }

        public static Rect GetPosition(DependencyObject obj)
        {
            return (Rect)obj.GetValue(PositionProperty);
        }

        public static void SetPosition(DependencyObject obj, Rect value)
        {
            obj.SetValue(PositionProperty, value);
        }

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