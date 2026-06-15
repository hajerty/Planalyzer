// Planalyzer — single-page UI (vanilla ES module, no deps).

const API = ""; // same-origin
const LS_KEY = "planalyzer.v1";

// ---------- utilities ----------

const $ = (sel, root = document) => root.querySelector(sel);
const $$ = (sel, root = document) => Array.from(root.querySelectorAll(sel));

function escapeHtml(s) {
  if (s == null) return "";
  return String(s)
    .replaceAll("&", "&amp;")
    .replaceAll("<", "&lt;")
    .replaceAll(">", "&gt;")
    .replaceAll('"', "&quot;")
    .replaceAll("'", "&#39;");
}

function loadPrefs() {
  try { return JSON.parse(localStorage.getItem(LS_KEY) || "{}"); }
  catch { return {}; }
}
function savePrefs(p) {
  try { localStorage.setItem(LS_KEY, JSON.stringify(p)); } catch { /* ignore */ }
}

function showError(msg) {
  const box = $("#globalError");
  box.textContent = msg;
  box.hidden = false;
  window.scrollTo({ top: 0, behavior: "smooth" });
}
function clearError() {
  const box = $("#globalError");
  box.hidden = true; box.textContent = "";
}

async function api(path, opts = {}) {
  clearError();
  const init = {
    method: opts.method || "GET",
    headers: { "Accept": "application/json", ...(opts.headers || {}) },
  };
  if (opts.body !== undefined) {
    init.headers["Content-Type"] = "application/json";
    init.body = JSON.stringify(opts.body);
  }
  let resp;
  try {
    resp = await fetch(API + path, init);
  } catch (e) {
    throw new Error("Errore di rete: " + (e && e.message || e));
  }
  const text = await resp.text();
  let json = null;
  if (text) {
    try { json = JSON.parse(text); } catch { /* not json */ }
  }
  if (!resp.ok) {
    const err = (json && (json.error || json.title)) || text || resp.statusText;
    throw new Error(`HTTP ${resp.status} — ${err}`);
  }
  return json;
}

function readFileAsText(file) {
  return new Promise((res, rej) => {
    const r = new FileReader();
    r.onload = () => res(String(r.result || ""));
    r.onerror = () => rej(new Error("Impossibile leggere il file"));
    r.readAsText(file);
  });
}

function sevClass(sev) {
  switch (String(sev || "").toLowerCase()) {
    case "critical": return "sev-critical";
    case "high":     return "sev-high";
    case "medium":   return "sev-medium";
    case "low":      return "sev-low";
    case "info":     return "sev-info";
    default:         return "sev-low";
  }
}

// ---------- tab routing ----------

function activateTab(name) {
  $$("#tabs .tab").forEach(b => b.classList.toggle("active", b.dataset.tab === name));
  $$(".panel").forEach(p => { p.hidden = p.dataset.panel !== name; });
  clearError();
}

$$("#tabs .tab").forEach(b => b.addEventListener("click", () => activateTab(b.dataset.tab)));

// ---------- health badge ----------

async function pingHealth() {
  const h = $("#health");
  try {
    await api("/api/health");
    h.textContent = "online";
    h.classList.add("ok"); h.classList.remove("ko");
  } catch {
    h.textContent = "offline";
    h.classList.add("ko"); h.classList.remove("ok");
  }
}

// ---------- rules listing ----------

async function loadRules() {
  const list = $("#ruleList");
  try {
    const rules = await api("/api/rules");
    if (!Array.isArray(rules) || rules.length === 0) {
      list.innerHTML = '<span class="muted">nessuna regola</span>';
      return;
    }
    list.innerHTML = rules.map(r => {
      const id = r.ruleId || r.id || r.code || "?";
      const title = r.title || r.name || "";
      return `<label title="${escapeHtml(title)}"><input type="checkbox" value="${escapeHtml(id)}" data-rule/>${escapeHtml(id)}</label>`;
    }).join("");
  } catch (e) {
    list.innerHTML = `<span class="muted">errore caricamento regole: ${escapeHtml(e.message)}</span>`;
  }
}

// ---------- CERTIFY ----------

