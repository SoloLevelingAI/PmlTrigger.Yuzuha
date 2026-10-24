"""Build the evidence-led video from checked-in narration, source, receipts and real WPF captures.

Requires Pillow and FFmpeg. No network, live AVEVA calls, or invented execution logs.
"""
import argparse, hashlib, json, math, subprocess, wave
from functools import lru_cache
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parents[1]
W, H = 1920, 1080
INK, MUTED, BLUE, BG = '#213047', '#64758d', '#365de6', '#f3f6fb'

@lru_cache(None)
def font(size, bold=False, mono=False):
    file = 'consola.ttf' if mono else ('msyhbd.ttc' if bold else 'msyh.ttc')
    return ImageFont.truetype(str(Path('C:/Windows/Fonts') / file), size)

def text(draw, xy, value, size=34, color=INK, bold=False, mono=False):
    draw.text(xy, value, font=font(size,bold,mono), fill=color)

def card(draw, box, fill='white', outline=None):
    draw.rounded_rectangle(box, radius=24, fill=fill, outline=outline, width=2)

def screenshot(image, path, box):
    picture = Image.open(path).convert('RGB')
    picture.thumbnail((box[2]-box[0],box[3]-box[1]),Image.Resampling.LANCZOS)
    x=box[0]+((box[2]-box[0])-picture.width)//2
    y=box[1]+((box[3]-box[1])-picture.height)//2
    image.paste(picture,(x,y))

def base(scene, index):
    image=Image.new('RGB',(W,H),BG); d=ImageDraw.Draw(image)
    card(d,(72,54,122,104),BLUE); text(d,(86,57),'P',33,'white',True)
    text(d,(142,60),'Phayton',30,INK,True)
    text(d,(72,145),scene['tag'],25,BLUE)
    text(d,(72,194),scene['title'],63,INK,True)
    text(d,(1670,63),f'{index+1:02d} / 10',25,MUTED,mono=True)
    return image

