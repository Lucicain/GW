"""场景：`scenarios/<名字>.json` 里的命令序列加断言，一次调用跑完，返回通过/失败与证据。

格式见 README.md「场景」。断言路径用 `捕获名.键.键.下标`，例如 `after.sentinels.CaseSettle.triggers`。
"""

from __future__ import annotations

import json
import time
from typing import Any

from . import fences
from .config import Config

STEP_COMMANDS = ("status", "console", "advance", "query", "act", "load_save", "save")
OPS = ("eq", "ne", "gt", "ge", "lt", "le", "approx", "exists", "absent", "contains",
       "len_eq", "len_ge", "len_le", "truthy", "falsy")
_NO_OPERAND = {"exists", "absent", "truthy", "falsy"}
_TOP_KEYS = {"description", "save", "modules", "stop_after", "stop_on_failure", "steps"}
_DO_KEYS = {"do", "args", "as", "expect_error", "note"}
_ASSERT_KEYS = {"path", "op", "value", "ref", "tolerance", "message"}
_MISSING = object()


def validate(data: Any) -> list[str]:
    errors: list[str] = []
    if not isinstance(data, dict):
        return ["场景顶层必须是对象"]
    for key in data:
        if key not in _TOP_KEYS and not key.startswith("_"):
            errors.append(f"未知顶层键：{key}")
    if "save" in data:
        reason = fences.check_save_name(data["save"])
        if reason:
            errors.append(reason)
    if "modules" in data and not (isinstance(data["modules"], list) and all(isinstance(m, str) for m in data["modules"])):
        errors.append("modules 必须是字符串数组")
    for key in ("stop_after", "stop_on_failure"):
        if key in data and not isinstance(data[key], bool):
            errors.append(f"{key} 必须是布尔值")
    steps = data.get("steps")
    if not isinstance(steps, list) or not steps:
        errors.append("steps 必须是非空数组")
        return errors
    names: set[str] = set()
    for i, step in enumerate(steps):
        where = f"steps[{i}]"
        if not isinstance(step, dict):
            errors.append(f"{where} 必须是对象")
            continue
        if ("do" in step) == ("assert" in step):
            errors.append(f"{where} 必须恰好含 do 或 assert 之一")
            continue
        if "do" in step:
            for key in step:
                if key not in _DO_KEYS:
                    errors.append(f"{where} 未知键：{key}")
            if step["do"] not in STEP_COMMANDS:
                errors.append(f"{where}.do 必须是 {', '.join(STEP_COMMANDS)} 之一")
            if "args" in step and not isinstance(step["args"], dict):
                errors.append(f"{where}.args 必须是对象")
            if "as" in step:
                if not isinstance(step["as"], str) or not step["as"].isidentifier():
                    errors.append(f"{where}.as 必须是标识符")
                else:
                    names.add(step["as"])
            if "expect_error" in step and not isinstance(step["expect_error"], str):
                errors.append(f"{where}.expect_error 必须是错误码字符串")
        else:
            a = step["assert"]
            if not isinstance(a, dict):
                errors.append(f"{where}.assert 必须是对象")
                continue
            for key in a:
                if key not in _ASSERT_KEYS:
                    errors.append(f"{where}.assert 未知键：{key}")
            if a.get("op") not in OPS:
                errors.append(f"{where}.assert.op 必须是 {', '.join(OPS)} 之一")
            if not isinstance(a.get("path"), (str, list)):
                errors.append(f"{where}.assert.path 必须是字符串或数组")
            elif _root(a["path"]) not in names:
                errors.append(f"{where}.assert.path 引用了此前没有捕获的名字：{_root(a['path'])}")
            if a.get("op") not in _NO_OPERAND and ("value" in a) == ("ref" in a):
                errors.append(f"{where}.assert 必须恰好给 value 或 ref 之一")
            if "ref" in a and isinstance(a["ref"], (str, list)) and _root(a["ref"]) not in names:
                errors.append(f"{where}.assert.ref 引用了此前没有捕获的名字：{_root(a['ref'])}")
    return errors


