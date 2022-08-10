#include <Arduino_GFX_Library.h>
#include "icon_def.h"
#include "Adafruit_TinyUSB.h"

// Definition for GPIO.
#define ENC_A		D0
#define ENC_B		D1
#define SW1			D2
#define SW2			D3
#define SW3			D4
/*
#define TFT_CS		5
#define TFT_DC		7
#define TFT_RST		6
*/
// #define TFT_BL 6
#define BACKGROUND BLACK

Arduino_DataBus *bus = new Arduino_HWSPI(TFT_DC, TFT_CS);	// General hardware SPI
Arduino_GC9A01 *gfx = new Arduino_GC9A01(bus, TFT_RST, 0 /* rotation */, true /* IPS */);	// GC9A01 IPS LCD 240x240

const int16_t icon_size = 32;
const int32_t icon_scale = 3;
const int16_t icon_pos_x = (gfx->width()-icon_size*icon_scale)/2-1;
const int16_t icon_pos_y = (gfx->height()-icon_size*icon_scale)/2-1;

/* アイコン描画処理 */
void draw16bitRGBBitmapNx(int16_t x, int16_t y, uint16_t *bitmap, int16_t w, int16_t h, int32_t scale)
{
    gfx->startWrite();
    for (int16_t j = 0; j < h*scale; j++)
    {
        for (int16_t i = 0; i < w*scale; i++)
        {
			gfx->drawPixel(x + i, y + j, bitmap[i/scale+j/scale*w]);
       }
    }
    gfx->endWrite();
}

/* データID定義 */
enum data_id {
	OFFSET_ID = 'a',
	DATA_VOL,		// （デバイス→PC）ボリューム値変化通知
	DATA_SW1,		//（デバイス→PC）スイッチ（1）押下通知
	DATA_SW2,		//（デバイス→PC）スイッチ（2）押下通知
	DATA_SW3,		//（デバイス→PC）スイッチ（3）押下通知
	DATA_INIT,		//（PC→デバイス）音量調節対象のアプリ名と音量を初期化
	DATA_ICON,		//（PC→デバイス）アイコン画像の送信
	DATA_READY,		//（PC→デバイス）PC制御アプリ起動
	DATA_MUTE,		// （PC→デバイス）アプリミュート
};

/* 通信パケット定義 */
enum com_packet {
	COM_DATA_ID,		// data_id
	COM_PACKET_NO,		// パケット番号
	COM_PACKET_NUM,		// 総パケット数
	COM_DATA_SIZE_LOW,	// データサイズ(下位バイト)
	COM_DATA_SIZE_HIGH, // データサイズ(上位バイト)
	COM_DATA_VAL,		// データ先頭
};

volatile bool is_mute = false;
volatile bool device_ready = false;
volatile uint16_t sta_pos = 0;
volatile uint16_t end_pos = 1;
String str_exe = "default";

/* USB HID関係 */
#define PACKET_SIZE 64
#define PAKET_DATA_SIZE (PACKET_SIZE-COM_DATA_VAL)

uint8_t const desc_hid_report[] =
{
	TUD_HID_REPORT_DESC_GENERIC_INOUT(PACKET_SIZE)
};

Adafruit_USBD_HID usb_hid(desc_hid_report, sizeof(desc_hid_report), HID_ITF_PROTOCOL_NONE, 2, true);

void TinyUSB_setup()
{
	usb_hid.setReportCallback(get_report_callback, set_report_callback);
	usb_hid.begin();

	while( !TinyUSBDevice.mounted() ) delay(1);
}

uint16_t get_report_callback (uint8_t report_id, hid_report_type_t report_type, uint8_t* buffer, uint16_t reqlen)
{
	/* 何もしない */
	(void) report_id;
	(void) report_type;
	(void) buffer;
	(void) reqlen;
	return 0;
}

void set_report_callback(uint8_t report_id, hid_report_type_t report_type, uint8_t const* buffer, uint16_t bufsize)
{
	(void) report_id;
	(void) report_type;
	if(buffer[COM_DATA_ID] == DATA_READY && device_ready) {
		// 制御アプリ起動
		uint8_t buf[PACKET_SIZE] = {0};
		buf[COM_DATA_ID] = DATA_INIT;
		usb_hid.sendReport(0, buf, PACKET_SIZE);
	} else if(buffer[COM_DATA_ID] == DATA_INIT) {
		// 音量調節対象のアプリ名と音量を初期化
		char str_buf[64] = {0};
		Set_ENC_count(buffer[COM_DATA_VAL]);
		is_mute = buffer[COM_DATA_VAL + 1];
		memcpy(str_buf, &buffer[COM_DATA_VAL + 2], PAKET_DATA_SIZE-1);
		str_exe = str_buf;
		sta_pos = 0;
		end_pos = 1;
	} else if(buffer[COM_DATA_ID] == DATA_ICON) {
		// アイコン画像の送信
		uint16_t data_offset = buffer[COM_PACKET_NO]*PAKET_DATA_SIZE;
		uint16_t data_size = (buffer[COM_DATA_SIZE_HIGH] << 8) | buffer[COM_DATA_SIZE_LOW];
		uint16_t data_rest = data_size - data_offset;

		memcpy(&rev_icon.recv_buf[data_offset], &buffer[COM_DATA_VAL], (data_rest > PAKET_DATA_SIZE) ? PAKET_DATA_SIZE : data_rest);

		if(buffer[COM_PACKET_NO] + 1 == buffer[COM_PACKET_NUM]) {
			draw16bitRGBBitmapNx(icon_pos_x, icon_pos_y, rev_icon.icon_buf, icon_size, icon_size, icon_scale);
		}
		device_ready = true;
	} else if(buffer[COM_DATA_ID] == DATA_MUTE) {
		// アプリミュート
		is_mute = buffer[COM_DATA_VAL];
	}
}

