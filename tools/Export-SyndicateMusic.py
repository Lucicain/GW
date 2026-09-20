"""Export the accepted local lab's decoded PCM and transition data, without mastering.
Requires ffmpeg. Run from repository root; every changed runtime asset is mirrored.
"""
import argparse, hashlib, json, pathlib, shutil, subprocess

p = argparse.ArgumentParser()
p.add_argument('--lab', default=r'D:\GwentSyndicate\辛迪加全卡档案\music-lab')
p.add_argument('--live', default=r'D:\steam\steamapps\common\Mount & Blade II Bannerlord\Modules\GreyWarden')
a = p.parse_args()
lab = pathlib.Path(a.lab)
root = pathlib.Path(__file__).resolve().parents[1]
out = root / 'GreyWardenPolicePurity/_Module/Music/Lab'
out.mkdir(parents=True, exist_ok=True)
data = json.loads((lab / 'data.json').read_text(encoding='utf-8-sig'))
keys = ['intro', 'low', 'medium', 'high', 'redraw', 'outro']
ids = {str(it['SegmentID']) for key in keys for it in data['playlists'][key]['items']}
ids.update(str(r['AkMusicTransitionObject']['segmentID']) for r in data['rules'] if r.get('AkMusicTransitionObject'))
manifest = {'format': 'stereo float32 little endian, 48000 Hz', 'gain': 1, 'segments': {}}
for sid in sorted(ids):
    s = data['segments'][sid]
    src, dst = lab / s['file'], out / (sid + '.pcm')
    subprocess.run(['ffmpeg', '-v', 'error', '-y', '-i', str(src), '-ar', '48000', '-ac', '2', '-c:a', 'pcm_f32le', '-f', 'f32le', str(dst)], check=True)
    s['file'] = dst.name
    s['frames'] = dst.stat().st_size // 8
    manifest['segments'][sid] = {'source': str(src), 'sourceSha256': hashlib.sha256(src.read_bytes()).hexdigest(), 'pcmSha256': hashlib.sha256(dst.read_bytes()).hexdigest(), 'frames': s['frames']}
    target = pathlib.Path(a.live) / 'Music/Lab' / dst.name
    target.parent.mkdir(parents=True, exist_ok=True)
    shutil.copy2(dst, target)
    assert hashlib.sha256(target.read_bytes()).hexdigest() == manifest['segments'][sid]['pcmSha256']
result = {'segments': {sid: data['segments'][sid] for sid in sorted(ids)}, 'playlists': {k: data['playlists'][k] for k in keys}, 'rules': data['rules'], 'notes': data['notes']}
for name, value in [('score.json', result), ('manifest.json', manifest)]:
    dst = out / name
    dst.write_text(json.dumps(value, ensure_ascii=False, indent=2), encoding='utf-8')
    shutil.copy2(dst, pathlib.Path(a.live) / 'Music/Lab' / name)
print(f'Exported and hash-verified {len(ids)} neutral PCM segments: {sum((out / (sid+".pcm")).stat().st_size for sid in ids)/1024**2:.1f} MiB')