function readValidationOptions() {
  const opt = {};
  const num = (id, key) => {
    const v = $(id).value.trim();
    if (v !== "") opt[key] = parseInt(v, 10);
  };
  num("#opt-maxOutputColumns", "maxOutputColumns");
  num("#opt-maxJoinedTables", "maxJoinedTables");
  num("#opt-maxDerivedColumns", "maxDerivedColumns");
  num("#opt-maxEstimatedRows", "maxEstimatedRows");
  num("#opt-maxTablesWithoutAlias", "maxTablesWithoutAlias");
  num("#opt-maxStatementLines", "maxStatementLines");
  const disabled = $$("#ruleList input[data-rule]:checked").map(i => i.value);
  if (disabled.length) opt.disabledRules = disabled;
  return Object.keys(opt).length ? opt : undefined;
}

function renderValidation(report) {
  const out = $("#validateOutput");
  out.hidden = false;

  const score = (typeof report.score === "number") ? report.score : "--";
  $("#vScore").textContent = score;

  const cert = !!report.certifiable;
  const badge = $("#vBadge");
  badge.textContent = cert ? "CERTIFICATA" : "NON CERTIFICATA";
  badge.className = "cert-badge " + (cert ? "ok" : "ko");

  const counts = report.countsBySeverity || {};
  $("#vCounts").innerHTML = Object.entries(counts).map(([k, v]) =>
    `<span class="pill"><span class="sev ${sevClass(k)}">${escapeHtml(k)}</span> ${escapeHtml(v)}</span>`
  ).join("") || '<span class="muted">nessun finding</span>';

  const findings = Array.isArray(report.findings) ? report.findings : [];
  const tbody = $("#vFindings");
  if (findings.length === 0) {
    tbody.innerHTML = '<tr><td colspan="6" class="muted">nessun finding — query pulita</td></tr>';
    return;
  }
  // sort: severity weight then ruleId
  const wt = { critical: 5, high: 4, medium: 3, low: 2, info: 1 };
  findings.sort((a, b) => (wt[String(b.severity).toLowerCase()] || 0) - (wt[String(a.severity).toLowerCase()] || 0));
  tbody.innerHTML = findings.map(f => {
    const lc = (f.line != null && f.column != null) ? `${f.line}:${f.column}` :
               (f.line != null ? `${f.line}` : "");
    const snippet = f.snippet ? `<div class="snippet">${escapeHtml(f.snippet)}</div>` : "";
    const fix = f.fixHint ? `<div class="fix">${escapeHtml(f.fixHint)}</div>` : "";
    const just = f.justification ? `<div class="just">justify: ${escapeHtml(f.justification)}</div>` : "";
    const title = f.title ? `<div class="muted">${escapeHtml(f.title)}</div>` : "";
    return `<tr>
      <td><span class="sev ${sevClass(f.severity)}">${escapeHtml(f.severity)}</span></td>
      <td><code>${escapeHtml(f.ruleId)}</code>${title}</td>
      <td>${escapeHtml(lc)}</td>
      <td>${escapeHtml(f.source || "")}</td>
      <td>${escapeHtml(f.message || "")}${just}</td>
      <td>${snippet}${fix}</td>
    </tr>`;
  }).join("");
}

async function doValidate() {
  const sql = $("#certifySql").value.trim();
  if (!sql) { showError("Inserisci il SQL da validare."); return; }
  const btn = $("#btnValidate"); btn.disabled = true;
  try {
    const body = { sql };
    const options = readValidationOptions();
    if (options) body.options = options;
    const report = await api("/api/validate", { method: "POST", body });
    renderValidation(report);
  } catch (e) { showError(e.message); }
  finally { btn.disabled = false; }
}

$("#btnValidate").addEventListener("click", doValidate);
$("#btnValidateClear").addEventListener("click", () => {
  $("#certifySql").value = "";
  $("#validateOutput").hidden = true;
});

// ---------- ANALYZE ----------