def scene_frame(scene,index,progress,subtitle,args,summary):
    image=base(scene,index); d=ImageDraw.Draw(image); kind=scene['id']
    if kind=='intro':
        text(d,(78,312),'AI 的理解与判断',38,INK,True)
        text(d,(78,375),'+ 工程软件的底层能力',38,INK,True)
        text(d,(78,490),'本机连接 · 可查资料 · 可核对结果',26,MUTED)
        screenshot(image,args.frames/'Light-normal.png',(620,300,1860,930))
    elif kind=='routes':
        for x,title,subtitle1,subtitle2 in [(72,'开发工具','编写 → 加载 → 调试','沉淀为可复用的 PML / 插件'),(998,'直接协作','读取 / 观察 → 判断 → 核查','结合数据与视图，分析问题和关系')]:
            card(d,(x,350,x+850,820)); text(d,(x+46,402),title,49,BLUE,True)
            text(d,(x+46,530),subtitle1,34,INK,True); text(d,(x+46,640),subtitle2,29,MUTED)
        text(d,(76,868),'共同基础：Phayton 连接 AI 与 AVEVA / PML',32,INK)
    elif kind=='write':
        card(d,(72,332,718,865)); text(d,(110,373),'演示任务',25,BLUE,True)
        for j,line in enumerate(['一个原生按钮','调用函数与对象','显示结果与版本','可以重新加载并验证']): text(d,(112,446+j*82),line,37,INK,True)
        card(d,(755,332,1848,865),'#172232')
        text(d,(798,367),'YuzuhaDevAdd.pmlfnc',29,'#9eb5ff',mono=True)
        source=(ROOT/'tests/ai-pml/development-probe/YuzuhaDevAdd.pmlfnc').read_text(encoding='utf-8')
        lines=[line for line in source.splitlines() if not line.lstrip().startswith('--')]
        lines+=['', '!!YuzuhaDevAdd(2,3)  →  REAL 5']
        for j,line in enumerate(lines[:min(len(lines),2+int(progress*8))]): text(d,(795,442+j*55),line,25,'#e3eafb',mono=True)
        text(d,(795,802),'函数 + 对象 + 窗体 + 验证宏',28,'#aabbce')
    elif kind=='load':
        labels=['核验会话','确认 PMLLIB','备份复制','REHASH','重载对象 / 窗体','读取并比对']
        active=min(5,int(progress*6))
        for j,label in enumerate(labels):
            row,col=divmod(j,3); x=72+col*607; y=355+row*238
            card(d,(x,y,x+570,y+190),BLUE if j==active else 'white')
            text(d,(x+30,y+25),f'0{j+1}',27,'#c9d5ff' if j==active else MUTED,mono=True)
            text(d,(x+30,y+91),label,36,'white' if j==active else INK,True)
        text(d,(78,870),'10 个实际步骤；命令发出前记录，失败或结果未知即停止。',29,MUTED)
    elif kind=='debug':
        card(d,(72,348,1848,510),'#fff0ec')
        text(d,(111,382),'execute-validation  /  已停止',30,'#a84032',True)
        text(d,(111,443),'(2,761) incompatible types (GADGET=MENU)',32,'#a84032',mono=True)
        card(d,(72,553,1848,848)); text(d,(112,592),'核对类型访问规则 → 修正 → 独立重跑验证',38,INK,True)
        text(d,(112,677),'!kind = !this.attribute(!name).objectType()',36,BLUE,mono=True)
        text(d,(112,768),'保留原失败记录；不把“命令已发送”当成“结果正确”。',30,MUTED)
    elif kind=='verify':
        for x,big,small in [(72,'10 / 10','步骤通过'),(680,'8 / 8','结果匹配'),(1288,'v1 → v2','对象与窗体更新')]:
            card(d,(x,352,x+560,580)); text(d,(x+38,384),big,65,BLUE,True); text(d,(x+38,493),small,31,MUTED)
        card(d,(72,621,1848,865),'#172232'); text(d,(112,660),'原生按钮点击后的历史回读',28,'#aabbce')
        text(d,(112,734),summary['button']['result'],50,'#b7eadb',mono=True)
    elif kind=='collaborate':
        for j,label in enumerate(['对象数据','视图与截图','本地资料']):
            card(d,(72,350+j*165,518,483+j*165)); text(d,(112,391+j*165),label,37,INK,True)
        card(d,(665,448,1235,744),BLUE); text(d,(715,494),'AI 理解与判断',45,'white',True)
        text(d,(715,602),'发现问题 · 形成假设',31,'#dce4ff')
        card(d,(1378,448,1848,744)); text(d,(1420,493),'通过 PML 核查',35,INK,True)
        text(d,(1420,598),'回读结果 · 修正判断',28,MUTED)
        text(d,(558,552),'→',61,BLUE); text(d,(1275,552),'→',61,BLUE)
        text(d,(78,890),'连接层提供入口和回执；具体判断依赖 AI、可用数据及平台能力。',28,MUTED)
    elif kind=='workspace':
        screenshot(image,args.ui/'Light'/'r2-knowledge-1600.png',(72,308,1190,921))
        text(d,(1245,365),'把工作过程留下',39,INK,True)
        for j,label in enumerate(['快速启动工程','查看连接与执行状态','检索资料与维护笔记','定位日志与脚本说明']): text(d,(1245,477+j*90),label,30,BLUE)
    elif kind=='users':
        for x,big,small in [(72,'5+','已知线上用户'),(998,'数十人','交流社群')]:
            card(d,(x,360,x+850,762)); text(d,(x+48,406),big,104,BLUE,True); text(d,(x+48,599),small,39,INK,True)
        text(d,(78,817),'线下熟人不计入；群成员数不等于活跃用户数。',30,MUTED)
        text(d,(78,877),'来源：维护者提供，未收集遥测或用户身份信息。',26,MUTED)
    else:
        card(d,(72,345,1848,855),'#172232'); text(d,(120,395),'开发工具  /  直接协作',61,'white',True)
        text(d,(120,515),'从连接开始，把每一步变得可核对。',39,'#b9c9e2')
        text(d,(120,661),'github.com/SoloLevelingAI/PmlTrigger.Yuzuha',34,'#a8bcff',mono=True)
        text(d,(120,754),'源码 · 功能文档 · 复刻夹具 · 验证记录',30,'#b9c9e2')
    d=ImageDraw.Draw(image)
    d.rectangle((0,969,W,H),fill='#172232')
    # One short narration clause at a time, never a page of tiny subtitles.
    bbox=d.textbbox((0,0),subtitle,font=font(32)); text(d,((W-(bbox[2]-bbox[0]))//2,993),subtitle,32,'white')
    d.rectangle((0,1068,int(W*(index+progress)/10),1080),fill=BLUE)
    return image

def srt_time(seconds):
    ms=round(seconds*1000); return f'{ms//3600000:02d}:{ms//60000%60:02d}:{ms//1000%60:02d},{ms%1000:03d}'

def main():
    parser=argparse.ArgumentParser(); parser.add_argument('--ffmpeg',required=True,type=Path)
    parser.add_argument('--frames',required=True,type=Path); parser.add_argument('--ui',required=True,type=Path)
    parser.add_argument('--audio',required=True,type=Path); parser.add_argument('--work',required=True,type=Path)
    args=parser.parse_args(); args.work.mkdir(parents=True,exist_ok=True)
    story=json.loads((ROOT/'docs/media/storyboard.json').read_text(encoding='utf-8'))
    summary=json.loads((ROOT/'docs/validation/2026-10-23-pml-development.summary.json').read_text(encoding='utf-8'))
    segments=[]; subtitles=[]; chapters=[]; elapsed=0; counter=1
    for index,scene in enumerate(story['scenes']):
        audio=args.audio/(scene['id']+'.wav')
        with wave.open(str(audio),'rb') as w: spoken=w.getnframes()/w.getframerate()
        duration=math.ceil((spoken+1.1)*24)/24
        frames=args.work/scene['id']; frames.mkdir(exist_ok=True)
        bounds=[]; point=0.35; total=sum(len(x) for x in scene['lines'])
        for line in scene['lines']:
            length=spoken*len(line)/total; bounds.append((point,point+length,line))
            subtitles.append(f'{counter}\n{srt_time(elapsed+point)} --> {srt_time(elapsed+point+length)}\n{line}\n'); counter+=1; point+=length
        for frame in range(math.ceil(duration*6)):
            t=frame/6; subtitle=next((line for a,b,line in bounds if a<=t<b),'')
            img=scene_frame(scene,index,min(t/duration,1),subtitle,args,summary)
            img.save(frames/f'{frame:04d}.png',compress_level=1)
            if frame==math.ceil(duration*3): img.save(args.work/(scene['id']+'-preview.jpg'),quality=92)
            if index==0 and frame==18: img.save(ROOT/'docs/media/poster.jpg',quality=94)
        segment=args.work/(scene['id']+'.mp4')
        command=[str(args.ffmpeg),'-y','-v','error','-framerate','6','-i',str(frames/'%04d.png'),'-i',str(audio),
          '-vf',f'fps=24,fade=t=in:st=0:d=0.3,fade=t=out:st={duration-0.3}:d=0.3',
          '-af','adelay=350|350,apad','-t',str(duration),'-c:v','libx264','-preset','fast','-crf','20','-pix_fmt','yuv420p',
          '-c:a','aac','-b:a','128k','-ar','48000','-ac','2','-movflags','+faststart',str(segment)]
        subprocess.run(command,check=True); segments.append(segment)
        chapters.append({'id':scene['id'],'title':scene['title'],'start':round(elapsed,3),'duration':duration,'sourceTag':scene['tag']})
        elapsed+=duration; print(f'{index+1}/10 {scene["id"]}: {duration:.2f}s',flush=True)
    concat=args.work/'concat.txt'; concat.write_text('\n'.join("file '"+str(p.resolve()).replace('\\','/').replace("'","'\\''")+"'" for p in segments),encoding='utf-8')
    output=ROOT/'docs/media/Phayton-Workflow-zh-CN.mp4'
    subprocess.run([str(args.ffmpeg),'-y','-v','error','-f','concat','-safe','0','-i',str(concat),'-c','copy','-movflags','+faststart',str(output)],check=True)
    (ROOT/'docs/media/Phayton-Workflow-zh-CN.srt').write_text('\n'.join(subtitles),encoding='utf-8')
    result={'width':W,'height':H,'fps':24,'duration':round(elapsed,3),'sha256':hashlib.sha256(output.read_bytes()).hexdigest(),'bytes':output.stat().st_size,'chapters':chapters,
      'audio':'Local Windows SAPI Huihui; no remote TTS','evidence':'New WPF captures + existing 2026-10-23 E3D 2.1 public receipts. No new AVEVA execution.'}
    (ROOT/'docs/media/video-info.json').write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    subprocess.run([str(args.ffmpeg),'-v','error','-i',str(output),'-f','null','-'],check=True)
    print(json.dumps(result,ensure_ascii=False),flush=True)

if __name__=='__main__': main()
