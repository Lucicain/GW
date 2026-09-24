"""Export the Syndicate score as the original Wwise source clips plus the data to mix them at runtime.

Each source is the vgmstream decode of the original WEM (16-bit, 48 kHz, stereo), stored as FLAC
(lossless; export verifies ffmpeg decodes it back to the identical PCM). Segments are no longer
pre-rendered: GwpMusicScore places the clips on each segment's timeline and applies track volume
and the original clip automation (fade-in, fade-out, volume envelopes) while playing.

Requires ffmpeg. Run from the repository root; every changed runtime asset is mirrored to live.
"""
import argparse, hashlib, json, pathlib, shutil, subprocess

p = argparse.ArgumentParser()
p.add_argument('--lab', default=r'D:\lucigames\toys\syndicate-cards\app\music\data.json')
p.add_argument('--research', default=r'D:\lucigames\assets\syndicate\research\music-arrangement')
p.add_argument('--live', default=r'D:\steam\steamapps\common\Mount & Blade II Bannerlord\Modules\GreyWarden')
a = p.parse_args()
research = pathlib.Path(a.research)
root = pathlib.Path(__file__).resolve().parents[1]
out = root / 'GreyWardenPolicePurity/_Module/Music/Lab'
live = pathlib.Path(a.live) / 'Music/Lab'
out.mkdir(parents=True, exist_ok=True)
live.mkdir(parents=True, exist_ok=True)
data = json.loads(pathlib.Path(a.lab).read_text(encoding='utf-8-sig'))
audit = json.loads((research / 'clip-automation-audit.json').read_text(encoding='utf-8'))

keys = ['intro', 'low', 'medium', 'high', 'redraw', 'outro']
ids = {str(it['SegmentID']) for key in keys for it in data['playlists'][key]['items']}
ids.update(str(r['AkMusicTransitionObject']['segmentID']) for r in data['rules'] if r.get('AkMusicTransitionObject'))

AUTO_TYPES = {0: 'volume', 3: 'fadeIn', 4: 'fadeOut'}


def automations(track_id):
    """Parse wwiser's AkClipAutomation dump: points are (seconds from clip start, value, curve)."""
    result = []
    for fields in audit.get(track_id, []):
        head = {f['name']: int(f['value']) for f in fields if f['name'] in ('uClipIndex', 'eAutoType', 'uNumPoints')}
        values = [f for f in fields if f['name'] in ('From', 'To', 'Interp')]
        points = [[float(values[i]['value']), float(values[i + 1]['value']), int(values[i + 2]['value'])] for i in range(0, len(values), 3)]
        assert len(points) == head['uNumPoints'], track_id
        kind = AUTO_TYPES[head['eAutoType']]  # LPF/HPF automation would need a filter; none exists in this score
        result.append({'clip': head['uClipIndex'], 'type': kind, 'points': points})
    return result


def crc8(data):
    c = 0
    for b in data:
        c ^= b
        for _ in range(8):
            c = ((c << 1) ^ 0x07) & 0xFF if c & 0x80 else (c << 1) & 0xFF
    return c


