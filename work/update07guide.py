"""Render the small local guide's Markdown subset, preserving its existing CSS."""
from pathlib import Path
import html,re
root=Path(__file__).resolve().parent.parent
source=(root/'distribution/LEEME.md').read_text(encoding='utf-8')
target=root/'distribution/LEEME.html'
prefix=target.read_text(encoding='utf-8').split('<main>')[0]
def inline(text):
    text=html.escape(text)
    text=re.sub(r'\*\*(.+?)\*\*',r'<strong>\1</strong>',text)
    text=re.sub(r'`([^`]+)`',r'<code>\1</code>',text)
    text=re.sub(r'\[([^\]]+)\]\(([^)]+)\)',r'<a href="\2">\1</a>',text)
    return text
lines=source.splitlines();out=[];i=0
while i<len(lines):
    line=lines[i].strip()
    if not line:i+=1;continue
    if line.startswith('#'):
        heading,body=line.split(' ',1);n=len(heading);out.append(f'<h{n}>{inline(body)}</h{n}>');i+=1;continue
    if line.startswith('|'):
        rows=[]
        while i<len(lines) and lines[i].startswith('|'):
            cells=[c.strip() for c in lines[i].strip().strip('|').split('|')]
            if not all(re.fullmatch(r':?-+:?',c) for c in cells):rows.append(cells)
            i+=1
        out.append('<table><thead><tr>'+''.join('<th>'+inline(c)+'</th>' for c in rows[0])+'</tr></thead><tbody>')
        out.extend('<tr>'+''.join('<td>'+inline(c)+'</td>' for c in row)+'</tr>' for row in rows[1:]);out.append('</tbody></table>');continue
    if re.match(r'^(?:- |\d+\. )',line):
        tag='ul' if line.startswith('- ') else 'ol';out.append('<'+tag+'>')
        while i<len(lines) and re.match(r'^(?:- |\d+\. )',lines[i]):
            out.append('<li>'+inline(re.sub(r'^(?:- |\d+\. )','',lines[i]))+'</li>');i+=1
        out.append('</'+tag+'>');continue
    paragraph=[]
    while i<len(lines) and lines[i].strip():paragraph.append(lines[i].strip());i+=1
    out.append('<p>'+inline(' '.join(paragraph))+'</p>')
target.write_text(prefix+'<main>'+ '\n'.join(out)+'</main>\n',encoding='utf-8')
print('Guide rendered:',target)
