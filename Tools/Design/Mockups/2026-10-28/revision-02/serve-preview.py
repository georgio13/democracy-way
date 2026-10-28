from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from importlib.util import spec_from_file_location, module_from_spec
from pathlib import Path
import re

render_path = Path('C:/Users/giorg/.codex/plugins/cache/openai-bundled/visualize/1.0.57/skills/visualize/scripts/render.py')
spec = spec_from_file_location('visualize_render', render_path)
module = module_from_spec(spec)
import sys
sys.modules[spec.name] = module
spec.loader.exec_module(module)
source = Path('C:/Users/giorg/.codex/visualizations/2026/10/28/01a1807e-85d5-7cb0-be5a-c89d3fa244fe/democracy-way-project-mockups.html').read_text(encoding='utf-8')
pages = {}
sections = re.findall(r'<section data-variant=.*?</section>', source, flags=re.S)
for index, section in enumerate(sections):
    isolated = source
    for other in sections:
        modified = re.sub(r'(<section[^>]*?) hidden>', r'\1>', other, count=1) if other == section else re.sub(r'(<section[^>]*?)(?: hidden)?>', lambda match: re.sub(r' hidden$', '', match.group(1))+' hidden>', other, count=1)
        isolated = isolated.replace(other, modified)
    pages['/'+str(index)+'.html'] = module._render_document(isolated, 'Οδός Δημοκρατίας — μακέτα '+str(index+1)).encode('utf-8')

class Handler(BaseHTTPRequestHandler):
    def do_GET(self):
        payload = pages.get(self.path)
        if payload is None:
            self.send_error(404)
            return
        self.send_response(200)
        self.send_header('Content-Type','text/html; charset=utf-8')
        self.send_header('Content-Length',str(len(payload)))
        self.end_headers()
        self.wfile.write(payload)
    def log_message(self,*args):
        pass

print('Preview ready: http://127.0.0.1:8877/0.html', flush=True)
ThreadingHTTPServer(('127.0.0.1',8877),Handler).serve_forever()
