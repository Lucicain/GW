"""Shared helpers for the offline tests: read production source, cut members or
blocks out of it, and run a small C# program. Tests import these from here,
never from another test module."""
import importlib.util
from pathlib import Path
import re
import subprocess
import tempfile

ROOT = Path(__file__).resolve().parents[2]
MODULE = ROOT / 'GreyWardenPolicePurity'

_spec = importlib.util.spec_from_file_location('hygiene', ROOT / 'tools/Check-CodeHygiene.py')
hygiene = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(hygiene)


def read(name):
    return (MODULE / name).read_text(encoding='utf-8-sig')


def extract(source, name):
    """A block-bodied method declared with an access modifier."""
    mask = hygiene.code_mask(source)
    match = re.search(r"(?m)^[ \t]+(?:private|internal|public)[^\n]+\b" + name + r"\([^)]*\)\s*\{", source)
    if not match:
        raise AssertionError("Missing production method: " + name)
    end = hygiene.match_brace(source, mask, match.end() - 1) + 1
    return source[match.start():end]


def method(source, name):
    """A method or expression-bodied member at any indentation."""
    match = re.search(r'(?m)^\s*(?:private|internal|public)[^\n]*\b' + name + r'\s*\(', source)
    if not match:
        raise AssertionError(name)
    brace, arrow = source.find('{', match.end()), source.find('=>', match.end())
    if arrow >= 0 and (brace < 0 or arrow < brace):
        return source[match.start():source.index(';', arrow) + 1]
    return extract(source, name)


def class_method(source, name):
    """Like method, but only private/internal members at class-member indentation."""
    match = re.search(r'(?m)^        (?:private|internal)[^\n]*\b' + name + r'\s*\(', source)
    if not match:
        raise AssertionError(name)
    start = match.start()
    brace, arrow = source.find('{', match.end()), source.find('=>', match.end())
    if arrow >= 0 and (brace < 0 or arrow < brace):
        return source[start:source.index(';', arrow) + 1]
    return extract(source, name)


def expression_member(source, pattern):
    """An expression-bodied member (property or method) matching pattern."""
    match = re.search(r'(?ms)^\s*(?:private|internal|public)[^\n]*\b' + pattern + r'\b[^\n]*=>.*?;\s*$', source)
    if not match:
        raise AssertionError('Missing production member: ' + pattern)
    return match.group(0)


def members(text, *names):
    """Several class members joined; overrides lose override so they compile in a stub class."""
    result = []
    for name in names:
        match = re.search(r"(?m)^        (?:internal|private|public).*\b" + name + r"\(", text)
        assert match, name
        body = re.search(r"\{|=>", text[match.end():])
        pos = match.end() + body.start()
        end = (hygiene.match_brace(text, hygiene.code_mask(text), pos) + 1
               if text[pos] == "{" else text.index(";", pos) + 1)
        result.append(text[match.start():end].replace("public override", "public"))
    return "\n".join(result)


def extract_methods(file, names):
    """Block-bodied private/public class methods of one source file, joined."""
    source = read(file)
    mask = hygiene.code_mask(source)
    result = []
    for name in names:
        match = re.search(r"(?m)^        (?:private|public) [^\n]+\b" + name + r"\([^)]*\)\s*\{", source)
        assert match, name
        end = hygiene.match_brace(source, mask, match.end() - 1) + 1
        result.append(source[match.start():end].replace("public override void", "public void"))
    return "\n".join(result)


def block(source, header):
    """The braced block that starts at the first line matching header."""
    start = re.search(r'(?m)^\s*' + header, source)
    if not start:
        raise AssertionError('Missing block: ' + header)
    depth, i = 0, source.index('{', start.end())
    for j in range(i, len(source)):
        depth += {'{': 1, '}': -1}.get(source[j], 0)
        if depth == 0:
            return source[start.start():j + 1]
    raise AssertionError(header)


def type_block(text, name):
    """A nested class or struct declaration."""
    start = re.search(r"(?m)^    (?:internal|public).*\b(?:class|struct) " + name + r"\b", text).start()
    brace = text.index("{", start)
    return text[start:hygiene.match_brace(text, hygiene.code_mask(text), brace) + 1]


def run(code):
    """Compile and run a console program; raise with its output if it fails."""
    # Negative controls return a failing process without invoking Windows crash reporting.
    code = re.sub(r'static void Main\(\)\s*\{',
                  'static int Main(){try{Run();return 0;}catch(Exception e){Console.WriteLine(e.Message);return 1;}} static void Run(){', code)
    major = subprocess.check_output(['dotnet', '--version'], text=True).split('.')[0]
    with tempfile.TemporaryDirectory(prefix='gwp-city-flow-') as temp:
        p = Path(temp)
        (p / 'Test.csproj').write_text(f'<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net{major}.0</TargetFramework><Nullable>enable</Nullable><NoWarn>CS8618;CS0649;CS0414;CS8600;CS8601;CS8602;CS8603;CS8604</NoWarn><EnableNETAnalyzers>false</EnableNETAnalyzers></PropertyGroup></Project>', encoding='utf-8')
        (p / 'Program.cs').write_text(code, encoding='utf-8')
        result = subprocess.run(['dotnet', 'run', '--project', temp, '-c', 'Release', '-v', 'quiet'], capture_output=True, text=True)
        if result.returncode:
            raise AssertionError(result.stdout + result.stderr)
        print(result.stdout.strip())
