using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using NAudio.CoreAudioApi;

namespace USB_Volumemixer
{
	public partial class Form1 : Form
	{
		/* USB HID 通信パケット定義 */
		enum data_id
		{
			OFFSET_ID = 'a',
			DATA_VOL,
			DATA_SW1,
			DATA_SW2,
			DATA_SW3,
			DATA_INIT,
			DATA_ICON,
			DATA_READY,
		};

		enum com_packet
		{
			COM_DATA_ID,
			COM_PACKET_NO,
			COM_PACKET_NUM,
			COM_DATA_SIZE_LOW,
			COM_DATA_SIZE_HIGH,
			COM_DATA_VAL,
		};

		const int PACKET_SIZE = 64;
		const int PAKET_DATA_SIZE = PACKET_SIZE - (int)com_packet.COM_DATA_VAL;

		const int VendorId = 0x2886;
		const int ProductId = 0x8042;
		HidLibrary.HidDevice usbdevice;
		int selectItem = 0;
		MMDeviceEnumerator devEnum;
		AppVolumeManageer volm;

		List<AppAudioInfo> appList = new List<AppAudioInfo>();

		public Form1()
		{
			InitializeComponent();

			statusLabel.Text = "Disconnected";
			timer1.Interval = 500;
			timer1.Start();

			devEnum = new MMDeviceEnumerator();
			volm = new AppVolumeManageer();
		}
		private void openFormToolStripMenuItem_Click(object sender, EventArgs e)
		{
			this.WindowState = FormWindowState.Normal;
		}

		private void timer1_Tick(object sender, EventArgs e)
		{
			TryDeviceConnect();
		}

		void TryDeviceConnect()
		{
			usbdevice = HidLibrary.HidDevices.Enumerate(VendorId, ProductId).FirstOrDefault();

			if (usbdevice != null)
			{
				usbdevice.Inserted += () => Invoke((MethodInvoker)(() =>
				{
					statusLabel.Text = "Connected";
					UpdateSessionList();

					if (usbdevice != null)
					{
						HidLibrary.HidReport rep = new HidLibrary.HidReport(PACKET_SIZE+1);
						rep.Data = BuildReadyPacket(appList[0]);
						usbdevice.WriteReport(rep);
					}
				}));

				usbdevice.Removed += () => Invoke((MethodInvoker)(() =>
				{
					statusLabel.Text = "Disconnected";
					timer1.Start();
				}));

				usbdevice.MonitorDeviceEvents = true;
				usbdevice.ReadReport(OnReport);
				usbdevice.OpenDevice();
				timer1.Stop();
			}
		}

		void OnReport(HidLibrary.HidReport report)
		{
			this.Invoke((MethodInvoker)(() =>
			{
				HidLibrary.HidReport rep = new HidLibrary.HidReport(PACKET_SIZE+1);

				switch ((data_id)report.Data[Convert.ToByte(com_packet.COM_DATA_ID)])
				{
					case data_id.DATA_VOL:
						appList[listBox1.SelectedIndex].AppVol = report.Data[(byte)com_packet.COM_DATA_VAL];
						break;

					case data_id.DATA_SW1:
						UpdateSessionList();
						if (1 > selectItem)
						{
							selectItem = listBox1.Items.Count - 1;
							listBox1.SelectedIndex = selectItem;
						}
						else
						{
							selectItem--;
							listBox1.SelectedIndex = selectItem;
						}
						rep.Data = BuildInitPacket(appList[selectItem]);
						usbdevice.WriteReport(rep);
						foreach (var item in BuiltIconPacket(appList[selectItem]))
						{
							rep.Data = item;
							usbdevice.WriteReport(rep);
						}
						break;

					case data_id.DATA_SW2:
						appList[selectItem].Mute = !appList[selectItem].Mute;
						rep.Data = BuildVolumePacket(appList[selectItem]);
						usbdevice.WriteReport(rep);
						break;

					case data_id.DATA_SW3:
						UpdateSessionList();

						if (listBox1.Items.Count-1 > selectItem)
						{
							selectItem++;
							listBox1.SelectedIndex = selectItem;
						}
						else
						{
							selectItem = 0;
							listBox1.SelectedIndex = selectItem;
						}
						rep.Data = BuildInitPacket(appList[selectItem]);
						usbdevice.WriteReport(rep);
						foreach (var item in BuiltIconPacket(appList[selectItem]))
						{
							rep.Data = item;
							usbdevice.WriteReport(rep);
						}

						break;

					case data_id.DATA_INIT:
						rep.Data = BuildInitPacket(appList[0]);
						listBox1.SelectedIndex = 0;

						usbdevice.WriteReport(rep);
						foreach (var item in BuiltIconPacket(appList[0]))
						{
							rep.Data = item;
							usbdevice.WriteReport(rep);
						}
						break;

					default:
						break;
				}
			}));
			if (usbdevice != null)
			{
				usbdevice.ReadReport(OnReport);
			}
		}

