using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows;

namespace BrucesBlocks
{
    /// <summary>Interface for block manager types.</summary>
    public interface IBlockManager
    {
        /// <summary>The set of block types supported by this manager.</summary>
        IBlock[] BlockTypes { get; }
        /// <summary>Creates a new block of the same type.</summary>
        IBlock NewBlock(IBlock block);
        /// <summary>Creates a new block group.</summary>
        IBlockGroup NewBlockGroup();
        /// <summary>Creates a new block that matches the line.</summary>
        IBlock NewBlock(string line);
        /// <summary>Creates an array of new blocks that match the lines.</summary>
        IBlock[] NewBlocks(string buffer);
        /// <summary>Produces a string buffer from an array of blocks.</summary>
        string ToString(IBlock[] blocks);

        /// <summary>Creates an array of new blocks from a text file.</summary>
        bool ReadFile(string path, out IBlock[] blocks);
        /// <summary>Writes a text file from an array of blocks.</summary>
        bool WriteFile(string path, IBlock[] blocks);
    }

    public class BlockManager : IBlockManager
    {
        public BlockManager()
        {
            // Add your block types:
            blockTypes.Add(new BlockBrush());
            blockTypes.Add(new BlockBulb());
            blockTypes.Add(new BlockCoffee());
            blockTypes.Add(new BlockGlasses());
            blockTypes.Add(new BlockPalette());
            blockTypes.Add(new BlockPencil());
            blockTypes.Add(new BlockGroup());
        }

        protected List<IBlock> blockTypes = new List<IBlock>();

        public IBlock[] BlockTypes { get { return blockTypes.ToArray(); } }

        public IBlock NewBlock(IBlock block) { return (IBlock)Activator.CreateInstance(block.GetType()); }

        public IBlockGroup NewBlockGroup() { return new BlockGroup(); }

        public IBlock NewBlock(string line)
        {
            if (!string.IsNullOrEmpty(line) && line != BlockGroup.EndMarker)
            {
                foreach (IBlock blockType in blockTypes)
                {
                    IBlock block = blockType.NewBlock(line);
                    if (block != null)
                        return block;
                }
            }

            return null;
        }

        public IBlock[] NewBlocks(string buffer)
        {
            List<IBlock> listBlocks = new List<IBlock>();
            IBlockGroup group = null;
            Stack<IBlockGroup> groupStack = new Stack<IBlockGroup>();
            string[] lines = buffer.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (string.IsNullOrEmpty(line)) continue;

                if (group != null && line == BlockGroup.EndMarker)
                {
                    groupStack.Pop();
                    group = (groupStack.Count > 0) ? groupStack.Peek() : null;
                    continue;
                }

                IBlock block = NewBlock(line);
                if (block != null)
                {
                    if (group != null)
                        group.Add(block);
                    else
                        listBlocks.Add(block);

                    if (block is IBlockGroup)
                    {
                        group = (IBlockGroup)block;
                        groupStack.Push(group);
                    }
                }
            }

            return listBlocks.ToArray();
        }

        public bool ReadFile(string path, out IBlock[] blocks)
        {
            blocks = null;
            if (!File.Exists(path)) return false;

            List<IBlock> listBlocks = new List<IBlock>();
            using (var sr = new StreamReader(path))
            {
                IBlockGroup group = null;
                Stack<IBlockGroup> groupStack = new Stack<IBlockGroup>();
                string line;
                while ((line = sr.ReadLine()) != null)
                {
                    line = line.Trim();
                    if (string.IsNullOrEmpty(line)) continue;

                    if (group != null && line == BlockGroup.EndMarker)
                    {
                        groupStack.Pop();
                        group = (groupStack.Count > 0) ? groupStack.Peek() : null;
                        continue;
                    }

                    IBlock block = NewBlock(line);
                    if (block != null)
                    {
                        if (group != null)
                            group.Add(block);
                        else
                            listBlocks.Add(block);

                        if (block is IBlockGroup)
                        {
                            group = (IBlockGroup)block;
                            groupStack.Push(group);
                        }
                    }
                }
            }

            if (listBlocks.Count == 0) return false;
            blocks = listBlocks.ToArray();
            return true;
        }

        public bool WriteFile(string path, IBlock[] blocks)
        {
            try
            {
                using (var sw = new StreamWriter(path))
                {
                    int indent = 0;
                    WriteLines(sw, blocks, ref indent);
                }
                return true;
            }
            catch (Exception exc)
            {
                string message = (exc.InnerException != null) ? exc.InnerException.Message : exc.Message;
                MessageBox.Show(string.Format("Failed to save: \"{0}\" {1}", path, message), "Block Editor", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        private void WriteLines(StreamWriter sw, IBlock[] blocks, ref int indent)
        {
            foreach (IBlock block in blocks)
            {
                for (int i = 0; i < indent; i++) sw.Write("  ");
                sw.WriteLine(block.ToString().Trim());

                if (block is IBlockGroup)
                {
                    ++indent;
                    IBlockGroup group = (IBlockGroup)block;
                    WriteLines(sw, group.Blocks, ref indent);
                    --indent;
                    for (int i = 0; i < indent; i++) sw.Write("  ");
                    sw.WriteLine(BlockGroup.EndMarker);
                }
            }
        }

        public string ToString(IBlock[] blocks)
        {
            var sb = new StringBuilder();
            int indent = 0;
            ToStringBuilder(sb, blocks, ref indent);
            return sb.ToString();
        }

        private void ToStringBuilder(StringBuilder sb, IBlock[] blocks, ref int indent)
        {
            foreach (IBlock block in blocks)
            {
                for (int i = 0; i < indent; i++) sb.Append("  ");
                sb.AppendLine(block.ToString().Trim());

                if (block is IBlockGroup)
                {
                    ++indent;
                    IBlockGroup group = (IBlockGroup)block;
                    ToStringBuilder(sb, group.Blocks, ref indent);
                    --indent;
                    for (int i = 0; i < indent; i++) sb.Append("  ");
                    sb.AppendLine(BlockGroup.EndMarker);
                }
            }
        }
    }
}
