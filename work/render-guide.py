"""Render the small, local player guide without third-party dependencies."""
from pathlib import Path
import html
import re

root = Path(__file__).resolve().parent.parent
lines = (root / "distribution/LEEME.md").read_text(encoding="utf-8").splitlines()
def inline(text):
    text = html.escape(text)
    return re.sub(r"\*\*(.+?)\*\*", r"<strong>\1</strong>", text)

body = []
list_kind = ""
in_table = False
for line in lines + [""]:
    kind = "ul" if line.startswith("- ") else "ol" if re.match(r"\d+\. ", line) else ""
    if list_kind and kind != list_kind:
        body.append(f"</{list_kind}>")
        list_kind = ""
    if in_table and not line.startswith("|"):
        body.append("</tbody></table>")
        in_table = False
    if not line:
        continue
    if line.startswith("|"):
        if re.match(r"\|[-| ]+\|$", line):
            continue
        cells = [cell.strip() for cell in line.strip("|").split("|")]
        if not in_table:
            body.append("<table><thead><tr>" + "".join(f"<th>{inline(c)}</th>" for c in cells) + "</tr></thead><tbody>")
            in_table = True
        else:
            body.append("<tr>" + "".join(f"<td>{inline(c)}</td>" for c in cells) + "</tr>")
    elif kind:
        if not list_kind:
            body.append(f"<{kind}>")
            list_kind = kind
        body.append("<li>" + inline(re.sub(r"^(?:- |\d+\. )", "", line)) + "</li>")
    elif line.startswith("## "):
        body.append("<h2>" + inline(line[3:]) + "</h2>")
    elif line.startswith("# "):
        body.append("<h1>" + inline(line[2:]) + "</h1>")
    else:
        body.append("<p>" + inline(line) + "</p>")
page = """<!doctype html><html lang="es"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>Let me sleep — Guía</title><style>
body{margin:0;background:#f5efd9;color:#25313b;font:17px/1.6 system-ui,sans-serif}main{max-width:880px;margin:auto;padding:44px 24px 80px}h1{font-size:36px;line-height:1.12;max-width:680px;color:#244b4d}h2{margin-top:40px;font-size:24px;border-top:2px solid #d8ceb4;padding-top:18px}strong{color:#224b50}li{margin:10px 0}table{border-collapse:collapse;width:100%;font-size:15px}td,th{padding:12px;border:1px solid #cfc5ac;text-align:left}th{background:#e9dfc4}@media(max-width:600px){main{padding:24px 16px}h1{font-size:28px}table{font-size:12px}td,th{padding:7px}}
</style><main>""" + "\n".join(body) + "</main></html>\n"
(root / "distribution/LEEME.html").write_text(page, encoding="utf-8", newline="\n")
print("Rendered LEEME.html from LEEME.md")