/* ロータリーエンコーダ関係 */
volatile bool pinA = false;
volatile bool pinB = false;
volatile byte current = 0;
volatile byte previous = 0;
volatile int16_t counter = 0;
volatile int16_t cw[] = {1, 3, 0, 2};
volatile int16_t ccw[] = {2, 0, 3, 1};

uint16_t Get_ENC_count()
{
	return counter;
}

void Set_ENC_count(int16_t c)
{
	if(c > 100) {
		counter = 100;
	}else if(c < 0) {
		counter = 0;
	} else {
		counter = c;
	}
}

void ENC_READ()
{
	//ロータリーエンコーダの現在値を読みだす
	pinA = digitalRead(ENC_A);
	pinB = digitalRead(ENC_B);

	//0～3の数字に変換
	current = pinA + pinB * 2;

	//currentの値とCW/CCWの値を比較。一致すればcounterを増減
	if (current == cw[previous]) counter++;
	if (current == ccw[previous]) counter--;

	if(counter > 100) {
		counter = 100;
	}
	if(counter < 0) {
		counter = 0;
	}

	if(counter != previous) {
		/* カウント値更新時に行う処理 */
		uint8_t buf[PACKET_SIZE] = {0};
		buf[COM_DATA_ID] = DATA_VOL;
		buf[COM_PACKET_NO] = 0;
		buf[COM_PACKET_NUM] = 0;
		buf[COM_DATA_SIZE_LOW] = 1;
		buf[COM_DATA_SIZE_HIGH] = 0;
		buf[COM_DATA_VAL] = counter;
		usb_hid.sendReport(0, buf, PACKET_SIZE);
	}
	previous = current;
}

/* ボタン関係 */
volatile unsigned long msTime1, msTime2, msTime3;
volatile uint8_t int_state_sw1, int_state_sw2, int_state_sw3;
#define SW_DELAY 100

void SW1_READ()
{
	int_state_sw1 = true;
}

void SW2_READ()
{
	int_state_sw2 = true;
}
void SW3_READ()
{
	int_state_sw3 = true;
}

void check_sw_state()
{
	if(int_state_sw1) {
		if((millis() - msTime1) > SW_DELAY) {	// SW_DELAY未満の割り込みは無視する
			msTime1 = millis();					// 割り込み時刻更新

			if(digitalRead(SW1) == 0) {
				uint8_t buf[PACKET_SIZE] = {0};
				buf[COM_DATA_ID] = DATA_SW1;
				buf[COM_PACKET_NO] = 0;
				buf[COM_PACKET_NUM] = 0;
				buf[COM_DATA_SIZE_LOW] = 1;
				buf[COM_DATA_SIZE_HIGH] = 0;
				buf[COM_DATA_VAL] = msTime1;

				usb_hid.sendReport(0, buf, PACKET_SIZE);
			}
		}
		int_state_sw1 = false;
	}

	if(int_state_sw2) {
		if((millis() - msTime2) > SW_DELAY) {
			msTime2 = millis();

			if(digitalRead(SW2) == 0) {
				uint8_t buf[PACKET_SIZE] = {0};
				buf[COM_DATA_ID] = DATA_SW2;
				buf[COM_PACKET_NO] = 0;
				buf[COM_PACKET_NUM] = 0;
				buf[COM_DATA_SIZE_LOW] = 1;
				buf[COM_DATA_SIZE_HIGH] = 0;
				buf[COM_DATA_VAL] = msTime2;
				
				usb_hid.sendReport(0, buf, PACKET_SIZE);
			}
		}
		int_state_sw2 = false;
	}

	if(int_state_sw3) {
		if((millis() - msTime3) > SW_DELAY) {
			msTime3 = millis();

			if(digitalRead(SW3) == 0) {
				uint8_t buf[PACKET_SIZE] = {0};
				buf[COM_DATA_ID] = DATA_SW3;
				buf[COM_PACKET_NO] = 0;
				buf[COM_PACKET_NUM] = 0;
				buf[COM_DATA_SIZE_LOW] = 1;
				buf[COM_DATA_SIZE_HIGH] = 0;
				buf[COM_DATA_VAL] = msTime3;
				
				usb_hid.sendReport(0, buf, PACKET_SIZE);
			}
		}
		int_state_sw3 = false;
	}
}

