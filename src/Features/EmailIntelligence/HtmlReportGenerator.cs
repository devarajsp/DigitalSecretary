using System.Text;

namespace DigitalSecretary.Features.EmailIntelligence;

/// <summary>
/// Writes the self-contained HTML5 report: a single <c>index.html</c> with tabbed views
/// (Overview, People, Timeline, Graph, Topics), plus <c>assets/</c> (CSS + JS) and a
/// <c>data/data.js</c> that holds the model. Data is loaded via a &lt;script&gt; tag, so the
/// report opens straight from <c>file://</c> with no server and no network. Fully offline.
/// </summary>
public sealed class HtmlReportGenerator
{
    public void Write(IntelligenceReport report, string outDir)
    {
        var assets = Path.Combine(outDir, "assets");
        var data = Path.Combine(outDir, "data");
        Directory.CreateDirectory(assets);
        Directory.CreateDirectory(data);

        var utf8 = new UTF8Encoding(false);
        File.WriteAllText(Path.Combine(outDir, "index.html"), IndexHtml, utf8);
        File.WriteAllText(Path.Combine(assets, "styles.css"), StylesCss, utf8);
        File.WriteAllText(Path.Combine(assets, "app.js"), AppJs, utf8);
        File.WriteAllText(Path.Combine(data, "data.js"),
            "window.__DATA__ = " + new JsonExporter().Serialize(report) + ";", utf8);
    }

    private const string IndexHtml = """
        <!DOCTYPE html>
        <html lang="en">
        <head>
          <meta charset="utf-8" />
          <meta name="viewport" content="width=device-width, initial-scale=1" />
          <title>Email Intelligence Report</title>
          <link rel="stylesheet" href="assets/styles.css" />
        </head>
        <body>
          <header>
            <h1>Email Intelligence</h1>
            <nav id="tabs"></nav>
          </header>
          <main id="view"></main>
          <footer>Generated locally by Digital Secretary &mdash; no data left this machine.</footer>
          <script src="data/data.js"></script>
          <script src="assets/app.js"></script>
        </body>
        </html>
        """;

    private const string StylesCss = """
        :root { --bg:#f5f6f8; --card:#fff; --ink:#21252b; --muted:#6b7280; --accent:#1f4e79; --line:#e3e6ea; }
        * { box-sizing:border-box; }
        body { margin:0; font-family:'Segoe UI',system-ui,sans-serif; background:var(--bg); color:var(--ink); }
        header { background:var(--accent); color:#fff; padding:14px 22px; }
        header h1 { margin:0 0 10px; font-size:20px; }
        nav { display:flex; gap:6px; flex-wrap:wrap; }
        .tab { background:rgba(255,255,255,.14); color:#fff; border:0; padding:7px 14px; border-radius:6px 6px 0 0; cursor:pointer; font-size:14px; }
        .tab.active { background:var(--card); color:var(--accent); font-weight:600; }
        main { padding:22px; }
        footer { padding:14px 22px; color:var(--muted); font-size:12px; }
        .cards { display:flex; gap:14px; flex-wrap:wrap; margin-bottom:18px; }
        .stat { background:var(--card); border:1px solid var(--line); border-radius:10px; padding:16px 20px; min-width:140px; }
        .stat b { display:block; font-size:26px; color:var(--accent); }
        .stat span { color:var(--muted); font-size:13px; }
        input[type=search] { width:100%; max-width:360px; padding:9px 12px; border:1px solid var(--line); border-radius:8px; font-size:14px; margin-bottom:12px; }
        table { width:100%; border-collapse:collapse; background:var(--card); border:1px solid var(--line); border-radius:10px; overflow:hidden; }
        th,td { text-align:left; padding:9px 12px; border-bottom:1px solid var(--line); font-size:14px; }
        th { background:#eef1f5; color:var(--accent); }
        tr.person { cursor:pointer; }
        tr.person:hover { background:#f0f4f9; }
        .pill { display:inline-block; padding:2px 8px; border-radius:999px; font-size:12px; background:#eef1f5; color:var(--accent); margin:2px; }
        .pill.warn { background:#fde8e8; color:#b42318; }
        .dossier { background:var(--card); border:1px solid var(--line); border-radius:10px; padding:18px; margin-bottom:18px; }
        .dossier h2 { margin:0 0 4px; }
        .dossier .meta { color:var(--muted); font-size:13px; margin-bottom:10px; }
        .kv { display:flex; gap:8px; margin:3px 0; font-size:14px; }
        .kv b { min-width:120px; color:var(--muted); font-weight:600; }
        .timeline { border-left:3px solid var(--accent); margin-left:8px; padding-left:14px; }
        .tl { margin:10px 0; }
        .tl .when { color:var(--muted); font-size:12px; }
        .tl .sent { color:#1f7a3d; font-weight:600; }
        .tl .received { color:#1f4e79; font-weight:600; }
        a.src { font-size:12px; color:var(--accent); }
        svg { background:var(--card); border:1px solid var(--line); border-radius:10px; width:100%; height:560px; }
        .node { cursor:pointer; }
        .muted { color:var(--muted); }
        """;

