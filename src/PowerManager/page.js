// Power Manager's web half. Runs in Root.html, the document every game page iframe sits in. Draws the panel over all
// of them in the warm gold look of the other mod panels: the battery, the generators and the powered devices, a tab for
// the fuel order, and one for settings. It also puts a button in the HUD's tool column (bottom left, above the phone
// and bag buttons), built like the game's own buttons there so it reads as part of the game. Talks to the plugin
// through vuplex.postMessage("slmod|powermanager|<verb>") and is driven by window.__slpower.
(function () {
  if (window.__slpower) return;

  var PREFIX = 'slmod|powermanager|';
  var HUD_CLASS = 'pm-hud-btn';
  // Where the button sits in the tool column among the other mods' buttons (Exiled's Toolkit sorts them by this).
  var DOCK_ORDER = '50';
  var BUSY_TIMEOUT_MS = 5000;
  var SVG_NS = 'http://www.w3.org/2000/svg';
  var BOLT_PATH = 'M13 2L4 14h6l-1 8 9-12h-6z';
  var TABS = ['power', 'fuel', 'settings'];
  var TAB_WORD = { power: 'tabPower', fuel: 'tabFuel', settings: 'tabSettings' };
  var FORECAST_WORD = { CityPower: 'fcCity', NoBattery: 'fcNoBattery', Steady: 'fcSteady', Draining: 'fcDraining', Charging: 'fcCharging', Empty: 'fcEmpty', Full: 'fcFull' };
  // The plugin keeps every number in range and to its step; these only grey out a button at the end of the range.
  var STEPS = {
    autominutes: { min: 10, max: 1440, step: 10 },
    shedbelow: { min: 0, max: 90, step: 5 }
  };
  var AUTO_AT = { min: 10, max: 90, step: 10 };
  var LOCAL = { tab: true };
  var SCROLLERS = ['pm-gens', 'pm-devs', 'pm-order', 'pm-spare', 'pm-stab'];

  var state = { data: emptyData(), isOpen: false, isLive: false, key: '', tab: 'power' };
  var ui = null;
  var isBusy = false;
  var busyTimer = 0;
  var hasReportedPageAccess = false;
  var hasReportedHudPlaced = false;

  function emptyData() {
    return { game: false, battery: null, gens: [], devs: [], order: [], spare: [], set: null };
  }

  // The plugin sends the words in the game's language before every call, so a missing one is a bug; its key shows.
  function say(key) {
    var words = window.__slpowerWords || {};
    var text = words[key] || key;

    for (var i = 1; i < arguments.length; i++) text = text.split('{' + (i - 1) + '}').join(String(arguments[i]));

    return text;
  }

  function send(message) {
    try {
      if (window.vuplex) window.vuplex.postMessage(PREFIX + message);
    } catch (e) {}
  }

  // One press at a time: a double click would otherwise ask twice. Every press is answered by a status line, which
  // frees the buttons; the timer frees them anyway if that answer never comes.
  function sendAction(message) {
    if (isBusy) return;

    isBusy = true;
    clearTimeout(busyTimer);
    busyTimer = setTimeout(clearBusy, BUSY_TIMEOUT_MS);
    send(message);
  }

  function clearBusy() {
    isBusy = false;
    clearTimeout(busyTimer);
  }

  function el(tag, className, text) {
    var node = document.createElement(tag);

    if (className) node.className = className;
    if (text != null) node.textContent = text;

    return node;
  }

  function add(parent) {
    for (var i = 1; i < arguments.length; i++) {
      if (arguments[i]) parent.appendChild(arguments[i]);
    }

    return parent;
  }

  // Marks anything as clickable: a click on it or inside it does this action.
  function act(node, action, args) {
    node.setAttribute('data-act', action);

    if (args != null) node.setAttribute('data-args', String(args));

    return node;
  }

  function button(text, action, args, isEnabled, extraClass) {
    var node = act(el('button', 'pm-btn' + (extraClass ? ' ' + extraClass : ''), text), action, args);

    if (!isEnabled) node.disabled = true;

    return node;
  }

  function svg(path, className) {
    var node = document.createElementNS(SVG_NS, 'svg');
    var shape = document.createElementNS(SVG_NS, 'path');

    node.setAttribute('viewBox', '0 0 24 24');
    node.setAttribute('class', className || 'pm-svg');
    shape.setAttribute('d', path);
    shape.setAttribute('fill', 'currentColor');
    node.appendChild(shape);

    return node;
  }

  var CSS = [
    '#pm-root{--pm-accent:#FFC83A;--pm-glow:rgba(255,200,58,.35);--pm-text:#f0e8d8;--pm-text2:#cbbfa8;--pm-text3:#a89a82;--pm-text4:#7a6e5a;--pm-line:rgba(220,188,120,.2);--pm-soft:rgba(220,188,120,.11);--pm-card:rgba(70,58,44,.55);--pm-red:#e53935;--pm-red2:#ff8a80;--pm-green:#8cc56a;--pm-green2:#b5e09a;position:fixed;inset:0;z-index:2147483000;display:none;align-items:center;justify-content:center;background:rgba(10,6,2,.45);font-family:"Noto Sans SC","Microsoft YaHei","PingFang SC",sans-serif;color:var(--pm-text);user-select:none}',
    '#pm-root *{box-sizing:border-box}',
    '.pm-plate{position:relative;width:min(1400px,96vw);height:min(780px,92vh);display:flex;flex-direction:column;gap:12px;padding:18px 22px;border-radius:4px;border:1px solid rgba(220,188,120,.28);background:radial-gradient(ellipse at 15% 0%,rgba(150,100,40,.3),transparent 55%),linear-gradient(180deg,#3a3129 0%,#2b241d 55%,#241e18 100%);box-shadow:0 30px 90px rgba(0,0,0,.7),inset 0 1px 0 rgba(255,230,180,.08)}',
    '.pm-plate::before{content:"";position:absolute;top:0;left:0;right:0;height:2px;background:linear-gradient(90deg,transparent 10%,var(--pm-accent) 50%,transparent 90%);opacity:.6}',
    '.pm-svg{width:15px;height:15px;flex:none}',
    '.pm-hd{display:flex;align-items:center;gap:16px}',
    '.pm-logo{width:40px;height:40px;flex:none;border-radius:4px;display:flex;align-items:center;justify-content:center;background:linear-gradient(180deg,#ffd25a,#e8a91c);border:1px solid #ffd36a;color:#2a1f10;box-shadow:0 0 14px rgba(255,200,58,.25)}',
    '.pm-logo .pm-svg{width:24px;height:24px}',
    '.pm-ttl{font-size:26px;letter-spacing:6px;color:#fff;line-height:1;white-space:nowrap}',
    '.pm-ttl small{display:block;font-size:12px;letter-spacing:1px;color:var(--pm-text3);margin-top:5px}',
    '.pm-sp{flex:1}',
    '.pm-tabs{display:flex;gap:8px}',
    '.pm-tab{padding:7px 18px;border-radius:4px;border:1px solid var(--pm-line);background:rgba(255,240,210,.05);font-size:14px;color:var(--pm-text2);cursor:pointer;white-space:nowrap}',
    '.pm-tab:hover{border-color:rgba(255,200,58,.6);color:#fff}',
    '.pm-tab.pm-on{color:var(--pm-accent);font-weight:600;background:rgba(255,200,58,.13);border-color:var(--pm-accent);box-shadow:0 0 12px rgba(255,200,58,.18)}',
    '.pm-body{flex:1;min-height:0;display:flex;gap:12px}',
    '.pm-well{flex:1;min-width:0;display:flex;flex-direction:column;background:linear-gradient(180deg,rgba(58,48,38,.75),rgba(44,36,28,.75));border:1px solid var(--pm-line);border-radius:4px;min-height:0;overflow:hidden}',
    '.pm-list{flex:1;min-height:0;overflow-y:auto;display:flex;flex-direction:column;gap:5px;padding:0 12px 12px}',
    '.pm-list::-webkit-scrollbar,.pm-stab::-webkit-scrollbar{width:6px}',
    '.pm-list::-webkit-scrollbar-thumb,.pm-stab::-webkit-scrollbar-thumb{background:rgba(255,200,58,.35);border-radius:3px}',
    '.pm-sect{font-size:15px;font-weight:700;letter-spacing:3px;color:var(--pm-accent);text-shadow:0 0 12px var(--pm-glow);display:flex;align-items:center;gap:10px;padding:14px 16px 8px;text-transform:uppercase}',
    '.pm-sect::before{content:"";width:4px;height:16px;background:var(--pm-accent);box-shadow:0 0 8px var(--pm-glow)}',
    '.pm-sect::after{content:"";flex:1;height:1px;background:linear-gradient(90deg,rgba(255,200,58,.25),transparent)}',
    '.pm-btn{font:inherit;font-size:13px;color:var(--pm-text);padding:6px 12px;border-radius:4px;border:1px solid var(--pm-line);background:linear-gradient(180deg,rgba(92,78,60,.9),rgba(66,55,42,.92));cursor:pointer;white-space:nowrap;display:inline-flex;gap:6px;align-items:center}',
    '.pm-btn:hover:not(:disabled){border-color:var(--pm-accent);background:linear-gradient(180deg,rgba(110,90,58,.95),rgba(80,64,40,.95))}',
    '.pm-btn:disabled{opacity:.4;cursor:default}',
    '.pm-btn.pm-sm{font-size:12px;padding:3px 9px}',
    '.pm-btn.pm-x{width:30px;height:30px;padding:0;font-size:18px;justify-content:center}',
    '.pm-btn.pm-gold{color:#2a1f10;font-weight:700;border-color:#ffd36a;background:linear-gradient(180deg,#ffd25a,#e8a91c)}',
    '.pm-btn.pm-gold:hover:not(:disabled){background:linear-gradient(180deg,#ffdf80,#f0b52a)}',
    '.pm-bulk{display:flex;gap:6px;flex-wrap:wrap;padding:0 12px 10px}',
    '.pm-bat{margin:0 12px 6px;padding:10px 14px;border-radius:4px;background:var(--pm-card);border:1px solid var(--pm-soft);display:flex;flex-direction:column;gap:6px}',
    '.pm-batl{display:flex;align-items:baseline;gap:12px;font-size:13px;color:var(--pm-text3)}',
    '.pm-batl b{font-size:22px;color:var(--pm-text)}',
    '.pm-batl .pm-fc{margin-left:auto;color:var(--pm-text2)}',
    '.pm-track{display:flex;height:10px;border-radius:5px;background:rgba(255,255,255,.1);overflow:hidden}',
    '.pm-fill{height:100%;border-radius:5px;background:var(--pm-green)}',
    '.pm-fill.pm-mid{background:var(--pm-accent)}',
    '.pm-fill.pm-lo{background:var(--pm-red)}',
    '.pm-small{font-size:12px;color:var(--pm-text3)}',
    '.pm-warn{margin:0 12px 6px;padding:7px 12px;border-radius:4px;border:1px solid rgba(255,193,7,.6);background:rgba(255,193,7,.1);color:#f3d38a;font-size:13px}',
    '.pm-row{display:flex;align-items:center;gap:10px;padding:8px 10px;border-radius:4px;background:var(--pm-card);border:1px solid var(--pm-soft)}',
    '.pm-nm{flex:1;min-width:0;display:flex;flex-direction:column;gap:3px}',
    '.pm-nm b{font-size:14px;font-weight:600;overflow:hidden;text-overflow:ellipsis;white-space:nowrap}',
    '.pm-nm small{font-size:12px;color:var(--pm-text3)}',
    '.pm-ctl{display:flex;align-items:center;gap:6px;flex:none}',
    '.pm-badge{flex:none;font-size:11px;padding:2px 7px;border-radius:3px;background:rgba(255,255,255,.1);color:rgba(238,238,238,.8);white-space:nowrap}',
    '.pm-b-on{background:rgba(124,179,66,.24);color:var(--pm-green2)}',
    '.pm-b-off{background:rgba(255,255,255,.08);color:var(--pm-text3)}',
    '.pm-b-shed{background:rgba(229,57,53,.24);color:var(--pm-red2)}',
    '.pm-b-busy{background:rgba(100,181,246,.2);color:#90caf9}',
    '.pm-lab{font-size:12px;color:var(--pm-text3);white-space:nowrap}',
    '.pm-note{padding:16px;color:var(--pm-text3);font-size:14px;text-align:center}',
    '.pm-hint{padding:0 16px 8px;font-size:12px;color:var(--pm-text3)}',
    '.pm-n{min-width:34px;text-align:right;font-size:13px;color:var(--pm-accent);font-weight:600}',
    '.pm-stab{flex:1;overflow-y:auto}',
    '.pm-sgrid{display:grid;grid-template-columns:1fr 1fr;gap:18px;padding:6px 16px 16px}',
    '.pm-col{display:flex;flex-direction:column;gap:6px}',
    '.pm-col .pm-sect{padding-left:0}',
    '.pm-set{display:flex;align-items:center;gap:14px;padding:10px 14px;border-radius:4px;background:var(--pm-card);border:1px solid var(--pm-soft)}',
    '.pm-lbl{flex:1;min-width:0}',
    '.pm-lbl b{display:block;font-size:14px;font-weight:600}',
    '.pm-lbl small{display:block;font-size:12px;color:var(--pm-text3);margin-top:2px}',
    '.pm-kbd{font-size:14px;color:var(--pm-text2);border:1px solid rgba(220,188,120,.4);border-bottom-width:2px;border-radius:4px;padding:2px 10px}',
    '.pm-tog{width:38px;height:20px;padding:0;border-radius:10px;background:rgba(20,14,8,.5);border:1px solid var(--pm-line);position:relative;flex:none;cursor:pointer}',
    '.pm-tog::after{content:"";position:absolute;top:2px;left:2px;width:14px;height:14px;border-radius:50%;background:#a89a82}',
    '.pm-tog.pm-on{background:rgba(255,200,58,.35);border-color:var(--pm-accent)}',
    '.pm-tog.pm-on::after{left:20px;background:var(--pm-accent)}',
    '.pm-tog:disabled{opacity:.4;cursor:default}',
    '.pm-stepper{display:inline-flex;align-items:center;border:1px solid var(--pm-line);border-radius:4px;overflow:hidden;font-size:13px;flex:none}',
    '.pm-stepper button{font:inherit;padding:3px 9px;color:var(--pm-text3);cursor:pointer;background:rgba(255,240,210,.06);border:none}',
    '.pm-stepper button:hover:not(:disabled){color:var(--pm-accent)}',
    '.pm-stepper button:disabled{opacity:.4;cursor:default}',
    '.pm-stepper b{padding:3px 10px;color:var(--pm-accent);font-weight:600;min-width:58px;text-align:center}',
    '.pm-foot{display:flex;gap:14px;align-items:center;font-size:13px;color:var(--pm-text3);border-top:1px solid var(--pm-soft);padding-top:10px}',
    '.pm-status{flex:1;color:var(--pm-text2)}'
  ].join('\n');

  function build() {
    if (ui && document.body.contains(ui.root)) return;

    addStyle();

    var root = el('div');
    root.id = 'pm-root';
    var plate = el('div', 'pm-plate unity-interactive');
    var head = el('div', 'pm-hd');
    var body = el('div', 'pm-body');
    var foot = el('div', 'pm-foot');
    var status = el('div', 'pm-status');
    var keys = el('div');

    add(foot, status, keys);
    add(plate, head, body, foot);
    root.appendChild(plate);

    document.body.appendChild(root);

    // Drawn at the player's UI size (Exiled's Toolkit). The full-screen mask always fills the screen, so the window on
    // it is what must fit.
    uiSize(root, { fit: plate });

    root.addEventListener('click', onClick);
    // Clicks inside the plate must not reach the game pages underneath; the mask itself closes nothing on purpose.
    root.addEventListener('mousedown', function (e) { e.stopPropagation(); });

    ui = { root: root, head: head, body: body, status: status, keys: keys };
  }

  // Hands a panel to Exiled's Toolkit to be drawn at the player's UI size. Before the Toolkit's script has loaded it
  // waits in a list the Toolkit reads.
  function uiSize(node, options) {
    options = Object.assign({ mod: 'PowerManager' }, options);
    var toolkit = window.ExiledUi;

    if (toolkit) {
      toolkit.size(node, options);
      return;
    }

    var waiting = window.__exiledUiWait = window.__exiledUiWait || [];
    waiting.push([node, options]);
    if (waiting.length > 32) waiting.shift();
  }

  // The style outlives a lost panel root, so a rebuild must not stack another copy of it.
  function addStyle() {
    if (document.getElementById('pm-style')) return;

    var style = el('style');
    style.id = 'pm-style';
    style.textContent = CSS;
    document.head.appendChild(style);
  }

  function onClick(e) {
    var target = e.target.closest('[data-act]');

    if (!target) return;

    e.stopPropagation();

    if (target.disabled) return;

    var action = target.getAttribute('data-act');
    var args = target.getAttribute('data-args');

    if (LOCAL[action]) {
      state.tab = args;
      render();

      return;
    }

    // Opening or shutting the panel gets no status line back, so it must not hold the buttons busy.
    if (action === 'close' || action === 'toggle') {
      send(action);

      return;
    }

    sendAction(args ? action + '|' + args : action);
  }

  function sect(text) {
    return el('div', 'pm-sect', text);
  }

  function isLocked() {
    return !!(state.data.battery && state.data.battery.locked);
  }

  // Game minutes as the panel words them: minutes under an hour, hours and minutes under a day, then days and hours.
  function duration(minutes) {
    var total = Math.max(0, Math.round(minutes));

    if (total < 60) return say('minutes', total);
    if (total < 1440) return say('hours', Math.floor(total / 60), total % 60);

    return say('days', Math.floor(total / 1440), Math.floor((total % 1440) / 60));
  }

  function forecast(battery) {
    var word = FORECAST_WORD[battery.fc] || 'fcSteady';

    return word === 'fcDraining' || word === 'fcCharging' ? say(word, duration(battery.mins)) : say(word);
  }

  function render() {
    var scrolls = {};

    SCROLLERS.forEach(function (name) {
      var node = ui.body.querySelector('.' + name);

      if (node) scrolls[name] = node.scrollTop || 0;
    });

    if (TABS.indexOf(state.tab) < 0) state.tab = 'power';

    renderHead();
    ui.body.textContent = '';

    if (state.tab === 'settings') ui.body.appendChild(settingsTab());
    else if (!state.data.game) ui.body.appendChild(add(el('div', 'pm-well'), el('div', 'pm-note', say('noGame'))));
    else if (state.tab === 'fuel') fuelTab().forEach(function (node) { ui.body.appendChild(node); });
    else add(ui.body, generatorWell(), deviceWell());

    SCROLLERS.forEach(function (name) {
      var node = ui.body.querySelector('.' + name);

      if (node && scrolls[name]) node.scrollTop = scrolls[name];
    });

    renderKeys();
  }

  function renderHead() {
    var battery = state.data.battery;
    var ttl = el('div', 'pm-ttl', say('title'));
    var tabs = el('div', 'pm-tabs');

    if (battery) ttl.appendChild(el('small', '', say('battery') + ' ' + say('percent', battery.pct) + ', ' + forecast(battery)));

    TABS.forEach(function (tab) {
      tabs.appendChild(act(el('span', 'pm-tab' + (state.tab === tab ? ' pm-on' : ''), say(TAB_WORD[tab])), 'tab', tab));
    });

    ui.head.textContent = '';
    add(ui.head, add(el('div', 'pm-logo'), svg(BOLT_PATH)), ttl, el('div', 'pm-sp'), tabs, button('X', 'close', null, true, 'pm-x'));
  }

  // ===== The power tab =====

  function generatorWell() {
    var data = state.data;
    var well = el('div', 'pm-well');
    var list = el('div', 'pm-list pm-gens');
    var gens = data.gens || [];
    var hasGens = gens.length > 0;
    var anyAuto = gens.some(function (gen) { return gen.auto; });

    add(well, sect(say('battery')), batteryCard(data.battery));

    if (isLocked()) well.appendChild(el('div', 'pm-warn', say('locked')));

    add(well, sect(say('generators')), add(el('div', 'pm-bulk'),
      button(say('startAll'), 'startall', null, hasGens),
      button(say('stopAll'), 'stopall', null, hasGens),
      button(say('refuelAll'), 'refuelall', null, hasGens, 'pm-gold'),
      button(anyAuto ? say('autoAllOff') : say('autoAllOn'), 'autoall', anyAuto ? '0' : '1', hasGens)));

    if (!hasGens) list.appendChild(el('div', 'pm-note', say('noGenerators')));

    gens.forEach(function (gen) { list.appendChild(generatorRow(gen)); });

    return add(well, list);
  }

  function batteryCard(battery) {
    var card = el('div', 'pm-bat');
    var pct = Math.max(0, Math.min(100, battery.pct));
    var line = add(el('div', 'pm-batl'), el('b', '', say('percent', battery.pct)), el('span', '', say('stored', battery.stored, battery.cap)), el('span', 'pm-fc', forecast(battery)));
    var fill = el('div', 'pm-fill' + (pct < 20 ? ' pm-lo' : pct < 50 ? ' pm-mid' : ''));

    fill.style.width = pct + '%';

    return add(card, line, add(el('div', 'pm-track'), fill), el('div', 'pm-small', say('genCons', battery.gen, battery.cons)));
  }

  function generatorRow(gen) {
    var fuel = gen.mins > 0 ? say('fuelLeft', duration(gen.mins)) : say('noFuel');
    var name = add(el('div', 'pm-nm'), el('b', '', gen.name), el('small', '', fuel + ' / ' + say('slots', gen.slots, gen.max)));
    var badge = gen.busy ? el('span', 'pm-badge pm-b-busy', say('refuelling'))
      : el('span', 'pm-badge ' + (gen.on ? 'pm-b-on' : 'pm-b-off'), say(gen.on ? 'running' : 'stopped'));
    var power = gen.on ? button(say('stop'), 'stop', gen.id, true) : button(say('start'), 'start', gen.id, true);
    var refuel = button(say('refuel'), 'refuel', gen.id, !gen.busy);
    var auto = add(el('span', 'pm-ctl'),
      el('span', 'pm-lab', say('auto')),
      act(el('button', 'pm-tog' + (gen.auto ? ' pm-on' : '')), 'auto', gen.id + '|' + (gen.auto ? '0' : '1')),
      autoAt(gen));

    return add(el('div', 'pm-row'), name, badge, auto, power, refuel);
  }

  // The battery share the generator starts below, in tenths as the game keeps it; sent as a share (0.3 for 30%).
  function autoAt(gen) {
    var box = el('span', 'pm-stepper');
    var at = Math.round(gen.at / AUTO_AT.step) * AUTO_AT.step;

    return add(box,
      button('-', 'autoat', gen.id + '|' + share(at - AUTO_AT.step), at > AUTO_AT.min),
      el('b', '', say('autoBelow', at)),
      button('+', 'autoat', gen.id + '|' + share(at + AUTO_AT.step), at < AUTO_AT.max));
  }

  function share(percent) {
    return String(Math.round(percent / 10) / 10);
  }

  function deviceWell() {
    var data = state.data;
    var set = data.set;
    var well = el('div', 'pm-well');
    var list = el('div', 'pm-list pm-devs');
    var devs = data.devs || [];
    var isShedding = set && set.shedBelow > 0;

    add(well, sect(say('devices')), el('div', 'pm-hint', isShedding ? say('shedNote', set.shedBelow, set.restoreAt) : say('shedOffNote')));

    if (devs.length === 0) list.appendChild(el('div', 'pm-note', say('noDevices')));

    devs.forEach(function (dev) { list.appendChild(deviceRow(dev)); });

    return add(well, list);
  }

  function deviceRow(dev) {
    var name = add(el('div', 'pm-nm'), el('b', '', dev.name), el('small', '', say('perTick', dev.w)));
    var row = add(el('div', 'pm-row'), name);
    var canSwitch = !dev.managed && !isLocked();

    if (dev.managed) row.appendChild(el('span', 'pm-badge', say('managed')));
    if (dev.shed) row.appendChild(el('span', 'pm-badge pm-b-shed', say('shed')));

    var lowToggle = act(el('button', 'pm-tog' + (dev.low ? ' pm-on' : '')), 'low', dev.id + '|' + (dev.low ? '0' : '1'));
    var powerToggle = act(el('button', 'pm-tog' + (dev.on ? ' pm-on' : '')), 'device', dev.id + '|' + (dev.on ? '0' : '1'));

    // Defense Manager runs its devices' power, and the game ignores a switch while it holds them.
    if (!canSwitch) powerToggle.disabled = true;
    if (dev.managed) lowToggle.disabled = true;

    return add(row,
      add(el('span', 'pm-ctl'), el('span', 'pm-lab', say('low')), lowToggle),
      add(el('span', 'pm-ctl'), el('span', 'pm-lab', say(dev.on ? 'on' : 'off')), powerToggle));
  }

  // ===== The fuel tab =====

  // The two lists are rebuilt only when they or the words changed: the add list holds every burnable item in the game,
  // and the rest of the view changes every second while a generator runs.
  var fuelDrawn = null;

  function fuelTab() {
    var key = JSON.stringify([state.data.order || [], state.data.spare || []]);
    var words = window.__slpowerWords;

    if (!fuelDrawn || fuelDrawn.key !== key || fuelDrawn.words !== words) fuelDrawn = { key: key, words: words, nodes: [orderWell(), spareWell()] };

    return fuelDrawn.nodes;
  }

  // How many of it storage holds and its burn value; an item the game does not burn shows only the count.
  function fuelInfo(line) {
    return line.burn > 0 ? say('fuelInfo', line.n, line.burn) : say('inStorage', line.n);
  }

  function orderWell() {
    var order = state.data.order || [];
    var well = el('div', 'pm-well');
    var list = el('div', 'pm-list pm-order');

    add(well, sect(say('fuelOrder')), el('div', 'pm-hint', say('fuelNote')));

    if (order.length === 0) list.appendChild(el('div', 'pm-note', say('emptyOrder')));

    order.forEach(function (line, i) {
      var name = add(el('div', 'pm-nm'), el('b', '', (i + 1) + '. ' + line.name), el('small', '', fuelInfo(line)));

      list.appendChild(add(el('div', 'pm-row'), name,
        button(say('up'), 'fuelup', line.cfg, i > 0, 'pm-sm'),
        button(say('down'), 'fueldown', line.cfg, i < order.length - 1, 'pm-sm'),
        button(say('removeFuel'), 'fueloff', line.cfg, true, 'pm-sm')));
    });

    return add(well, list);
  }

  function spareWell() {
    var spare = state.data.spare || [];
    var well = el('div', 'pm-well');
    var list = el('div', 'pm-list pm-spare');

    well.appendChild(sect(say('otherFuel')));

    if (spare.length === 0) list.appendChild(el('div', 'pm-note', say('noOtherFuel')));

    spare.forEach(function (line) {
      var name = add(el('div', 'pm-nm'), el('b', '', line.name), el('small', '', fuelInfo(line)));

      list.appendChild(add(el('div', 'pm-row'), name, button(say('addFuel'), 'fueladd', line.cfg, true, 'pm-sm')));
    });

    return add(well, list);
  }

  // ===== The settings tab =====

  function settingsTab() {
    var s = state.data.set;
    var box = el('div', 'pm-well pm-stab');

    if (!s) return add(box, el('div', 'pm-note', say('noGame')));

    var grid = el('div', 'pm-sgrid');
    var left = el('div', 'pm-col');
    var right = el('div', 'pm-col');

    add(left,
      sect(say('setControls')),
      setRow('setKey', 'setKeyDesc', el('span', 'pm-kbd', s.key || say('keyNone'))),
      sect(say('setShedding')),
      setRow('setShedBelow', 'setShedBelowDesc', stepper('shedbelow', s.shedBelow, function (v) { return say('percent', v); })));
    add(right,
      sect(say('setGenerators')),
      setRow('setAutoRefuel', 'setAutoRefuelDesc', onOff('autorefuel', s.autoRefuel)),
      setRow('setAutoMinutes', 'setAutoMinutesDesc', stepper('autominutes', s.autoMinutes, duration)));

    return add(box, add(grid, left, right));
  }

  function onOff(name, isOn) {
    return act(el('button', 'pm-tog' + (isOn ? ' pm-on' : '')), 'set', name + '|' + (isOn ? '0' : '1'));
  }

  function setRow(nameKey, descKey, control) {
    var label = add(el('div', 'pm-lbl'), el('b', '', say(nameKey)), el('small', '', say(descKey)));

    return add(el('div', 'pm-set'), label, control);
  }

  // Minus and plus send the value one step along; the plugin keeps it in range and redraws the tab with what it saved.
  function stepper(name, value, format) {
    var range = STEPS[name];

    return add(el('span', 'pm-stepper'),
      button('-', 'set', name + '|' + (value - range.step), value > range.min),
      el('b', '', format(value)),
      button('+', 'set', name + '|' + (value + range.step), value < range.max));
  }

  function renderKeys() {
    ui.keys.textContent = state.key ? say('keyClose', state.key) : say('escClose');
  }

  function open(data) {
    build();
    state.data = data || state.data;
    state.isOpen = true;
    ui.status.textContent = '';
    ui.root.style.display = 'flex';
    render();
  }

  function setData(data) {
    state.data = data || state.data;

    if (state.isOpen) render();
  }

  function close() {
    state.isOpen = false;

    if (ui) ui.root.style.display = 'none';
  }

  function status(text) {
    build();
    clearBusy();

    // With the panel closed the plugin shows the line as an Exiled's Toolkit toast instead.
    if (state.isOpen) ui.status.textContent = text;
  }

  // Whether a game is loaded, and the key that opens the panel ('' when none): the HUD button is only shown in a game,
  // and both it and the panel name the key.
  function live(isLive, key) {
    state.isLive = !!isLive;
    state.key = key || '';

    if (ui && state.isOpen) renderKeys();

    patchPagesSafely();
  }

  // The HUD is a same-origin iframe, so its document can be reached from here; polled because the game creates and
  // drops that iframe whenever it likes.
  function patchPages() {
    var frames = document.querySelectorAll('iframe');

    placedHud = null;

    for (var i = 0; i < frames.length; i++) {
      var src = frames[i].src || '';

      if (!/CoreUI1\.html/.test(src)) continue;

      var doc = null;

      try {
        doc = frames[i].contentDocument;
      } catch (e) {}

      if (!doc) {
        if (!hasReportedPageAccess) send('diag|cannot reach a game page document (root ' + location.protocol + ', iframe ' + src + ')');
        hasReportedPageAccess = true;
        continue;
      }

      placeHudButton(frames[i], doc);
    }
  }

  function patchPagesSafely() {
    try {
      patchPages();
    } catch (e) {}
  }

  // What this script last put on the HUD, with the iframe and document it went into. The 400 ms check looks for
  // iframes only when the button is gone or out of date; outside a game, with nothing placed, it does nothing.
  var placedHud = null;

  function checkPages() {
    try {
      if (!state.isLive && !placedHud) return;
      if (isHudPlaced()) return;

      patchPages();
    } catch (e) {}
  }

  // A node in an iframe the game dropped, or in a document the iframe has since replaced, still reads as connected to
  // its own document, so the iframe and its document are checked too.
  function isHudPlaced() {
    var placed = placedHud;

    return !!placed && state.isLive && placed.frame.isConnected && placed.frame.contentDocument === placed.doc &&
      placed.column.isConnected && placed.node.parentNode === placed.column && placed.node.getAttribute('data-key') === state.key;
  }

  // First in the tool column: the column is anchored at the bottom, so the first child sits on top and the game's own
  // buttons do not move. The column hides itself in build mode, and the button with it.
  function placeHudButton(frame, doc) {
    var column = doc.querySelector('.vertical-tools');
    var existing = doc.querySelector('.' + HUD_CLASS);

    if (!column || !state.isLive) {
      if (existing) existing.remove();

      return;
    }

    if (existing && existing.parentNode === column && existing.getAttribute('data-key') === state.key) {
      placedHud = { frame: frame, doc: doc, column: column, node: existing };

      return;
    }

    if (existing) existing.remove();

    var node = hudButton(doc);

    node.setAttribute('data-etk-dock', DOCK_ORDER);
    column.insertBefore(node, column.firstChild);
    placedHud = { frame: frame, doc: doc, column: column, node: node };

    if (!hasReportedHudPlaced) send('diag|HUD button placed in the tool column');
    hasReportedHudPlaced = true;
  }

  // The same markup as the game's bag button: its classes, an svg icon, and the hover tip its stylesheet shows. The
  // HUD page watches its body for new buttons and tells the game where they are, so a click on this one reaches it.
  function hudButton(doc) {
    var node = doc.createElement('button');
    node.className = 'ui-btn icon-sq interactive-btn has-entry-tip ' + HUD_CLASS;
    node.setAttribute('data-interactive', '');
    node.setAttribute('data-key', state.key);
    node.appendChild(bolt(doc));
    node.appendChild(entryTip(doc));
    node.addEventListener('click', function (e) {
      e.stopPropagation();
      send('toggle');
    });

    return node;
  }

  function bolt(doc) {
    var icon = doc.createElementNS(SVG_NS, 'svg');
    var path = doc.createElementNS(SVG_NS, 'path');

    icon.setAttribute('class', 'icon-svg');
    icon.setAttribute('viewBox', '0 0 24 24');
    path.setAttribute('d', BOLT_PATH);
    icon.appendChild(path);

    return icon;
  }

  function entryTip(doc) {
    var tip = doc.createElement('div');
    var title = doc.createElement('div');
    var body = doc.createElement('div');

    tip.className = 'entry-tip';
    title.className = 'entry-tip-title';
    title.textContent = 'Power Manager';
    body.className = 'entry-tip-body';
    body.textContent = say('tip');

    if (state.key) {
      var key = doc.createElement('span');
      key.className = 'entry-tip-key';
      key.textContent = state.key;
      title.appendChild(key);
    }

    tip.appendChild(title);
    tip.appendChild(body);

    return tip;
  }

  setInterval(checkPages, 400);

  window.__slpower = { open: open, setData: setData, close: close, status: status, live: live };
  send('ready');
})();
