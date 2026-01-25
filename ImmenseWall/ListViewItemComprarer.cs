using System;
using System.Collections;
using System.Collections.Generic;
using System.Windows.Forms;

namespace pylorak.TinyWall
{
    // Implements the manual sorting of items by columns.
    internal class ListViewItemComparer : IComparer<ListViewItem>, IComparer
    {
        private readonly ImageList? _imageList;

        internal ListViewItemComparer(int column, ImageList? imageList = null, bool ascending = true)
        {
            Column = column;
            Ascending = ascending;
            _imageList = imageList;
        }

        public int Compare(ListViewItem x, ListViewItem y)
        {
            int order = Ascending ? +1 : -1;

            if (_imageList != null)
            {
                int deletedKey = _imageList.Images.IndexOfKey("deleted");
                if (x.ImageIndex != y.ImageIndex)
                {
                    if (x.ImageIndex == deletedKey)
                        return order * 1;
                    if (y.ImageIndex == deletedKey)
                        return order * -1;
                }
            }

            //try
            //{

            var index = Column;
            return order * string.Compare(x.SubItems[index].Text, y.SubItems[index].Text, StringComparison.CurrentCulture);
            //}
            //catch
            //{
            //    return order;
            //}
        }

        int IComparer.Compare(dynamic? x, dynamic? y)
        {
            if ((x is ListViewItem lx) && (y is ListViewItem ly))
                return Compare(lx, ly);

            throw new ArgumentException($"Both arguments must by of type {nameof(ListViewItem)}.");
        }

        internal int Column { get; }

        internal bool Ascending { get; set; }
    }
}