function renderStatements(statements) {
  const host = $("#analyzeStatements");
  if (!Array.isArray(statements) || !statements.length) {
    host.innerHTML = '<p class="muted">nessuno statement.</p>'; return;
  }
  host.innerHTML = statements.map((s, idx) => {
    const findings = Array.isArray(s.findings) ? s.findings : [];
    const items = findings.map(f => {
      const sev = `<span class="sev ${sevClass(f.severity)}">${escapeHtml(f.severity || "info")}</span>`;
      const msg = escapeHtml(f.message || f.title || "");
      const fix = f.fixHint ? ` <span class="fix">— ${escapeHtml(f.fixHint)}</span>` : "";
      return `<li>${sev} ${msg}${fix}</li>`;
    }).join("");
    const kv = [];
    if (s.label) kv.push(["label", s.label]);
    if (s.statementType) kv.push(["type", s.statementType]);
    if (typeof s.estimatedRows === "number") kv.push(["est. rows", s.estimatedRows]);
    if (typeof s.estimatedCost === "number") kv.push(["est. cost", s.estimatedCost]);
    const kvHtml = kv.length
      ? `<dl class="kv">${kv.map(([k, v]) => `<dt>${escapeHtml(k)}</dt><dd>${escapeHtml(String(v))}</dd>`).join("")}</dl>`
      : "";
    return `<div class="statement-card">
      <div class="head"><strong>Statement #${idx + 1}</strong><span>${escapeHtml(s.title || "")}</span></div>
      ${kvHtml}
      ${items ? `<ul>${items}</ul>` : '<p class="muted">nessun finding sullo statement.</p>'}
    </div>`;
  }).join("");
}

async function doAnalyze() {
  const file = $("#analyzeFile").files[0];
  if (!file) { showError("Seleziona un file piano."); return; }
  const level = $("#analyzeLevel").value || "beginner";
  const btn = $("#btnAnalyze"); btn.disabled = true;
  try {
    const planXml = await readFileAsText(file);
    const res = await api("/api/analyze", { method: "POST", body: { planXml, level } });
    $("#analyzeOutput").hidden = false;
    $("#analyzeText").textContent = res.text || "(vuoto)";
    renderStatements(res.statements);
  } catch (e) { showError(e.message); }
  finally { btn.disabled = false; }
}

$("#btnAnalyze").addEventListener("click", doAnalyze);

// ---------- COMPARE ----------

async function doCompare() {
  const est = $("#estimatedFile").files[0];
  const act = $("#actualFile").files[0];
  if (!est || !act) { showError("Seleziona entrambi i file."); return; }
  const btn = $("#btnCompare"); btn.disabled = true;
  try {
    const [estimatedXml, actualXml] = await Promise.all([readFileAsText(est), readFileAsText(act)]);
    const res = await api("/api/compare", { method: "POST", body: { estimatedXml, actualXml } });
    $("#compareOutput").hidden = false;
    $("#compareText").textContent = res.text || "(vuoto)";
    const rows = Array.isArray(res.rows) ? res.rows
               : Array.isArray(res.diffs) ? res.diffs
               : Array.isArray(res.nodes) ? res.nodes
               : [];
    const tbody = $("#compareRows");
    if (!rows.length) {
      tbody.innerHTML = '<tr><td colspan="4" class="muted">nessun diff strutturato</td></tr>';
    } else {
      tbody.innerHTML = rows.map(r => {
        const skew = (typeof r.skew === "number") ? r.skew
                   : (typeof r.ratio === "number") ? r.ratio
                   : null;
        const cls = skew != null ? (skew >= 10 ? "skew-high" : (skew >= 2 ? "skew-med" : "")) : "";
        const skewStr = skew != null ? (Math.round(skew * 100) / 100) + "x" : "";
        return `<tr>
          <td>${escapeHtml(r.node || r.label || r.operator || r.name || "")}</td>
          <td>${escapeHtml(r.estimated ?? r.estimatedRows ?? "")}</td>
          <td>${escapeHtml(r.actual ?? r.actualRows ?? "")}</td>
          <td class="${cls}">${escapeHtml(skewStr)}</td>
        </tr>`;
      }).join("");
    }
  } catch (e) { showError(e.message); }
  finally { btn.disabled = false; }
}

$("#btnCompare").addEventListener("click", doCompare);

// ---------- MULTIDB ----------

const variants = []; // { label, planXml, fileName }