def frame_offsets(blob, block, total):
    """Byte offset of every FLAC frame. Fixed block size, so frame k holds samples [k*block, ...)."""
    pos = 4
    while True:  # metadata blocks
        last, length = blob[pos] & 0x80, int.from_bytes(blob[pos + 1:pos + 4], 'big')
        pos += 4 + length
        if last:
            break
    offsets, count = [], -(-total // block)
    for k in range(count):
        # The next frame starts at a sync code whose header CRC-8 checks and whose number is k.
        while True:
            assert pos < len(blob), f'frame {k} not found'
            if blob[pos] == 0xFF and blob[pos + 1] == 0xF8:
                n, length = blob[pos + 4], 1
                if n >= 0xC0:
                    length = 2 if n < 0xE0 else 3
                    value = n & (0x1F if length == 2 else 0x0F)
                    for i in range(1, length):
                        value = (value << 6) | (blob[pos + 4 + i] & 0x3F)
                else:
                    value = n
                end = pos + 4 + length
                code = blob[pos + 2] >> 4
                end += 1 if code == 6 else 2 if code == 7 else 0
                if value == k and crc8(blob[pos:end]) == blob[end]:
                    break
            pos += 1
        offsets.append(pos)
        pos += 6  # smallest possible frame: 6-byte header + CRC; silent frames are only 14 bytes
    return offsets


segments, sources, manifest = {}, {}, {'format': 'FLAC, 16-bit, 48000 Hz, stereo (lossless copy of the vgmstream decode of the original WEM)', 'sources': {}}
for sid in sorted(ids):
    s = dict(data['segments'][sid])
    s.pop('file', None)
    s['frames'] = round(s['durationMs'] * 48)
    tracks = []
    for t in s['tracks']:
        t = dict(t)
        t['automation'] = automations(t['id'])
        assert all(x['clip'] < len(t['clips']) for x in t['automation']), t['id']
        tracks.append(t)
        for clip in t['clips']:
            sources.setdefault(str(clip['sourceID']), None)
    s['tracks'] = tracks
    segments[sid] = s

for src in sorted(sources):
    wav = research / 'decoded' / f'{src}.wav'
    flac = out / f'{src}.flac'
    subprocess.run(['ffmpeg', '-v', 'error', '-y', '-i', str(wav), '-map_metadata', '-1', '-fflags', '+bitexact',
                    '-c:a', 'flac', '-compression_level', '8', str(flac)], check=True)
    pcm = subprocess.run(['ffmpeg', '-v', 'error', '-i', str(wav), '-f', 's16le', '-'], check=True, capture_output=True).stdout
    back = subprocess.run(['ffmpeg', '-v', 'error', '-i', str(flac), '-f', 's16le', '-'], check=True, capture_output=True).stdout
    assert pcm == back, f'{src}: FLAC is not lossless'
    blob = flac.read_bytes()
    info = blob[8:8 + 34]  # STREAMINFO
    block = int.from_bytes(info[0:2], 'big')
    assert block == int.from_bytes(info[2:4], 'big'), f'{src}: variable block size'
    bits = int.from_bytes(info[10:18], 'big')
    rate, channels, depth, total = bits >> 44, ((bits >> 41) & 7) + 1, ((bits >> 36) & 31) + 1, bits & ((1 << 36) - 1)
    assert (rate, channels, depth, total) == (48000, 2, 16, len(pcm) // 4), f'{src}: {rate} {channels} {depth} {total}'
    sources[src] = {'file': flac.name, 'bytes': len(blob), 'frames': total, 'block': block, 'offsets': frame_offsets(blob, block, total)}
    # Paths relative to --research: no developer paths in the shipped module.
    manifest['sources'][src] = {'wav': wav.relative_to(research).as_posix(), 'wavSha256': hashlib.sha256(wav.read_bytes()).hexdigest(),
                                'pcmSha256': hashlib.sha256(pcm).hexdigest(), 'flacSha256': hashlib.sha256(blob).hexdigest(),
                                'frames': total}
    shutil.copy2(flac, live / flac.name)
    assert hashlib.sha256((live / flac.name).read_bytes()).hexdigest() == manifest['sources'][src]['flacSha256']

notes = [
    '以本机 Wwise 配置还原乐段调度；不是游戏内录音。',
    '源文件裁剪、时间轴、多轨静态音量与逐剪辑包络（淡入、淡出、音量自动化）来自 soundbank，运行时实时混音。',
    '包络与交叉淡化曲线按 Wwise 引擎插值公式计算；未仿真 Wwise 总线效果（Music 总线 −6 dB、主总线限制器、20 Hz 高通）。',
    '切换为保留完整 pre-entry 会等待足够提前量的下一同步点；极短间隔连续指令不保证与 Wwise 仲裁相同；源侧淡出偏移 iFadeOffset 未施加。',
]
result = {'segments': segments, 'sources': sources, 'playlists': {k: data['playlists'][k] for k in keys}, 'rules': data['rules'], 'notes': notes}
for name, value in [('score.json', result), ('manifest.json', manifest)]:
    dst = out / name
    dst.write_text(json.dumps(value, ensure_ascii=False, indent=1 if name == 'manifest.json' else None, separators=None if name == 'manifest.json' else (',', ':')), encoding='utf-8')
    shutil.copy2(dst, live / name)
for stale in list(out.glob('*.pcm')) + list(live.glob('*.pcm')) + [f for f in list(out.glob('*.flac')) + list(live.glob('*.flac')) if f.stem not in sources]:
    stale.unlink()
print(f'Exported {len(segments)} segments from {len(sources)} lossless sources: {sum(s["bytes"] for s in sources.values()) / 1024**2:.1f} MiB')
