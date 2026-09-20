"""Rasterize the simple repository-owned SVG geometry into a multi-size PNG ICO.
No external dependencies. The geometry/colors match assets/yuzuha.svg.
"""
import pathlib
import struct
import zlib

def segment(x,y,a,b):
    dx=b[0]-a[0];dy=b[1]-a[1]
    t=max(0,min(1,((x-a[0])*dx+(y-a[1])*dy)/(dx*dx+dy*dy)))
    return (x-a[0]-t*dx)**2+(y-a[1]-t*dy)**2<=12**2

def pixel(x,y):
    if (x-186)**2+(y-72)**2<=18**2:return (237,189,98,255)
    if any(segment(x,y,a,b) for a,b in [((70,72),(128,132)),((128,132),(186,72)),((128,132),(128,194))]):return (219,243,232,255)
    dx=max(64-x,0,x-192);dy=max(64-y,0,y-192)
    return (22,60,64,255) if dx*dx+dy*dy<=56**2 else (0,0,0,0)

def png(size):
    rows=[]
    for y in range(size):
        row=bytearray([0])
        for x in range(size):
            samples=[pixel((x+(sx+.5)/2)*256/size,(y+(sy+.5)/2)*256/size) for sy in range(2) for sx in range(2)]
            alpha=sum(p[3] for p in samples)
            row.extend([round(sum(p[c]*p[3] for p in samples)/alpha) if alpha else 0 for c in range(3)]+[round(alpha/4)])
        rows.append(row)
    def chunk(name,data):return struct.pack('>I',len(data))+name+data+struct.pack('>I',zlib.crc32(name+data)&0xffffffff)
    return b'\x89PNG\r\n\x1a\n'+chunk(b'IHDR',struct.pack('>IIBBBBB',size,size,8,6,0,0,0))+chunk(b'IDAT',zlib.compress(b''.join(rows),9))+chunk(b'IEND',b'')

def main():
    folder=pathlib.Path(__file__).resolve().parent/'assets'
    sizes=[16,24,32,48,64,128,256];images=[png(s) for s in sizes]
    offset=6+16*len(sizes);entries=[]
    for size,data in zip(sizes,images):
        entries.append(struct.pack('<BBBBHHII',size%256,size%256,0,0,1,32,len(data),offset));offset+=len(data)
    (folder/'yuzuha.ico').write_bytes(struct.pack('<HHH',0,1,len(sizes))+b''.join(entries)+b''.join(images))
    (folder/'yuzuha.png').write_bytes(images[-1])

if __name__=='__main__':main()
