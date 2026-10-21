"""Measure source-level loudness; apply the separately verified native music bus gain.

The PCM input is MusicTests --fingerprint --dump (float32 stereo, 48 kHz).
No game or audio device is opened. FFmpeg loudnorm reports EBU R128 input
loudness; output is discarded. The installed bank inspector verifies routing.
"""
import argparse
from concurrent.futures import ThreadPoolExecutor
import importlib.util
import json
from pathlib import Path
import statistics
import subprocess


def loudness(path, raw=False):
    args = ['ffmpeg', '-hide_banner', '-nostdin']
    if raw:
        args += ['-f', 'f32le', '-ar', '48000', '-ac', '2']
    args += ['-i', str(path), '-af', 'loudnorm=print_format=json', '-f', 'null', '-']
    result = subprocess.run(args, capture_output=True, text=True, check=True)
    start = result.stderr.rfind('{')
    values, _ = json.JSONDecoder().raw_decode(result.stderr[start:])
    return dict(file=path.name, integrated_lufs=float(values['input_i']), true_peak_dbfs=float(values['input_tp']))


def measure(game, pcm):
    spec = importlib.util.spec_from_file_location('fmod_mix', Path(__file__).parent / 'native/inspect-fmod-mix.py')
    mix = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mix)
    sounds = game / 'Modules/Native/Sounds/PC'
    master = mix.inspect(sounds / 'MasterBank.bank', sounds / 'MasterBank.strings.bank')
    music = mix.inspect(sounds / 'music.bank', sounds / 'MasterBank.strings.bank')
    buses = master['buses'] | music['buses']
    effects = master['effects'] | music['effects']
    event = next(v for v in music['events'].values() if v['path'] == 'event:/music')
    route, seen, gain = [], set(), 0.0
    key = event['input_bus']
    while key != '00000000-0000-0000-0000-000000000000':
        if key in seen:
            raise ValueError('Cyclic music route')
        seen.add(key)
        bus = buses[key]
        gain += bus['volume_db']
        row = dict(guid=key, path=bus['path'], fader_db=bus['volume_db'], effects=[])
        for effect_id in bus['pre_effects'] + bus['post_effects']:
            effect = effects[effect_id]
            if effect['dsp_type'] != 7:
                raise ValueError(f'Uncalibrated DSP type {effect["dsp_type"]}')
            value = effect['parameters'][0]['value']
            gain += value
            row['effects'].append(dict(guid=effect_id, type='FMOD_DSP_TYPE_FADER', gain_db=value))
        route.append(row)
        key = bus['parent']
    files = sorted(p for p in (game / 'Modules/Native/Music/PC').iterdir() if p.stem.startswith(('Battle_', 'Siege_')))
    with ThreadPoolExecutor(max_workers=6) as executor:
        native = list(executor.map(loudness, files))
    source = loudness(pcm, raw=True)
    reference = statistics.median(x['integrated_lufs'] for x in native)
    compensation = reference - source['integrated_lufs']
    return dict(method='EBU R128 integrated loudness, FFmpeg loudnorm input_i; median of installed Battle_/Siege_ source tracks',
                game=str(game), source=source, native_track_count=len(native), native_source_median_lufs=reference,
                native_music_static_gain_db=gain, content_compensation_db=round(compensation, 2),
                output_gain_db=round(compensation + gain, 2), reference_output_lufs=round(reference + gain, 2),
                music_route=route, native_tracks=native,
                limits='Static normal playback calibration; live cutscene, video, notification snapshots and subjective arrangement balance are not measured.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--game', type=Path, default=Path('D:/steam/steamapps/common/Mount & Blade II Bannerlord'))
    parser.add_argument('--score-pcm', required=True, type=Path)
    parser.add_argument('--output', type=Path)
    parser.add_argument('--output-pcm', type=Path, help='Optional actual ProcessSamples output from --output-dump')
    args = parser.parse_args()
    report = measure(args.game, args.score_pcm)
    if args.output_pcm:
        report['measured_output'] = loudness(args.output_pcm, raw=True)
        report['output_difference_lu'] = round(report['measured_output']['integrated_lufs'] - report['reference_output_lufs'], 2)
    text = json.dumps(report, ensure_ascii=False, indent=2) + '\n'
    if args.output:
        args.output.write_text(text, encoding='utf-8')
    print(json.dumps({k: v for k, v in report.items() if k not in ('native_tracks', 'music_route')}, ensure_ascii=False, indent=2))
