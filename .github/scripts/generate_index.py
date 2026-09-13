#!/usr/bin/env python3
"""Generate INDEX.tsv: a greppable index of every public type in the library.

Reads the C# sources directly rather than the DocFX metadata under Docs/obj, so
types gated behind define symbols that docfx.json does not set (CINEMACHINE,
UNITY_ADVERTISEMENTS, FIREBASE_APP, FIREBASE_AUTH) still appear, tagged with the
symbol that gates them.

Usage:
    python .github/scripts/generate_index.py            # write INDEX.tsv
    python .github/scripts/generate_index.py --check    # exit 1 if stale
"""

import argparse
import json
import os
import re
import sys

REPO_ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
INDEX_PATH = os.path.join(REPO_ROOT, "INDEX.tsv")

EXCLUDED_DIRS = {"obj", "bin", "_site", "Library", "Temp"}

HEADER = ["Type", "Kind", "Base", "Namespace", "Path", "Gate", "Summary"]

NAMESPACE_RE = re.compile(r"^\s*namespace\s+([\w.]+)")

MODIFIERS = r"(?:public|internal|private|protected|static|sealed|abstract|partial|readonly|ref|new|unsafe)"

# A declaration may share its line with its attributes, as in
# `[Serializable] public class BreakEvent : UnityEvent<GameObject> { }`.
ATTRIBUTE_PREFIX = r"(?:\[[^\]]*\]\s*)*"

TYPE_RE = re.compile(
    r"^\s*" + ATTRIBUTE_PREFIX + r"(?P<mods>(?:" + MODIFIERS + r"\s+)*)"
    r"(?P<kind>class|struct|interface|enum|record)\s+"
    r"(?P<name>[A-Za-z_]\w*)"
    # A type parameter list holds only identifiers and commas. Matching anything
    # else here would swallow the base list in `class Foo<T> : Bar<T>`.
    r"(?P<generic><[\w\s,]*>)?"
    r"(?P<rest>.*)$"
)

DELEGATE_RE = re.compile(
    r"^\s*" + ATTRIBUTE_PREFIX
    + r"(?P<mods>(?:(?:public|internal|private|protected|static|unsafe)\s+)*)"
    r"delegate\s+[\w<>\[\],.?\s]+?\s+(?P<name>[A-Za-z_]\w*)"
    # The parameter list may start on the following line.
    r"\s*(?:<[\w\s,]*>)?\s*(?:\(|$)"
)

SUMMARY_RE = re.compile(r"<summary>(.*?)</summary>", re.DOTALL)
XML_TAG_RE = re.compile(r"<[^>]+>")
CREF_RE = re.compile(r'<(?:see|seealso)\s+cref="(?:[A-Za-z]:)?([^"]+)"\s*/?>')
ATTRIBUTE_RE = re.compile(r"^\[")

# Bases worth surfacing in their own column: they answer "how do I use this?"
# before a consumer opens the file.
UNITY_BASES = ("MonoBehaviour", "ScriptableObject", "NetworkBehaviour", "EditorWindow",
               "Editor", "PropertyDrawer", "StateMachineBehaviour", "Attribute")


def sanitize(line, state):
    """Strip comments and string/char literals so brace counting is reliable.

    Format strings such as "{0}" are common here, and counting their braces
    would corrupt the nesting depth used to track namespaces and nested types.
    `state` carries the in-block-comment flag between lines.
    """
    out = []
    i = 0
    n = len(line)
    while i < n:
        if state["block_comment"]:
            end = line.find("*/", i)
            if end == -1:
                return "".join(out)
            state["block_comment"] = False
            i = end + 2
            continue
        ch = line[i]
        nxt = line[i + 1] if i + 1 < n else ""
        if ch == "/" and nxt == "/":
            break
        if ch == "/" and nxt == "*":
            state["block_comment"] = True
            i += 2
            continue
        if ch == "@" and nxt == chr(34):
            i += 2
            while i < n:
                if line[i] == chr(34):
                    if i + 1 < n and line[i + 1] == chr(34):
                        i += 2
                        continue
                    i += 1
                    break
                i += 1
            continue
        if ch == chr(34) or ch == chr(39):
            quote = ch
            i += 1
            while i < n:
                if line[i] == "\\":
                    i += 2
                    continue
                if line[i] == quote:
                    i += 1
                    break
                i += 1
            continue
        out.append(ch)
        i += 1
    return "".join(out)


