// Planalyzer — single-page UI (vanilla ES module, no dependencies).
// Contract: REST API su same-origin. Vedi /swagger per la specifica.

const API = "";
const LS_KEY = "planalyzer.v2";

// ============================================================
// utilities
// ============================================================

const $  = (sel, root = document) => root.querySelector(sel);
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
function mergePrefs(patch) { const p = loadPrefs(); Object.assign(p, patch); savePrefs(p); }

function showError(msg) {
  const box = $("#globalError");
  box.querySelector(".alert-msg").textContent = msg;
  box.hidden = false;
  window.scrollTo({ top: 0, behavior: "smooth" });
}
function clearError() {
  const box = $("#globalError");
  box.hidden = true;
  box.querySelector(".alert-msg").textContent = "";
}
$(".alert-close")?.addEventListener("click", clearError);

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
  if (text) { try { json = JSON.parse(text); } catch { /* not json */ } }
  if (!resp.ok) {
    const err = (json && (json.error || json.title || json.detail)) || text || resp.statusText;
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

function sevKey(sev) {
  const s = String(sev || "").toLowerCase();
  if (["critical","high","medium","low","info"].includes(s)) return s;
  return "low";
}
function sevClass(sev) { return "sev-" + sevKey(sev); }
const SEV_WEIGHT = { critical: 5, high: 4, medium: 3, low: 2, info: 1 };
const SEV_ORDER  = ["critical", "high", "medium", "low", "info"];

// ============================================================
// close buttons for output panels
// ============================================================

$$(".output-close").forEach(btn => {
  btn.addEventListener("click", () => {
    const id = btn.dataset.close;
    if (!id) return;
    const box = document.getElementById(id);
    if (box) box.hidden = true;
    // show corresponding empty state
    const emptyId = id.replace(/Output$/, "Empty");
    const empty = document.getElementById(emptyId);
    if (empty) empty.hidden = false;
  });
});

function showOutput(outputId, emptyId) {
  document.getElementById(outputId).hidden = false;
  if (emptyId) {
    const empty = document.getElementById(emptyId);
    if (empty) empty.hidden = true;
  }
}

// ============================================================
// tab routing (sidebar + mobile select)
// ============================================================

function activateTab(name) {
  $$("#tabs .tab").forEach(b => b.classList.toggle("active", b.dataset.tab === name));
  $$(".panel").forEach(p => { p.hidden = p.dataset.panel !== name; });
  const sel = $("#tabSelect");
  if (sel && sel.value !== name) sel.value = name;
  clearError();
}
$$("#tabs .tab").forEach(b => b.addEventListener("click", () => activateTab(b.dataset.tab)));
$("#tabSelect").addEventListener("change", e => activateTab(e.target.value));

// ============================================================
// health badge
// ============================================================

async function pingHealth() {
  const h = $("#health");
  const label = h.querySelector(".label");
  try {
    await api("/api/health");
    label.textContent = "online";
    h.classList.add("ok"); h.classList.remove("ko");
  } catch {
    label.textContent = "offline";
    h.classList.add("ko"); h.classList.remove("ok");
  }
}

// ============================================================
// help modal
// ============================================================

function openHelp() { $("#helpModal").hidden = false; }
function closeHelp() { $("#helpModal").hidden = true; }
$("#btnHelp").addEventListener("click", openHelp);
$$("[data-close-help]").forEach(el => el.addEventListener("click", closeHelp));

// generic confirm modal
function confirmModal({ title = "Conferma", message = "Confermi?" } = {}) {
  return new Promise(resolve => {
    const modal = $("#confirmModal");
    $("#confirmTitle").textContent = title;
    $("#confirmMessage").innerHTML = message; // richiama testo con <em> etc.
    modal.hidden = false;
    const ok = $("#confirmOk");
    const cleanup = () => {
      modal.hidden = true;
      ok.removeEventListener("click", onOk);
      $$("[data-close-confirm]").forEach(el => el.removeEventListener("click", onCancel));
    };
    const onOk = () => { cleanup(); resolve(true); };
    const onCancel = () => { cleanup(); resolve(false); };
    ok.addEventListener("click", onOk);
    $$("[data-close-confirm]").forEach(el => el.addEventListener("click", onCancel));
  });
}

// ============================================================
// RULES CATALOG (Certify tab)
// ============================================================

let allRules = [];           // [{ruleId, title, severity, source}]
const disabledRules = new Set();

async function loadRules() {
  const tbody = $("#rulesTable");
  try {
    const rules = await api("/api/rules");
    allRules = Array.isArray(rules) ? rules.map(r => ({
      ruleId:   r.ruleId || r.id || r.code || "?",
      title:    r.title  || r.name || "",
      severity: r.severity || r.sev || "info",
      source:   r.source || "",
    })) : [];
    $("#rulesCount").textContent = `(${allRules.length})`;
    renderRulesTable();
  } catch (e) {
    tbody.innerHTML = `<tr><td colspan="5" class="muted">errore: ${escapeHtml(e.message)}</td></tr>`;
  }
}

function renderRulesTable(filter = "") {
  const tbody = $("#rulesTable");
  if (!allRules.length) { tbody.innerHTML = '<tr><td colspan="5" class="muted">nessuna regola</td></tr>'; return; }
  const f = filter.trim().toLowerCase();
  const filtered = f
    ? allRules.filter(r => [r.ruleId, r.title, r.severity, r.source].join(" ").toLowerCase().includes(f))
    : allRules;
  if (!filtered.length) { tbody.innerHTML = '<tr><td colspan="5" class="muted">nessuna corrispondenza</td></tr>'; return; }
  tbody.innerHTML = filtered.map(r => {
    const enabled = !disabledRules.has(r.ruleId);
    return `<tr data-rule="${escapeHtml(r.ruleId)}" class="${enabled ? "" : "dim"}">
      <td class="center"><input type="checkbox" data-rule-toggle="${escapeHtml(r.ruleId)}" ${enabled ? "checked" : ""}/></td>
      <td class="rule-id">${escapeHtml(r.ruleId)}</td>
      <td>${escapeHtml(r.title)}</td>
      <td><span class="sev ${sevClass(r.severity)}">${escapeHtml(r.severity)}</span></td>
      <td><code>${escapeHtml(r.source)}</code></td>
    </tr>`;
  }).join("");
  $$("[data-rule-toggle]", tbody).forEach(cb => {
    cb.addEventListener("change", () => {
      const id = cb.dataset.ruleToggle;
      if (cb.checked) disabledRules.delete(id);
      else disabledRules.add(id);
      cb.closest("tr").classList.toggle("dim", !cb.checked);
    });
  });
}

$("#rulesFilter").addEventListener("input", e => renderRulesTable(e.target.value));
$("#rulesEnableAll").addEventListener("click", () => {
  disabledRules.clear();
  renderRulesTable($("#rulesFilter").value);
});
$("#rulesDisableAll").addEventListener("click", () => {
  allRules.forEach(r => disabledRules.add(r.ruleId));
  renderRulesTable($("#rulesFilter").value);
});

function scrollToRule(ruleId) {
  const details = $("details.catalog");
  if (details && !details.open) details.open = true;
  // clear filter so the rule is visible
  $("#rulesFilter").value = "";
  renderRulesTable("");
  const row = document.querySelector(`#rulesTable tr[data-rule="${CSS.escape(ruleId)}"]`);
  if (row) {
    row.scrollIntoView({ behavior: "smooth", block: "center" });
    row.classList.add("highlight");
    setTimeout(() => row.classList.remove("highlight"), 1600);
  }
}

// ============================================================
// CERTIFY
// ============================================================

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
  if (disabledRules.size) opt.disabledRules = Array.from(disabledRules);
  // persist thresholds
  mergePrefs({ validationOptions: opt });
  return Object.keys(opt).length ? opt : undefined;
}