function refreshVariantUi() {
  const host = $("#variantList");
  if (!variants.length) {
    host.innerHTML = '<p class="muted">nessuna variante. Aggiungine almeno due.</p>';
  } else {
    host.innerHTML = variants.map((v, i) => `
      <div class="variant-item">
        <label style="flex:0 0 auto;">Label
          <input type="text" data-vlabel="${i}" value="${escapeHtml(v.label)}"/>
        </label>
        <span class="file-name">${escapeHtml(v.fileName || "(no file)")}</span>
        <span class="status ${v.planXml ? "ok" : "ko"}">${v.planXml ? "ok" : "vuoto"}</span>
        <button class="ghost" data-vrm="${i}">rimuovi</button>
      </div>`).join("");
    $$("[data-vlabel]", host).forEach(inp => {
      inp.addEventListener("input", () => { variants[+inp.dataset.vlabel].label = inp.value; });
    });
    $$("[data-vrm]", host).forEach(btn => {
      btn.addEventListener("click", () => {
        variants.splice(+btn.dataset.vrm, 1);
        refreshVariantUi();
      });
    });
  }
  $("#btnRunMulti").disabled = variants.length < 2;
}

function addVariantFromFile() {
  const inp = document.createElement("input");
  inp.type = "file";
  inp.accept = ".sqlplan,.xml,text/xml";
  inp.addEventListener("change", async () => {
    const f = inp.files[0];
    if (!f) return;
    try {
      const planXml = await readFileAsText(f);
      const label = (f.name || "").replace(/\.(sqlplan|xml)$/i, "") || `variant-${variants.length + 1}`;
      variants.push({ label, planXml, fileName: f.name });
      refreshVariantUi();
    } catch (e) { showError(e.message); }
  });
  inp.click();
}

$("#btnAddVariant").addEventListener("click", addVariantFromFile);
$("#btnClearVariants").addEventListener("click", () => { variants.length = 0; refreshVariantUi(); $("#multiOutput").hidden = true; });

async function doMulti() {
  if (variants.length < 2) { showError("Servono almeno due varianti."); return; }
  const btn = $("#btnRunMulti"); btn.disabled = true;
  try {
    const body = { variants: variants.map(v => ({ label: v.label, planXml: v.planXml })) };
    const res = await api("/api/multidb", { method: "POST", body });
    $("#multiOutput").hidden = false;
    $("#multiText").textContent = res.text || "(vuoto)";
    const variantsOut = Array.isArray(res.variants) ? res.variants : [];
    $("#multiVariants").innerHTML = variantsOut.length
      ? variantsOut.map(v => `<div class="statement-card">
          <div class="head"><strong>${escapeHtml(v.label || "?")}</strong><span>${escapeHtml(v.summary || "")}</span></div>
          ${v.notes ? `<p>${escapeHtml(v.notes)}</p>` : ""}
          ${Array.isArray(v.findings) && v.findings.length ? `<ul>${v.findings.map(f =>
            `<li><span class="sev ${sevClass(f.severity)}">${escapeHtml(f.severity || "info")}</span> ${escapeHtml(f.message || f.title || "")}</li>`
          ).join("")}</ul>` : ""}
        </div>`).join("")
      : '<p class="muted">nessun dettaglio per variante</p>';

    const common = Array.isArray(res.commonRecommendations) ? res.commonRecommendations
                 : Array.isArray(res.common) ? res.common : [];
    const div = Array.isArray(res.divergentRecommendations) ? res.divergentRecommendations
              : Array.isArray(res.divergent) ? res.divergent : [];
    $("#multiCommon").innerHTML = common.length ? common.map(s => `<li>${escapeHtml(typeof s === "string" ? s : JSON.stringify(s))}</li>`).join("") : '<li class="muted">nessuna</li>';
    $("#multiDiverge").innerHTML = div.length ? div.map(s => `<li>${escapeHtml(typeof s === "string" ? s : JSON.stringify(s))}</li>`).join("") : '<li class="muted">nessuna</li>';

    $("#multiSql").textContent = res.suggestedSql || res.sql || "(nessun SQL suggerito)";
  } catch (e) { showError(e.message); }
  finally { btn.disabled = false; }
}

$("#btnRunMulti").addEventListener("click", doMulti);
refreshVariantUi();

// ---------- RUN & HISTORY ----------