def _segments(path: str | list) -> list:
    return list(path) if isinstance(path, list) else path.split(".")


def _root(path: str | list) -> str:
    segs = _segments(path)
    return str(segs[0]) if segs else ""


def resolve(captures: dict, path: str | list) -> Any:
    cur: Any = captures
    for seg in _segments(path):
        if isinstance(cur, dict):
            if seg not in cur:
                return _MISSING
            cur = cur[seg]
        elif isinstance(cur, list):
            try:
                idx = int(seg)
            except (TypeError, ValueError):
                # 依据：路径段写成了非数字却落在数组上，按「路径不存在」交给断言报告。
                return _MISSING
            if not -len(cur) <= idx < len(cur):
                return _MISSING
            cur = cur[idx]
        else:
            return _MISSING
    return cur


def _is_number(v: Any) -> bool:
    return isinstance(v, (int, float)) and not isinstance(v, bool)


def evaluate(op: str, actual: Any, expected: Any, tolerance: float) -> tuple[bool, str | None]:
    """返回 (是否通过, 不能比较时的原因)。"""
    if op == "exists":
        return actual is not _MISSING, None
    if op == "absent":
        return actual is _MISSING, None
    if actual is _MISSING:
        return False, "路径不存在"
    if op == "truthy":
        return bool(actual), None
    if op == "falsy":
        return not actual, None
    if op == "eq":
        return actual == expected, None
    if op == "ne":
        return actual != expected, None
    if op in ("gt", "ge", "lt", "le", "approx"):
        if not (_is_number(actual) and _is_number(expected)):
            return False, "只能比较数字"
        return {"gt": actual > expected, "ge": actual >= expected, "lt": actual < expected,
                "le": actual <= expected, "approx": abs(actual - expected) <= tolerance}[op], None
    if op == "contains":
        if isinstance(actual, (str, list, dict)):
            return expected in actual, None
        return False, "contains 只适用于字符串、数组、对象"
    if op in ("len_eq", "len_ge", "len_le"):
        if not isinstance(actual, (str, list, dict)) or not _is_number(expected):
            return False, "len_* 需要可求长度的值和数字"
        n = len(actual)
        return {"len_eq": n == expected, "len_ge": n >= expected, "len_le": n <= expected}[op], None
    return False, f"未知操作 {op}"


def _clip(value: Any, limit: int) -> Any:
    if value is _MISSING:
        return "<不存在>"
    text = json.dumps(value, ensure_ascii=False, default=str)
    if len(text) <= limit:
        return value
    return text[:limit] + f"…（截断，共 {len(text)} 字符）"


def load(cfg: Config, name: str) -> tuple[dict | None, dict | None]:
    reason = fences.check_scenario_name(name)
    if reason:
        return None, {"code": "refused", "message": reason}
    path = cfg.scenarios_dir / f"{name}.json"
    if not path.is_file():
        known = sorted(p.stem for p in cfg.scenarios_dir.glob("*.json"))
        return None, {"code": "not_found", "message": f"没有场景 {name}；现有：{', '.join(known) or '无'}"}
    try:
        data = json.loads(path.read_text(encoding="utf-8"))
    except json.JSONDecodeError as exc:
        return None, {"code": "invalid_scenario", "message": f"{path.name} 不是合法 JSON：{exc}"}
    errors = validate(data)
    if errors:
        return None, {"code": "invalid_scenario", "message": "；".join(errors)}
    return data, None


def list_scenarios(cfg: Config) -> list[dict]:
    out = []
    for path in sorted(cfg.scenarios_dir.glob("*.json")):
        try:
            desc = json.loads(path.read_text(encoding="utf-8")).get("description", "")
        except (json.JSONDecodeError, AttributeError):
            # 依据：列表只是索引，坏文件在 run_scenario 时才报详细错误。
            desc = "<无法解析>"
        out.append({"name": path.stem, "description": desc})
    return out


