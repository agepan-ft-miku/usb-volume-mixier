using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;
using System;
using System.Drawing;

namespace USB_Volumemixer
{
    public class AppAudioInfo : IAudioSessionEventsHandler
    {
        public string appName = "";
        public Bitmap bmp = null;
        private AudioSessionControl session;

        public class VolumeChangedEventArgs
        {
            public float volume;
            public bool isMuted;

            public VolumeChangedEventArgs(float volume, bool isMuted)
            {
                this.volume = volume;
                this.isMuted = isMuted;
            }
        }
        public event EventHandler SessionStateExpired;
        public event EventHandler<VolumeChangedEventArgs> VolumeChanged;

        public AppAudioInfo(AudioSessionControl session)
        {
            this.session = session;
            this.session.RegisterEventClient(this);
        }
        public string sessionId 
        {
            get { return this.session.GetSessionIdentifier; }
        }

        public void UnregisterSession()
        {
            this.session.UnRegisterEventClient(this);
            this.session.Dispose();
            this.session = null;
        }

        public int AppVol
        {
            get { return (int)(this.session.SimpleAudioVolume.Volume*100); }
            set
            {
                this.session.SimpleAudioVolume.Volume = (float)value / 100;
            }
        }

        public bool Mute
        {
            get { return this.session.SimpleAudioVolume.Mute; }
            set { this.session.SimpleAudioVolume.Mute = value; }
        }

        public void OnChannelVolumeChanged(uint channelCount, IntPtr newVolumes, uint channelIndex) { }
        public void OnDisplayNameChanged(string displayName) { }
        public void OnGroupingParamChanged(ref Guid groupingId) { }
        public void OnIconPathChanged(string iconPath) { }
        public void OnSessionDisconnected(AudioSessionDisconnectReason disconnectReason) { }
        public void OnStateChanged(AudioSessionState state)
        {
            switch (state)
            {
                case AudioSessionState.AudioSessionStateExpired:
                    if (SessionStateExpired != null)
                    {
                        SessionStateExpired(this, null);
                    }
                    break;
                default:
                    break;
            }
/*            Console.WriteLine(appName + " OnStateChanged: " + state);*/
        }
        public void OnVolumeChanged(float volume, bool isMuted)
        {
            if (VolumeChanged != null)
            {
                var e = new VolumeChangedEventArgs(volume, isMuted);
                VolumeChanged(this, e);
            }
/*            Console.WriteLine(appName + " OnVolumeChanged volume: " + (int)(volume*100) +" mute: "+ isMuted);*/
        }
    }
}
