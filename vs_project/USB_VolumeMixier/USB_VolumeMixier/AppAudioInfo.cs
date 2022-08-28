using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;
using System;
using System.Drawing;

namespace USB_Volumemixer
{
    public class AppAudioInfo
    {
        public string appName = "";
        public string iconPath = "";
        public Bitmap bmp = null;
        private int appVol = 0;
        private AudioSessionControl session;

        public AppAudioInfo(AudioSessionControl session)
        {
            this.session = session;
            this.appVol = (int)(session.SimpleAudioVolume.Volume * 100);
        }

        public int AppVol
        {
            get { return appVol; }
            set
            {
                appVol = value;
                this.session.SimpleAudioVolume.Volume = (float)appVol / 100;
            }
        }

        public bool Mute
        {
            get { return this.session.SimpleAudioVolume.Mute; }
            set { this.session.SimpleAudioVolume.Mute = value; }
        }
    }
}
