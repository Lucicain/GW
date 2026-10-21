"""Read installed FMOD 0x8e bank routing/faders without loading or starting the game.

Layout reference: https://github.com/Masusder/FModBankParser (Apache-2.0),
FModReader, FRadixTreePacked, BusNode, FMixerStrip and EventNode.
Only metadata is read. This does not measure live snapshot activation or output.
"""
import argparse
import json
from pathlib import Path
import struct
import uuid


class Reader:
    def __init__(self, data):
        self.data, self.pos = data, 0

    def take(self, n):
        value = self.data[self.pos:self.pos + n]
        if len(value) != n:
            raise ValueError('Truncated FMOD metadata')
        self.pos += n
        return value

    def unpack(self, fmt):
        return struct.unpack('<' + fmt, self.take(struct.calcsize('<' + fmt)))

    def x16(self):
        value, = self.unpack('H')
        return (value & 0x7fff) | (self.unpack('H')[0] << 15) if value & 0x8000 else value

    def array(self, read):
        count = self.x16() >> 1
        if count:
            self.take(2)  # element width
        return [read() for _ in range(count)]

    def guid(self):
        return str(uuid.UUID(bytes_le=self.take(16)))

    def strip(self):
        self.take(2)  # versioned mixer-strip payload
        volume, pitch = self.unpack('ff')
        return dict(volume_db=volume, pitch=pitch, vcas=self.array(self.guid))


def chunks(data, start=12, end=None):
    end = len(data) if end is None else end
    while start + 8 <= end:
        if data[start] == 0:
            start += 1
        kind, size = struct.unpack_from('<4sI', data, start)
        stop = start + 8 + size
        if stop > end:
            raise ValueError(f'Invalid RIFF chunk at {start:x}')
        if kind == b'LIST':
            yield from chunks(data, start + 12, stop)
        else:
            yield kind, data[start + 8:stop]
        start = stop


def names(data):
    payload = next(value for kind, value in chunks(data) if kind == b'STDT')
    r = Reader(payload)
    table_type, = r.unpack('I')
    if table_type != 1:
        raise ValueError(f'Unsupported string table {table_type}')
    nodes = r.array(lambda: r.unpack('II'))
    guids = r.array(r.guid)
    blob = r.take(r.x16())
    leaves = [int.from_bytes(r.take(3), 'little') for _ in range(r.x16())]
    parents = [int.from_bytes(r.take(3), 'little') for _ in range(r.x16())]
    result = {}
    for guid, leaf in zip(guids, leaves):
        parts, seen = [], set()
        while leaf != 0xffffff:
            if leaf in seen:
                raise ValueError('Cyclic string tree')
            seen.add(leaf)
            offset = nodes[leaf][0] & 0xffffff
            if offset != 0xffffff:
                parts.append(blob[offset:blob.index(b'\0', offset)].decode())
            leaf = parents[leaf]
        result[guid] = ''.join(reversed(parts))
    return result


def inspect(bank, strings):
    data = bank.read_bytes()
    version = next(struct.unpack('<II', value) for kind, value in chunks(data) if kind == b'FMT ')
    if version != (0x8e, 0x8c):
        raise ValueError(f'Unverified bank version {version}')
    paths = names(strings.read_bytes())
    buses, vcas, events, snapshots, effects = {}, {}, {}, {}, {}
    current = None
    current_effect = None
    for kind, payload in chunks(data):
        r = Reader(payload)
        if kind in (b'GBSB', b'IBSB', b'RBSB', b'MBSB', b'PRTB'):
            current = r.guid()
            r.take(2)
            buses[current] = dict(kind=kind.decode(), parent=r.guid())
        elif kind == b'BUS ':
            flags, layout = r.unpack('II')
            pre, post = r.array(r.guid), r.array(r.guid)
            buses[current].update(r.strip(), pre_effects=pre, post_effects=post)
        elif kind == b'VCAB':
            key = r.guid()
            strips = r.array(r.guid)
            vcas[key] = dict(strips=strips, **r.strip())
        elif kind == b'EVTB':
            key = r.guid()
            snapshots_guid, timeline, bus, master_track = [r.guid() for _ in range(4)]
            events[key] = dict(snapshot=snapshots_guid, input_bus=bus, master_track=master_track)
        elif kind == b'SNAB':
            key = r.guid()
            priority, = r.unpack('i')
            entries = r.array(lambda: dict(property_id=r.unpack('I')[0], target=r.guid(), index=r.unpack('I')[0], value=r.unpack('f')[0]))
            snapshots[key] = dict(priority=priority, entries=entries)
        elif kind == b'BEFB':
            current_effect = r.guid()
            effects[current_effect] = dict(dsp_type=r.unpack('i')[0])
        elif kind == b'PMEF' and current_effect is not None:
            count, = r.unpack('i')
            parameters = []
            for _ in range(count):
                kind, = r.unpack('i')
                if kind in (0, 1):
                    value = r.unpack('f')[0]
                elif kind == 2:
                    value = bool(r.unpack('B')[0])
                elif kind == 3:
                    value = r.take(r.x16()).hex()
                else:
                    raise ValueError(f'Unsupported DSP parameter type {kind}')
                parameters.append(dict(type=kind, value=value))
            effects[current_effect]['parameters'] = parameters
        elif kind == b'EFFB' and current_effect is not None:
            flags, wet_mix, wet_level, dry_level, input_gain = r.unpack('Iffff')
            effects[current_effect]['mix'] = dict(flags=flags, wet_mix=wet_mix, wet_level=wet_level, dry_level=dry_level, input_gain=input_gain)
    for group in (buses, vcas, events, snapshots, effects):
        for key, value in group.items():
            value['path'] = paths.get(key, key)
    return dict(version=version, buses=buses, vcas=vcas, events=events, snapshots=snapshots, effects=effects)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('bank', type=Path)
    parser.add_argument('strings', type=Path)
    args = parser.parse_args()
    print(json.dumps(inspect(args.bank, args.strings), indent=2))