		private byte[] BuildInitPacket(AppAudioInfo appAudioInfo)
		{
			byte[] bytes = new byte[PACKET_SIZE+1];
			bytes[(byte)com_packet.COM_DATA_ID]				= Convert.ToByte(data_id.DATA_INIT);
			bytes[(byte)com_packet.COM_PACKET_NO]			= 0;
			bytes[(byte)com_packet.COM_PACKET_NUM]		= 1;
			bytes[(byte)com_packet.COM_DATA_SIZE_LOW]	= Convert.ToByte(PAKET_DATA_SIZE);
			bytes[(byte)com_packet.COM_DATA_SIZE_HIGH]	= 0;
			bytes[(byte)com_packet.COM_DATA_VAL]				= Convert.ToByte(appAudioInfo.AppVol);
			bytes[(byte)com_packet.COM_DATA_VAL+1]			= Convert.ToByte(appAudioInfo.Mute);

			byte[] appName = System.Text.Encoding.ASCII.GetBytes(appAudioInfo.appName);
			Array.Copy(appName, 0, bytes, (uint)com_packet.COM_DATA_VAL+2,
				((PAKET_DATA_SIZE-1) < appName.Length) ? (PAKET_DATA_SIZE-1) : appName.Length);

			return bytes;
		}

		private byte[] BuildVolumePacket(AppAudioInfo appAudioInfo)
		{
			byte[] bytes = new byte[PACKET_SIZE+1];
			bytes[(byte)com_packet.COM_DATA_ID]				= Convert.ToByte(data_id.DATA_VOL);
			bytes[(byte)com_packet.COM_PACKET_NO]			= 0;
			bytes[(byte)com_packet.COM_PACKET_NUM]		= 1;
			bytes[(byte)com_packet.COM_DATA_SIZE_LOW]	= 2;
			bytes[(byte)com_packet.COM_DATA_SIZE_HIGH]	= 0;
			bytes[(byte)com_packet.COM_DATA_VAL]				= Convert.ToByte(appAudioInfo.AppVol);
			bytes[(byte)com_packet.COM_DATA_VAL + 1]		= Convert.ToByte(appAudioInfo.Mute);

			return bytes;
		}

		private byte[] BuildReadyPacket(AppAudioInfo appAudioInfo)
		{
			byte[] bytes = new byte[PACKET_SIZE+1];
			bytes[(byte)com_packet.COM_DATA_ID]				= Convert.ToByte(data_id.DATA_READY);
			bytes[(byte)com_packet.COM_PACKET_NO]			= 0;
			bytes[(byte)com_packet.COM_PACKET_NUM]		= 1;
			bytes[(byte)com_packet.COM_DATA_SIZE_LOW]	= 1;
			bytes[(byte)com_packet.COM_DATA_SIZE_HIGH]	= 0;
			bytes[(byte)com_packet.COM_DATA_VAL]				= 0;

			return bytes;
		}

		private byte[][] BuiltIconPacket(AppAudioInfo appAudioInfo)
		{
			byte[] bitmap = volm.IconToBinaty(appAudioInfo);
			byte[][] data = new byte[bitmap.Length/PAKET_DATA_SIZE+1][];

			for (uint i = 0; i<data.Length; i++)
			{
				data[i] = new byte[PACKET_SIZE];
				data[i][(byte)com_packet.COM_DATA_ID]					= Convert.ToByte(data_id.DATA_ICON);
				data[i][(byte)com_packet.COM_PACKET_NO]				= Convert.ToByte(i);
				data[i][(byte)com_packet.COM_PACKET_NUM]			= Convert.ToByte(data.Length);
				data[i][(byte)com_packet.COM_DATA_SIZE_LOW]		= Convert.ToByte(bitmap.Length & 255);
				data[i][(byte)com_packet.COM_DATA_SIZE_HIGH]	= Convert.ToByte(bitmap.Length >> 8);
				data[i][(byte)com_packet.COM_DATA_VAL]				= Convert.ToByte(appAudioInfo.AppVol);

				Array.Copy(bitmap, i*PAKET_DATA_SIZE, data[i], (uint)com_packet.COM_DATA_VAL,
								(PAKET_DATA_SIZE < bitmap.Length-i*PAKET_DATA_SIZE) ? PAKET_DATA_SIZE : bitmap.Length-i*PAKET_DATA_SIZE);
			}

			return data;
		}

		private void UpdateSessionList()
		{
			var device = devEnum.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
			var sessions = device.AudioSessionManager.Sessions;

			for (var i = 0; i < sessions.Count; i++)
			{
				var session = sessions[i];
				if ( !appList.Any(m => m.sessionId == session.GetSessionIdentifier))
                {
					var appInfo = volm.ConvertSessionToInfo(session);
					appList.Add(appInfo);
					appInfo.SessionStateExpired += (sender, e) => {
						if (usbdevice != null && appList.IndexOf(appInfo) == selectItem)
						{
							HidLibrary.HidReport rep = new HidLibrary.HidReport(PACKET_SIZE+1);
							rep.Data = BuildReadyPacket(appList[0]);
							usbdevice.WriteReport(rep);
							selectItem = 0;
						}
						appList.Remove(appInfo);
					};
                    appInfo.VolumeChanged += (sender, e) =>
                    {
                        AppAudioInfo senderInfo = sender as AppAudioInfo;
                        if (usbdevice != null && appList.IndexOf(senderInfo) == selectItem)
                        {
                            HidLibrary.HidReport rep = new HidLibrary.HidReport(PACKET_SIZE+1);
                            rep.Data = BuildVolumePacket(appList[selectItem]);
                            usbdevice.WriteReport(rep);
                        }
                    };
                }
			}

			UpdateListBox();
		}

		private void UpdateListBox()
        {
			listBox1.Items.Clear();
			foreach (AppAudioInfo appAudioInfo in appList)
			{
				listBox1.Items.Add(appAudioInfo.appName);
			}
			if (listBox1.SelectedIndex == -1)
            {
				listBox1.SelectedIndex = 0;
			}
		}
	}
}
