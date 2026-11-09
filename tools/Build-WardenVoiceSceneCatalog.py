"""Join the local Field Officer evidence to the proposed Bannerlord scenes."""

import argparse
from collections import Counter
import html
import json
from pathlib import Path
import re


ROOT = Path(__file__).resolve().parents[1]
SCENES = ROOT / "GreyWardenPolicePurity/docs/reference/warden-voice-scenes.md"
ACTION_EVENTS = {
    "Play_FemMason_Exertion",
    "Play_HighPriority_FemMason",
    "Play_LowPriority_FemMason_01",
}
NAME = re.compile(
    r"\b(?:masons?|agatha|agathians?|tenosia(?:ns?)?|malric|arg[oe]n|"
    r"galencourt|lionspire|coxwell|darkforest|falmire|rudhelm|aberfell|"
    r"thayic|azkandir|baudwyn|trayan|montcrux)\b", re.I
)
WORLD_SLOGAN = re.compile(r"\b(?:iron fist|for the order|red and black)\b", re.I)

# Audited text-level distinctions which would be lost by mapping only source events.
EXTRA = {
    "872213925": ("fight.last_stand",),
    "1002189163": ("fight.last_stand",),
    "731352577": ("act.recover_footing",),
    "977781715": ("act.recover_footing",),
}
PREFERRED = {
    "715615126": ("scene.arena_threat",),
    "577699237": ("scene.arena_threat",),
    "1056141462": ("scene.arena_formation",),
    "232985138": ("scene.arena_formation",),
    "171728278": ("scene.arena",),
}


def read_scenes():
    scenes = {}
    aliases = {}
    for line in SCENES.read_text(encoding="utf-8").splitlines():
        if not re.match(r"^\| [a-z]+\.[a-z_]+ \|", line):
            continue
        sid, kind, keys, speaker, state, timing, hook = [
            item.strip() for item in line.strip("|").split("|")
        ]
        if sid in scenes:
            raise ValueError(f"重复场景：{sid}")
        scenes[sid] = dict(kind=kind, speaker=speaker, state=state, timing=timing, hook=hook)
        for key in keys.split():
            if key != "—":
                aliases.setdefault(key, []).append(sid)
    return scenes, aliases


def read_transcript(path):
    raw = path.read_text(encoding="utf-8-sig")
    lines = re.findall(r"^(\d+)  (.*?)  \[(.*?)\]  (.*?)\r?$", raw, re.M)
    result = {mid: (filename, tags.split(" / "), caption) for mid, filename, tags, caption in lines}
    if len(lines) != len(result):
        raise ValueError("校对稿含重复媒体 ID")
    return result


def keys_for(event):
    name = event["name"]
    if name in ACTION_EVENTS:
        keys = sorted({f'S:{path[0]["value"]}' for path in event["switch_paths"]})
        if not keys:
            raise ValueError(f"动作事件缺少切换值：{name}")
        return keys
    if name == "Play_Mason_Defender_Hippodrome":
        return ["Hippodrome"]
    return [name.removeprefix("Play_VO_FemMason_")]


