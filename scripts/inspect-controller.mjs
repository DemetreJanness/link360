import fs from 'node:fs';
import crypto from 'node:crypto';
const target = process.argv[2];
if (!target) throw new Error('Supply an executable path');
const b = fs.readFileSync(target);
const pe = b.readUInt32LE(0x3c);
if (b.toString('ascii', pe, pe + 4) !== 'PE\0\0') throw new Error('Not PE');
const strings = [];
const text = b.toString('latin1');
const matcher = /[\x20-\x7e]{6,}/g;
for (const m of text.matchAll(matcher)) {
  if (/libusb|winusb|hid_|uvc|gimbal|ptz|extensionunit|IKsControl|DeviceIoControl|KsProperty|CameraControl|VideoProcAmp|tracking|SetZoom|SetPan|SetTilt/i.test(m[0]) && m[0].length < 500)
    strings.push({fileOffset: '0x' + m.index.toString(16), text: m[0]});
}
console.log(JSON.stringify({source:'Node static string inspection (not REA)',sha256:crypto.createHash('sha256').update(b).digest('hex'),machine:'0x'+b.readUInt16LE(pe+4).toString(16),matches:strings},null,2));