def clean_condition(text):
    """Normalise a preprocessor condition into a compact gate label."""
    text = text.split("//")[0].strip()
    text = text.replace("(", "").replace(")", "")
    return re.sub(r"\s+", " ", text).strip()


def extract_summary(doc_lines):
    """Reduce an XML doc block to a single short sentence."""
    if not doc_lines:
        return ""
    blob = "\n".join(doc_lines)
    match = SUMMARY_RE.search(blob)
    text = match.group(1) if match else blob
    text = CREF_RE.sub(r"\1", text)
    text = XML_TAG_RE.sub(" ", text)
    text = text.replace("&lt;", "<").replace("&gt;", ">").replace("&amp;", "&")
    text = re.sub(r"\s+", " ", text).strip()
    # First sentence only: the index is a pointer, not the documentation.
    cut = re.search(r"(?<=[.!?])\s", text)
    if cut:
        text = text[: cut.start()]
    text = text.strip().rstrip(".").strip()
    if len(text) > 140:
        text = text[:137].rstrip() + "..."
    return text


def pick_base(rest):
    """Return the most informative base type from a declaration's base list."""
    if ":" not in rest:
        return "-"
    tail = rest.split(":", 1)[1]
    tail = re.split(r"\bwhere\b|\{", tail)[0]
    # Split on commas that are not inside generic arguments.
    parts = []
    current = []
    depth = 0
    for ch in tail:
        if ch in "<[":
            depth += 1
        elif ch in ">]":
            depth -= 1
        if ch == "," and depth == 0:
            parts.append("".join(current))
            current = []
        else:
            current.append(ch)
    parts.append("".join(current))
    bases = [p.strip() for p in parts if p.strip()]
    if not bases:
        return "-"
    for base in bases:
        root = base.split("<")[0].split(".")[-1]
        if root in UNITY_BASES:
            return root
    return bases[0].split("<")[0].split(".")[-1]


def scan_file(path, rel_path):
    """Collect one row per public type declared in a single .cs file."""
    with open(path, "r", encoding="utf-8-sig", errors="replace") as handle:
        lines = handle.read().splitlines()

    rows = []
    state = {"block_comment": False}
    depth = 0
    namespaces = []   # (name, depth at declaration)
    types = []        # (name, depth at declaration)
    gates = []
    doc_lines = []

    for line in lines:
        stripped = line.strip()

        if stripped.startswith("///"):
            doc_lines.append(stripped[3:].strip())
            continue

        if stripped.startswith("#"):
            if stripped.startswith("#if"):
                gates.append(clean_condition(stripped[3:]))
            elif stripped.startswith("#elif"):
                if gates:
                    gates[-1] = clean_condition(stripped[5:])
            elif stripped.startswith("#else"):
                if gates:
                    top = gates[-1]
                    gates[-1] = top[1:] if top.startswith("!") else "!" + top
            elif stripped.startswith("#endif"):
                if gates:
                    gates.pop()
            continue

        code = sanitize(line, state)
        code_stripped = code.strip()
        if not code_stripped:
            continue

        namespace_match = NAMESPACE_RE.match(code)
        if namespace_match:
            namespaces.append((namespace_match.group(1), depth))
            depth += code.count("{") - code.count("}")
            continue

        kind = None
        match = TYPE_RE.match(code)
        if match:
            kind = "type"
        else:
            match = DELEGATE_RE.match(code)
            if match:
                kind = "delegate"

        if kind:
            # Keep the type parameter list in the name: TapestryEvent,
            # TapestryEvent<T> and TapestryEvent<T1,T2> are distinct types, and
            # a grep for "TapestryEvent" still matches all three.
            generic = re.sub(r"\s+", "", match.group("generic") or "") if kind == "type" else ""
            name = match.group("name") + generic
            if "public" in (match.group("mods") or "").split():
                outer = ".".join(outer_name for outer_name, _ in types)
                rows.append({
                    "Type": f"{outer}.{name}" if outer else name,
                    "Kind": match.group("kind") if kind == "type" else "delegate",
                    "Base": pick_base(match.group("rest")) if kind == "type" else "-",
                    # Namespaces nest: `namespace A { namespace B { … } }` means
                    # the type lives in A.B, not B.
                    "Namespace": ".".join(n for n, _ in namespaces) or "-",
                    "Path": rel_path,
                    "Gate": " && ".join(gate for gate in gates if gate) or "-",
                    "Summary": extract_summary(doc_lines) or "-",
                })
            if kind == "type" and match.group("kind") != "enum":
                types.append((name, depth))
            doc_lines = []
            depth += code.count("{") - code.count("}")
            continue

        if not ATTRIBUTE_RE.match(code_stripped):
            doc_lines = []

        depth += code.count("{") - code.count("}")

        while types and depth <= types[-1][1]:
            types.pop()
        while namespaces and depth <= namespaces[-1][1]:
            namespaces.pop()

    return rows