#define MUTE_VALUE -1

void print_volume_value(int16_t val, int16_t pos_y)
{
	static int16_t val_old = 0;
	int16_t x1 = 0;
	int16_t y1 = pos_y;
	uint16_t w = 0;
	uint16_t h = 0;
	String str_num;

	gfx->setTextSize(4);
	if(val == MUTE_VALUE) {
		str_num = "MUTE";
	} else {
		str_num = String(val);
	}
	gfx->getTextBounds(str_num, x1, y1, &x1, &y1, &w, &h);
	x1 = (gfx->width()-w)/2;
	gfx->setCursor(x1, y1);

	if(val != val_old) {
		gfx->fillRect(72, y1, 96, h, BACKGROUND);
	}
	gfx->println(str_num);

	val_old = val;
}

void setup(void)
{
	msTime1 = msTime2 = msTime3 = millis();
	int_state_sw1 = int_state_sw2 = int_state_sw3 = false;

	pinMode(ENC_A, INPUT_PULLUP);
	pinMode(ENC_B, INPUT_PULLUP);
	pinMode(SW1, INPUT_PULLUP);
	pinMode(SW2, INPUT_PULLUP);
	pinMode(SW3, INPUT_PULLUP);

	attachInterrupt(digitalPinToInterrupt(ENC_A), ENC_READ, CHANGE);
	attachInterrupt(digitalPinToInterrupt(ENC_B), ENC_READ, CHANGE);
	attachInterrupt(digitalPinToInterrupt(SW1), SW1_READ, FALLING);
	attachInterrupt(digitalPinToInterrupt(SW2), SW2_READ, FALLING);
	attachInterrupt(digitalPinToInterrupt(SW3), SW3_READ, FALLING);

	TinyUSB_setup();

    gfx->begin();
    gfx->fillScreen(BACKGROUND);

	uint8_t buf[PACKET_SIZE] = {0};
	buf[COM_DATA_ID] = DATA_INIT;
	
	while(!device_ready)
	{
		usb_hid.sendReport(0, buf, PACKET_SIZE);
		delay(100);
	}
}

void loop()
{ 
	/* ボリュームメータ表示 */
	static int16_t cnt = 0;

	cnt = Get_ENC_count();
	check_sw_state();

	uint16_t offset = 135;
	uint16_t offset_maxpos = 180 - offset;
	uint16_t offset_minpos = offset;
	uint16_t min = 0;
	uint16_t max = 270;
	uint16_t cur_pos = offset_minpos;

	cur_pos = map(cnt, 0, 100, min, max) + offset_minpos;
	if(cnt >= 360) {
		cur_pos -= 360;
	}
	if(cnt < 100) {
		gfx->fillArc(gfx->width()/2, gfx->height()/2, (gfx->width()-12)/2, 90+1, cur_pos+1, offset_maxpos-1, BACKGROUND);
	}
	if(cnt > 0) {
		gfx->fillArc(gfx->width()/2, gfx->height()/2, (gfx->width()-12)/2, 90+1, offset_minpos+1, cur_pos-1, GREENYELLOW);
	}
	gfx->drawArc(gfx->width()/2, gfx->height()/2, (gfx->width()-10)/2, 90, offset_minpos, offset_maxpos, WHITE);

	/* 対象アプリ名表示 */
	static int16_t text_x = 0;
	static int16_t text_y = 175;
	static uint16_t text_w = 0;
	static uint16_t text_h = 0;
	static int16_t mask_x = 0;
	static uint16_t mask_w = 0;
	static uint16_t mask_h = 0;
	const uint16_t show_len = 9;

	uint16_t str_len = str_exe.length();
	String show_str = str_exe.substring(sta_pos, end_pos);

	gfx->setTextSize(2);
	gfx->getTextBounds("@@@@@@@@", 0, text_y, &mask_x, &text_y, &mask_w, &mask_h);
	gfx->fillRect((gfx->width() - mask_w) / 2, text_y, mask_w, mask_h, BACKGROUND);

	gfx->getTextBounds(show_str, 0, text_y, &text_x, &text_y, &text_w, &text_h);

	if(end_pos == str_len) {
		text_x = (gfx->width() - mask_w) / 2;
	} else {
		text_x = (gfx->width() + mask_w) / 2 - text_w;
	}
	gfx->setCursor(text_x, text_y);
	gfx->println(show_str);

	end_pos++;
	if(end_pos > str_len) {
		end_pos = str_len;
	}
	
	if(str_len > show_len) {		
		if(end_pos >= show_len && end_pos <= str_len) {
			sta_pos++;
		}
	} else {
		if(end_pos >= str_len) {
			sta_pos++;
		}
	}

	if(sta_pos > str_len-1) {
		sta_pos = 0;
		end_pos = 1;
	}

	/* ボリューム値表示 */
	if(is_mute) {
		print_volume_value(MUTE_VALUE, 200);
	} else {
		print_volume_value(cnt, 200);
	}
}