def build(source, transcript, audio):
    scenes, aliases = read_scenes()
    media = source["media"]
    if set(media) != set(transcript):
        raise ValueError("校对稿与音库索引的媒体 ID 不一致")
    expected = {(e["bank"], e["name"], mid) for e in source["events"] for mid in e["media_ids"]}
    seen = set()
    used_keys = set()
    records = []
    for mid, item in media.items():
        filename, tags, caption = transcript[mid]
        if filename != item["source_name"]:
            raise ValueError(f"录音名不符：{mid} {filename} != {item['source_name']}")
        path = (audio / item["wav"]).resolve()
        if not path.is_relative_to(audio) or not path.is_file():
            raise ValueError(f"找不到音频：{mid} {path}")
        routes = {}
        sources = []
        for e in item["events"]:
            seen.add((e["bank"], e["name"], mid))
            keys = keys_for(e)
            used_keys.update(keys)
            sources.append(f'{e["bank"]} · {e["name"]}' + (f" · {', '.join(keys)}" if e["name"] in ACTION_EVENTS else ""))
            for key in keys:
                for sid in aliases[key]:
                    routes.setdefault(sid, []).append(key)
        if mid in PREFERRED:
            routes.pop("scene.arena", None)
            for sid in PREFERRED[mid]:
                routes[sid] = ["逐录音：竞技过场台词已拆分"]
        for sid in EXTRA.get(mid, ()):
            routes[sid] = ["逐录音：措辞可用于此状态"]
        if re.fullmatch(r"FemMason_Death_Arm_0[12]\.wav", filename, re.I):
            routes["act.wound_arm"] = ["逐录音：仅喊手臂受创"]

        proper = "原作名词" in tags or bool(NAME.search(caption) or WORLD_SLOGAN.search(caption))
        role = "角色立场冲突" in tags
        review = "待复听" in tags
        if proper:
            routes["scene.historical_quote"] = ["原音：模拟阵营或有标识的引语"]
        if role:
            routes["scene.scripted_role"] = ["原音：换到能成立的剧情角色"]
        if not routes or any(sid not in scenes for sid in routes):
            raise ValueError(f"缺少有效场景：{mid}")
        if review:
            disposition = "先复听"
        elif proper or role:
            disposition = "原音限专场"
        elif all(scenes[sid]["kind"] == "专场" for sid in routes):
            disposition = "需专门场景"
        else:
            disposition = "常规候选"
        scene_rows = []
        for sid, source_keys in routes.items():
            if review:
                gate = "待复听确认；暂不进自动池"
            elif (proper or role) and sid not in {"scene.historical_quote", "scene.scripted_role", "scene.preview"}:
                gate = "普通灰袍场景需改词重录或用中性同池原音"
            else:
                gate = "候选；按字幕、说话立场和动作验收"
            scene_rows.append(dict(id=sid, from_keys=sorted(set(source_keys)), gate=gate, **scenes[sid]))
        records.append(dict(id=mid, file=filename, caption=caption, tags=tags, source=sources,
                            audio=path.as_uri(), disposition=disposition, scenes=scene_rows))
    if seen != expected or used_keys != set(aliases):
        raise ValueError(f"来源有缺口：{expected - seen}; 多余场景来源：{set(aliases) - used_keys}")
    return scenes, records, dict(
        media=len(records), pairs=len({(b, n) for b, n, _ in seen}), links=len(seen),
        switches=sum(key.startswith("S:") for key in used_keys),
        disposition=dict(Counter(r["disposition"] for r in records)),
        scenes=dict(Counter(s["kind"] for s in scenes.values())),
        recheck=sum("待复听" in r["tags"] for r in records),
    )


