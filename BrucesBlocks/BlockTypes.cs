using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace BrucesBlocks
{
    /// <summary>Base interface for all block types.</summary>
    public interface IBlock
    {
        /// <summary>Parent block group, or null for top-level blocks.</summary>
        IBlockGroup BlockGroup { get; set; }
        /// <summary>Each block maintains its own block and code listbox items.</summary>
        ListBoxItem BlockItem { get; }
        /// <summary>Each block maintains its own block and code listbox items.</summary>
        ListBoxItem CodeItem { get; }
        /// <summary>Creates a new block from a line of gcode, or null if no match.</summary>
        IBlock NewBlock(string line);
    }

    /// <summary>Interface for block types that maintain a group of child blocks.</summary>
    public interface IBlockGroup : IBlock
    {
        /// <summary>The group of child blocks.</summary>
        IBlock[] Blocks { get; set; }
        /// <summary>Adds a block to the group.</summary>
        void Add(IBlock block);
    }

    public abstract class BlockBase
    {
        public BlockBase()
        {
            control = new BlockControl();

            blockItem = new ListBoxItem();
            blockItem.Tag = this;
            blockItem.FocusVisualStyle = null;
            blockItem.Content = control;

            codeItem = new ListBoxItem();
            codeItem.ContentStringFormat = " {0}";
            codeItem.Tag = this;
            codeItem.FocusVisualStyle = null;
        }

        public IBlockGroup BlockGroup { get; set; }

        protected BlockControl control;
        protected ListBoxItem blockItem;
        protected ListBoxItem codeItem;

        public ListBoxItem BlockItem { get { return blockItem; } }

        public virtual ListBoxItem CodeItem
        {
            get
            {
                codeItem.Content = ToString();
                return codeItem;
            }
        }

        public void Configure(ImageSource icon, string label1, string label2, string label3 = null)
        {
            control.image.Source = icon;
            control.tb1.Text = label1;
            control.tb2.Text = label2;

            if (label3 == null)
            {
                control.tb3.Visibility = Visibility.Collapsed;
            }
            else
            {
                control.tb3.Text = label3;
                control.tb3.Visibility = Visibility.Visible;
            }
        }

        protected static BitmapImage GetImage(string name)
        {
            string uri = string.Format("/BrucesBlocks;component/Images/{0}.png", name).Replace(" ", "%20");
            return new BitmapImage(new Uri(uri, UriKind.Relative));
        }
    }

    public abstract class SimpleBlock : BlockBase, IBlock
    {
        protected SimpleBlock(string name, string label1, string label2)
        {
            this.name = name;
            this.label1 = label1;
            this.label2 = label2;
            Configure(GetImage("block" + name), name, label1, label2);
        }

        private readonly string name;
        private readonly string label1;
        private readonly string label2;

        public override string ToString() { return name; }

        public IBlock NewBlock(string line)
        {
            if (line != name)
                return null;

            return (IBlock)Activator.CreateInstance(GetType());
        }
    }

    public class BlockBrush : SimpleBlock
    {
        public BlockBrush() : base("Brush", "for", "painting") { }
    }

    public class BlockBulb : SimpleBlock
    {
        public BlockBulb() : base("Bulb", "for", "light") { }
    }

    public class BlockCoffee : SimpleBlock
    {
        public BlockCoffee() : base("Coffee", "for", "focus") { }
    }

    public class BlockGlasses : SimpleBlock
    {
        public BlockGlasses() : base("Glasses", "for", "seeing") { }
    }

    public class BlockPalette : SimpleBlock
    {
        public BlockPalette() : base("Palette", "for", "colors") { }
    }

    public class BlockPencil : SimpleBlock
    {
        public BlockPencil() : base("Pencil", "for", "sketching") { }
    }

    public class BlockGroup : BlockBase, IBlockGroup
    {
        public BlockGroup()
        {
            Configure(GetImage("group"), "Block", "Group", "(0 blocks)");
        }

        private List<IBlock> blocks = new List<IBlock>();

        public IBlock[] Blocks
        {
            get { return blocks.ToArray(); }
            set
            {
                blocks.Clear();
                blocks.AddRange(value);
                foreach (IBlock block in blocks)
                    block.BlockGroup = this;
                control.tb3.Text = string.Format("({0} {1})", blocks.Count, "blocks");
            }
        }

        public void Add(IBlock block)
        {
            blocks.Add(block);
            block.BlockGroup = this;
            control.tb3.Text = string.Format("({0} {1})", blocks.Count, "blocks");
        }

        public override ListBoxItem CodeItem { get { return null; } }  // block group start and end markers are not shown in the code pane

        public static readonly string StartMarker = "Group..";
        public static readonly string EndMarker = "..Group";

        public override string ToString()  // block group start marker
        {
            return StartMarker;
        }

        public IBlock NewBlock(string line)
        {
            if (line != StartMarker) return null;

            return new BlockGroup();
        }
    }
}