def _run_step(controller, cmd: str, args: dict) -> dict:
    if cmd == "status":
        return controller.bridge_status()
    if cmd == "console":
        return controller.console(args.get("command"))
    if cmd == "advance":
        return controller.advance(args.get("hours"))
    if cmd == "query":
        return controller.query(args.get("kind"), args.get("args"))
    if cmd == "act":
        return controller.act(args.get("kind"), args.get("args"))
    if cmd == "load_save":
        return controller.load_save(args.get("name"))
    return controller.save(args.get("name"), bool(args.get("overwrite", False)))


def run(controller, cfg: Config, name: str, keep_running: bool = False) -> dict:
    started = time.monotonic()
    data, error = load(cfg, name)
    if error:
        return {"ok": False, "passed": False, "scenario": name, "error": error}

    out: dict = {"ok": True, "passed": False, "scenario": name, "description": data.get("description", ""),
                 "steps": [], "failures": []}
    save = data.get("save")
    running = controller.is_running()
    if not running:
        if not save:
            return {**out, "ok": False, "stage": "start",
                    "error": {"code": "not_running", "message": "游戏没在运行，场景也没写 save，无法开始"}}
        res = controller.start(save, data.get("modules"))
        if not res["ok"]:
            return {**out, "ok": False, "stage": "start", "error": res.get("error"), "start": res}
        out["start"] = {"pid": res.get("pid"), "timings": res.get("timings"), "warnings": res.get("warnings")}
    elif save:
        res = controller.load_save(save)
        if not res["ok"]:
            return {**out, "ok": False, "stage": "load_save", "error": res.get("error"), "load": res}

    captures: dict = {}
    stop_on_failure = data.get("stop_on_failure", True)
    for i, step in enumerate(data["steps"]):
        t0 = time.monotonic()
        if "do" in step:
            cmd, args = step["do"], step.get("args", {})
            res = _run_step(controller, cmd, args)
            expect = step.get("expect_error")
            rec: dict = {"index": i, "do": cmd, "ok": res["ok"], "duration_s": round(time.monotonic() - t0, 3)}
            if step.get("note"):
                rec["note"] = step["note"]
            if res["ok"]:
                passed = expect is None
                value = res.get("result")
                if expect is not None:
                    rec["reason"] = f"期望错误 {expect}，命令却成功"
            else:
                code = (res.get("error") or {}).get("code")
                passed = expect is not None and code == expect
                value = res.get("error")
                rec["error"] = res.get("error")
                if res.get("killed") is not None:
                    rec["killed"] = res["killed"]
                    rec["evidence"] = res.get("evidence")
            if "as" in step:
                captures[step["as"]] = value
                rec["as"] = step["as"]
            rec["passed"] = passed
            out["steps"].append(rec)
            if not passed:
                out["failures"].append({"index": i, "do": cmd, "error": rec.get("error"), "reason": rec.get("reason")})
                if stop_on_failure or not controller.is_running():
                    break
        else:
            a = step["assert"]
            actual = resolve(captures, a["path"])
            expected = resolve(captures, a["ref"]) if "ref" in a else a.get("value")
            if expected is _MISSING:
                passed, why = False, "ref 路径不存在"
            else:
                passed, why = evaluate(a["op"], actual, expected, float(a.get("tolerance", 1e-6)))
            rec = {"index": i, "assert": a["op"], "path": a["path"], "passed": passed,
                   "actual": _clip(actual, cfg.evidence_chars)}
            if a["op"] not in _NO_OPERAND:
                rec["expected"] = _clip(expected, cfg.evidence_chars)
            if "ref" in a:
                rec["ref"] = a["ref"]
            if why:
                rec["reason"] = why
            if a.get("message"):
                rec["message"] = a["message"]
            out["steps"].append(rec)
            if not passed:
                out["failures"].append(rec)
                if stop_on_failure:
                    break

    out["passed"] = not out["failures"] and len(out["steps"]) == len(data["steps"])
    out["captures"] = {k: _clip(v, cfg.evidence_chars) for k, v in captures.items()}
    if data.get("stop_after", True) and not keep_running:
        out["stop"] = controller.stop()
    out["duration_s"] = round(time.monotonic() - started, 2)
    return out