ASMDEF_CACHE = {}


def asmdef_constraints(directory):
    """The defineConstraints of the nearest enclosing .asmdef, walking up to the repo root.

    An assembly-level constraint gates every type in it just as surely as an #if does, but
    leaves no trace in the source file — so without this the Gate column would claim a
    constrained type is unconditional.
    """
    if directory in ASMDEF_CACHE:
        return ASMDEF_CACHE[directory]

    constraints = []
    try:
        entries = sorted(name for name in os.listdir(directory) if name.endswith(".asmdef"))
    except OSError:
        entries = []

    if entries:
        try:
            with open(os.path.join(directory, entries[0]), "r", encoding="utf-8-sig") as handle:
                constraints = [c for c in json.load(handle).get("defineConstraints", []) if c]
        except (OSError, ValueError):
            constraints = []
    elif os.path.normpath(directory) != os.path.normpath(REPO_ROOT):
        parent = os.path.dirname(directory)
        if parent and parent != directory:
            constraints = asmdef_constraints(parent)

    ASMDEF_CACHE[directory] = constraints
    return constraints


def apply_assembly_gates(rows, directory):
    """Fold the owning assembly's define constraints into each row's Gate."""
    constraints = asmdef_constraints(directory)
    if not constraints:
        return

    for row in rows:
        parts = list(constraints)
        if row["Gate"] != "-":
            parts.extend(part for part in row["Gate"].split(" && ") if part)

        seen = []
        for part in parts:
            if part not in seen:
                seen.append(part)
        row["Gate"] = " && ".join(seen) or "-"


def collect_rows():
    rows = []
    for dirpath, dirnames, filenames in os.walk(REPO_ROOT):
        dirnames[:] = [d for d in dirnames
                       if d not in EXCLUDED_DIRS and not d.startswith(".")]
        for filename in filenames:
            if not filename.endswith(".cs"):
                continue
            full = os.path.join(dirpath, filename)
            rel = os.path.relpath(full, REPO_ROOT).replace(os.sep, "/")
            file_rows = scan_file(full, rel)
            apply_assembly_gates(file_rows, dirpath)
            rows.extend(file_rows)
    rows.sort(key=lambda row: (row["Type"].lower(), row["Namespace"].lower(), row["Path"].lower()))
    return rows


def render(rows):
    out = ["#" + "\t".join(HEADER)]
    for row in rows:
        out.append("\t".join(row[column].replace("\t", " ") for column in HEADER))
    return "\n".join(out) + "\n"


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--check", action="store_true",
                        help="verify INDEX.tsv matches the sources; exit 1 if not")
    args = parser.parse_args()

    rows = collect_rows()
    content = render(rows)

    if args.check:
        existing = ""
        if os.path.exists(INDEX_PATH):
            with open(INDEX_PATH, "r", encoding="utf-8", newline="") as handle:
                existing = handle.read()
        if existing != content:
            sys.stderr.write("INDEX.tsv is out of date. Regenerate it with:\n"
                             "    python .github/scripts/generate_index.py\n")
            return 1
        sys.stderr.write(f"INDEX.tsv is up to date ({len(rows)} types).\n")
        return 0

    with open(INDEX_PATH, "w", encoding="utf-8", newline="\n") as handle:
        handle.write(content)

    gated = sum(1 for row in rows if row["Gate"] != "-")
    undocumented = sum(1 for row in rows if row["Summary"] == "-")
    sys.stderr.write(f"Wrote INDEX.tsv: {len(rows)} public types, "
                     f"{gated} gated, {undocumented} without a <summary>.\n")
    return 0


if __name__ == "__main__":
    sys.exit(main())
