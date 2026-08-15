namespace IcomRigControl.Services;

/// <summary>
/// The self-contained mobile web page served by WebRemoteServer — a single HTML
/// document with inline CSS and JavaScript, no external files or CDNs (so it works
/// on an isolated/offline network, e.g. a Pi in the field). It opens a WebSocket
/// back to the same host, shows live frequency/mode/meters, and sends tuning, mode,
/// and PTT commands. Scope and audio arrive in later milestones. See CLAUDE.md web remote.
/// </summary>
public static class WebRemotePage
{
    public const string Html = """
<!DOCTYPE html>
<html lang="en">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1, maximum-scale=1, user-scalable=no">
<title>IcomRigControl Remote</title>
<style>
  :root { --bg:#141414; --panel:#0d0d0d; --line:#2a2a2a; --grn:#00ff88; --blu:#88ccff; --red:#ff4444; --txt:#e6e6e6; --dim:#888; }
  * { box-sizing:border-box; -webkit-tap-highlight-color:transparent; }
  body { margin:0; background:var(--bg); color:var(--txt); font-family:-apple-system,Segoe UI,Roboto,sans-serif; padding:12px; }
  h1 { font-size:15px; font-weight:600; margin:0 0 10px; color:var(--blu); display:flex; justify-content:space-between; align-items:center; }
  .dot { width:10px; height:10px; border-radius:50%; background:var(--red); display:inline-block; }
  .dot.ok { background:var(--grn); }
  .panel { background:var(--panel); border:1px solid var(--line); border-radius:10px; padding:14px; margin-bottom:12px; }
  .freq { font-family:'Courier New',monospace; font-size:min(13vw,52px); font-weight:700; color:var(--grn); text-align:center; letter-spacing:1px; }
  .mhz { text-align:center; color:var(--dim); font-size:12px; margin-top:2px; }
  .row { display:flex; gap:8px; flex-wrap:wrap; }
  button { flex:1; min-width:52px; background:#232323; color:var(--txt); border:1px solid var(--line); border-radius:8px; padding:12px 6px; font-size:15px; cursor:pointer; }
  button:active { background:var(--grn); color:#000; }
  button.sel { background:var(--blu); color:#000; font-weight:700; }
  .tune button { font-size:18px; font-weight:700; }
  .meter { margin:8px 0; }
  .meter .lbl { display:flex; justify-content:space-between; font-size:12px; color:var(--dim); margin-bottom:3px; }
  .bar { height:12px; background:#000; border-radius:6px; overflow:hidden; border:1px solid var(--line); }
  .bar > span { display:block; height:100%; width:0; background:linear-gradient(90deg,var(--grn),#ffcc00,var(--red)); transition:width .1s; }
  .ptt { font-size:20px; font-weight:800; padding:18px; }
  .ptt.tx { background:var(--red); color:#fff; }
  .ptt.inh { background:#3a1a1a; color:#ff8888; }
  .grid2 { display:grid; grid-template-columns:1fr 1fr; gap:6px 14px; font-size:14px; }
  .grid2 b { color:var(--blu); font-weight:600; }
  input { width:100%; padding:12px; font-size:16px; background:#000; color:var(--grn); border:1px solid var(--line); border-radius:8px; font-family:'Courier New',monospace; }
  .note { color:var(--dim); font-size:11px; text-align:center; margin-top:4px; }
</style>
</head>
<body>
  <h1><span>IcomRigControl Remote</span><span id="conn" class="dot"></span></h1>

  <div class="panel">
    <div class="freq" id="freq">--.------</div>
    <div class="mhz" id="mode">--</div>
  </div>

  <div class="panel tune">
    <div class="row" style="margin-bottom:8px">
      <button data-step="10">10 Hz</button>
      <button data-step="100">100</button>
      <button data-step="1000" class="sel">1 kHz</button>
      <button data-step="10000">10 k</button>
    </div>
    <div class="row">
      <button id="dn2">&laquo;</button>
      <button id="dn1">&#8249;</button>
      <button id="up1">&#8250;</button>
      <button id="up2">&raquo;</button>
    </div>
    <div class="row" style="margin-top:8px">
      <input id="fentry" inputmode="decimal" placeholder="MHz e.g. 14.074">
      <button id="fset" style="flex:0 0 70px">Set</button>
    </div>
  </div>

  <div class="panel">
    <div class="row" id="modes"></div>
  </div>

  <div class="panel">
    <div class="meter"><div class="lbl"><span>S / Signal</span><span id="slbl">S0</span></div><div class="bar"><span id="sbar"></span></div></div>
    <div class="meter"><div class="lbl"><span>Power</span><span id="plbl">0%</span></div><div class="bar"><span id="pbar"></span></div></div>
    <div class="grid2">
      <span><b>SWR</b> <span id="swr">1.0</span></span>
      <span><b>ALC</b> <span id="alc">0</span></span>
      <span><b>Volts</b> <span id="volts">--</span></span>
      <span><b>Amps</b> <span id="amps">--</span></span>
    </div>
  </div>

  <div class="panel">
    <button id="ptt" class="ptt">PTT</button>
    <div class="note" id="status">Connecting&hellip;</div>
  </div>

<script>
(function(){
  var step = 1000, ws = null, tx = false, inhibited = false;
  var $ = function(id){ return document.getElementById(id); };
  var MODES = ["LSB","USB","CW","CW-R","RTTY","RTTY-R","AM","FM","USB-D"];

  // Build mode buttons
  var mv = $("modes");
  MODES.forEach(function(m){
    var b = document.createElement("button");
    b.textContent = m; b.dataset.mode = m;
    b.onclick = function(){ send({cmd:"mode", mode:m}); };
    mv.appendChild(b);
  });

  // Token: remembered, or asked once if the server rejects us.
  function token(){ return localStorage.getItem("irc_token") || ""; }

  function wsUrl(){
    var proto = location.protocol === "https:" ? "wss:" : "ws:";
    var t = token();
    return proto + "//" + location.host + "/ws" + (t ? "?token=" + encodeURIComponent(t) : "");
  }

  function connect(){
    try { ws = new WebSocket(wsUrl()); } catch(e){ setStatus("Bad address"); return; }
    ws.onopen = function(){ $("conn").className = "dot ok"; setStatus("Connected"); };
    ws.onclose = function(){ $("conn").className = "dot"; setStatus("Disconnected — retrying…"); setTimeout(connect, 1500); };
    ws.onmessage = function(ev){ try { render(JSON.parse(ev.data)); } catch(e){} };
  }

  function send(obj){ if (ws && ws.readyState === 1) ws.send(JSON.stringify(obj)); }
  function setStatus(s){ $("status").textContent = s; }

  function fmtFreq(hz){
    var mhz = Math.floor(hz / 1e6), rest = hz % 1e6;
    var k = Math.floor(rest / 1000), h = rest % 1000;
    return mhz + "." + String(k).padStart(3,"0") + "." + String(h).padStart(3,"0");
  }

  function render(s){
    if (s.type === "unauthorized"){ askToken(); return; }
    if (s.freq != null) $("freq").textContent = fmtFreq(s.freq);
    if (s.mode != null){
      $("mode").textContent = s.mode + (s.connected ? "" : "  (radio offline)");
      Array.prototype.forEach.call(mv.children, function(b){ b.className = (b.dataset.mode === s.mode) ? "sel" : ""; });
    }
    if (s.s != null){ $("slbl").textContent = "S" + s.s + (s.sdbm ? "  " + Math.round(s.sdbm) + " dBm" : ""); $("sbar").style.width = Math.min(100, s.s/9*60 + (s.sdbm>-73? (s.sdbm+73)/1 : 0)) + "%"; }
    if (s.power != null){ $("plbl").textContent = Math.round(s.power) + "%"; $("pbar").style.width = Math.min(100, s.power) + "%"; }
    if (s.swr != null) $("swr").textContent = (Math.round(s.swr*10)/10).toFixed(1);
    if (s.alc != null) $("alc").textContent = Math.round(s.alc);
    if (s.volts != null) $("volts").textContent = (Math.round(s.volts*10)/10).toFixed(1);
    if (s.amps != null) $("amps").textContent = (Math.round(s.amps*10)/10).toFixed(1);
    inhibited = !!s.inhibited; tx = !!s.ptt;
    var p = $("ptt");
    p.className = "ptt" + (inhibited ? " inh" : (tx ? " tx" : ""));
    p.textContent = inhibited ? "TX INHIBITED" : (tx ? "ON AIR — TAP TO STOP" : "PTT");
  }

  function askToken(){
    var t = prompt("This station needs an access token:");
    if (t){ localStorage.setItem("irc_token", t); if (ws) ws.close(); connect(); }
    else setStatus("Access token required");
  }

  // Tuning
  Array.prototype.forEach.call(document.querySelectorAll("[data-step]"), function(b){
    b.onclick = function(){
      step = parseInt(b.dataset.step,10);
      document.querySelectorAll("[data-step]").forEach(function(x){ x.className = ""; });
      b.className = "sel";
    };
  });
  $("dn2").onclick = function(){ send({cmd:"tune", delta:-step*10}); };
  $("dn1").onclick = function(){ send({cmd:"tune", delta:-step}); };
  $("up1").onclick = function(){ send({cmd:"tune", delta:step}); };
  $("up2").onclick = function(){ send({cmd:"tune", delta:step*10}); };
  $("fset").onclick = function(){
    var v = parseFloat($("fentry").value);
    if (!isNaN(v)) send({cmd:"freq", hz:Math.round(v*1e6)});
  };
  $("ptt").onclick = function(){ if (!inhibited) send({cmd:"ptt", on:!tx}); };

  connect();
})();
</script>
</body>
</html>
""";
}