function restoreValidationOptions() {
  const p = loadPrefs();
  const opt = p.validationOptions || {};
  const set = (id, key) => { if (opt[key] != null) $(id).value = opt[key]; };
  set("#opt-maxOutputColumns", "maxOutputColumns");
  set("#opt-maxJoinedTables", "maxJoinedTables");
  set("#opt-maxDerivedColumns", "maxDerivedColumns");
  set("#opt-maxEstimatedRows", "maxEstimatedRows");
  set("#opt-maxTablesWithoutAlias", "maxTablesWithoutAlias");
  set("#opt-maxStatementLines", "maxStatementLines");
}

function scoreBoxClass(score) {
  if (typeof score !== "number") return "";
  if (score >= 80) return "score-ok";
  if (score >= 50) return "score-warn";
  return "score-bad";
}

function renderSeverityBars(counts) {
  const host = $("#vSeverityBars");
  const total = Object.values(counts).reduce((a, b) => a + Number(b || 0), 0);
  if (total === 0) { host.innerHTML = '<p class="muted small">nessun finding</p>'; return; }
  host.innerHTML = SEV_ORDER.map(sev => {
    const n = Number(counts[sev] || 0);
    if (n === 0) return "";
    const pct = Math.max(2, Math.round((n / total) * 100));
    return `<div class="sev-bar-row">
      <span class="sev-bar-label" style="color:var(--sev-${sev})">${sev}</span>
      <div class="sev-bar-track"><div class="sev-bar-fill ${sevClass(sev)}" style="width:${pct}%"></div></div>
      <span class="sev-bar-count">${n}</span>
    </div>`;
  }).join("");
}

