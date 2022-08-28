using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using System.Drawing;
using System.Diagnostics;
using System.Management;
using System.Drawing.Imaging;
using System.IO;
using NAudio.CoreAudioApi;

namespace USB_Volumemixer
{
    internal class AppVolumeManageer
    {
        static private string ProcessExecutablePath(Process process)
        {
            try
            {
                return process.MainModule.FileName;
            }
            catch
            {
                string path;
                var appResolver = (IApplicationResolver)new ApplicationResolver();
                appResolver.GetAppIDForProcess((uint)process.Id, out path, out _, out _, out _);
                Marshal.ReleaseComObject(appResolver);

                if (path != null)
                {
                    return path;
                }
            }

            return "";
        }


        public AppAudioInfo[] GetAudioSession(MMDevice device)
        {
            IconManage iconManage = new IconManage();
            var sessions = device.AudioSessionManager.Sessions;
            var sessionCount = sessions.Count;

            AppAudioInfo[] appInfo = new AppAudioInfo[sessionCount];
            for (var i = 0; i < sessionCount; i++)
            {
                var session = device.AudioSessionManager.Sessions[i];
                Process process = Process.GetProcessById((int)session.GetProcessID);

                appInfo[i] = new AppAudioInfo(session);
                string exePath = session.IconPath;

                // dllからアイコンを取得する
                string[] arr = exePath.Split(',');
                exePath = System.Environment.ExpandEnvironmentVariables(arr[0].Replace("@", ""));

                if (Path.GetExtension(exePath).ToLower() == ".dll")
                {
                    appInfo[i].bmp = iconManage.GetIconFromEXEDLL(exePath, Convert.ToInt32(arr[1]), true).ToBitmap();
                }
                else
                {
                    exePath = ProcessExecutablePath(process);
                    appInfo[i].iconPath = exePath;
                    appInfo[i].bmp = iconManage.GetIcon(exePath);
                }

                if (process.Id == 0)
                {
                    appInfo[i].appName = "System Sound";
                }
                else if (session.DisplayName != "")
                {
                    appInfo[i].appName = session.DisplayName;
                }
                else
                {
                    appInfo[i].appName = Path.GetFileName(exePath);
                }
            }

            return appInfo;
        }

        public byte[] IconToBinaty(AppAudioInfo appInfo)
        {
            return BitmapToByteArray(appInfo.bmp);
        }

        private byte[] BitmapToByteArray(Bitmap bmp)
        {
            Rectangle rect = new Rectangle(0, 0, bmp.Width, bmp.Height);
            System.Drawing.Imaging.BitmapData bmpData =
                bmp.LockBits(rect, System.Drawing.Imaging.ImageLockMode.ReadWrite,
                PixelFormat.Format16bppRgb565);

            // Bitmapの先頭アドレスを取得
            IntPtr ptr = bmpData.Scan0;

            // 32bppArgbフォーマットで値を格納
            int bytes = bmp.Width * bmp.Height * 2;
            byte[] rgbValues = new byte[bytes];

            // Bitmapをbyte[]へコピー
            Marshal.Copy(ptr, rgbValues, 0, bytes);

            bmp.UnlockBits(bmpData);
            return rgbValues;
        }
        
    }
}
