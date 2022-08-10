using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NAudio.CoreAudioApi;
using System.Drawing;
using NAudio.CoreAudioApi.Interfaces;

namespace USB_Volumemixer
{
    internal class AppAudioInfo
    {
        public string AppName = "";
        public string iconPath = "";
        public Bitmap bmp = null;
        private int appVol = 0;
        private AudioSessionControl thissession;

        public AppAudioInfo(AudioSessionControl session)
        {
            this.thissession = session;
            this.appVol = (int)(session.SimpleAudioVolume.Volume * 100);
        }

        public int AppVol
        {
            get { return appVol; }
            set
            {
                appVol = value;
                this.thissession.SimpleAudioVolume.Volume = (float)appVol / 100;
            }
        }

        public bool Mute
        {
            get { return this.thissession.SimpleAudioVolume.Mute; }
            set { this.thissession.SimpleAudioVolume.Mute = value; }
        }
    }
}
