(function () {
  'use strict';

  const D = window.__benchData;
  if (!D) { document.body.innerHTML = '<p style="color:red;padding:2rem">No benchmark data found.</p>'; return; }

  const CC = ['#38bdf8','#4ade80','#f87171','#fbbf24','#a78bfa','#f472b6','#34d399','#fb923c','#60a5fa','#e879f9'];

  // ── Pending chart initializers (deferred until after innerHTML) ────────
  const _pendingCharts = [];

  function scheduleChart(fn) { _pendingCharts.push(fn); }

  function flushCharts() {
    for (const fn of _pendingCharts) {
      try { fn(); } catch(e) { console.warn('chart init error', e); }
    }
    _pendingCharts.length = 0;
  }

  // ── Helpers ────────────────────────────────────────────────────────────

  function esc(s) { return String(s).replace(/&/g,'&amp;').replace(/</g,'&lt;').replace(/>/g,'&gt;').replace(/"/g,'&quot;'); }
  function slug(s) { return s.toLowerCase().replace(/\s+/g,'-').replace(/\./g,'-'); }
  function cleanRc(rc) { return rc.replace('[RowCount=','').replace(']',''); }
  function f4(v) { return v.toFixed(4); }

  function fmtMs(ms) {
    if (ms >= 1000) return (ms/1000).toFixed(2)+' s';
    if (ms >= 1)    return ms.toFixed(3)+' ms';
    return (ms*1000).toFixed(0)+' µs';
  }

  function fmtMb(kb) {
    const mb = kb/1024;
    return mb >= 1 ? mb.toFixed(2)+' MB' : kb.toFixed(1)+' KB';
  }

  function perfClass(mean, min, max) {
    if (max <= min) return '';
    const t = (mean-min)/(max-min);
    return t < 0.33 ? 'cell-fast' : t < 0.66 ? 'cell-medium' : 'cell-slow';
  }

  function heatColor(t) {
    let r,g,b;
    if (t < 0.5) {
      const u = t*2;
      r = Math.round(56  + u*(251-56));
      g = Math.round(189 + u*(191-189));
      b = Math.round(248 + u*(36-248));
    } else {
      const u = (t-0.5)*2;
      r = Math.round(251 + u*(248-251));
      g = Math.round(191 + u*(113-191));
      b = Math.round(36  + u*(113-36));
    }
    const a = (40 + t*160)/255;
    return `rgba(${r},${g},${b},${a.toFixed(2)})`;
  }

  function sortedRc(rows) {
    return [...new Set(rows.map(r=>r.rowCount))]
      .sort((a,b)=>{ const na=parseInt(cleanRc(a))||0, nb=parseInt(cleanRc(b))||0; return na-nb; });
  }

  function chip(k,v,l) {
    return `<div class="kpi-chip ${k}"><span class="dot"></span><span class="val">${esc(v)}</span><span class="lbl">${esc(l)}</span></div>`;
  }

  function sc(c,v,l,s) {
    return `<div class="stat-card ${c}">
      <div class="stat-label">${esc(l)}</div>
      <div class="stat-val ${c}">${esc(v)}</div>
      <div class="stat-sub">${esc(s)}</div>
    </div>`;
  }

  const TOOLTIP_OBJ = {
    backgroundColor:'#161d2c',
    borderColor:'rgba(255,255,255,.08)',
    borderWidth:1,
    titleColor:'#e8ecf4',
    bodyColor:'#8b93a8',
    padding:10
  };

  const SCALES_OBJ = {
    x:{ticks:{color:'#4d5568',font:{family:'JetBrains Mono',size:10}},grid:{color:'rgba(255,255,255,.04)'}},
    y:{ticks:{color:'#4d5568',font:{family:'JetBrains Mono',size:10}},grid:{color:'rgba(255,255,255,.04)'},beginAtZero:true}
  };

  function baseChartOptions(extra) {
    return Object.assign({
      responsive:true,
      maintainAspectRatio:false,
      interaction:{mode:'index',intersect:false},
      plugins:{legend:{display:false},tooltip:TOOLTIP_OBJ},
      scales:SCALES_OBJ
    }, extra || {});
  }

  // ── KPI chips ──────────────────────────────────────────────────────────

  function renderKpiChips(rows) {
    const total  = rows.length;
    const failed = rows.filter(r=>!r.success).length;
    const classes = new Set(rows.map(r=>r.class)).size;
    const ok     = rows.filter(r=>r.success);
    const slow   = ok.length ? fmtMs(Math.max(...ok.map(r=>r.meanMs))) : '—';
    return chip('info',total,'benchmarks')
         + chip('ok',(total-failed).toString(),'passed')
         + chip(failed>0?'fail':'ok',failed.toString(),'failed')
         + chip('info',classes.toString(),'classes')
         + chip('warn',slow,'slowest');
  }

  // ── Tab buttons ────────────────────────────────────────────────────────

  function renderTabButtons(classes, rows) {
    let html = `<button class="tab-btn active" id="tab-overview" onclick="showTab('overview')">Overview</button>`;
    for (const cls of classes) {
      const s     = slug(cls);
      const count = rows.filter(r=>r.class===cls).length;
      const err   = rows.some(r=>r.class===cls&&!r.success) ? `<span class="tab-err"></span>` : '';
      html += `<button class="tab-btn" id="tab-${s}" onclick="showTab('${s}')">${esc(cls)}<span class="tab-count">${count}</span>${err}</button>`;
    }
    return html;
  }

  // ── Overview page ──────────────────────────────────────────────────────

  function renderOverview(rows, classes) {
    const ok = rows.filter(r=>r.success);
    if (!ok.length) return '<p style="color:var(--text3)">No data</p>';

    const failed      = rows.filter(r=>!r.success).length;
    const gmin        = Math.min(...ok.map(r=>r.meanMs));
    const gmax        = Math.max(...ok.map(r=>r.meanMs));
    const gavg        = ok.reduce((s,r)=>s+r.meanMs,0)/ok.length;
    const gpeakMb     = Math.max(...ok.map(r=>r.allocKb))/1024;
    const anyGen2     = ok.some(r=>r.gen2>0);

    const globalStats = `<div class="stats-row" style="grid-template-columns:repeat(auto-fill,minmax(160px,1fr))">
      ${sc('cyan',rows.length.toString(),'Total benchmarks',classes.length+' classes')}
      ${sc(failed>0?'red':'green',failed.toString(),'Failed',failed>0?'check setup':'all passed')}
      ${sc('cyan',fmtMs(gmin),'Global best',ok.reduce((a,b)=>a.meanMs<b.meanMs?a:b).method)}
      ${sc('red',fmtMs(gmax),'Global worst',ok.reduce((a,b)=>a.meanMs>b.meanMs?a:b).method)}
      ${sc('amber',fmtMs(gavg),'Global avg',ok.length+' results')}
      ${sc(gpeakMb>100?'red':'cyan',gpeakMb.toFixed(1)+' MB','Peak alloc',ok.reduce((a,b)=>a.allocKb>b.allocKb?a:b).method)}
      ${sc(anyGen2?'red':'green',anyGen2?'Yes':'None','Gen2 GC',anyGen2?'LOH pressure':'Clean')}
    </div>`;

    const maxRcGlobal = sortedRc(ok).pop() || '';
    const atMaxRc     = ok.filter(r=>r.rowCount===maxRcGlobal);

    // Top 10 slowest
    const slow10   = [...atMaxRc].sort((a,b)=>b.meanMs-a.meanMs).slice(0,10);
    const s10Max   = slow10.length ? Math.max(...slow10.map(r=>r.meanMs)) : 1;
    const slowRows = slow10.map(r=>{
      const pc   = r.meanMs>=s10Max*0.66?'cell-slow':r.meanMs>=s10Max*0.33?'cell-medium':'cell-fast';
      const barC = pc==='cell-slow'?'#f87171':pc==='cell-medium'?'#fbbf24':'#4ade80';
      const barW = Math.round(r.meanMs/s10Max*160);
      return `<tr>
        <td style="font-weight:600">${esc(r.method)}</td>
        <td style="color:var(--text3);font-size:11px">${esc(r.class)}</td>
        <td class="${pc}">${fmtMs(r.meanMs)}</td>
        <td>${fmtMb(r.allocKb)}</td>
        <td ${r.gen2>0?'style="color:var(--red)"':''}>${r.gen2}</td>
        <td><div style="background:${barC};height:6px;width:${barW}px;border-radius:3px;opacity:.7"></div></td>
      </tr>`;
    }).join('');

    const slowestTable = `<div class="sec-divider">Top 10 slowest @ RowCount=${cleanRc(maxRcGlobal)}</div>
    <div class="table-wrap"><table class="overview-table">
      <thead><tr>
        <th style="text-align:left">Method</th><th style="text-align:left">Class</th>
        <th>Mean</th><th>Alloc</th><th>Gen2</th><th style="text-align:left;width:200px">Bar</th>
      </tr></thead><tbody>${slowRows}</tbody>
    </table></div>`;

    // Top 10 heaviest
    const heavy10   = [...atMaxRc].sort((a,b)=>b.allocKb-a.allocKb).slice(0,10);
    const h10Max    = heavy10.length ? Math.max(...heavy10.map(r=>r.allocKb)) : 1;
    const heavyRows = heavy10.map(r=>{
      const barW = Math.round(r.allocKb/h10Max*160);
      return `<tr>
        <td style="font-weight:600">${esc(r.method)}</td>
        <td style="color:var(--text3);font-size:11px">${esc(r.class)}</td>
        <td style="color:var(--cyan)">${fmtMb(r.allocKb)}</td>
        <td>${fmtMs(r.meanMs)}</td>
        <td>${r.gen0}</td><td>${r.gen1}</td>
        <td ${r.gen2>0?'style="color:var(--red)"':''}>${r.gen2}</td>
        <td><div style="background:#38bdf8;height:6px;width:${barW}px;border-radius:3px;opacity:.6"></div></td>
      </tr>`;
    }).join('');

    const heavyTable = `<div class="sec-divider">Top 10 heaviest alloc @ RowCount=${cleanRc(maxRcGlobal)}</div>
    <div class="table-wrap"><table class="overview-table">
      <thead><tr>
        <th style="text-align:left">Method</th><th style="text-align:left">Class</th>
        <th>Alloc</th><th>Mean</th><th>Gen0</th><th>Gen1</th><th>Gen2</th>
        <th style="text-align:left;width:200px">Bar</th>
      </tr></thead><tbody>${heavyRows}</tbody>
    </table></div>`;

    // Worst degradation
    const degList = [...new Set(ok.map(r=>r.class+'::'+r.method))].map(key=>{
      const [cls,method] = key.split('::');
      const mRows = ok.filter(r=>r.class===cls&&r.method===method)
        .sort((a,b)=>(parseInt(cleanRc(a.rowCount))||0)-(parseInt(cleanRc(b.rowCount))||0));
      if (mRows.length<2) return null;
      const ratio = mRows[0].meanMs>0 ? mRows[mRows.length-1].meanMs/mRows[0].meanMs : 1;
      return {cls,method,ratio,minMs:mRows[0].meanMs,maxMs:mRows[mRows.length-1].meanMs};
    }).filter(Boolean).sort((a,b)=>b.ratio-a.ratio).slice(0,10);

    const degTable = degList.length ? `<div class="sec-divider">Worst degradation</div>
    <div class="table-wrap"><table class="overview-table">
      <thead><tr>
        <th style="text-align:left">Method</th><th style="text-align:left">Class</th>
        <th>Min</th><th>Max</th><th>Ratio</th>
      </tr></thead><tbody>${degList.map(x=>{
        const rc = x.ratio<2?'cell-fast':x.ratio<10?'cell-medium':'cell-slow';
        const rs = x.ratio>=100?x.ratio.toFixed(0)+'×':x.ratio.toFixed(1)+'×';
        return `<tr>
          <td style="font-weight:600">${esc(x.method)}</td>
          <td style="color:var(--text3);font-size:11px">${esc(x.cls)}</td>
          <td style="color:var(--green)">${fmtMs(x.minMs)}</td>
          <td style="color:var(--red)">${fmtMs(x.maxMs)}</td>
          <td class="${rc}" style="font-weight:600">${rs}</td>
        </tr>`;
      }).join('')}</tbody>
    </table></div>` : '';

    // Gen2
    const gen2Rows = ok.filter(r=>r.gen2>0).sort((a,b)=>b.gen2-a.gen2).slice(0,10);
    const gen2Table = gen2Rows.length ? `<div class="sec-divider">Gen2 GC pressure</div>
    <div class="table-wrap"><table class="overview-table">
      <thead><tr>
        <th style="text-align:left">Method</th><th style="text-align:left">Class</th>
        <th>RowCount</th><th>Gen2</th><th>Alloc</th><th>Mean</th>
      </tr></thead><tbody>${gen2Rows.map(r=>`<tr>
        <td style="font-weight:600">${esc(r.method)}</td>
        <td style="color:var(--text3);font-size:11px">${esc(r.class)}</td>
        <td>${cleanRc(r.rowCount)}</td>
        <td style="color:var(--red);font-weight:600">${r.gen2}</td>
        <td>${fmtMb(r.allocKb)}</td><td>${fmtMs(r.meanMs)}</td>
      </tr>`).join('')}</tbody>
    </table></div>` : '';

    // Class summary
    const classSummary = `<div class="sec-divider">Class summary</div>
    <div class="stats-row" style="grid-template-columns:repeat(auto-fill,minmax(220px,1fr))">
      ${classes.map(cls=>{
        const cOk     = ok.filter(r=>r.class===cls);
        const cFailed = rows.filter(r=>r.class===cls&&!r.success).length;
        if (!cOk.length) return '';
        const cMin  = Math.min(...cOk.map(r=>r.meanMs));
        const cMax  = Math.max(...cOk.map(r=>r.meanMs));
        const color = cFailed>0?'red':'cyan';
        return `<div class="stat-card ${color}" style="cursor:pointer" onclick="showTab('${slug(cls)}')">
          <div class="stat-label">${esc(cls)}</div>
          <div class="stat-val ${color}">${fmtMs(cMin)} – ${fmtMs(cMax)}</div>
          <div class="stat-sub">${cOk.length} passed · ${cFailed} failed · click to open</div>
        </div>`;
      }).join('')}
    </div>`;

    return `<div class="page-header">
      <div>
        <div class="page-title">Overview</div>
        <div class="page-meta">${classes.length} classes · ${ok.length} passed · ${esc(D.runDate)}</div>
      </div>
    </div>
    ${globalStats}${slowestTable}${heavyTable}${degTable}${gen2Table}${classSummary}`;
  }

  // ── Class page ─────────────────────────────────────────────────────────

  function renderClassPage(cls, rows) {
    const methods  = [...new Set(rows.map(r=>r.method))].sort();
    const rcList   = sortedRc(rows);
    const ok       = rows.filter(r=>r.success);
    const failed   = rows.filter(r=>!r.success).length;
    const passed   = ok.length;
    const failBadge = failed>0 ? `<span class="badge badge-red">${failed} failed</span>` : '';

    return `<div class="page-header">
      <div>
        <div class="page-title">${esc(cls)}</div>
        <div class="page-meta">${methods.length} methods · ${rcList.length} row-count variants · ${rows.length} benchmarks</div>
      </div>
      <div class="badges">
        <span class="badge badge-cyan">${methods.length} methods</span>
        <span class="badge badge-green">${passed} passed</span>
        ${failBadge}
      </div>
    </div>
    ${renderStatCards(rows, rcList)}
    <div class="chart-card" style="margin-bottom:14px" id="cc-mean-${slug(cls)}">${renderMeanChart(cls, rows, methods, rcList)}</div>
    <div class="chart-card" style="margin-bottom:20px" id="cc-alloc-${slug(cls)}">${renderAllocChart(cls, rows, methods, rcList)}</div>
    ${renderHeatmap(cls, rows, methods, rcList)}
    <div class="sec-divider">Results by method</div>
    ${renderGroupedTable(cls, rows, methods, rcList)}
    ${renderAnomalies(rows)}
    ${renderMemorySection(rows, rcList)}`;
  }

  // ── Stat cards ─────────────────────────────────────────────────────────

  function renderStatCards(rows, rcList) {
    const ok = rows.filter(r=>r.success);
    if (!ok.length) return '';
    const maxRc   = rcList[rcList.length-1] || '';
    const atMaxRc = ok.filter(r=>r.rowCount===maxRc);
    const minMean = Math.min(...ok.map(r=>r.meanMs));
    const maxMean = Math.max(...ok.map(r=>r.meanMs));
    const avgMean = ok.reduce((s,r)=>s+r.meanMs,0)/ok.length;
    const peakMb  = atMaxRc.length ? Math.max(...atMaxRc.map(r=>r.allocKb))/1024 : 0;
    const gen2    = ok.some(r=>r.gen2>0);
    const failed  = rows.filter(r=>!r.success).length;
    const allocStr = peakMb>1 ? peakMb.toFixed(2)+' MB' : (peakMb*1024).toFixed(0)+' KB';
    return `<div class="stats-row">
      ${sc('cyan',fmtMs(minMean),'Best mean',ok.reduce((a,b)=>a.meanMs<b.meanMs?a:b).method)}
      ${sc('red',fmtMs(maxMean),'Worst mean',ok.reduce((a,b)=>a.meanMs>b.meanMs?a:b).method)}
      ${sc('amber',fmtMs(avgMean),'Avg mean',ok.length+' benchmarks')}
      ${sc(peakMb>50?'red':'cyan',allocStr,'Peak alloc','@ '+cleanRc(maxRc)+' rows')}
      ${sc(gen2?'red':'green',gen2?'Yes':'None','Gen2 GC',gen2?'LOH pressure':'No LOH pressure')}
      ${sc(failed>0?'red':'green',failed.toString(),'Failed',failed>0?'check setup':'all passed')}
    </div>`;
  }

  // ── Mean chart ─────────────────────────────────────────────────────────

  function renderMeanChart(cls, rows, methods, rcList) {
    const id     = 'mc_'+slug(cls);
    const labels = rcList.map(cleanRc);

    const datasets = methods.map((m,i)=>{
      const c    = CC[i%CC.length];
      const data = rcList.map(rc=>{
        const r = rows.find(x=>x.method===m&&x.rowCount===rc);
        return (r&&r.success) ? r.meanMs : null;
      });
      return {label:m,data,borderColor:c,backgroundColor:c+'22',pointBackgroundColor:c,
              pointRadius:4,pointHoverRadius:6,tension:.35,fill:false,borderWidth:2};
    });

    scheduleChart(()=>{
      const el = document.getElementById(id);
      if (!el) return;
      new Chart(el, {type:'line',data:{labels,datasets},options:baseChartOptions()});
    });

    const legend = methods.map((m,i)=>{
      const lid = 'leg_'+id+'_'+i;
      return `<span class="legend-item" id="${lid}" onclick="toggleDataset('${lid}',${i},this)">
        <span class="legend-swatch" style="background:${CC[i%CC.length]}"></span>${esc(m)}
      </span>`;
    }).join('');

    return `<div class="chart-title">Mean execution time <span style="color:var(--text3);font-weight:400">ms · by row count</span></div>
    <div class="chart-legend">${legend}</div>
    <div class="chart-wrap" style="height:260px"><canvas id="${id}" role="img" aria-label="Mean time chart for ${esc(cls)}"></canvas></div>`;
  }

  // ── Alloc chart ────────────────────────────────────────────────────────

  function renderAllocChart(cls, rows, methods, rcList) {
    const id = 'ac_'+slug(cls);
    const ok = rows.filter(r=>r.success);
    if (!ok.length) return '<div class="chart-title">Alloc — no data</div>';

    const labels   = methods;
    const datasets = rcList.map((rc,i)=>{
      const c    = CC[i%CC.length];
      const data = methods.map(m=>{
        const r = ok.find(x=>x.method===m&&x.rowCount===rc);
        return r ? r.allocKb/1024 : null;
      });
      return {label:cleanRc(rc),data,backgroundColor:c+'cc',borderColor:c,borderWidth:1,borderRadius:3,borderSkipped:false};
    });

    scheduleChart(()=>{
      const el = document.getElementById(id);
      if (!el) return;
      new Chart(el, {type:'bar',data:{labels,datasets},options:baseChartOptions()});
    });

    const legend = rcList.map((rc,i)=>{
      const lid = 'leg_'+id+'_'+i;
      return `<span class="legend-item" id="${lid}" onclick="toggleDataset('${lid}',${i},this)">
        <span class="legend-swatch" style="background:${CC[i%CC.length]}"></span>${esc(cleanRc(rc))}
      </span>`;
    }).join('');

    return `<div class="chart-title">Allocated <span style="color:var(--text3);font-weight:400">MB · by method</span></div>
    <div class="chart-legend">${legend}</div>
    <div class="chart-wrap" style="height:220px"><canvas id="${id}" role="img" aria-label="Allocated MB chart for ${esc(cls)}"></canvas></div>`;
  }

  // ── Heatmap ────────────────────────────────────────────────────────────

  function renderHeatmap(cls, rows, methods, rcList) {
    const ok   = rows.filter(r=>r.success);
    if (!ok.length) return '';
    const gmin = Math.min(...ok.map(r=>r.meanMs));
    const gmax = Math.max(...ok.map(r=>r.meanMs));
    const headerCells = rcList.map(rc=>`<th>${cleanRc(rc)}</th>`).join('');
    const bodyRows = methods.map(m=>{
      const cells = rcList.map(rc=>{
        const r = rows.find(x=>x.method===m&&x.rowCount===rc);
        if (!r||!r.success) return `<td class="heatmap-cell-wrap"><div class="heatmap-cell" style="background:rgba(255,255,255,.03);color:#4d5568">NA</div></td>`;
        const t  = gmax>gmin ? (r.meanMs-gmin)/(gmax-gmin) : 0;
        const bg = heatColor(t);
        const tc = t>0.55?'#fff':'#e8ecf4';
        return `<td class="heatmap-cell-wrap"><div class="heatmap-cell" style="background:${bg};color:${tc}" title="${esc(m)} @ ${cleanRc(rc)}: ${fmtMs(r.meanMs)}">${fmtMs(r.meanMs)}</div></td>`;
      }).join('');
      return `<tr><td class="heatmap-method" title="${esc(m)}">${esc(m)}</td>${cells}</tr>`;
    }).join('');
    return `<div class="heatmap-wrap">
      <div class="heatmap-title">Performance heatmap — mean time (cyan = fastest · red = slowest)</div>
      <table class="heatmap-table">
        <thead><tr><th></th>${headerCells}</tr></thead>
        <tbody>${bodyRows}</tbody>
      </table>
    </div>`;
  }

  // ── Grouped table ──────────────────────────────────────────────────────

  function renderGroupedTable(cls, rows, methods, rcList) {
    const tableId  = 'tbl_'+slug(cls);
    const ok       = rows.filter(r=>r.success);
    const classMax = ok.length ? Math.max(...ok.map(r=>r.meanMs)) : 1;
    const classMin = ok.length ? Math.min(...ok.map(r=>r.meanMs)) : 0;
    const colNames = ['Method / RowCount','RowCount','Mean','Median','StdDev','Min','Max','Alloc MB','Gen0','Gen1','Gen2'];
    const colNum   = [false,false,true,true,true,true,true,true,true,true,true];
    const headers  = colNames.map((n,i)=>`<th onclick="sortTable('${tableId}',${i},${colNum[i]})">${n}</th>`).join('');

    let groupHtml = '';
    methods.forEach((m,mIdx)=>{
      const groupId = slug(cls)+'_'+mIdx;
      const mRows   = rows.filter(r=>r.method===m);
      const mOk     = mRows.filter(r=>r.success);
      const mFailed = mRows.filter(r=>!r.success).length;
      const mBest   = mOk.length ? fmtMs(Math.min(...mOk.map(r=>r.meanMs))) : '—';
      const mWorst  = mOk.length ? fmtMs(Math.max(...mOk.map(r=>r.meanMs))) : '—';
      const peakMb  = mOk.length ? Math.max(...mOk.map(r=>r.allocKb))/1024 : 0;
      const allocBdg = `<span class="group-badge" style="color:${peakMb>50?'var(--amber)':'var(--text2)'}"> ${peakMb.toFixed(1)} MB peak</span>`;
      const failBdg  = mFailed>0 ? `<span class="group-badge" style="color:var(--red)">${mFailed} failed</span>` : '';

      groupHtml += `<tr class="group-row" id="grp-${groupId}" onclick="toggleGroup('${groupId}')" data-group="${groupId}">
        <td colspan="11"><div class="group-toggle">
          <span class="arrow">▶</span>${esc(m)}
          <span class="group-badge">${mRows.length} results</span>${allocBdg}${failBdg}
          <span class="group-meta" style="margin-left:auto;padding-right:16px">${mBest} – ${mWorst}</span>
        </div></td>
      </tr>`;

      groupHtml += `<tr class="detail-row" id="det-${groupId}" data-group="${groupId}">
        <td colspan="11">${renderMethodDetail(groupId, m, mRows, rcList)}</td>
      </tr>`;

      rcList.forEach(rc=>{
        const r = mRows.find(x=>x.rowCount===rc);
        if (!r) return;
        if (!r.success) {
          groupHtml += `<tr class="data-row" data-group="${groupId}">
            <td style="padding-left:32px">${esc(m)}</td>
            <td data-val="${cleanRc(rc)}">${cleanRc(rc)}</td>
            <td colspan="9" class="cell-na">failed / NA</td>
          </tr>`;
          return;
        }
        const pc      = perfClass(r.meanMs, classMin, classMax);
        const barC    = pc==='cell-fast'?'#4ade80':pc==='cell-slow'?'#f87171':'#fbbf24';
        const barW    = classMax>0 ? Math.round(r.meanMs/classMax*36) : 0;
        const bar     = `<span class="inline-bar" style="width:${barW}px;background:${barC}"></span>`;
        const g2style = r.gen2>0 ? 'style="color:var(--red)"' : '';
        groupHtml += `<tr class="data-row" data-group="${groupId}">
          <td style="padding-left:32px;color:var(--text2);font-size:11px">${esc(m)}</td>
          <td data-val="${cleanRc(rc)}">${cleanRc(rc)}</td>
          <td class="${pc}" data-val="${f4(r.meanMs)}">${fmtMs(r.meanMs)}${bar}</td>
          <td data-val="${f4(r.medianMs)}">${fmtMs(r.medianMs)}</td>
          <td data-val="${f4(r.stdDevMs)}">${fmtMs(r.stdDevMs)}</td>
          <td data-val="${f4(r.minMs)}">${fmtMs(r.minMs)}</td>
          <td data-val="${f4(r.maxMs)}">${fmtMs(r.maxMs)}</td>
          <td data-val="${f4(r.allocKb/1024)}">${(r.allocKb/1024).toFixed(3)}</td>
          <td data-val="${r.gen0}">${r.gen0}</td>
          <td data-val="${r.gen1}">${r.gen1}</td>
          <td data-val="${r.gen2}" ${g2style}>${r.gen2}</td>
        </tr>`;
      });
    });

    return `<div class="table-wrap"><table id="${tableId}">
      <thead><tr>${headers}</tr></thead>
      <tbody>${groupHtml}</tbody>
    </table></div>`;
  }

  // ── Method detail panel ────────────────────────────────────────────────

  function renderMethodDetail(groupId, method, rows, rcList) {
    const ok = rows.filter(r=>r.success);
    if (!ok.length) return '';
    const avg    = ok.reduce((s,r)=>s+r.meanMs,0)/ok.length;
    const stddev = ok.length>1 ? Math.sqrt(ok.reduce((s,r)=>s+Math.pow(r.meanMs-avg,2),0)/(ok.length-1)) : 0;
    const cv     = avg>0 ? stddev/avg*100 : 0;
    const peakMb = Math.max(...ok.map(r=>r.allocKb))/1024;
    const gen2   = ok.some(r=>r.gen2>0);
    const cvCls  = cv<10?'good':cv<30?'mid':'bad';

    const statsSection = `<div class="detail-section">
      <div class="detail-section-title">Statistics</div>
      <div class="stat-mini-grid">
        <div class="stat-mini"><div class="stat-mini-label">Min mean</div><div class="stat-mini-val good">${fmtMs(Math.min(...ok.map(r=>r.meanMs)))}</div></div>
        <div class="stat-mini"><div class="stat-mini-label">Max mean</div><div class="stat-mini-val bad">${fmtMs(Math.max(...ok.map(r=>r.meanMs)))}</div></div>
        <div class="stat-mini"><div class="stat-mini-label">Avg mean</div><div class="stat-mini-val">${fmtMs(avg)}</div></div>
        <div class="stat-mini"><div class="stat-mini-label">CV (σ/μ)</div><div class="stat-mini-val ${cvCls}">${cv.toFixed(1)}%</div></div>
        <div class="stat-mini"><div class="stat-mini-label">Peak alloc</div><div class="stat-mini-val ${peakMb>50?'bad':''}">${peakMb.toFixed(2)} MB</div></div>
        <div class="stat-mini"><div class="stat-mini-label">Gen2 GC</div><div class="stat-mini-val ${gen2?'bad':'good'}">${gen2?'Yes':'None'}</div></div>
      </div>
    </div>`;

    // Chart — scheduled, not inline script
    let chartSection = '';
    if (ok.length >= 2) {
      const canvasId = 'dc_'+groupId;
      const sorted   = [...ok].sort((a,b)=>(parseInt(cleanRc(a.rowCount))||0)-(parseInt(cleanRc(b.rowCount))||0));
      const labels   = sorted.map(r=>cleanRc(r.rowCount));
      const meanData = sorted.map(r=>r.meanMs);
      const minData  = sorted.map(r=>r.minMs);
      const maxData  = sorted.map(r=>r.maxMs);

      // Register initializer — will be called when detail panel opens
      window['_chartInit_'+canvasId] = function() {
        const el = document.getElementById(canvasId);
        if (!el || el._done) return;
        el._done = true;
        new Chart(el, {
          type:'line',
          data:{labels,datasets:[
            {label:'Mean',data:meanData,borderColor:'#38bdf8',backgroundColor:'#38bdf822',pointRadius:4,tension:.35,fill:false,borderWidth:2},
            {label:'Min', data:minData, borderColor:'#4ade80',borderDash:[4,3],pointRadius:3,tension:.35,fill:false,borderWidth:1.5},
            {label:'Max', data:maxData, borderColor:'#f87171',borderDash:[4,3],pointRadius:3,tension:.35,fill:false,borderWidth:1.5}
          ]},
          options:baseChartOptions()
        });
      };

      chartSection = `<div class="detail-section">
        <div class="detail-section-title">Mean · Min · Max by row count</div>
        <div style="display:flex;gap:10px;margin-bottom:10px;font-size:10px">
          <span style="display:flex;align-items:center;gap:4px;color:var(--text2)"><span style="width:8px;height:2px;background:#38bdf8;display:inline-block"></span>Mean</span>
          <span style="display:flex;align-items:center;gap:4px;color:var(--text2)"><span style="width:8px;height:2px;background:#4ade80;display:inline-block;opacity:.7"></span>Min</span>
          <span style="display:flex;align-items:center;gap:4px;color:var(--text2)"><span style="width:8px;height:2px;background:#f87171;display:inline-block;opacity:.7"></span>Max</span>
        </div>
        <div class="detail-chart-wrap"><canvas id="${canvasId}" data-chart-init="_chartInit_${canvasId}" role="img" aria-label="Detail chart for ${esc(method)}"></canvas></div>
      </div>`;
    }

    const degSection = renderDegradationSection(ok);
    return `<div class="detail-inner">${chartSection}${statsSection}${degSection}</div>`;
  }

  // ── Degradation section ────────────────────────────────────────────────

  function renderDegradationSection(ok) {
    if (ok.length<2) return '';
    const sorted = [...ok].sort((a,b)=>(parseInt(cleanRc(a.rowCount))||0)-(parseInt(cleanRc(b.rowCount))||0));
    let rows = '';
    for (let i=1;i<sorted.length;i++) {
      const prev  = sorted[i-1], curr = sorted[i];
      const ratio = prev.meanMs>0 ? curr.meanMs/prev.meanMs : 1;
      const cls   = ratio<2?'ok':ratio<5?'mid':'bad';
      const str   = ratio>=100?ratio.toFixed(0)+'×':ratio.toFixed(1)+'×';
      rows += `<tr>
        <td>${cleanRc(prev.rowCount)} → ${cleanRc(curr.rowCount)}</td>
        <td>${fmtMs(curr.meanMs)}</td>
        <td class="deg-ratio ${cls}">${str}</td>
      </tr>`;
    }
    const total    = sorted[0].meanMs>0 ? sorted[sorted.length-1].meanMs/sorted[0].meanMs : 1;
    const totalCls = total<5?'ok':total<20?'mid':'bad';
    const totalStr = total>=100?total.toFixed(0)+'×':total.toFixed(1)+'×';
    rows += `<tr style="border-top:1px solid var(--border2)">
      <td style="color:var(--text3)">Overall</td><td></td>
      <td class="deg-ratio ${totalCls}">${totalStr}</td>
    </tr>`;
    return `<div class="detail-section">
      <div class="detail-section-title">Degradation analysis</div>
      <table class="degradation-table">
        <tr><td style="color:var(--text3);font-size:9px">Transition</td><td style="color:var(--text3);font-size:9px">Time</td><td style="color:var(--text3);font-size:9px">Ratio</td></tr>
        ${rows}
      </table>
    </div>`;
  }

  // ── Anomalies ──────────────────────────────────────────────────────────

  function renderAnomalies(rows) {
    const items = [];
    rows.filter(r=>!r.success).forEach(r=>
      items.push({k:'error',i:'✗',t:'Failed — '+r.method+' @ '+cleanRc(r.rowCount),d:'No results — check GlobalSetup / IterationSetup'}));
    rows.filter(r=>r.success&&r.meanMs>0&&r.stdDevMs/r.meanMs>0.20).forEach(r=>
      items.push({k:'warn',i:'~',t:'High variance — '+r.method+' @ '+cleanRc(r.rowCount),d:'StdDev '+fmtMs(r.stdDevMs)+' = '+(r.stdDevMs/r.meanMs*100).toFixed(0)+'% of mean'}));
    [...new Set(rows.map(r=>r.method))].forEach(m=>{
      const sorted = rows.filter(r=>r.method===m&&r.success)
        .sort((a,b)=>(parseInt(cleanRc(a.rowCount))||0)-(parseInt(cleanRc(b.rowCount))||0));
      if (sorted.length<2) return;
      const ratio = sorted[0].meanMs>0 ? sorted[sorted.length-1].meanMs/sorted[0].meanMs : 1;
      if (ratio>10) items.push({k:'warn',i:'↑',t:'Degradation — '+m,d:cleanRc(sorted[0].rowCount)+'→'+cleanRc(sorted[sorted.length-1].rowCount)+': '+fmtMs(sorted[0].meanMs)+' → '+fmtMs(sorted[sorted.length-1].meanMs)+' ('+ratio.toFixed(0)+'×)'});
    });
    rows.filter(r=>r.success&&r.allocKb>51200).forEach(r=>
      items.push({k:'warn',i:'M',t:'Heavy alloc — '+r.method+' @ '+cleanRc(r.rowCount),d:fmtMb(r.allocKb)+' per operation'}));
    rows.filter(r=>r.success&&r.gen2>0).forEach(r=>
      items.push({k:'info',i:'G',t:'Gen2 GC — '+r.method+' @ '+cleanRc(r.rowCount),d:'Gen2='+r.gen2+' collections'}));
    if (!items.length) return '';
    return `<div class="sec-divider">Anomalies &amp; warnings</div>
    <div class="anomaly-grid">
      ${items.map(x=>`<div class="anomaly-card">
        <div class="anomaly-icon ${x.k}">${x.i}</div>
        <div><div class="anomaly-title">${esc(x.t)}</div><div class="anomaly-detail">${esc(x.d)}</div></div>
      </div>`).join('')}
    </div>`;
  }

  // ── Memory section ─────────────────────────────────────────────────────

  function renderMemorySection(rows, rcList) {
    const maxRc = rcList[rcList.length-1] || '';
    const top   = rows.filter(r=>r.rowCount===maxRc&&r.success).sort((a,b)=>b.allocKb-a.allocKb);
    if (!top.length) return '';
    return `<div class="sec-divider">Memory @ RowCount=${cleanRc(maxRc)}</div>
    <div class="mem-grid">
      ${top.map(r=>`<div class="mem-card">
        <div class="mem-method" title="${esc(r.method)}">${esc(r.method)}</div>
        <div class="mem-alloc">${fmtMb(r.allocKb)}</div>
        <div class="mem-gc">
          <div class="mem-gc-item"><span class="g0d"></span>G0 ${r.gen0}</div>
          <div class="mem-gc-item"><span class="g1d"></span>G1 ${r.gen1}</div>
          <div class="mem-gc-item"><span class="g2d"></span>G2 ${r.gen2}</div>
        </div>
      </div>`).join('')}
    </div>`;
  }

  // ── Bootstrap ──────────────────────────────────────────────────────────

  function render() {
    const rows    = D.rows;
    const classes = [...new Set(rows.map(r=>r.class))].sort();

    document.getElementById('kpi-chips').innerHTML = renderKpiChips(rows);
    document.getElementById('tab-nav').innerHTML   = renderTabButtons(classes, rows);

    const main = document.getElementById('main-content');
    let pages  = '<div class="page active" id="page-overview">'+renderOverview(rows, classes)+'</div>';
    classes.forEach(cls=>{
      pages += '<div class="page" id="page-'+slug(cls)+'">'+renderClassPage(cls, rows.filter(r=>r.class===cls))+'</div>';
    });
    main.innerHTML = pages;

    // Fire all chart initializers after innerHTML is set
    requestAnimationFrame(()=>{ requestAnimationFrame(flushCharts); });
  }

  render();

  // ── Global UI handlers ─────────────────────────────────────────────────

  window.showTab = function(id) {
    document.querySelectorAll('.page').forEach(p=>p.classList.remove('active'));
    document.querySelectorAll('.tab-btn').forEach(b=>b.classList.remove('active'));
    document.getElementById('page-'+id).classList.add('active');
    document.getElementById('tab-'+id).classList.add('active');
  };

  window.toggleGroup = function(id) {
    const grp = document.getElementById('grp-'+id);
    const det = document.getElementById('det-'+id);
    if (!grp||!det) return;
    const open = det.classList.toggle('open');
    grp.classList.toggle('open', open);
    if (open && !det._chartsDone) {
      det._chartsDone = true;
      det.querySelectorAll('canvas[data-chart-init]').forEach(canvas=>{
        const fn = window[canvas.dataset.chartInit];
        if (typeof fn === 'function') fn();
      });
    }
  };

  window.sortTable = function(tableId, col, isNum) {
    const tbl = document.getElementById(tableId);
    if (!tbl) return;
    const ths   = tbl.querySelectorAll('thead th');
    const tbody = tbl.querySelector('tbody');
    const groupOrder = [], groups = {};
    tbody.querySelectorAll('tr').forEach(r=>{
      const g = r.dataset.group;
      if (!g) return;
      if (!groups[g]) { groups[g]={grp:null,det:null,rows:[]}; groupOrder.push(g); }
      if (r.classList.contains('group-row'))       groups[g].grp = r;
      else if (r.classList.contains('detail-row')) groups[g].det = r;
      else if (r.classList.contains('data-row'))   groups[g].rows.push(r);
    });
    const asc = ths[col].classList.contains('sort-desc') || !ths[col].classList.contains('sort-asc');
    ths.forEach(th=>th.classList.remove('sort-asc','sort-desc'));
    ths[col].classList.add(asc?'sort-asc':'sort-desc');
    groupOrder.forEach(g=>{
      groups[g].rows.sort((a,b)=>{
        const av = a.cells[col]?.dataset.val ?? a.cells[col]?.textContent ?? '';
        const bv = b.cells[col]?.dataset.val ?? b.cells[col]?.textContent ?? '';
        if (isNum) { const d=(parseFloat(av)||0)-(parseFloat(bv)||0); return asc?d:-d; }
        return asc ? av.localeCompare(bv) : bv.localeCompare(av);
      });
    });
    groupOrder.sort((ga,gb)=>{
      const ra=groups[ga].rows[0], rb=groups[gb].rows[0];
      if (!ra||!rb) return 0;
      const av = ra.cells[col]?.dataset.val ?? ra.cells[col]?.textContent ?? '';
      const bv = rb.cells[col]?.dataset.val ?? rb.cells[col]?.textContent ?? '';
      if (isNum) { const d=(parseFloat(av)||0)-(parseFloat(bv)||0); return asc?d:-d; }
      return asc ? av.localeCompare(bv) : bv.localeCompare(av);
    });
    groupOrder.forEach(g=>{
      const {grp,det,rows} = groups[g];
      if (grp) tbody.appendChild(grp);
      if (det) tbody.appendChild(det);
      rows.forEach(r=>tbody.appendChild(r));
    });
  };

  window.toggleDataset = function(legendId, datasetIndex, legendEl) {
    const item = legendEl || document.getElementById(legendId);
    if (!item) return;
    const canvas = item.closest('.chart-card')?.querySelector('canvas');
    if (!canvas) return;
    const chart = Chart.getChart(canvas.id);
    if (!chart) return;
    const meta = chart.getDatasetMeta(datasetIndex);
    meta.hidden = !meta.hidden;
    item.classList.toggle('hidden', meta.hidden);
    chart.update();
  };

})();