function renderValidation(report) {
  showOutput("validateOutput", "certifyEmpty");

  const score = (typeof report.score === "number") ? report.score : null;
  const scoreBox = $(".score-box");
  scoreBox.className = "score-box " + scoreBoxClass(score);
  $("#vScore").textContent = score != null ? score : "--";

  const cert = !!report.certifiable;
  const badge = $("#vBadge");
  badge.textContent = cert ? "CERTIFICATA" : "NON CERTIFICATA";
  badge.className = "cert-badge " + (cert ? "ok" : "ko");

  const counts = report.countsBySeverity || {};
  const total = Object.values(counts).reduce((a, b) => a + Number(b || 0), 0);
  $("#vSummaryText").textContent =
    total === 0
      ? "Nessun finding rilevato."
      : `${total} finding rilevat${total === 1 ? "o" : "i"} su ${allRules.length || "?"} regole applicate.`;

  renderSeverityBars(counts);

  const findings = Array.isArray(report.findings) ? report.findings.slice() : [];
  findings.sort((a, b) => (SEV_WEIGHT[sevKey(b.severity)] || 0) - (SEV_WEIGHT[sevKey(a.severity)] || 0));
  const tbody = $("#vFindings");
  if (findings.length === 0) {
    tbody.innerHTML = '<tr><td colspan="4" class="muted">query pulita, nessun finding</td></tr>';
    return;
  }
  tbody.innerHTML = findings.map((f, i) => {
    const lc = (f.line != null && f.column != null) ? `${f.line}:${f.column}`
             : (f.line != null ? `${f.line}` : "");
    return `<tr class="finding-row" data-finding="${i}">
      <td><span class="sev ${sevClass(f.severity)}">${escapeHtml(f.severity)}</span></td>
      <td><span class="rule-link" data-open-finding="${i}">${escapeHtml(f.ruleId)}</span>
          <div class="muted small">${escapeHtml(f.title || "")}</div></td>
      <td class="muted">${escapeHtml(lc)}</td>
      <td>${escapeHtml(f.message || "")}</td>
    </tr>`;
  }).join("");

  // wire expand/collapse
  $$("[data-open-finding]", tbody).forEach(el => {
    el.addEventListener("click", (ev) => {
      ev.stopPropagation();
      const idx = +el.dataset.openFinding;
      toggleFindingDetail(idx, findings[idx]);
    });
  });
  $$(".finding-row", tbody).forEach(tr => {
    tr.addEventListener("click", () => {
      const idx = +tr.dataset.finding;
      toggleFindingDetail(idx, findings[idx]);
    });
  });
}

function toggleFindingDetail(idx, f) {
  const row = document.querySelector(`.finding-row[data-finding="${idx}"]`);
  if (!row) return;
  const next = row.nextElementSibling;
  if (next && next.classList.contains("finding-detail-row")) {
    next.remove(); return;
  }
  const parts = [];
  if (f.snippet)       parts.push(`<div><span class="muted small">Snippet SQL:</span><div class="snippet">${escapeHtml(f.snippet)}</div></div>`);
  if (f.fixHint)       parts.push(`<div class="fix"><strong>Fix consigliato:</strong> ${escapeHtml(f.fixHint)}</div>`);
  if (f.justification) parts.push(`<div class="just"><strong>Justify:</strong> ${escapeHtml(f.justification)}</div>`);
  parts.push(`<div class="explain-link"><span class="rule-link" data-scroll-rule="${escapeHtml(f.ruleId)}">Spiega ${escapeHtml(f.ruleId)} nel catalogo &rarr;</span></div>`);
  const detail = document.createElement("tr");
  detail.className = "finding-detail-row";
  detail.innerHTML = `<td colspan="4"><div class="detail-grid">${parts.join("")}</div></td>`;
  row.after(detail);
  detail.querySelector("[data-scroll-rule]")?.addEventListener("click", () => scrollToRule(f.ruleId));
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
  $("#certifyEmpty").hidden = false;
});

// ============================================================
// ANALYZE
// ============================================================

function renderStatements(statements) {
  const host = $("#analyzeStatements");
  if (!Array.isArray(statements) || !statements.length) {
    host.innerHTML = '<p class="muted">nessuno statement.</p>'; return;
  }
  host.innerHTML = statements.map((s, idx) => {
    const findings = Array.isArray(s.findings) ? s.findings : [];
    const ops = Array.isArray(s.topOperators) ? s.topOperators
              : Array.isArray(s.operators) ? s.operators : [];
    const kv = [];
    if (s.statementType) kv.push(["Tipo", s.statementType]);
    if (typeof s.estimatedRows === "number") kv.push(["Righe stimate", s.estimatedRows]);
    if (typeof s.estimatedCost === "number") kv.push(["Costo stimato", s.estimatedCost]);
    if (typeof s.actualRows === "number")    kv.push(["Righe effettive", s.actualRows]);
    const kvHtml = kv.length
      ? `<dl class="kv">${kv.map(([k, v]) => `<dt>${escapeHtml(k)}</dt><dd>${escapeHtml(String(v))}</dd>`).join("")}</dl>`
      : '<p class="muted small">nessun dettaglio numerico</p>';

    const findingsHtml = findings.length
      ? `<table><thead><tr><th style="width:100px;">Severity</th><th>Titolo / spiegazione</th></tr></thead>
         <tbody>${findings.map(f => `<tr>
           <td><span class="sev ${sevClass(f.severity)}">${escapeHtml(f.severity || "info")}</span></td>
           <td><strong>${escapeHtml(f.title || f.ruleId || "")}</strong>
               <div class="muted small">${escapeHtml(f.message || "")}</div>
               ${f.fixHint ? `<div class="small" style="color:var(--ok)">${escapeHtml(f.fixHint)}</div>` : ""}
           </td>
         </tr>`).join("")}</tbody></table>`
      : '<p class="muted small">nessun finding.</p>';

    const opsHtml = ops.length
      ? `<table><thead><tr><th>Nodo</th><th>Tipo</th><th style="width:110px;">Est.</th><th style="width:110px;">Actual</th><th style="width:80px;">Cost %</th></tr></thead>
         <tbody>${ops.map(o => `<tr>
           <td>${escapeHtml(o.node || o.name || o.id || "")}</td>
           <td>${escapeHtml(o.type || o.physicalOp || "")}</td>
           <td>${escapeHtml(o.estimatedRows ?? o.est ?? "")}</td>
           <td>${escapeHtml(o.actualRows ?? o.actual ?? "")}</td>
           <td>${escapeHtml(o.costPercent ?? o.cost ?? "")}</td>
         </tr>`).join("")}</tbody></table>`
      : '<p class="muted small">nessun operatore riportato.</p>';

    const rawTxt = s.rawText || s.text || "";
    const rawHtml = rawTxt
      ? `<details><summary>Piano completo</summary><pre class="code">${escapeHtml(rawTxt)}</pre></details>`
      : "";

    return `<div class="statement-card">
      <div class="head"><strong>Statement #${idx + 1}</strong>
        <span class="badge">${escapeHtml(s.label || s.title || "")}</span></div>
      <details open><summary>Sintesi</summary>${kvHtml}</details>
      <details open><summary>Warning e findings</summary>${findingsHtml}</details>
      <details><summary>Top operatori</summary>${opsHtml}</details>
      ${rawHtml}
    </div>`;
  }).join("");
}

