using System;
using System.Runtime.InteropServices;
using System.Drawing;

namespace USB_Volumemixer
{
    internal class IconManage
    {
        // SHGetFileInfo関数
        [DllImport("shell32.dll")]
        private static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes, ref SHFILEINFO psfi, uint cbSizeFileInfo, uint uFlags);

        // SHGetFileInfo関数で使用するフラグ
        private const uint SHGFI_ICON = 0x100; // アイコン・リソースの取得
        private const uint SHGFI_LARGEICON = 0x0; // 大きいアイコン
        private const uint SHGFI_SMALLICON = 0x1; // 小さいアイコン

        // SHGetFileInfo関数で使用する構造体
        private struct SHFILEINFO
        {
            public IntPtr hIcon;
            public IntPtr iIcon;
            public uint dwAttributes;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string szDisplayName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
            public string szTypeName;
        };

        // ExtractIconEx関数  
        [DllImport("shell32.dll", EntryPoint = "ExtractIconEx", CharSet = CharSet.Auto)]
        private static extern int ExtractIconEx(
            [MarshalAs(UnmanagedType.LPTStr)] string file,
            int index,
            out IntPtr largeIconHandle,
            out IntPtr smallIconHandle,
            int icons
        );

        [DllImport("User32.dll")]
        private static extern bool DestroyIcon(IntPtr hIcon);

        public Bitmap GetIcon(string exePath)
        {
            // アプリケーション・アイコンを取得
            SHFILEINFO shinfo = new SHFILEINFO();
            IntPtr hSuccess = SHGetFileInfo(exePath, 0, ref shinfo, (uint)Marshal.SizeOf(shinfo), SHGFI_ICON | SHGFI_LARGEICON);
            if (hSuccess != IntPtr.Zero)
            {
                Icon appIcon = Icon.FromHandle(shinfo.hIcon);
                return appIcon.ToBitmap();
            }

            return null;
        }

        public Icon GetIconFromEXEDLL(string iconpath, int iconIndex, bool iconSize)
        {
            try
            {
                Icon[] icons = new Icon[2];
                IntPtr largeIconHandle = IntPtr.Zero;
                IntPtr smallIconHandle = IntPtr.Zero;

                int ret = ExtractIconEx(iconpath, iconIndex, out largeIconHandle, out smallIconHandle, 1);
                icons[0] = (Icon)Icon.FromHandle(largeIconHandle).Clone();
                icons[1] = (Icon)Icon.FromHandle(smallIconHandle).Clone();
                DestroyIcon(largeIconHandle);
                DestroyIcon(smallIconHandle);

                if (iconSize)
                {
                    return icons[0];
                }
                else
                {
                    return icons[1];
                }
            }
            catch (Exception) { }

            return null;
        }
    }
}