function renderRunRevision(rev) {
  if (!rev) { $("#runRevision").innerHTML = '<p class="muted">nessuna revisione</p>'; return; }
  const fields = [
    ["rev", rev.revision ?? rev.rev ?? rev.id ?? "?"],
    ["timestamp", rev.timestamp || rev.createdAt || ""],
    ["mode", rev.mode || ""],
    ["durata (ms)", rev.durationMs ?? rev.duration ?? ""],
    ["CPU", rev.cpu ?? rev.cpuTime ?? ""],
    ["logical reads", rev.logicalReads ?? rev.lr ?? ""],
    ["physical reads", rev.physicalReads ?? rev.pr ?? ""],
    ["rows", rev.rows ?? rev.rowCount ?? ""],
    ["note", rev.note || ""],
  ];
  $("#runRevision").innerHTML = `<dl class="kv">${
    fields.map(([k, v]) => `<dt>${escapeHtml(k)}</dt><dd>${escapeHtml(String(v))}</dd>`).join("")
  }</dl>`;
}

function renderRunValidation(report) {
  if (!report) { $("#runValidation").innerHTML = '<p class="muted">nessun report</p>'; return; }
  const cert = !!report.certifiable;
  const score = (typeof report.score === "number") ? report.score : "--";
  const counts = report.countsBySeverity || {};
  const findings = Array.isArray(report.findings) ? report.findings : [];
  const countsHtml = Object.entries(counts).map(([k, v]) =>
    `<span class="pill"><span class="sev ${sevClass(k)}">${escapeHtml(k)}</span> ${escapeHtml(v)}</span>`
  ).join("");
  const findHtml = findings.length
    ? `<table class="findings"><thead><tr><th>Sev</th><th>Regola</th><th>Messaggio</th></tr></thead><tbody>${
        findings.map(f => `<tr>
          <td><span class="sev ${sevClass(f.severity)}">${escapeHtml(f.severity)}</span></td>
          <td><code>${escapeHtml(f.ruleId)}</code></td>
          <td>${escapeHtml(f.message || "")}</td>
        </tr>`).join("")
      }</tbody></table>`
    : '<p class="muted">nessun finding</p>';
  $("#runValidation").innerHTML = `
    <div class="cert-summary">
      <div class="score-box"><div class="score">${escapeHtml(score)}</div><div class="score-label">Score</div></div>
      <div class="cert-badge ${cert ? "ok" : "ko"}">${cert ? "CERTIFICATA" : "NON CERTIFICATA"}</div>
      <div class="counts">${countsHtml}</div>
    </div>
    ${findHtml}`;
}

async function doRun() {
  const body = {
    connectionString: $("#runConn").value.trim(),
    slug:             $("#runSlug").value.trim(),
    sql:              $("#runSql").value,
    note:             $("#runNote").value,
    mode:             $("#runMode").value,
    historyDbPath:    $("#runHistoryDb").value.trim(),
  };
  if (!body.connectionString || !body.slug || !body.sql) {
    showError("Connection string, slug e SQL sono obbligatori.");
    return;
  }
  // persist non-sensitive prefs
  const prefs = loadPrefs();
  prefs.connectionString = body.connectionString;
  prefs.historyDbPath = body.historyDbPath;
  prefs.slug = body.slug;
  savePrefs(prefs);

  const btn = $("#btnRun"); btn.disabled = true;
  try {
    const res = await api("/api/run", { method: "POST", body });
    $("#runOutput").hidden = false;
    renderRunRevision(res.revision);
    renderRunValidation(res.validation);
    $("#runAnalysis").textContent = (res.analysis && (res.analysis.text || res.analysis)) || "(nessuna analisi)";
    await loadHistory();
  } catch (e) { showError(e.message); }
  finally { btn.disabled = false; }
}