let lastAnalysisText = "";

async function doAnalyze() {
  const file = $("#analyzeFile").files[0];
  if (!file) { showError("Seleziona un file piano."); return; }
  const level = $("#analyzeLevel").value || "beginner";
  const btn = $("#btnAnalyze"); btn.disabled = true;
  try {
    const planXml = await readFileAsText(file);
    const res = await api("/api/analyze", { method: "POST", body: { planXml, level } });
    showOutput("analyzeOutput", "analyzeEmpty");

    lastAnalysisText = res.text || "";
    $("#analyzeText").textContent = lastAnalysisText || "(vuoto)";

    const stmts = Array.isArray(res.statements) ? res.statements : [];
    $("#anStmtCount").textContent  = stmts.length || "0";
    $("#anTotalCost").textContent  = (res.totalCost != null) ? String(res.totalCost) : (stmts.reduce((s, st) => s + (Number(st.estimatedCost) || 0), 0).toFixed(3) || "–");
    $("#anPlanKind").textContent   = res.planKind || res.type || (stmts.some(s => s.actualRows != null) ? "actual" : "estimated");
    $("#anServer").textContent     = res.serverBuild || res.serverVersion || "n/d";
    renderStatements(stmts);
  } catch (e) { showError(e.message); }
  finally { btn.disabled = false; }
}
$("#btnAnalyze").addEventListener("click", doAnalyze);
$("#btnDownloadAnalysis").addEventListener("click", () => {
  const blob = new Blob([lastAnalysisText || "(vuoto)"], { type: "text/plain;charset=utf-8" });
  const url = URL.createObjectURL(blob);
  const a = document.createElement("a");
  a.href = url;
  a.download = `planalyzer-analysis-${Date.now()}.txt`;
  document.body.appendChild(a);
  a.click();
  a.remove();
  URL.revokeObjectURL(url);
});

// ============================================================
// COMPARE (estimated vs actual)
// ============================================================

async function doCompare() {
  const est = $("#estimatedFile").files[0];
  const act = $("#actualFile").files[0];
  if (!est || !act) { showError("Seleziona entrambi i file."); return; }
  const btn = $("#btnCompare"); btn.disabled = true;
  try {
    const [estimatedXml, actualXml] = await Promise.all([readFileAsText(est), readFileAsText(act)]);
    const res = await api("/api/compare", { method: "POST", body: { estimatedXml, actualXml } });
    showOutput("compareOutput", "compareEmpty");

    const shapeMatch = (res.shapeMatch != null) ? !!res.shapeMatch
                     : (res.shape && res.shape.match != null) ? !!res.shape.match
                     : null;
    const banner = $("#compareBanner");
    if (shapeMatch === true) {
      banner.className = "banner ok";
      banner.innerHTML = `<div>Shape coincidente</div><div class="banner-sub">Struttura del piano identica tra stimato ed effettivo.</div>`;
    } else if (shapeMatch === false) {
      banner.className = "banner ko";
      banner.innerHTML = `<div>Shape differente</div><div class="banner-sub">${escapeHtml(res.shapeDetail || res.shapeReason || "Il piano effettivo ha una struttura diversa da quella stimata.")}</div>`;
    } else {
      banner.className = "banner";
      banner.innerHTML = `<div>Confronto completato</div>`;
    }

    $("#compareText").textContent = res.text || "(vuoto)";

    const rows = Array.isArray(res.rows) ? res.rows
               : Array.isArray(res.diffs) ? res.diffs
               : Array.isArray(res.nodes) ? res.nodes : [];
    const tbody = $("#compareRows");
    if (!rows.length) {
      tbody.innerHTML = '<tr><td colspan="4" class="muted">nessun diff strutturato</td></tr>';
    } else {
      tbody.innerHTML = rows.map(r => {
        const skew = (typeof r.skew === "number") ? r.skew
                   : (typeof r.ratio === "number") ? r.ratio : null;
        let rowCls = "", cellCls = "";
        if (skew != null) {
          if (skew >= 10) { rowCls = "skew-row-high"; cellCls = "skew-high"; }
          else if (skew >= 2) { rowCls = "skew-row-med"; cellCls = "skew-med"; }
        }
        const skewStr = skew != null ? `${Math.round(skew * 100) / 100}x` : "";
        return `<tr class="${rowCls}">
          <td>${escapeHtml(r.node || r.label || r.operator || r.name || "")}</td>
          <td>${escapeHtml(r.estimated ?? r.estimatedRows ?? "")}</td>
          <td>${escapeHtml(r.actual ?? r.actualRows ?? "")}</td>
          <td class="${cellCls}">${escapeHtml(skewStr)}</td>
        </tr>`;
      }).join("");
    }
  } catch (e) { showError(e.message); }
  finally { btn.disabled = false; }
}
$("#btnCompare").addEventListener("click", doCompare);

