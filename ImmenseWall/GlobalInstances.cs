using pylorak.TinyWall.DatabaseClasses;
using System;
using System.Drawing;

namespace pylorak.TinyWall
{
    internal static class GlobalInstances
    {
        internal static AppDatabase? AppDatabase;
        internal static Controller? Controller;
        internal static Guid ClientChangeset;
        internal static Guid ServerChangeset;

        public static void InitClient()
        {
            Controller ??= new Controller("TinyWallController");
        }

        private static Bitmap? _applyBtnIcon;
        internal static Bitmap ApplyBtnIcon
        {
            get
            {
                _applyBtnIcon ??= Utils.ScaleImage(Resources.Icons.accept, Utils.DpiScalingFactor, Utils.DpiScalingFactor);

                return _applyBtnIcon;
            }
        }

        private static Bitmap? _cancelBtnIcon;
        internal static Bitmap CancelBtnIcon
        {
            get
            {
                _cancelBtnIcon ??= Utils.ScaleImage(Resources.Icons.cancel, Utils.DpiScalingFactor, Utils.DpiScalingFactor);

                return _cancelBtnIcon;
            }
        }

        private static Bitmap? _uninstallBtnIcon;
        internal static Bitmap UninstallBtnIcon
        {
            get
            {
                _uninstallBtnIcon ??= Utils.ScaleImage(Resources.Icons.uninstall, Utils.DpiScalingFactor, Utils.DpiScalingFactor);

                return _uninstallBtnIcon;
            }
        }

        private static Bitmap? _addBtnIcon;
        internal static Bitmap AddBtnIcon
        {
            get
            {
                _addBtnIcon ??= Utils.ScaleImage(Resources.Icons.add, Utils.DpiScalingFactor, Utils.DpiScalingFactor);

                return _addBtnIcon;
            }
        }

        private static Bitmap? _modifyBtnIcon;
        internal static Bitmap ModifyBtnIcon
        {
            get
            {
                _modifyBtnIcon ??= Utils.ScaleImage(Resources.Icons.modify, Utils.DpiScalingFactor, Utils.DpiScalingFactor);

                return _modifyBtnIcon;
            }
        }

        private static Bitmap? _removeBtnIcon;
        internal static Bitmap RemoveBtnIcon
        {
            get
            {
                _removeBtnIcon ??= Utils.ScaleImage(Resources.Icons.remove, Utils.DpiScalingFactor, Utils.DpiScalingFactor);

                return _removeBtnIcon;
            }
        }

        private static Bitmap? _submitBtnIcon;
        internal static Bitmap SubmitBtnIcon
        {
            get
            {
                _submitBtnIcon ??= Utils.ScaleImage(Resources.Icons.submit, Utils.DpiScalingFactor, Utils.DpiScalingFactor);

                return _submitBtnIcon;
            }
        }

        private static Bitmap? _importBtnIcon;
        internal static Bitmap ImportBtnIcon
        {
            get
            {
                _importBtnIcon ??= Utils.ScaleImage(Resources.Icons.import, Utils.DpiScalingFactor, Utils.DpiScalingFactor);

                return _importBtnIcon;
            }
        }

        private static Bitmap? _exportBtnIcon;
        internal static Bitmap ExportBtnIcon
        {
            get
            {
                _exportBtnIcon ??= Utils.ScaleImage(Resources.Icons.export, Utils.DpiScalingFactor, Utils.DpiScalingFactor);

                return _exportBtnIcon;
            }
        }

        private static Bitmap? _updateBtnIcon;
        internal static Bitmap UpdateBtnIcon
        {
            get
            {
                _updateBtnIcon ??= Utils.ScaleImage(Resources.Icons.update, Utils.DpiScalingFactor, Utils.DpiScalingFactor);

                return _updateBtnIcon;
            }
        }

        private static Bitmap? _webBtnIcon;
        internal static Bitmap WebBtnIcon
        {
            get
            {
                _webBtnIcon ??= Utils.ScaleImage(Resources.Icons.web, Utils.DpiScalingFactor, Utils.DpiScalingFactor);

                return _webBtnIcon;
            }
        }
    }
}