def render(scenes, records, stats):
    controls = "".join(
        f'<option value="{html.escape(sid)}">{html.escape(sid)} · {html.escape(s["kind"])} · {html.escape(s["speaker"])}</option>'
        for sid, s in scenes.items()
    )
    dispositions = "".join(f"<option>{html.escape(key)}</option>" for key in stats["disposition"])
    payload = json.dumps(records, ensure_ascii=False).replace("<", "\\u003c")
    totals = " · ".join(f"{k} {v}" for k, v in stats["disposition"].items())
    return f"""<!doctype html>
<html lang="zh-CN"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>灰袍语音：骑砍逐录音场景分配</title>
<style>
:root{{color-scheme:dark;--bg:#141513;--card:#20231f;--fg:#ede9db;--muted:#b5bbab;--accent:#dfbe7c}}
*{{box-sizing:border-box}}body{{background:var(--bg);color:var(--fg);font:16px/1.6 system-ui,sans-serif;max-width:1150px;margin:auto;padding:1.5rem}}
h1{{font-size:2rem;margin:.15rem 0;color:var(--accent)}}p{{margin:.5rem 0}}.bar{{display:flex;flex-wrap:wrap;gap:.6rem;margin:1rem 0}}
input,select,button{{font:inherit;border:1px solid #5d6558;border-radius:5px;background:#272d25;color:var(--fg);padding:.45rem;max-width:100%}}input{{flex:1;min-width:200px}}
details{{margin:.55rem 0;padding:.6rem .85rem;border:1px solid #41483c;border-radius:6px;background:var(--card)}}summary{{cursor:pointer}}
small,dt{{color:var(--muted)}}audio{{max-width:100%;display:block;margin:.7rem 0}}
section{{border-left:3px solid var(--accent);padding:.35rem .7rem;margin:.7rem 0}}dl{{margin:.25rem 0;display:grid;grid-template-columns:5.5em 1fr;gap:.12rem .5rem}}dd{{margin:0}}
</style>
<h1>灰袍语音：骑砍逐录音场景分配</h1>
<p>骑砍 1.4.8 的候选调度。每条列角色、动作和状态、出声时点与入口；没有装进游戏。</p>
<p>{stats['media']} 段原音 · {stats['pairs']} 组源事件 · {stats['switches']} 个动作切换值 · {len(scenes)} 个目标场景</p>
<p>{html.escape(totals)}。专名与立场不合的原音只能进指定演练、引语或剧情。待复听的机器稿不可直接作字幕。</p>
<div class="bar"><input id="q" aria-label="搜索" placeholder="搜台词、媒体 ID、动作或原作事件">
<select id="scene" aria-label="目标场景"><option value="">所有目标场景</option>{controls}</select>
<select id="gate" aria-label="录音状态"><option value="">所有录音状态</option>{dispositions}</select></div>
<p id="count" aria-live="polite"></p><main id="list"></main><button id="more">继续显示</button>
<script id="data" type="application/json">{payload}</script>
<script>
const entries=JSON.parse(document.getElementById("data").textContent);
const byId=id=>document.getElementById(id);
const el=(tag,text)=>{{const x=document.createElement(tag);if(text!==undefined)x.textContent=text;return x;}};
const searchable=new Map(entries.map(r=>[r.id,JSON.stringify(r).toLowerCase()]));
let filtered=[],shown=0;
function row(r){{
  const d=el("details");
  d.append(el("summary",r.caption+" · "+r.id+" · "+r.disposition));
  d.append(el("small",r.file+" · "+r.tags.join(" / ")));
  d.addEventListener("toggle",()=>{{
    if(!d.open||d.querySelector("audio"))return;
    const a=el("audio");a.controls=true;a.preload="none";a.src=r.audio;
    d.querySelector("small").after(a);
  }});
  const active=byId("scene").value;
  for(const s of r.scenes){{
    if(active&&s.id!==active)continue;
    const section=el("section");section.append(el("strong",s.id+" · "+s.kind));
    const dl=el("dl");
    for(const [k,v] of [["谁说",s.speaker],["动作和状态",s.state],["什么时候",s.timing],["接入点",s.hook],["素材依据",s.from_keys.join(" / ")],["准入",s.gate]])dl.append(el("dt",k),el("dd",v));
    section.append(dl);d.append(section);
  }}
  d.append(el("small","原作："+r.source.join("；")));
  return d;
}}
function more(){{
  const limit=Math.min(shown+60,filtered.length);
  byId("list").append(...filtered.slice(shown,limit).map(row));shown=limit;
  byId("more").hidden=shown>=filtered.length;
  byId("count").textContent="匹配 "+filtered.length+" 段；已显示 "+shown+" 段";
}}
function filter(){{
  const words=byId("q").value.trim().toLowerCase().split(/\\s+/).filter(Boolean);
  const scene=byId("scene").value,gate=byId("gate").value;
  filtered=entries.filter(r=>(!scene||r.scenes.some(s=>s.id===scene))&&(!gate||r.disposition===gate)&&words.every(w=>searchable.get(r.id).includes(w)));
  byId("list").replaceChildren();shown=0;more();
}}
byId("q").addEventListener("input",filter);
for(const id of ["scene","gate"])byId(id).addEventListener("change",filter);
byId("more").addEventListener("click",more);filter();
</script></html>
"""


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    for key in ("source", "transcript", "audio", "output"):
        parser.add_argument(key, type=Path)
    args = parser.parse_args()
    source = json.loads(args.source.read_text(encoding="utf-8-sig"))
    scenes, records, stats = build(source, read_transcript(args.transcript), args.audio.resolve())
    args.output.write_text(render(scenes, records, stats), encoding="utf-8")
    print(json.dumps(stats, ensure_ascii=False))


if __name__ == "__main__":
    main()