// ============================================================
// MULTIDB
// ============================================================

const variants = []; // { label, planXml, fileName }

function updateVariantCounter() {
  $("#variantCounter").textContent = `Piani caricati: ${variants.length} / 2+`;
  $("#btnRunMulti").disabled = variants.length < 2;
}

function refreshVariantUi() {
  const host = $("#variantList");
  if (!variants.length) {
    host.innerHTML = "";
    $("#multiEmpty").hidden = false;
  } else {
    $("#multiEmpty").hidden = true;
    host.innerHTML = variants.map((v, i) => `
      <div class="variant-item">
        <div class="v-label-wrap">
          <span>Etichetta</span>
          <input type="text" data-vlabel="${i}" value="${escapeHtml(v.label)}"/>
        </div>
        <span class="file-name">${escapeHtml(v.fileName || "(nessun file)")}</span>
        <span class="status ${v.planXml ? "ok" : "ko"}">${v.planXml ? "caricato" : "vuoto"}</span>
        <button class="btn ghost small" data-vrm="${i}">rimuovi</button>
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
  updateVariantCounter();
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
$("#btnClearVariants").addEventListener("click", () => {
  variants.length = 0;
  refreshVariantUi();
  $("#multiOutput").hidden = true;
});

// tiny SQL keyword highlighter (safe: operates on already-escaped HTML)
const SQL_KW = /\b(SELECT|FROM|WHERE|JOIN|LEFT|RIGHT|INNER|OUTER|CROSS|ON|AND|OR|NOT|IN|EXISTS|GROUP|BY|ORDER|HAVING|UNION|ALL|DISTINCT|INSERT|UPDATE|DELETE|MERGE|CREATE|ALTER|DROP|TABLE|INDEX|VIEW|WITH|AS|CASE|WHEN|THEN|ELSE|END|IS|NULL|SET|VALUES|INTO|TOP|OPTION|DECLARE|BEGIN|COMMIT|ROLLBACK|TRANSACTION|IF|WHILE|RETURN|OUTPUT)\b/gi;
function highlightSql(text) {
  let s = escapeHtml(text);
  s = s.replace(/('([^']|'')*')/g, '<span class="str">$1</span>');
  s = s.replace(/(--[^\n]*)/g, '<span class="cmt">$1</span>');
  s = s.replace(SQL_KW, m => `<span class="kw">${m}</span>`);
  s = s.replace(/\b(\d+(\.\d+)?)\b/g, '<span class="num">$1</span>');
  return s;
}

async function doMulti() {
  if (variants.length < 2) { showError("Servono almeno due varianti."); return; }
  const btn = $("#btnRunMulti"); btn.disabled = true;
  try {
    const body = { variants: variants.map(v => ({ label: v.label, planXml: v.planXml })) };
    const res = await api("/api/multidb", { method: "POST", body });
    showOutput("multiOutput", "multiEmpty");
    $("#multiText").textContent = res.text || "(vuoto)";

    const variantsOut = Array.isArray(res.variants) ? res.variants : [];
    $("#multiVariants").innerHTML = variantsOut.length
      ? variantsOut.map(v => `<div class="statement-card">
          <div class="head"><strong>${escapeHtml(v.label || "?")}</strong>
            <span class="badge">${escapeHtml(v.summary || "")}</span></div>
          ${v.notes ? `<p class="small">${escapeHtml(v.notes)}</p>` : ""}
          ${Array.isArray(v.findings) && v.findings.length ? `<ul>${v.findings.map(f =>
            `<li><span class="sev ${sevClass(f.severity)}">${escapeHtml(f.severity || "info")}</span> ${escapeHtml(f.message || f.title || "")}</li>`
          ).join("")}</ul>` : ""}
        </div>`).join("")
      : '<p class="muted">nessun dettaglio per variante</p>';

    const common = Array.isArray(res.commonRecommendations) ? res.commonRecommendations
                 : Array.isArray(res.common) ? res.common : [];
    const div = Array.isArray(res.divergentRecommendations) ? res.divergentRecommendations
              : Array.isArray(res.divergent) ? res.divergent : [];
    $("#multiCommon").innerHTML = common.length ? common.map(s => `<li>${escapeHtml(typeof s === "string" ? s : JSON.stringify(s))}</li>`).join("") : '<li class="muted small">nessuna</li>';
    $("#multiDiverge").innerHTML = div.length ? div.map(s => `<li>${escapeHtml(typeof s === "string" ? s : JSON.stringify(s))}</li>`).join("") : '<li class="muted small">nessuna</li>';

    const suggested = res.suggestedSql || res.sql || "";
    $("#multiSql").innerHTML = suggested ? highlightSql(suggested) : "(nessun SQL suggerito)";
  } catch (e) { showError(e.message); }
  finally { btn.disabled = false; }
}
$("#btnRunMulti").addEventListener("click", doMulti);
refreshVariantUi();

// ============================================================
// RUN & HISTORY
// ============================================================

let historyItems = []; // ultima history caricata
const selectedRevs = new Set(); // rev ids per confronto

function renderRunRevision(rev) {
  if (!rev) { $("#runRevision").innerHTML = '<p class="muted">nessuna revisione</p>'; return; }
  const fields = [
    ["Rev",             rev.revision ?? rev.rev ?? rev.id ?? "?"],
    ["Timestamp",       rev.timestamp || rev.createdAt || ""],
    ["Mode",            rev.mode || ""],
    ["Durata (ms)",     rev.durationMs ?? rev.duration ?? ""],
    ["CPU",             rev.cpu ?? rev.cpuTime ?? ""],
    ["Logical reads",   rev.logicalReads ?? rev.lr ?? ""],
    ["Physical reads",  rev.physicalReads ?? rev.pr ?? ""],
    ["Rows",            rev.rows ?? rev.rowCount ?? ""],
    ["Note",            rev.note || ""],
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
  const boxCls = scoreBoxClass(typeof report.score === "number" ? report.score : null);
  const countsHtml = Object.entries(counts).map(([k, v]) =>
    `<span class="sev ${sevClass(k)}">${escapeHtml(k)} ${escapeHtml(v)}</span>`
  ).join(" ");
  const findHtml = findings.length
    ? `<div class="table-wrap"><table class="findings"><thead><tr><th style="width:110px;">Severity</th><th style="width:130px;">Regola</th><th>Messaggio</th></tr></thead><tbody>${
        findings.map(f => `<tr>
          <td><span class="sev ${sevClass(f.severity)}">${escapeHtml(f.severity)}</span></td>
          <td><code>${escapeHtml(f.ruleId)}</code></td>
          <td>${escapeHtml(f.message || "")}</td>
        </tr>`).join("")
      }</tbody></table></div>`
    : '<p class="muted">nessun finding</p>';
  $("#runValidation").innerHTML = `
    <div class="cert-summary">
      <div class="score-box ${boxCls}"><div class="score">${escapeHtml(score)}</div><div class="score-label">Score</div></div>
      <div class="score-details">
        <div class="cert-badge ${cert ? "ok" : "ko"}">${cert ? "CERTIFICATA" : "NON CERTIFICATA"}</div>
        <div>${countsHtml}</div>
      </div>
    </div>
    ${findHtml}`;
}

function collectRunBody(extra = {}) {
  return {
    connectionString:        $("#runConn").value.trim(),
    slug:                    $("#runSlug").value.trim(),
    sql:                     $("#runSql").value,
    note:                    $("#runNote").value,
    mode:                    $("#runMode").value,
    historyConnectionString: $("#runHistoryConn").value.trim(),
    ...extra,
  };
}

function persistRunPrefs() {
  mergePrefs({
    connectionString:        $("#runConn").value.trim(),
    historyConnectionString: $("#runHistoryConn").value.trim(),
    slug:                    $("#runSlug").value.trim(),
  });
}

async function doRun() {
  const body = collectRunBody();
  if (!body.connectionString || !body.slug || !body.sql) {
    showError("Connection string, slug e SQL sono obbligatori.");
    return;
  }
  persistRunPrefs();
  const btn = $("#btnRun"); btn.disabled = true;
  try {
    const res = await api("/api/run", { method: "POST", body });
    showOutput("runOutput");
    renderRunRevision(res.revision);
    renderRunValidation(res.validation);
    const analysis = res.analysis && (res.analysis.text || res.analysis);
    $("#runAnalysis").textContent = (typeof analysis === "string" ? analysis : JSON.stringify(analysis, null, 2)) || "(nessuna analisi)";
    await loadHistory();
  } catch (e) { showError(e.message); }
  finally { btn.disabled = false; }
}

async function doRunValidateOnly() {
  const sql = $("#runSql").value.trim();
  if (!sql) { showError("Inserisci il SQL da validare."); return; }
  const btn = $("#btnRunValidateOnly"); btn.disabled = true;
  try {
    const body = { sql };
    const options = readValidationOptions();
    if (options) body.options = options;
    const report = await api("/api/validate", { method: "POST", body });
    showOutput("runOutput");
    $("#runRevision").innerHTML = '<p class="muted">solo validazione — nessuna esecuzione contro il DB.</p>';
    renderRunValidation(report);
    $("#runAnalysis").textContent = "(nessuna analisi, esecuzione non richiesta)";
  } catch (e) { showError(e.message); }
  finally { btn.disabled = false; }
}

async function loadHistory() {
  const slug = $("#runSlug").value.trim();
  const conn = $("#runHistoryConn").value.trim();
  const tbody = $("#historyRows");
  if (!slug) {
    tbody.innerHTML = '<tr><td colspan="7" class="muted">imposta uno slug per caricare la history</td></tr>';
    historyItems = []; selectedRevs.clear(); refreshCompareRevsButton();
    return;
  }
  persistRunPrefs();
  try {
    const qs = conn ? `?historyConnectionString=${encodeURIComponent(conn)}` : "";
    const list = await api(`/api/history/${encodeURIComponent(slug)}${qs}`);
    const items = Array.isArray(list) ? list
                 : Array.isArray(list && list.revisions) ? list.revisions
                 : [];
    historyItems = items;
    selectedRevs.clear(); refreshCompareRevsButton();
    if (!items.length) {
      tbody.innerHTML = '<tr><td colspan="7" class="muted">nessuna revisione</td></tr>';
      return;
    }
    tbody.innerHTML = items.map((r, idx) => {
      const rev = r.revision ?? r.rev ?? idx;
      const durMs = r.durationMs ?? r.duration;
      const dur = durMs != null ? `${durMs} ms` : "";
      return `<tr class="clickable" data-idx="${idx}">
        <td onclick="event.stopPropagation()"><input type="checkbox" class="rev-checkbox" data-rev-sel="${escapeHtml(rev)}"/></td>
        <td>${escapeHtml(rev)}</td>
        <td>${escapeHtml(r.timestamp || r.createdAt || "")}</td>
        <td>${escapeHtml(dur)}</td>
        <td>${escapeHtml(r.logicalReads ?? r.lr ?? "")}</td>
        <td>${escapeHtml(r.rows ?? r.rowCount ?? "")}</td>
        <td>${escapeHtml(r.note || "")}</td>
      </tr>`;
    }).join("");
    $$("#historyRows tr.clickable").forEach(tr => {
      tr.addEventListener("click", () => {
        const idx = +tr.dataset.idx;
        openRevPanel(historyItems[idx]);
      });
    });
    $$("#historyRows [data-rev-sel]").forEach(cb => {
      cb.addEventListener("change", () => {
        const id = cb.dataset.revSel;
        if (cb.checked) selectedRevs.add(id);
        else selectedRevs.delete(id);
        cb.closest("tr").classList.toggle("selected", cb.checked);
        refreshCompareRevsButton();
      });
    });
  } catch (e) { showError(e.message); }
}

function refreshCompareRevsButton() {
  const n = selectedRevs.size;
  const btn = $("#btnCompareRevs");
  btn.textContent = `Confronta selezionate (${n}/2)`;
  btn.disabled = n !== 2;
}

$("#btnRun").addEventListener("click", doRun);
$("#btnRunValidateOnly").addEventListener("click", doRunValidateOnly);
$("#btnLoadHistory").addEventListener("click", loadHistory);

// SQL char/line counter
$("#runSql").addEventListener("input", () => {
  const v = $("#runSql").value;
  const lines = v ? v.split("\n").length : 0;
  $("#sqlCounter").textContent = `${lines} righe · ${v.length} caratteri`;
});

// Persist connection/slug on blur
["#runConn", "#runHistoryConn", "#runSlug"].forEach(sel => {
  $(sel).addEventListener("change", persistRunPrefs);
});

// ============================================================
// SIDE PANEL for revision detail
// ============================================================

let activeRev = null;
function openRevPanel(rev) {
  activeRev = rev;
  const revId = rev.revision ?? rev.rev ?? "?";
  const when = rev.timestamp || rev.createdAt || "";
  $("#revPanelTitle").textContent = `Revisione #${revId}${when ? " — " + when : ""}`;
  $("#revPanel").hidden = false;
  $("#revBackdrop").hidden = false;

  $("#revMeta").innerHTML = `<dl class="kv">
    <dt>Mode</dt><dd>${escapeHtml(rev.mode || "")}</dd>
    <dt>Durata</dt><dd>${escapeHtml(String(rev.durationMs ?? rev.duration ?? ""))} ms</dd>
    <dt>CPU</dt><dd>${escapeHtml(String(rev.cpu ?? rev.cpuTime ?? ""))}</dd>
    <dt>Logical reads</dt><dd>${escapeHtml(String(rev.logicalReads ?? rev.lr ?? ""))}</dd>
    <dt>Physical reads</dt><dd>${escapeHtml(String(rev.physicalReads ?? rev.pr ?? ""))}</dd>
    <dt>Rows</dt><dd>${escapeHtml(String(rev.rows ?? rev.rowCount ?? ""))}</dd>
    <dt>Note</dt><dd>${escapeHtml(rev.note || "")}</dd>
  </dl>`;
  $("#revSql").textContent = rev.sql || "(SQL non incluso nella history di questa revisione)";
}
function closeRevPanel() {
  $("#revPanel").hidden = true;
  $("#revBackdrop").hidden = true;
  activeRev = null;
}
$("#revPanelClose").addEventListener("click", closeRevPanel);
$("#revBackdrop").addEventListener("click", closeRevPanel);
document.addEventListener("keydown", e => {
  if (e.key === "Escape") {
    if (!$("#revPanel").hidden) closeRevPanel();
    else if (!$("#helpModal").hidden) closeHelp();
    else if (!$("#confirmModal").hidden) $("#confirmModal").hidden = true;
  }
});

$("#btnCopyRevSql").addEventListener("click", async () => {
  const txt = $("#revSql").textContent;
  try {
    await navigator.clipboard.writeText(txt);
    const b = $("#btnCopyRevSql");
    const old = b.textContent; b.textContent = "Copiato";
    setTimeout(() => { b.textContent = old; }, 1200);
  } catch { showError("Copia non riuscita."); }
});

$("#btnLoadRevSql").addEventListener("click", () => {
  if (!activeRev) return;
  if (activeRev.sql) {
    $("#runSql").value = activeRev.sql;
    $("#runSql").dispatchEvent(new Event("input"));
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
  const conn = $("#runHistoryConn").value.trim();
  if (!slug) { showError("Imposta slug prima del rollback."); return; }
  const ok = await confirmModal({
    title: "Confermi rollback?",
    message: `Il rollback crea una <strong>NUOVA revisione</strong> di <code>${escapeHtml(slug)}</code> con lo stesso SQL della revisione <code>#${escapeHtml(to)}</code>. <br/><em>Nulla viene eliminato dalla cronologia.</em>`
  });
  if (!ok) return;
  try {
    const body = { slug, to };
    if (conn) body.historyConnectionString = conn;
    await api("/api/rollback", { method: "POST", body });
    closeRevPanel();
    await loadHistory();
  } catch (e) { showError(e.message); }
});

// ============================================================
// DIFF between two revisions
// ============================================================

function computeDiff(a, b) {
  // simple LCS-based line diff — sufficient for SQL confronto.
  const aL = a.split("\n"), bL = b.split("\n");
  const n = aL.length, m = bL.length;
  const dp = Array(n + 1).fill(null).map(() => Array(m + 1).fill(0));
  for (let i = n - 1; i >= 0; i--)
    for (let j = m - 1; j >= 0; j--)
      dp[i][j] = aL[i] === bL[j] ? dp[i+1][j+1] + 1 : Math.max(dp[i+1][j], dp[i][j+1]);

  const outA = [], outB = [];
  let i = 0, j = 0;
  while (i < n && j < m) {
    if (aL[i] === bL[j]) { outA.push({ t: "same", s: aL[i] }); outB.push({ t: "same", s: bL[j] }); i++; j++; }
    else if (dp[i+1][j] >= dp[i][j+1]) { outA.push({ t: "del", s: aL[i] }); outB.push({ t: "same", s: "" }); i++; }
    else { outA.push({ t: "same", s: "" }); outB.push({ t: "add", s: bL[j] }); j++; }
  }
  while (i < n) { outA.push({ t: "del", s: aL[i] }); outB.push({ t: "same", s: "" }); i++; }
  while (j < m) { outA.push({ t: "same", s: "" }); outB.push({ t: "add", s: bL[j] }); j++; }
  return { a: outA, b: outB };
}
function renderDiffCol(lines) {
  return lines.map(l => `<span class="diff-line diff-${l.t}">${escapeHtml(l.s || " ")}</span>`).join("\n");
}

$("#btnCompareRevs").addEventListener("click", () => {
  if (selectedRevs.size !== 2) return;
  const [idA, idB] = Array.from(selectedRevs);
  const byId = (r) => String(r.revision ?? r.rev) === String(idA) || String(r.revision ?? r.rev) === String(idB);
  const picked = historyItems.filter(byId);
  if (picked.length !== 2) { showError("Impossibile recuperare le due revisioni."); return; }
  // sort by rev asc so A = più vecchia
  picked.sort((x, y) => Number(x.revision ?? x.rev) - Number(y.revision ?? y.rev));
  const a = picked[0], b = picked[1];
  const sqlA = a.sql || "(SQL non incluso)";
  const sqlB = b.sql || "(SQL non incluso)";
  const diff = computeDiff(sqlA, sqlB);

  $("#revDiffHead").innerHTML = `Confronto: <strong>#${escapeHtml(a.revision ?? a.rev)}</strong> (${escapeHtml(a.timestamp || "")}) &rarr; <strong>#${escapeHtml(b.revision ?? b.rev)}</strong> (${escapeHtml(b.timestamp || "")})`;
  $("#revDiffTitleA").textContent = `Revisione #${a.revision ?? a.rev} (precedente)`;
  $("#revDiffTitleB").textContent = `Revisione #${b.revision ?? b.rev} (più recente)`;
  $("#revDiffA").innerHTML = renderDiffCol(diff.a);
  $("#revDiffB").innerHTML = renderDiffCol(diff.b);
  showOutput("revDiffOutput");
  $("#revDiffOutput").scrollIntoView({ behavior: "smooth", block: "start" });
});

$("#btnClearRevSel").addEventListener("click", () => {
  selectedRevs.clear();
  $$("#historyRows [data-rev-sel]").forEach(cb => { cb.checked = false; cb.closest("tr").classList.remove("selected"); });
  refreshCompareRevsButton();
});

// ============================================================
// bootstrap
// ============================================================

function restorePrefs() {
  const p = loadPrefs();
  if (p.connectionString)        $("#runConn").value = p.connectionString;
  if (p.historyConnectionString) $("#runHistoryConn").value = p.historyConnectionString;
  if (p.slug)                    $("#runSlug").value = p.slug;
  restoreValidationOptions();
}

restorePrefs();
activateTab("certify");
pingHealth();
loadRules();

// tiny debug surface
export const Planalyzer = {
  api, activateTab, loadRules, loadHistory, openHelp, computeDiff,
};