async function loadHistory() {
  const slug = $("#runSlug").value.trim();
  const dbp  = $("#runHistoryDb").value.trim();
  const tbody = $("#historyRows");
  if (!slug) {
    tbody.innerHTML = '<tr><td colspan="9" class="muted">imposta uno slug per caricare la history</td></tr>';
    return;
  }
  try {
    const qs = dbp ? `?historyDbPath=${encodeURIComponent(dbp)}` : "";
    const list = await api(`/api/history/${encodeURIComponent(slug)}${qs}`);
    const items = Array.isArray(list) ? list
                 : Array.isArray(list && list.revisions) ? list.revisions
                 : [];
    if (!items.length) {
      tbody.innerHTML = '<tr><td colspan="9" class="muted">nessuna revisione</td></tr>';
      return;
    }
    tbody.innerHTML = items.map(r => `<tr class="clickable" data-rev='${escapeHtml(JSON.stringify(r))}'>
      <td>${escapeHtml(r.revision ?? r.rev ?? "?")}</td>
      <td>${escapeHtml(r.timestamp || r.createdAt || "")}</td>
      <td>${escapeHtml(r.mode || "")}</td>
      <td>${escapeHtml(r.durationMs ?? r.duration ?? "")}</td>
      <td>${escapeHtml(r.cpu ?? r.cpuTime ?? "")}</td>
      <td>${escapeHtml(r.logicalReads ?? r.lr ?? "")}</td>
      <td>${escapeHtml(r.physicalReads ?? r.pr ?? "")}</td>
      <td>${escapeHtml(r.rows ?? r.rowCount ?? "")}</td>
      <td>${escapeHtml(r.note || "")}</td>
    </tr>`).join("");
    $$("#historyRows tr.clickable").forEach(tr => {
      tr.addEventListener("click", () => {
        try { openRevPanel(JSON.parse(tr.dataset.rev)); } catch { /* ignore */ }
      });
    });
  } catch (e) { showError(e.message); }
}

$("#btnRun").addEventListener("click", doRun);
$("#btnLoadHistory").addEventListener("click", loadHistory);

// ----- side panel for revision detail -----

let activeRev = null;
function openRevPanel(rev) {
  activeRev = rev;
  $("#revPanelTitle").textContent = `Revisione #${rev.revision ?? rev.rev ?? "?"} — ${rev.timestamp || ""}`;
  $("#revPanel").hidden = false;
  $("#revMeta").innerHTML = `<dl class="kv">
    <dt>mode</dt><dd>${escapeHtml(rev.mode || "")}</dd>
    <dt>durata</dt><dd>${escapeHtml(String(rev.durationMs ?? rev.duration ?? ""))} ms</dd>
    <dt>CPU</dt><dd>${escapeHtml(String(rev.cpu ?? rev.cpuTime ?? ""))}</dd>
    <dt>logical reads</dt><dd>${escapeHtml(String(rev.logicalReads ?? rev.lr ?? ""))}</dd>
    <dt>physical reads</dt><dd>${escapeHtml(String(rev.physicalReads ?? rev.pr ?? ""))}</dd>
    <dt>rows</dt><dd>${escapeHtml(String(rev.rows ?? rev.rowCount ?? ""))}</dd>
    <dt>note</dt><dd>${escapeHtml(rev.note || "")}</dd>
  </dl>`;
  $("#revSql").textContent = rev.sql || "(SQL non incluso nella history)";
}
function closeRevPanel() { $("#revPanel").hidden = true; activeRev = null; }

$("#revPanelClose").addEventListener("click", closeRevPanel);

$("#btnLoadRevSql").addEventListener("click", () => {
  if (!activeRev) return;
  if (activeRev.sql) {
    $("#runSql").value = activeRev.sql;
    closeRevPanel();
    activateTab("run");
  } else {
    showError("La revisione non contiene SQL.");
  }
});

$("#btnRollback").addEventListener("click", async () => {
  if (!activeRev) return;
  const to = activeRev.revision ?? activeRev.rev;
  if (to == null) { showError("Revisione senza id."); return; }
  const slug = $("#runSlug").value.trim();
  const dbp  = $("#runHistoryDb").value.trim();
  if (!slug) { showError("Imposta slug prima del rollback."); return; }
  if (!confirm(`Confermi rollback di "${slug}" alla revisione ${to}?`)) return;
  try {
    await api("/api/rollback", { method: "POST", body: { slug, to, historyDbPath: dbp || undefined } });
    closeRevPanel();
    await loadHistory();
  } catch (e) { showError(e.message); }
});

// ---------- bootstrap ----------

function restorePrefs() {
  const p = loadPrefs();
  if (p.connectionString) $("#runConn").value = p.connectionString;
  if (p.historyDbPath)    $("#runHistoryDb").value = p.historyDbPath;
  if (p.slug)             $("#runSlug").value = p.slug;
}

restorePrefs();
activateTab("certify");
pingHealth();
loadRules();

// expose a tiny surface for debugging
export const Planalyzer = {
  api, activateTab, loadRules, loadHistory,
};