    private const string AppJs = """
        (function () {
          var D = window.__DATA__ || { people: [], edges: [], timelines: {}, topTopics: [], meta: {} };
          var tabs = ['Overview', 'People', 'Timeline', 'Graph', 'Topics', 'Life Data', 'Documents'];
          var current = 'Overview';
          var selected = D.people.length ? D.people[0].id : null;
          var search = '';

          function byId(id) { return D.people.filter(function (p) { return p.id === id; })[0]; }
          function esc(s) {
            return (s == null ? '' : String(s)).replace(/[&<>"]/g, function (c) {
              return { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' }[c];
            });
          }
          function day(s) { return s ? String(s).substring(0, 10) : '—'; }

          function renderTabs() {
            var nav = document.getElementById('tabs');
            nav.innerHTML = '';
            tabs.forEach(function (t) {
              var b = document.createElement('button');
              b.textContent = t;
              b.className = 'tab' + (t === current ? ' active' : '');
              b.onclick = function () { current = t; render(); };
              nav.appendChild(b);
            });
          }

          function stat(n, label) { return '<div class="stat"><b>' + n + '</b><span>' + label + '</span></div>'; }

          function overview() {
            var m = D.meta;
            return '<div class="cards">' +
              stat(m.peopleCount || 0, 'people') +
              stat(m.messageCount || 0, 'messages') +
              stat(m.duplicatesRemoved || 0, 'duplicates removed') +
              stat(m.attachmentCount || 0, 'attachments') +
              '</div><p class="muted">Owner: ' + esc(m.owner || '(unknown)') + ' &middot; generated ' + day(m.generatedUtc) + '</p>' +
              '<h3>Top topics</h3><div>' + (D.topTopics || []).map(function (t) { return '<span class="pill">' + esc(t) + '</span>'; }).join('') + '</div>';
          }

          function matches(p) {
            if (!search) return true;
            var q = search.toLowerCase();
            return (p.name || '').toLowerCase().indexOf(q) >= 0 ||
                   (p.addresses || []).join(' ').toLowerCase().indexOf(q) >= 0;
          }

          function peopleTab() {
            var rows = D.people.filter(matches).map(function (p) {
              return '<tr class="person" data-id="' + esc(p.id) + '">' +
                '<td>' + esc(p.name) + (p.dormant ? ' <span class="pill warn">dormant</span>' : '') + '</td>' +
                '<td>' + esc(p.id) + '</td>' +
                '<td>' + p.strength + '</td>' +
                '<td>' + p.total + '</td>' +
                '<td>' + day(p.last) + '</td></tr>';
            }).join('');
            var html = '<input type="search" id="q" placeholder="Search people by name or email…" value="' + esc(search) + '" />';
            if (selected) html += dossier(byId(selected));
            html += '<table><thead><tr><th>Name</th><th>Email</th><th>Strength</th><th>Messages</th><th>Last contact</th></tr></thead><tbody>' + rows + '</tbody></table>';
            return html;
          }

          function dossier(p) {
            if (!p) return '';
            var addrs = (p.addresses || []).map(function (a) { return '<span class="pill">' + esc(a) + '</span>'; }).join('');
            var phones = (p.phones || []).map(esc).join(', ') || '—';
            var urls = (p.urls || []).map(function (u) { return '<span class="pill">' + esc(u) + '</span>'; }).join('') || '—';
            var topics = (p.topics || []).map(function (t) { return '<span class="pill">' + esc(t) + '</span>'; }).join('') || '—';
            return '<div class="dossier"><h2>' + esc(p.name) + (p.dormant ? ' <span class="pill warn">dormant</span>' : '') + '</h2>' +
              '<div class="meta">Strength ' + p.strength + ' &middot; ' + p.total + ' messages (' + p.fromMe + ' sent, ' + p.fromThem + ' received) &middot; ' + day(p.first) + ' – ' + day(p.last) + '</div>' +
              '<div class="kv"><b>Addresses</b><span>' + addrs + '</span></div>' +
              '<div class="kv"><b>Organization</b><span>' + esc(p.org || '—') + '</span></div>' +
              '<div class="kv"><b>Phones</b><span>' + phones + '</span></div>' +
              '<div class="kv"><b>Links</b><span>' + urls + '</span></div>' +
              '<div class="kv"><b>Topics</b><span>' + topics + '</span></div>' +
              '<div class="kv"><b>Tone</b><span>' + esc(p.toneLabel || 'Neutral') + ' (' + (p.tone || 0) + ')</span></div>' +
              '<p><a href="#" id="toTimeline">View relationship timeline →</a></p></div>';
          }

          function timelineTab() {
            if (!selected) return '<p class="muted">Pick a person on the People tab.</p>';
            var p = byId(selected);
            var items = (D.timelines[selected] || []).slice().sort(function (a, b) { return (a.date || '').localeCompare(b.date || ''); });
            if (!items.length) return '<h3>' + esc(p ? p.name : '') + '</h3><p class="muted">No interactions recorded.</p>';
            var html = '<h3>Relationship history — ' + esc(p ? p.name : '') + '</h3><div class="timeline">';
            items.forEach(function (t) {
              var src = t.sourceFile ? ' <a class="src" href="file:///' + esc(String(t.sourceFile).replace(/\\/g, '/')) + '">open</a>' : '';
              html += '<div class="tl"><span class="when">' + day(t.date) + '</span> ' +
                '<span class="' + esc(t.kind) + '">' + esc(t.kind) + '</span> &middot; ' + esc(t.summary) + src + '</div>';
            });
            return html + '</div>';
          }

          function topicsTab() {
            return '<h3>Top topics across your archive</h3><div>' +
              (D.topTopics || []).map(function (t) { return '<span class="pill">' + esc(t) + '</span>'; }).join('') + '</div>';
          }

          function lifeTab() {
            var rows = (D.lifeData || []).map(function (l) {
              var amt = l.amount != null ? esc(((l.currency || '') + ' ' + l.amount).trim()) : '';
              return '<tr><td>' + esc(l.category) + '</td><td>' + day(l.date) + '</td><td>' + esc(l.sender) + '</td><td>' + esc(l.subject) + '</td><td>' + amt + '</td></tr>';
            }).join('');
            if (!rows) return '<p class="muted">No purchases, subscriptions, travel or account emails detected.</p>';
            return '<p class="muted">Heuristic extraction (no AI) — verify before relying on it.</p>' +
              '<table><thead><tr><th>Category</th><th>Date</th><th>Sender</th><th>Subject</th><th>Amount</th></tr></thead><tbody>' + rows + '</tbody></table>';
          }

          function documentsTab() {
            var rows = (D.documents || []).map(function (d) {
              return '<tr><td>' + esc(d.fileName) + '</td><td>' + esc(d.type) + '</td><td>' + (d.size || 0) + '</td><td>' + (d.count || 1) + '</td></tr>';
            }).join('');
            if (!rows) return '<p class="muted">No attachments found in the archive.</p>';
            return '<h3>Documents &amp; attachments</h3><table><thead><tr><th>File</th><th>Type</th><th>Size (bytes)</th><th>Times seen</th></tr></thead><tbody>' + rows + '</tbody></table>';
          }

          function graphTab() {
            var people = D.people.slice(0, 60);
            var ids = {};
            people.forEach(function (p, i) { ids[p.id] = i; });
            var cx = 470, cy = 280, R = 230, n = people.length || 1;
            var pos = people.map(function (p, i) {
              var a = (2 * Math.PI * i) / n;
              return { x: cx + R * Math.cos(a), y: cy + R * Math.sin(a), p: p };
            });
            var lines = (D.edges || []).filter(function (e) { return e.source in ids && e.target in ids; }).slice(0, 400)
              .map(function (e) {
                var s = pos[ids[e.source]], t = pos[ids[e.target]];
                return '<line x1="' + s.x.toFixed(1) + '" y1="' + s.y.toFixed(1) + '" x2="' + t.x.toFixed(1) + '" y2="' + t.y.toFixed(1) + '" stroke="#cdd5df" stroke-width="' + Math.min(4, e.weight) + '" />';
              }).join('');
            var nodes = pos.map(function (q) {
              var r = 5 + Math.min(18, (q.p.strength || 0) / 6);
              return '<g class="node" data-id="' + esc(q.p.id) + '"><circle cx="' + q.x.toFixed(1) + '" cy="' + q.y.toFixed(1) + '" r="' + r.toFixed(1) + '" fill="#1f4e79" opacity="0.85"><title>' + esc(q.p.name) + '</title></circle></g>';
            }).join('');
            return '<p class="muted">Each dot is a person; lines connect people who appear on the same emails. Click a dot to open their dossier.</p>' +
              '<svg viewBox="0 0 940 560">' + lines + nodes + '</svg>';
          }

          function render() {
            renderTabs();
            var view = document.getElementById('view');
            if (current === 'Overview') view.innerHTML = overview();
            else if (current === 'People') view.innerHTML = peopleTab();
            else if (current === 'Timeline') view.innerHTML = timelineTab();
            else if (current === 'Graph') view.innerHTML = graphTab();
            else if (current === 'Topics') view.innerHTML = topicsTab();
            else if (current === 'Life Data') view.innerHTML = lifeTab();
            else if (current === 'Documents') view.innerHTML = documentsTab();
            wire();
          }

          function wire() {
            var q = document.getElementById('q');
            if (q) q.oninput = function () { search = q.value; var v = document.getElementById('view'); v.innerHTML = peopleTab(); wire(); };
            Array.prototype.forEach.call(document.querySelectorAll('tr.person, g.node'), function (el) {
              el.onclick = function () { selected = el.getAttribute('data-id'); current = 'People'; render(); };
            });
            var tt = document.getElementById('toTimeline');
            if (tt) tt.onclick = function (e) { e.preventDefault(); current = 'Timeline'; render(); };
          }

          render();
        })();
        """;
}
