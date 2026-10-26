// Neighbor Basket's web half. Runs in Root.html, the document every game page iframe sits in. While the neighbor's
// basket is open it lays the basket station over the inventory page: every storage at home and the bag on the left,
// the basket's grid with her wishes, weight and progress in the middle, her status on the right, with the Fill, Put
// away, Call back and Send buttons. A "Game screen" button hides it until the basket is opened again. It also adds the
// Neighbor app to the phone, an alert line on the game's Rescue panel, and shows a short toast when Exiled's Toolkit
// is not there to. Talks to the plugin through
// vuplex.postMessage("slmod|neighborbasket|<verb>") and is driven by window.__slneighborbasket.
(function () {
  if (window.__slneighborbasket) return;

  var PREFIX = 'slmod|neighborbasket|';
  var BUSY_TIMEOUT_MS = 4000;
  var TOAST_MS = 4500;
  var STATION_ID = 'slnb-station';
  var OPEN_ID = 'slnb-station-open';
  var STATION_REFRESH_MS = 1000;
  var FIRST_ASK_MS = 300;
  var AFTER_MOVE_MS = 250;
  // How long an opening station waits for its first answer before it lets the game's own screen show.
  var LOADING_MS = 3000;
  // How long a fill's planned stacks show as landing without an answer.
  var FILL_SHOW_MS = 3000;
  // One basket cell on screen, and the gap between cells.
  var CELL = 70;
  var PITCH = 74;
  var APP_CLASS = 'slnb-app';
  var SCREEN_CLASS = 'slnb-phone';
  var PHONE_STYLE_ID = 'slnb-phone-style';
  var APP_REFRESH_MS = 5000;
  // The phone log's lines drawn when it opens, then per frame after that.
  var PHONE_FIRST = 30;
  var PHONE_CHUNK = 40;
  // The station's list rows drawn with the rest of the station, then per frame after that.
  var LIST_FIRST = 40;
  var LIST_CHUNK = 60;
  var PHONE_PAGE = /PhoneSMS\.html/;
  var CHASE_FRAMES = 120;
  var SVG_NS = 'http://www.w3.org/2000/svg';
  // A basket: the body and its handle.
  var BASKET_STROKES = ['M4 10h16l-2 9H6z', 'M8 10l4-6 4 6', 'M9 13v3', 'M15 13v3'];

  var settings = { fill: true, callBack: false, putAway: false, app: true, fillLabel: 'Fill', fillTitle: '', callBackLabel: 'Call back', callBackTitle: '', putAwayLabel: 'To storage', putAwayTitle: '', appLabel: 'Neighbor' };
  var phone = { screen: null, data: null, refreshAt: 0 };
  // The station's data from the plugin and the player's own choices in it, for the one basket opening under way
  // (isOpen, in doc). isOff is the Game screen button; it lasts until the basket closes. isGuess is set while the
  // station shows a move the plugin has not answered yet. version counts what changed the drawing.
  var station = {
    data: null, labels: null, byId: {}, names: {}, state: null, isOpen: false, doc: null, askAt: 0, isOff: false, isDead: false, isGuess: false,
    sel: 'all', filt: '', query: '', order: null, orderSet: {}, orderKey: '', seen: {}, version: 0, filling: 0, waitSince: 0, hovered: null,
    listKey: null, listShown: LIST_FIRST, listRows: [], listToken: 0
  };
  var isBusy = false;
  var busyTimer = 0;
  var toastTimer = 0;
  var hasReportedAccess = false;
  var hasReportedState = false;

  var TOAST_STYLE = '#slnb-toast{position:fixed;left:50%;bottom:150px;transform:translateX(-50%);z-index:2147483002;pointer-events:none;display:none;max-width:640px;text-align:center;padding:10px 18px;border-radius:8px;border:1px solid rgba(255,193,7,.75);background:linear-gradient(180deg,rgba(30,30,33,.94),rgba(18,18,20,.96));color:#eee;font-family:"Microsoft YaHei","PingFang SC",sans-serif;font-size:14px;box-shadow:0 6px 20px rgba(0,0,0,.6)}';

  // Every class is prefixed so the inventory page's own rules (it has its own .cell, .row and .btn) never reach in.
  var STATION_STYLE = [
    '#' + STATION_ID + '{position:fixed;left:0;top:0;right:0;bottom:0;z-index:900;display:none;box-sizing:border-box;padding:3vh 2vw;gap:16px;justify-content:center;align-items:stretch;background:radial-gradient(ellipse at 70% 30%,rgba(120,90,50,.25),transparent 60%),linear-gradient(180deg,rgba(44,38,32,.97),rgba(26,23,20,.98));font-family:"Microsoft YaHei","PingFang SC",sans-serif;color:#f0e8d8;font-size:13px}',
    '#' + STATION_ID + ' *{box-sizing:border-box}',
    '.nbs-pane{position:relative;display:flex;flex-direction:column;min-height:0;padding:14px 16px 12px;border-radius:4px;background:radial-gradient(ellipse at 15% 0%,rgba(150,100,40,.28),transparent 55%),linear-gradient(180deg,#3a3129 0%,#2b241d 55%,#241e18 100%);border:1px solid rgba(220,188,120,.28);box-shadow:0 12px 40px rgba(0,0,0,.6)}',
    '.nbs-pane::before{content:"";position:absolute;top:0;left:0;right:0;height:2px;background:linear-gradient(90deg,transparent 10%,#FFC83A 50%,transparent 90%);opacity:.6}',
    '.nbs-source{flex:1 1 760px;max-width:820px;min-width:0}',
    '.nbs-basketpane{flex:0 0 440px}',
    '.nbs-side{flex:0 0 280px;display:flex;flex-direction:column;gap:12px;min-height:0}',
    '.nbs-head{display:flex;align-items:center;margin-bottom:10px;min-height:26px}',
    '.nbs-title{display:flex;align-items:center;font-size:15px;font-weight:700;color:#fff;letter-spacing:3px;text-transform:uppercase;white-space:nowrap}',
    '.nbs-title::before{content:"";width:3px;height:15px;margin-right:9px;background:#FFC83A;box-shadow:0 0 8px rgba(255,200,58,.35)}',
    '.nbs-sub{margin-left:10px;font-size:12px;color:#a89a82;white-space:nowrap;overflow:hidden;text-overflow:ellipsis}',
    '.nbs-split{display:flex;gap:14px;flex:1;min-height:0}',
    '.nbs-places{width:190px;flex:none;display:flex;flex-direction:column;gap:3px;overflow-y:auto}',
    '.nbs-place{display:flex;align-items:center;gap:8px;padding:7px 9px;border-radius:4px;cursor:pointer;font-size:13px;color:#cbbfa8;background:rgba(70,58,44,.35);border:1px solid rgba(220,188,120,.08)}',
    '.nbs-place:hover{color:#fff;border-color:rgba(220,188,120,.3)}',
    '.nbs-place.is-on{color:#FFC83A;background:rgba(255,200,58,.13);border-color:rgba(255,200,58,.55)}',
    '.nbs-place .nbs-pname{flex:1;min-width:0;white-space:nowrap;overflow:hidden;text-overflow:ellipsis}',
    '.nbs-cnt{margin-left:auto;font-size:11.5px;color:#7a6e5a}',
    '.nbs-dot{width:7px;height:7px;border-radius:50%;background:transparent;flex:none}',
    '.nbs-dot.is-w{background:#FFC83A;box-shadow:0 0 6px rgba(255,200,58,.6)}',
    '.nbs-sep{margin:8px 2px 3px;font-size:10.5px;font-weight:700;letter-spacing:1.5px;text-transform:uppercase;color:#7a6e5a}',
    '.nbs-col{flex:1;display:flex;flex-direction:column;min-height:0;min-width:0}',
    '.nbs-search{position:relative}',
    '.nbs-search input{width:100%;height:32px;padding:0 10px 0 30px;background:rgba(20,14,8,.6);border:1px solid rgba(220,188,120,.2);border-radius:4px;color:#f0e8d8;font:inherit;font-size:13px;outline:none}',
    '.nbs-search input:focus{border-color:rgba(255,200,58,.7)}',
    '.nbs-search span{position:absolute;left:10px;top:7px;color:#7a6e5a;font-size:13px}',
    '.nbs-scope{display:flex;margin-top:8px;border:1px solid rgba(220,188,120,.2);border-radius:4px;overflow:hidden;background:rgba(20,14,8,.5)}',
    '.nbs-scope button{flex:1;display:flex;align-items:center;justify-content:center;gap:6px;padding:5px 8px;font:inherit;font-size:12.5px;border:none;background:none;color:#cbbfa8;cursor:pointer}',
    '.nbs-scope button+button{border-left:1px solid rgba(220,188,120,.2)}',
    '.nbs-scope button.is-on{color:#FFC83A;background:rgba(255,200,58,.13)}',
    '.nbs-scope b{min-width:16px;padding:0 5px;border-radius:7px;font-size:11px;background:rgba(255,240,210,.1);color:#cbbfa8}',
    '.nbs-scope button.is-on b{background:#FFC83A;color:#2a1f10}',
    '.nbs-list{flex:1;margin-top:8px;overflow-y:auto;padding-right:4px}',
    '.nbs-list::-webkit-scrollbar,.nbs-places::-webkit-scrollbar{width:5px}',
    '.nbs-list::-webkit-scrollbar-thumb,.nbs-places::-webkit-scrollbar-thumb{background:rgba(255,200,58,.3);border-radius:3px}',
    '.nbs-row{display:flex;align-items:center;gap:9px;margin-top:4px;padding:3px 8px 3px 4px;cursor:pointer;border-radius:4px;background:rgba(70,58,44,.55);border:1px solid rgba(220,188,120,.11);font-size:12.5px}',
    '.nbs-row:hover{border-color:rgba(255,200,58,.6)}',
    '.nbs-row img{width:34px;height:34px;object-fit:contain;flex:none}',
    '.nbs-nm{flex:1;min-width:0;white-space:nowrap;overflow:hidden;text-overflow:ellipsis}',
    '.nbs-meta{color:#a89a82;font-size:11.5px;white-space:nowrap}',
    '.nbs-gain{font-size:11px;font-weight:700;color:#b5e09a;white-space:nowrap}',
    '.nbs-tag{font-size:10.5px;font-weight:700;padding:1px 6px;border-radius:3px;background:rgba(255,200,58,.15);color:#FFC83A}',
    '.nbs-row.is-no{opacity:.4;cursor:not-allowed}',
    '.nbs-where{color:#7a6e5a;font-size:11px;white-space:nowrap;max-width:120px;overflow:hidden;text-overflow:ellipsis}',
    '.nbs-hint{font-size:11.5px;color:#7a6e5a;margin-top:6px}',
    '.nbs-basket{position:relative;margin:4px auto 2px}',
    '.nbs-cell{position:absolute;width:' + CELL + 'px;height:' + CELL + 'px;border-radius:3px;background:rgba(20,14,8,.3);border:1px solid rgba(220,188,120,.14)}',
    '.nbs-cell img{position:absolute;left:50%;top:50%;transform:translate(-50%,-50%);object-fit:contain}',
    '.nbs-cell .nbs-q{position:absolute;right:3px;bottom:1px;font-size:11px;font-weight:700;color:#fff;text-shadow:0 0 3px #000}',
    '.nbs-cell.is-item{z-index:1;background:rgba(40,30,18,.9);cursor:pointer}',
    '.nbs-cell.is-item:hover{border-color:#FFC83A}',
    '.nbs-cell.is-w{border-color:rgba(255,200,58,.75);box-shadow:inset 0 0 10px rgba(255,200,58,.18)}',
    '.nbs-cell.is-w::after{content:"";position:absolute;top:3px;left:3px;width:6px;height:6px;border-radius:50%;background:#FFC83A;box-shadow:0 0 5px #FFC83A}',
    '.nbs-cell.is-ghost{z-index:1;border:1px dashed rgba(140,197,106,.7);background:rgba(140,197,106,.07)}',
    '.nbs-cell.is-ghost img{opacity:.4}',
    '.nbs-cell.is-landing{z-index:1;background:rgba(40,30,18,.9);border-color:rgba(140,197,106,.8)}',
    '.nbs-chips{display:flex;flex-wrap:wrap;gap:6px}',
    '.nbs-chip{display:flex;align-items:center;gap:5px;padding:3px 9px;border-radius:12px;font-size:12px;border:1px solid rgba(220,188,120,.25);color:#cbbfa8}',
    '.nbs-chip.is-hit{border-color:#8cc56a;color:#8cc56a;background:rgba(140,197,106,.1)}',
    '.nbs-chip.is-miss{border-color:rgba(255,200,58,.6);color:#FFC83A}',
    '.nbs-lbl{font-size:10.5px;font-weight:700;letter-spacing:1.5px;text-transform:uppercase;color:#7a6e5a;margin:12px 0 6px}',
    '.nbs-meter{height:8px;border-radius:4px;background:rgba(20,14,8,.7);border:1px solid rgba(220,188,120,.15);overflow:hidden;display:flex}',
    '.nbs-meter i{display:block;height:100%;background:#FFC83A}',
    '.nbs-meter i.is-gain{background:repeating-linear-gradient(45deg,#8cc56a 0 4px,#6fa452 4px 8px)}',
    '.nbs-mline{display:flex;justify-content:space-between;font-size:12px;color:#a89a82;margin-top:4px}',
    '.nbs-mline b{color:#f0e8d8;font-weight:400}',
    '.nbs-g{color:#8cc56a}',
    '.nbs-plan{margin-top:8px;padding:7px 9px;border-radius:4px;background:rgba(20,14,8,.5);border:1px dashed rgba(220,188,120,.25);font-size:12px;color:#cbbfa8;line-height:1.5}',
    '.nbs-plan:empty{display:none}',
    '.nbs-btns{display:flex;gap:8px;margin-top:8px}',
    '.nbs-btns:empty{display:none}',
    '.nbs-btn{flex:1;height:36px;border-radius:4px;border:1px solid rgba(255,200,58,.55);background:rgba(255,200,58,.1);color:#FFC83A;font:inherit;font-size:13px;font-weight:700;cursor:pointer}',
    '.nbs-btn:hover{background:rgba(255,200,58,.22)}',
    '.nbs-btn.is-main{background:#FFC83A;color:#2a1f10}',
    '.nbs-btn.is-main:hover{background:#ffd664}',
    '.nbs-btn.is-quiet{border-color:rgba(220,188,120,.2);background:none;color:#cbbfa8;font-weight:400}',
    '.nbs-btn[disabled]{opacity:.35;cursor:default}',
    '.nbs-row.is-gone{opacity:.3;pointer-events:none}',
    '.nbs-stat{display:flex;align-items:center;justify-content:space-between;gap:10px;padding:6px 0;border-bottom:1px solid rgba(220,188,120,.09);font-size:12.5px}',
    '.nbs-stat span:first-child{color:#a89a82}',
    '.nbs-stat span:last-child{text-align:right}',
    '.nbs-stat .is-warn{color:#FFC83A}',
    '.nbs-stat .is-bad{color:#e07a5f}',
    '.nbs-tip{position:fixed;z-index:950;pointer-events:none;padding:8px 10px;border-radius:4px;background:#241e18;border:1px solid rgba(220,188,120,.35);font-size:12px;box-shadow:0 6px 20px rgba(0,0,0,.6);max-width:260px;display:none}',
    '.nbs-tip b{color:#fff;display:block;margin-bottom:3px}',
    '.nbs-tip span{color:#a89a82}',
    '#' + OPEN_ID + '{position:fixed;right:24px;top:18px;z-index:900;display:none;height:34px;padding:0 14px;border-radius:4px;border:1px solid rgba(255,200,58,.55);background:rgba(36,30,24,.92);color:#FFC83A;font-family:"Microsoft YaHei","PingFang SC",sans-serif;font-size:13px;font-weight:700;cursor:pointer}'
  ].join('\n');

  function send(message) {
    try {
      if (window.vuplex) window.vuplex.postMessage(PREFIX + message);
    } catch (e) {}
  }

  // One button press at a time; the plugin's answer (a toast, here or through the Toolkit) frees it, and the timer
  // frees it anyway.
  function press(verb) {
    if (isBusy) return;

    isBusy = true;
    clearTimeout(busyTimer);
    busyTimer = setTimeout(function () { isBusy = false; }, BUSY_TIMEOUT_MS);
    send(verb);
  }

  function toast() {
    if (!document.getElementById('slnb-toast-style')) {
      var style = document.createElement('style');
      style.id = 'slnb-toast-style';
      style.textContent = TOAST_STYLE;
      document.head.appendChild(style);
    }

    var node = document.getElementById('slnb-toast');

    if (node) return node;

    node = document.createElement('div');
    node.id = 'slnb-toast';
    document.body.appendChild(node);

    return node;
  }

  function status(text) {
    isBusy = false;
    clearTimeout(busyTimer);

    var node = toast();

    if (!text) {
      node.style.display = 'none';

      return;
    }

    node.textContent = text;
    node.style.display = 'block';
    clearTimeout(toastTimer);
    toastTimer = setTimeout(function () { node.style.display = 'none'; }, TOAST_MS);
  }

  function setup(next) {
    settings = next || settings;
    isBusy = false;
    station.version++;
    removeApps();
  }

  // The basket panel's Vue state; a game update could rename it, so a failure keeps the station hidden and is
  // reported once. A page that is still loading has no app yet and is simply not the basket. Her wish chips, progress
  // and the basket's kg come from the game's own panel.
  function basketState(doc) {
    var state = { isBasket: false, setup: null, wish: null, maxKg: 0, usedKg: 0 };
    var app = doc.getElementById('app');

    if (!app || !app._vnode || !app._vnode.component) return state;

    try {
      var setup = app._vnode.component.setupState;

      state.isBasket = !!setup.isBasket;
      state.setup = setup;
      state.wish = setup.basketWish || null;
    } catch (e) {
      if (!hasReportedState) send('diag|cannot read the basket panel state, the station stays hidden: ' + e);
      hasReportedState = true;

      return state;
    }

    try {
      var bag = typeof state.setup.getBag === 'function' ? state.setup.getBag('B') : state.setup.bagB;
      var max = Number(bag.maxWeight);
      var used = Number(bag.currentWeight);

      if (isFinite(max) && isFinite(used) && max > 0) {
        state.maxKg = max;
        state.usedKg = used;
      }
    } catch (e) {}

    return state;
  }

  function addStyle(doc) {
    if (doc.getElementById('slnb-style')) return;

    var style = doc.createElement('style');
    style.id = 'slnb-style';
    style.textContent = STATION_STYLE;
    doc.head.appendChild(style);
  }

  function esc(text) {
    return String(text == null ? '' : text).replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');
  }

  function num(value) {
    var n = Number(value);

    return isFinite(n) ? n : 0;
  }

  // The plugin weighs in the game's units; the panel's own max kg converts them.
  function kgOf(units, state) {
    var data = station.data;

    if (!data || !state || !(data.max > 0) || !(state.maxKg > 0)) return '';

    return (units * state.maxKg / data.max).toFixed(1) + ' kg';
  }

  function placeName(id) {
    return station.names[id] || '';
  }

  // The stacks in the chosen place ("all" is every storage, without the bag), before the wanted filter and search.
  function inScope() {
    var data = station.data;

    return data.items.filter(function (item) {
      return station.sel === 'all' ? item.owner !== data.bag : String(item.owner) === station.sel;
    });
  }

  function filterOf(scope) {
    if (station.filt) return station.filt;

    return scope.some(function (item) { return item.wanted; }) ? 'wanted' : 'all';
  }

  // Info Pack draws its own item tooltip on any element tagged data-slip-item, and data-slip-note adds a line under
  // the name; when it is there, our rows carry the tags and our own tip stays hidden.
  function infoTags() {
    var pack = window.__slinfopack;

    return pack && pack.tags && pack.tags.indexOf ? pack.tags : [];
  }

  function hasInfoTip(item) {
    return !!item.cfg && infoTags().indexOf('item') >= 0;
  }

  // The progress one item and the whole stack add now. The first of a wished kind also brings its bonus; past that
  // every item adds the same.
  function gainText(item) {
    var labels = station.data.labels;

    if (item.g1 === undefined || !(item.g1 > 0)) return '';

    return item.q > 1 ? labels.each.replace('{0}', item.g1).replace('{1}', item.gs).replace('{2}', item.q) : labels.one.replace('{0}', item.g1);
  }

  function infoAttrs(item) {
    if (!hasInfoTip(item)) return '';

    var note = gainText(item);

    return ' data-slip-item="' + item.cfg + '"' + (note && infoTags().indexOf('note') >= 0 ? ' data-slip-note="' + esc(note) + '"' : '');
  }

  function itemById(id) {
    return station.byId[id] || null;
  }

  // The station is drawn into fixed slots, and each slot is only touched when its own html changed, so a new answer
  // that moves one stack leaves the other few hundred rows, and their icons, alone.
  var FRAME_HTML =
    '<div class="nbs-pane nbs-source"><div class="nbs-head"><span class="nbs-title" data-s="srctitle"></span><span class="nbs-sub" data-s="srcsub"></span></div>' +
    '<div class="nbs-split"><div class="nbs-places" data-s="places"></div><div class="nbs-col">' +
    '<div class="nbs-search"><span>&#9906;</span><input data-q="1"></div><div class="nbs-scope" data-s="scope"></div>' +
    '<div class="nbs-list" data-s="list"></div><div class="nbs-hint" data-s="addhint"></div></div></div></div>' +
    '<div class="nbs-pane nbs-basketpane"><div class="nbs-head"><span class="nbs-title" data-s="btitle"></span><span class="nbs-sub" data-s="bsub"></span></div>' +
    '<div class="nbs-basket" data-s="grid"></div><div class="nbs-hint" style="text-align:center" data-s="takehint"></div>' +
    '<div data-s="wishes"></div><div data-s="meters"></div><div class="nbs-plan" data-s="plan"></div>' +
    '<div class="nbs-btns" style="margin-top:14px" data-s="btns"></div><div class="nbs-btns" data-s="send"></div></div>' +
    '<div class="nbs-side"><div class="nbs-pane" data-s="side"></div><button class="nbs-btn is-quiet" data-act="game" style="flex:none" data-s="game"></button></div>' +
    '<div class="nbs-tip"></div>';

  function fill(node, name, html) {
    var el = node.__slots[name];

    if (el.__html === html) return;

    el.innerHTML = html;
    el.__html = html;
  }

  // Puts the rows in el in order, reusing every node whose key and html are unchanged.
  function patchKeyed(doc, el, rows) {
    var old = {};
    var tmp = doc.createElement('div');
    var prev = null;

    for (var child = el.firstElementChild; child; child = child.nextElementSibling) {
      if (child.__k !== undefined) old[child.__k] = child;
    }

    rows.forEach(function (row) {
      var node = old[row.k];

      if (node && node.__h === row.h) {
        delete old[row.k];
      } else {
        tmp.innerHTML = row.h;
        node = tmp.firstElementChild;
        node.__k = row.k;
        node.__h = row.h;
      }

      var at = prev ? prev.nextSibling : el.firstChild;

      if (at !== node) el.insertBefore(node, at);

      prev = node;
    });

    var rest = prev ? prev.nextSibling : el.firstChild;

    while (rest) {
      var next = rest.nextSibling;

      el.removeChild(rest);
      rest = next;
    }
  }

  function rowHtml(item, state) {
    var labels = station.data.labels;

    if (item.gone) return '<div class="nbs-row is-gone"><img src="' + esc(item.ic) + '"><span class="nbs-nm">' + esc(item.n) + '</span><span class="nbs-meta">x0</span></div>';

    var kg = kgOf(item.kg, state);

    return '<div class="nbs-row' + (item.no ? ' is-no' : '') + '" data-add="' + item.id + '"' + infoAttrs(item) + '><img src="' + esc(item.ic) + '"><span class="nbs-nm">' + esc(item.n) + '</span>' +
      (item.wanted ? '<span class="nbs-tag">' + esc(labels.wanted) + '</span>' : '') +
      (item.g1 > 0 ? '<span class="nbs-gain">+' + item.g1 + '</span>' : '') +
      '<span class="nbs-meta">' + item.w + 'x' + item.h + ' &middot; x' + item.q + (kg ? ' &middot; ' + kg : '') + '</span>' +
      (station.sel === 'all' ? '<span class="nbs-where">' + esc(placeName(item.owner)) + '</span>' : '') + '</div>';
  }

  function drawSource(doc, node, state) {
    var data = station.data;
    var labels = data.labels;
    var counts = {};
    var wants = {};
    var storageN = 0;

    data.items.forEach(function (item) {
      counts[item.owner] = (counts[item.owner] || 0) + 1;

      if (item.wanted) wants[item.owner] = true;
      if (item.owner !== data.bag) storageN++;
    });

    var place = function (key, name, count, isWant) {
      return { k: 'p' + key, h: '<div class="nbs-place' + (station.sel === key ? ' is-on' : '') + '" data-p="' + esc(key) + '"><span class="nbs-dot' + (isWant ? ' is-w' : '') + '"></span><span class="nbs-pname">' + esc(name) + '</span><span class="nbs-cnt">' + count + '</span></div>' };
    };
    var places = [{ k: 'sep-s', h: '<div class="nbs-sep">' + esc(labels.storage) + '</div>' }, place('all', labels.allStorage, storageN, true)];

    data.places.forEach(function (p) {
      if (p.id !== data.bag) places.push(place(String(p.id), p.name, counts[p.id] || 0, !!wants[p.id]));
    });

    if (data.bag) places.push({ k: 'sep-b', h: '<div class="nbs-sep">' + esc(labels.onYou) + '</div>' }, place(String(data.bag), labels.bag, counts[data.bag] || 0, !!wants[data.bag]));

    patchKeyed(doc, node.__slots.places, places);

    var scope = inScope();
    var filt = filterOf(scope);
    var query = station.query.toLowerCase();
    var wantN = 0;
    var visible = scope.filter(function (item) {
      if (item.wanted) wantN++;

      return (filt !== 'wanted' || item.wanted) && (!query || String(item.n).toLowerCase().indexOf(query) >= 0);
    });
    var rows = steady(visible, station.sel + '|' + filt + '|' + station.query).map(function (item) {
      return { k: item.gone ? item.k : rowKey(item), h: rowHtml(item, state) };
    });

    if (!rows.length) rows = [{ k: 'hint', h: '<div class="nbs-hint">' + esc(filt === 'wanted' ? labels.nothingWanted : labels.nothing) + '</div>' }];

    if (station.listKey !== station.orderKey) {
      station.listKey = station.orderKey;
      station.listShown = LIST_FIRST;
    }

    drawList(doc, node, rows);
    fill(node, 'srctitle', esc(labels.takeFrom));
    fill(node, 'srcsub', esc(labels.takeFromSub));
    fill(node, 'scope', '<button data-f="wanted" class="' + (filt === 'wanted' ? 'is-on' : '') + '">' + esc(labels.sheWants) + ' <b>' + wantN + '</b></button>' +
      '<button data-f="all" class="' + (filt === 'all' ? 'is-on' : '') + '">' + esc(labels.everything) + ' <b>' + scope.length + '</b></button>');
    fill(node, 'addhint', esc(labels.addHint));

    if (node.__input.placeholder !== labels.search) node.__input.placeholder = labels.search;
  }

  // The first rows of the list go in with the station; the rest follow a few dozen a frame, so a home with hundreds of
  // stacks never holds a frame up. Rows already shown stay shown through later answers, so the list never shrinks
  // under the mouse. A newer draw ends the steps of the one before it.
  function drawList(doc, node, rows) {
    var shown = station.listShown;

    station.listRows = rows;

    if (rows.length <= shown || typeof requestAnimationFrame === 'undefined') {
      patchKeyed(doc, node.__slots.list, rows);

      return;
    }

    patchKeyed(doc, node.__slots.list, rows.slice(0, shown));

    var token = ++station.listToken;

    requestAnimationFrame(function () {
      if (station.listToken !== token || station.doc !== doc || !station.data) return;

      station.listShown = shown + LIST_CHUNK;
      drawList(doc, node, station.listRows);
    });
  }

  function cellHtml(item, className, attr) {
    var width = item.w * PITCH - (PITCH - CELL);
    var height = item.h * PITCH - (PITCH - CELL);
    var size = Math.max(16, Math.min(width, height) - 14);

    return '<div class="nbs-cell ' + className + '"' + attr + ' style="left:' + item.x * PITCH + 'px;top:' + item.y * PITCH + 'px;width:' + width + 'px;height:' + height + 'px">' +
      '<img src="' + esc(item.ic) + '" style="width:' + size + 'px;height:' + size + 'px"><span class="nbs-q">' + (item.q > 1 ? item.q : '') + '</span></div>';
  }

  // While a fill is on its way, its planned stacks show solid in their cells; the plugin's answer replaces them with
  // what really went in.
  function drawBasket(doc, node, state, isFilling) {
    var data = station.data;
    var labels = data.labels;
    var wish = state.wish || {};
    var cols = data.cols || 5;
    var rows = data.gridRows || 3;
    var cells = [];
    var used = 0;

    for (var c = 0; c < cols * rows; c++) cells.push({ k: 'c' + c, h: '<div class="nbs-cell" style="left:' + (c % cols) * PITCH + 'px;top:' + Math.floor(c / cols) * PITCH + 'px"></div>' });

    data.basket.forEach(function (item) {
      used += item.w * item.h;
      cells.push({ k: 'i' + item.id, h: cellHtml(item, 'is-item' + (item.wanted ? ' is-w' : ''), ' data-take="' + item.id + '"' + infoAttrs(item)) });
    });

    // Where Fill would put each stack, drawn faintly in the free cells.
    (data.picks || []).forEach(function (pick) {
      var item = itemById(pick.id);

      if (item) cells.push({ k: 'g' + pick.id, h: cellHtml({ x: pick.x, y: pick.y, w: item.w, h: item.h, ic: item.ic, q: pick.q || item.q }, isFilling ? 'is-landing' : 'is-ghost', '') });
    });

    var grid = node.__slots.grid;
    var size = 'width:' + (cols * PITCH - (PITCH - CELL)) + 'px;height:' + (rows * PITCH - (PITCH - CELL)) + 'px';

    if (grid.__size !== size) {
      grid.style.cssText = size;
      grid.__size = size;
    }

    patchKeyed(doc, grid, cells);
    fill(node, 'btitle', esc(labels.basket));
    fill(node, 'bsub', esc(labels.cells.replace('{0}', used).replace('{1}', cols * rows)));
    fill(node, 'takehint', esc(labels.takeHint));

    var chips = (wish.chips || []).map(function (chip) {
      return '<span class="nbs-chip ' + (chip.hit ? 'is-hit' : 'is-miss') + '">' + (chip.hit ? '&#10003; ' : '') + esc(chip.name) + '</span>';
    }).join('');

    if (wish.crisisShow && wish.crisisChipName) chips += '<span class="nbs-chip ' + (wish.crisisHit ? 'is-hit' : 'is-miss') + '">' + (wish.crisisHit ? '&#10003; ' : '') + esc(wish.crisisChipName) + '</span>';

    fill(node, 'wishes', chips ? '<div class="nbs-lbl">' + esc(labels.sheWants) + '</div><div class="nbs-chips">' + chips + '</div>' : '');

    var meters = '';

    if (state.maxKg > 0) {
      var kw = Math.min(100, state.usedKg / state.maxKg * 100);

      meters += '<div class="nbs-lbl">' + esc(labels.weight) + '</div><div class="nbs-meter"><i style="width:' + kw + '%"></i></div>' +
        '<div class="nbs-mline"><span><b>' + state.usedKg.toFixed(1) + '</b> / ' + state.maxKg.toFixed(1) + ' kg</span><span>' + esc(labels.kgFree.replace('{0}', Math.max(0, state.maxKg - state.usedKg).toFixed(1))) + '</span></div>';
    }

    var cur = num(wish.cur);
    var max = num(wish.max);
    var gain = num(wish.gain);

    if (max > 0) {
      var pw = Math.min(cur, max) / max * 100;
      var gw = Math.max(0, Math.min(gain, max - cur)) / max * 100;

      meters += '<div class="nbs-lbl">' + esc(labels.progress) + '</div><div class="nbs-meter"><i style="width:' + pw + '%"></i><i class="is-gain" style="width:' + gw + '%"></i></div>' +
        '<div class="nbs-mline"><span><b>' + cur + '</b> / ' + max + '</span><span class="nbs-g">' + (gain > 0 ? esc(labels.ifSent.replace('{0}', gain)) : '') + '</span></div>';
    }

    fill(node, 'meters', meters);
    fill(node, 'plan', esc(data.plan || ''));

    var buttons = '';
    var isEmpty = !data.basket.length;
    var breakable = data.basket.filter(function (it) { return it.br; }).length;

    if (settings.fill && !data.away) buttons += '<button class="nbs-btn" data-act="fill" title="' + esc(labels.fillTitle) + '">' + esc(labels.fill) + '</button>';
    if (!data.away) buttons += '<button class="nbs-btn is-quiet" data-act="takeall" title="' + esc(labels.toBagTitle) + '"' + (isEmpty ? ' disabled' : '') + '>' + esc(labels.toBag) + '</button>';
    if (breakable && !data.away) buttons += '<button class="nbs-btn is-quiet" data-act="breakdown" title="' + esc(labels.breakDownTitle) + '">' + esc(labels.breakDown) + ' (' + breakable + ')</button>';
    if (settings.putAway && !data.away) buttons += '<button class="nbs-btn is-quiet" data-act="putaway" title="' + esc(labels.putAwayTitle) + '"' + (isEmpty ? ' disabled' : '') + '>' + esc(labels.putAway) + '</button>';
    if (settings.callBack && data.away) buttons += '<button class="nbs-btn is-quiet" data-act="callback" title="' + esc(labels.callBackTitle) + '">' + esc(labels.callBack) + '</button>';

    fill(node, 'btns', buttons);
    fill(node, 'send', '<button class="nbs-btn is-main" data-act="send"' + (data.away || isEmpty ? ' disabled' : '') + '>' + esc(data.away ? labels.away : labels.send) + '</button>');
  }

  function drawSide(node, state) {
    var data = station.data;
    var labels = data.labels;
    var tierName = state.wish && state.wish.tierName ? String(state.wish.tierName) : '';
    var rows = (data.rows || []).map(function (row) {
      var name = row.tier ? (tierName || row.tier) : '';
      var value = name && row.value ? name + ', ' + row.value : (name || row.value);

      return '<div class="nbs-stat"><span>' + esc(row.label) + '</span><span class="' + (row.tone ? 'is-' + row.tone : '') + '">' + esc(value) + '</span></div>';
    }).join('');

    fill(node, 'side', '<div class="nbs-head"><span class="nbs-title">' + esc(labels.nextDoor) + '</span><span class="nbs-sub">' + esc(data.day) + '</span></div>' + rows);
    fill(node, 'game', esc(labels.game));
  }

  // The station before its first answer: the frame and its titles, so opening the basket shows it at once.
  function drawEmpty(doc, node) {
    if (node.__drawn === 'empty') return;

    var labels = station.labels || {};

    node.__drawn = 'empty';
    ['places', 'list', 'grid'].forEach(function (name) { patchKeyed(doc, node.__slots[name], []); });
    ['srcsub', 'scope', 'addhint', 'bsub', 'takehint', 'wishes', 'meters', 'plan', 'btns', 'send'].forEach(function (name) { fill(node, name, ''); });
    fill(node, 'srctitle', esc(labels.takeFrom));
    fill(node, 'btitle', esc(labels.basket));
    fill(node, 'side', '<div class="nbs-head"><span class="nbs-title">' + esc(labels.nextDoor) + '</span></div>');
    fill(node, 'game', esc(labels.game || settings.gameLabel || 'Game screen'));
  }

  // Redraws only when the data, the panel's own numbers or the player's choices changed.
  function draw(doc, node) {
    if (!station.data) return;

    var state = station.state || {};
    var wish = state.wish || {};
    var isFilling = station.filling > Date.now();
    var key = [station.version, station.sel, station.filt, station.query, state.maxKg, state.usedKg, wish.cur, wish.max, wish.gain, wish.tierName,
      JSON.stringify(wish.chips || []), wish.crisisShow, wish.crisisHit, wish.crisisChipName, isFilling, infoTags().join(',')].join('|');

    if (key === node.__drawn) return;

    node.__drawn = key;
    drawSource(doc, node, state);
    drawBasket(doc, node, state, isFilling);
    drawSide(node, state);
  }

  // The station lives in the page's body, outside the game's Vue app, so a re-render of the inventory leaves it alone.
  function stationNode(doc) {
    var node = doc.getElementById(STATION_ID);

    if (node && node.__slots) return node;
    if (node) node.parentNode.removeChild(node);

    node = doc.createElement('div');
    node.id = STATION_ID;
    node.className = 'unity-interactive';
    node.innerHTML = FRAME_HTML;
    node.__slots = {};
    node.querySelectorAll('[data-s]').forEach(function (el) { node.__slots[el.getAttribute('data-s')] = el; });
    node.__input = node.querySelector('[data-q]');
    node.__tip = node.querySelector('.nbs-tip');
    // Clicks and keys stay in the station, so the inventory never reads them as its own.
    node.addEventListener('mousedown', function (e) { e.stopPropagation(); });
    node.addEventListener('keydown', function (e) { if (e.key !== 'Escape') e.stopPropagation(); });
    node.addEventListener('click', function (e) { e.stopPropagation(); onClick(doc, node, e); });
    node.__input.addEventListener('input', function () {
      station.query = node.__input.value;
      draw(doc, node);
    });
    node.addEventListener('mousemove', function (e) { onHover(doc, node, e); });
    doc.body.appendChild(node);

    var open = doc.getElementById(OPEN_ID);

    if (open) open.parentNode.removeChild(open);

    open = doc.createElement('button');
    open.id = OPEN_ID;
    open.className = 'unity-interactive';
    open.addEventListener('mousedown', function (e) { e.stopPropagation(); });
    open.addEventListener('click', function (e) {
      e.stopPropagation();
      station.isOff = false;
      station.askAt = 0;
      node.__drawn = '';
    });
    doc.body.appendChild(open);

    return node;
  }

  // Keeps the rows where they were while items go in and out, so a click lands on the row that was under the mouse.
  // The order is set when the list is first shown and again when the place, tab or search changes; a stack that went
  // in whole keeps its row, greyed out at x0, and one new to the list joins at the bottom. A row belongs to a cell, not
  // an item id: taking one unit with Stacking sends the stack's own item into the basket and leaves the rest on the
  // same cell under a new id, and that rest has to stay on its row.
  function rowKey(item) {
    return item.owner + ':' + item.cfg + ':' + item.x + ',' + item.y;
  }

  function steady(items, key) {
    if (!station.order || station.orderKey !== key) {
      station.order = items.map(rowKey);
      station.orderSet = {};
      station.order.forEach(function (row) { station.orderSet[row] = true; });
      station.orderKey = key;
    }

    var byKey = {};

    items.forEach(function (item) {
      var row = rowKey(item);

      byKey[row] = item;
      station.seen[row] = item;

      if (!station.orderSet[row]) {
        station.orderSet[row] = true;
        station.order.push(row);
      }
    });

    return station.order.map(function (row) {
      if (byKey[row]) return byKey[row];

      var last = station.seen[row];

      return last ? { k: row, id: last.id, n: last.n, ic: last.ic, gone: true } : null;
    }).filter(function (item) { return item; });
  }

  function onClick(doc, node, e) {
    var target = e.target.closest('[data-p],[data-f],[data-add],[data-take],[data-act]');
    var data = station.data;

    if (!target || !data) return;

    if (target.hasAttribute('data-p')) {
      station.sel = target.getAttribute('data-p');
      draw(doc, node);
    } else if (target.hasAttribute('data-f')) {
      station.filt = target.getAttribute('data-f');
      draw(doc, node);
    } else if (target.hasAttribute('data-add')) {
      var item = itemById(target.getAttribute('data-add'));

      if (!item) return;
      if (item.no) {
        status(data.labels.cannotTake);

        return;
      }

      send('add|' + item.id + '|' + (e.shiftKey ? 'all' : 'one'));
      shownMoved(doc, node, item, e.shiftKey);
    } else if (target.hasAttribute('data-take')) {
      var taken = itemById(target.getAttribute('data-take'));

      send('take|' + target.getAttribute('data-take'));
      if (taken) shownMoved(doc, node, taken, true);
    } else {
      var act = target.getAttribute('data-act');

      if (act === 'game') {
        station.isOff = true;
        hideStation(doc);
      } else if (act === 'send') {
        sendBasket(doc);
      } else {
        // Shift+Fill packs the basket as full as it goes instead of to the goal, so only a plain Fill shows its picks
        // landing.
        var isMax = act === 'fill' && e.shiftKey;

        if (act === 'fill' && !isMax && !isBusy && (data.picks || []).length) {
          station.filling = Date.now() + FILL_SHOW_MS;
          station.isGuess = true;
        }

        press(isMax ? 'fillmax' : act);
        draw(doc, node);
      }
    }
  }

  // Send is the game's own button press, so the game's checks and its sending all happen as usual.
  function sendBasket(doc) {
    var state = basketState(doc);

    // A new round scores differently, so the list sorts afresh.
    station.order = null;

    try {
      if (state.setup && typeof state.setup.onBasketSend === 'function') state.setup.onBasketSend();
      else doc.querySelector('.basket-send-btn').click();
    } catch (err) {
      send('diag|send failed: ' + err);
    }

    station.askAt = Date.now() + AFTER_MOVE_MS;
  }

  // Shows the click at once instead of waiting for the plugin's answer: one fewer in the stack, or the stack gone. The
  // plugin answers every move right after it runs, even when nothing changed, so a refused move comes back; until that
  // answer lands the station asks afresh.
  function shownMoved(doc, node, item, isWhole) {
    var data = station.data;

    if (isWhole || item.q <= 1) {
      data.items = data.items.filter(function (other) { return other !== item; });
      data.basket = data.basket.filter(function (other) { return other !== item; });
    } else {
      item.q -= 1;
    }

    station.isGuess = true;
    station.version++;
    draw(doc, node);
  }

  // The tip's text is built when the mouse reaches a new stack; moving within it only moves the tip.
  function onHover(doc, node, e) {
    var tip = node.__tip;
    var target = e.target.closest('[data-add],[data-take]');

    if (target !== station.hovered) {
      station.hovered = target;

      var item = target && itemById(target.getAttribute('data-add') || target.getAttribute('data-take'));

      if (!item || hasInfoTip(item)) {
        station.hovered = null;
        tip.style.display = 'none';

        return;
      }

      var kg = kgOf(item.kg, station.state);
      var gain = gainText(item);

      tip.innerHTML = '<b>' + esc(item.n) + '</b><span>' + item.w + 'x' + item.h + ' &middot; x' + item.q + (kg ? ' &middot; ' + kg : '') + '<br>' + esc(placeName(item.owner)) + '</span>' +
        (gain ? '<span class="nbs-gain" style="display:block;margin-top:3px">' + esc(gain) + '</span>' : '');
      tip.style.display = 'block';
      // Measured once per stack: reading the size after each move would make the browser lay the page out again.
      tip.__w = tip.offsetWidth;
      tip.__h = tip.offsetHeight;
    }

    if (!target) return;

    var view = doc.defaultView;
    var left = e.clientX + 14;
    var top = e.clientY + 14;

    if (left + tip.__w > view.innerWidth - 8) left = e.clientX - tip.__w - 14;
    if (top + tip.__h > view.innerHeight - 8) top = e.clientY - tip.__h - 14;

    tip.style.left = left + 'px';
    tip.style.top = top + 'px';
  }

  function show(el, display) {
    if (el && el.style.display !== display) el.style.display = display;
  }

  function hideStation(doc) {
    show(doc.getElementById(STATION_ID), 'none');
    show(doc.getElementById(OPEN_ID), 'none');
  }

  // Each time the basket opens, the station opens with it, on all storage, and asks afresh: nothing from the last
  // opening is kept but the labels.
  function openSession(doc) {
    station.isOpen = true;
    station.doc = doc;
    station.isOff = false;
    station.sel = 'all';
    station.filt = '';
    station.query = '';
    station.data = null;
    station.byId = {};
    station.names = {};
    station.isGuess = false;
    station.order = null;
    station.seen = {};
    station.listKey = null;
    station.listShown = LIST_FIRST;
    station.listToken++;
    station.askAt = 0;
    station.filling = 0;
    station.hovered = null;
    station.waitSince = Date.now();
    station.version++;

    var node = stationNode(doc);

    node.__input.value = '';
    node.__tip.style.display = 'none';
  }

  function patchStation(doc, state) {
    var node = stationNode(doc);
    var open = doc.getElementById(OPEN_ID);
    var now = Date.now();

    station.state = state;

    if (station.isDead) {
      hideStation(doc);

      return;
    }

    if (station.isOff) {
      show(node, 'none');
      open.textContent = station.labels ? station.labels.station : settings.stationLabel || 'Basket station';
      show(open, 'block');

      return;
    }

    show(open, 'none');

    // Asks quickly until the first answer, then about once a second; afresh while it holds nothing or a guessed move.
    if (now >= station.askAt) {
      station.askAt = now + (station.data ? STATION_REFRESH_MS : FIRST_ASK_MS);
      send(station.data && !station.isGuess ? 'station' : 'station|new');
    }

    if (!station.data) {
      // An answer that does not come leaves the game's own screen, still asking.
      if (now - station.waitSince > LOADING_MS) {
        show(node, 'none');

        return;
      }

      drawEmpty(doc, node);
      show(node, 'flex');

      return;
    }

    draw(doc, node);
    show(node, 'flex');
  }

  // The plugin's answer to 'station'; false turns the station off until the next start, leaving the game's screen.
  // Any answer ends a fill's landing view. One that arrives while the basket is closed is dropped: the next opening
  // asks afresh.
  function setStation(data) {
    if (data === false) {
      station.isDead = true;
      station.data = null;

      return;
    }

    if (!data || typeof data !== 'object' || !data.labels) return;

    station.filling = 0;
    station.labels = data.labels;

    if (!station.isOpen) return;

    var byId = {};
    var names = {};

    data.items.forEach(function (item) { byId[item.id] = item; });
    data.basket.forEach(function (item) { byId[item.id] = item; });
    data.places.forEach(function (place) { names[place.id] = place.name; });
    names[data.bag] = data.labels.bag;

    station.data = data;
    station.byId = byId;
    station.names = names;
    station.isGuess = false;
    station.hovered = null;
    station.version++;

    // Drawn now rather than on the next tick, so a move shows its answer at once.
    try {
      var doc = station.doc;
      var node = doc && doc.getElementById(STATION_ID);

      if (node && node.__slots && !station.isOff && !station.isDead) {
        draw(doc, node);
        show(node, 'flex');
      }
    } catch (e) {}
  }

  // Checked every 150 ms. The game hides a page by its frame's own style, which costs nothing to read, so a hidden page
  // never makes the browser work out the layout.
  function isFrameShown(frame) {
    if (frame.style.display === 'none') return false;
    if (frame.offsetWidth === 0 && frame.offsetHeight === 0) return false;

    var style = window.getComputedStyle(frame);

    return style.display !== 'none' && style.visibility !== 'hidden';
  }

  // The game can keep an old inventory page hidden next to a new one; only a shown page with the basket open counts,
  // and the station in any other is hidden.
  function patchBasket() {
    var frames = document.getElementsByTagName('iframe');
    var active = null;
    var activeState = null;

    for (var i = 0; i < frames.length; i++) {
      if (!/BackpackUI\.html/.test(frames[i].src || '')) continue;

      var doc = null;

      try {
        doc = frames[i].contentDocument;
      } catch (e) {}

      if (!doc) {
        if (!hasReportedAccess) send('diag|cannot reach the inventory page document (iframe ' + frames[i].src + ')');
        hasReportedAccess = true;
        continue;
      }

      if (!doc.head || !doc.body) continue;

      // The panel's own state is a plain read; the layout is asked only of a page that has the basket open.
      var state = !active && frames[i].style.display !== 'none' ? basketState(doc) : null;

      if (state && state.isBasket && isFrameShown(frames[i])) {
        active = doc;
        activeState = state;
      } else {
        hideStation(doc);
      }
    }

    if (!active) {
      station.isOpen = false;
      station.doc = null;

      return;
    }

    addStyle(active);

    if (!station.isOpen || station.doc !== active) openSession(active);

    patchStation(active, activeState);
  }


  // ===== The phone app =====

  var PHONE_CSS = [
    '.app.' + APP_CLASS + ' .app-icon{background:linear-gradient(180deg,#e7b46a,#8a5a22)}',
    '.' + SCREEN_CLASS + '{position:absolute;left:0;top:0;right:0;bottom:0;z-index:50;display:flex;flex-direction:column;background:#16130f}',
    '.slnb-phone-bar{flex:none;display:flex;align-items:center;gap:10px;height:40px;padding:0 10px;background:#2a2016;color:#fbb034;font-size:15px}',
    '.slnb-phone-back{background:transparent;border:1px solid rgba(251,176,52,.45);border-radius:4px;color:#fbb034;padding:3px 10px;font-size:13px;cursor:pointer}',
    '.slnb-phone-title{flex:1}',
    '.slnb-phone-x{background:transparent;border:none;color:#fbb034;font-size:20px;line-height:16px;padding:4px 6px;cursor:pointer}',
    '.slnb-phone-body{flex:1;overflow-y:auto;padding:8px 12px;color:#ddd;font-size:13px;line-height:1.45}',
    '.slnb-rows{margin-bottom:10px;border-bottom:1px solid rgba(251,176,52,.25);padding-bottom:6px}',
    '.slnb-row{display:flex;justify-content:space-between;gap:10px;padding:2px 0}',
    '.slnb-row-value{color:#fbb034;text-align:right}',
    '.slnb-row-value.is-warn{color:#FFB74D;font-weight:600}',
    '.slnb-row-value.is-bad{color:#FF6E6E;font-weight:600}',
    '.slnb-day{margin:10px 0 4px;color:#fbb034;font-weight:600}',
    '.slnb-entry{padding:3px 0 3px 8px;border-left:2px solid rgba(251,176,52,.3);margin-bottom:4px}',
    '.slnb-entry.is-said{border-left-color:rgba(160,200,255,.45);color:#c8d8ee}',
    '.slnb-entry-head{color:#aaa;font-size:12px}',
    '.slnb-entry-note{color:#9fd77a;font-size:12px}',
    '.slnb-empty{color:#aaa;padding:12px 0;text-align:center}'
  ].join('\n');

  function make(doc, tag, className, text) {
    var node = doc.createElement(tag);

    if (className) node.className = className;
    if (text != null) node.textContent = text;

    return node;
  }

  function removeApps() {
    var frames = document.getElementsByTagName('iframe');

    for (var i = 0; i < frames.length; i++) {
      var doc = null;

      try {
        doc = frames[i].contentDocument;
      } catch (e) {}

      if (!doc || !/PhoneSMS\.html/.test(frames[i].src || '')) continue;

      var tiles = doc.querySelectorAll('.' + APP_CLASS);

      for (var j = 0; j < tiles.length; j++) tiles[j].remove();
    }
  }

  function addPhoneStyle(doc) {
    if (doc.getElementById(PHONE_STYLE_ID)) return;

    var style = doc.createElement('style');

    style.id = PHONE_STYLE_ID;
    style.textContent = PHONE_CSS;
    (doc.head || doc.body).appendChild(style);
  }

  // The markup of the phone's own apps: an icon square with a stroked glyph, and the label under it.
  function phoneApp(doc) {
    var app = make(doc, 'div', 'app ' + APP_CLASS);

    // Exiled's Toolkit puts marked apps in the phone's dock, lowest number first; without it this does nothing.
    app.setAttribute('data-etk-phone', '20');
    var icon = make(doc, 'div', 'app-icon');
    var glyph = make(doc, 'div', 'icon-glyph');
    var svg = doc.createElementNS(SVG_NS, 'svg');

    svg.setAttribute('viewBox', '0 0 24 24');
    svg.setAttribute('fill', 'none');
    BASKET_STROKES.forEach(function (d) {
      var stroke = doc.createElementNS(SVG_NS, 'path');

      stroke.setAttribute('d', d);
      stroke.setAttribute('stroke-linejoin', 'round');
      svg.appendChild(stroke);
    });
    glyph.appendChild(svg);
    icon.appendChild(glyph);
    app.appendChild(icon);
    app.appendChild(make(doc, 'div', 'app-label', settings.appLabel));
    app.addEventListener('click', function (e) {
      e.stopPropagation();
      openPhone(doc);
    });

    return app;
  }

  // The app this page was given, while it is still on the page. Exiled's Toolkit moves it into the phone's dock, which
  // keeps it on the page.
  function hasApp(doc) {
    var app = doc.slnbApp;

    return !!app && app.isConnected === true;
  }

  // The game and other mods change the phone page constantly while it is open, so once the app is in, each change
  // costs one check that it is still on the page, and no search.
  function addApp(doc) {
    if (!settings.app || hasApp(doc)) return;

    // Look for an app anywhere on the page, not only in the grid, in case one went in before this page was known.
    var found = doc.querySelector('.' + APP_CLASS);

    if (found) {
      doc.slnbApp = found;
      return;
    }

    var grid = doc.querySelector('.app-grid');

    if (!grid) return;

    addPhoneStyle(doc);
    doc.slnbApp = grid.appendChild(phoneApp(doc));
  }

  function addAppSafely(doc) {
    try {
      addApp(doc);
    } catch (e) {}
  }

  // The game builds a new phone page every time the phone opens and shows it two frames after the page starts, long
  // before the next page check. So a phone page is watched from the moment its frame appears, and the app goes in
  // while the game builds the page. The observer runs before the next paint; addApp only adds what is missing.
  function watchPhonePage(frame) {
    try {
      var doc = frame.contentDocument;
      var win = doc && doc.defaultView;

      if (!doc || !win || !PHONE_PAGE.test(String(win.location.href))) return false;

      if (!doc.slnbPhoneWatched && typeof win.MutationObserver !== 'undefined') {
        doc.slnbPhoneWatched = true;
        new win.MutationObserver(function () { addAppSafely(doc); }).observe(doc, { childList: true, subtree: true });
      }

      addAppSafely(doc);

      return true;
    } catch (e) {
      // A page from another origin, or one being torn down: the page check tries again.
      return true;
    }
  }

  // A new frame first holds an empty document; the phone page replaces it a moment later. Look each frame until it is
  // there, for about two seconds at most. A new look at a frame ends the one before it, so a frame has one chase.
  function chasePhonePage(frame) {
    var chase = frame.slnbPhoneChase = (frame.slnbPhoneChase || 0) + 1;
    var framesLeft = CHASE_FRAMES;

    function step() {
      if (frame.slnbPhoneChase !== chase || watchPhonePage(frame) || --framesLeft <= 0 || typeof requestAnimationFrame === 'undefined') return;

      requestAnimationFrame(step);
    }

    step();
  }

  function onFrame(frame) {
    if (!PHONE_PAGE.test(String(frame.src || ''))) return;

    if (!frame.slnbPhoneFrame) {
      frame.slnbPhoneFrame = true;
      frame.addEventListener('load', function () { chasePhonePage(frame); });
    }

    chasePhonePage(frame);
  }

  // Every game page is an iframe in this document: a new one, or one sent to a new address, is looked at at once.
  function watchFrames() {
    if (typeof MutationObserver === 'undefined') return;

    new MutationObserver(function (records) {
      for (var i = 0; i < records.length; i++) {
        var record = records[i];

        if (record.type === 'attributes') {
          if (record.target.tagName === 'IFRAME') onFrame(record.target);

          continue;
        }

        for (var j = 0; j < record.addedNodes.length; j++) {
          var node = record.addedNodes[j];

          if (node.nodeType !== 1) continue;

          if (node.tagName === 'IFRAME') {
            onFrame(node);
            continue;
          }

          var inner = node.getElementsByTagName('iframe');

          for (var k = 0; k < inner.length; k++) onFrame(inner[k]);
        }
      }
    }).observe(document, { childList: true, subtree: true, attributes: true, attributeFilter: ['src'] });
  }

  function patchPhone(doc, frame) {
    addApp(doc);

    if (phone.screen && phone.screen.ownerDocument === doc) {
      if (frame.style.display === 'none' || !phone.screen.isConnected) closePhone();
      else if (Date.now() >= phone.refreshAt) askForData();
    }
  }

  function openPhone(doc) {
    var panel = doc.querySelector('.sms-panel');

    if (!panel) return;

    closePhone();

    var screen = make(doc, 'div', SCREEN_CLASS);
    var bar = make(doc, 'div', 'slnb-phone-bar');
    var back = make(doc, 'button', 'slnb-phone-back', (phone.data && phone.data.back) || 'Back');
    var title = make(doc, 'span', 'slnb-phone-title', settings.appLabel);
    var close = make(doc, 'button', 'slnb-phone-x', '\u00d7');

    back.addEventListener('click', function (e) { e.stopPropagation(); closePhone(); });
    close.addEventListener('click', function (e) { e.stopPropagation(); closePhone(); });
    bar.appendChild(back);
    bar.appendChild(title);
    bar.appendChild(close);
    screen.appendChild(bar);
    screen.appendChild(make(doc, 'div', 'slnb-phone-body'));
    screen.addEventListener('mousedown', function (e) { e.stopPropagation(); });
    panel.appendChild(screen);
    phone.screen = screen;

    if (phone.data) render();

    askForData();
  }

  function closePhone() {
    if (phone.screen) phone.screen.remove();

    phone.screen = null;
  }

  // The plugin answers only when something changed since its last answer, so a page that holds nothing asks afresh.
  function askForData() {
    phone.refreshAt = Date.now() + APP_REFRESH_MS;
    send(phone.data ? 'app' : 'app|new');
  }

  // The plugin's answer to 'app': her status rows and the log, grouped by day, newest first.
  function app(data) {
    phone.data = data;

    if (phone.screen) render();
  }

  // The status rows and the newest entries go in at once; the rest of the log follows a few dozen a frame, so opening
  // the phone on a long log never holds a frame up. A newer render, or the phone closing, ends the one before it.
  function render() {
    var screen = phone.screen;
    var doc = screen.ownerDocument;
    var body = screen.__slnbBody || (screen.__slnbBody = screen.querySelector('.slnb-phone-body'));
    var data = phone.data;

    if (!body || !data || screen.__slnbData === data) return;

    screen.__slnbData = data;

    var scroll = body.scrollTop;
    var token = phone.renderToken = (phone.renderToken || 0) + 1;

    screen.querySelector('.slnb-phone-title').textContent = data.title;
    screen.querySelector('.slnb-phone-back').textContent = data.back;
    body.textContent = '';

    var rows = make(doc, 'div', 'slnb-rows');

    (data.rows || []).forEach(function (row) {
      var line = make(doc, 'div', 'slnb-row');

      var value = row.tier && row.value ? row.tier + ', ' + row.value : (row.tier || row.value);

      line.appendChild(make(doc, 'span', 'slnb-row-label', row.label));
      line.appendChild(make(doc, 'span', 'slnb-row-value' + (row.tone ? ' is-' + row.tone : ''), value));
      rows.appendChild(line);
    });
    body.appendChild(rows);

    if (!data.days || !data.days.length) body.appendChild(make(doc, 'div', 'slnb-empty', data.empty));

    var days = data.days || [];
    var at = { day: 0, entry: -1 };

    // Builds up to count more lines (a day's heading or an entry) off the page, then puts them in with one change.
    function more(count) {
      var chunk = make(doc, 'div', 'slnb-chunk');

      for (var n = 0; n < count && at.day < days.length; n++) {
        var day = days[at.day];

        if (at.entry < 0) {
          chunk.appendChild(make(doc, 'div', 'slnb-day', day.label));
        } else {
          var entry = day.entries[at.entry];
          var node = make(doc, 'div', 'slnb-entry is-' + entry.kind);

          node.appendChild(make(doc, 'div', 'slnb-entry-head', entry.time + '  ' + entry.label));
          node.appendChild(make(doc, 'div', 'slnb-entry-text', entry.text));

          if (entry.note) node.appendChild(make(doc, 'div', 'slnb-entry-note', entry.note));

          chunk.appendChild(node);
        }

        if (++at.entry >= (day.entries || []).length) {
          at.day++;
          at.entry = -1;
        }
      }

      body.appendChild(chunk);

      // The place the list was read to is kept as soon as the list is long enough to reach it again.
      if (body.scrollTop < scroll) body.scrollTop = scroll;
    }

    function step() {
      if (phone.renderToken !== token || phone.screen !== screen) return;

      more(PHONE_CHUNK);

      if (at.day < days.length && typeof requestAnimationFrame !== 'undefined') requestAnimationFrame(step);
    }

    more(PHONE_FIRST);

    if (at.day < days.length) {
      if (typeof requestAnimationFrame === 'undefined') more(Infinity);
      else requestAnimationFrame(step);
    }
  }

  function patchPhones() {
    var frames = document.getElementsByTagName('iframe');
    var isOpenSomewhere = false;

    for (var i = 0; i < frames.length; i++) {
      if (!/PhoneSMS\.html/.test(frames[i].src || '')) continue;

      var doc = null;

      try {
        doc = frames[i].contentDocument;
      } catch (e) {}

      if (!doc || !doc.body) continue;

      patchPhone(doc, frames[i]);

      if (phone.screen && phone.screen.ownerDocument === doc) isOpenSomewhere = true;
    }

    if (phone.screen && !isOpenSomewhere) closePhone();
  }

  // The alert line on the game's Rescue panel (the HUD page CoreUI1): the most urgent warning, "+N more", and every
  // warning in a list that opens on hover. The plugin sends it when it changes, or null when nothing is due. The
  // panel ignores the mouse, so the line takes it back and carries data-interactive, which is how the HUD tells the
  // game a pointer over it is on the UI.
  var HUD_ID = 'slnb-hud-alert';
  var HUD_STYLE_ID = 'slnb-hud-style';
  var HUD_STYLE = [
    '#' + HUD_ID + '{position:relative;pointer-events:auto;display:flex;align-items:center;gap:6px;font-size:11.5px;font-weight:bold;text-shadow:0.7px 0.7px 1.4px black;white-space:nowrap;cursor:default}',
    '#' + HUD_ID + '.is-warn{color:#FFC107}',
    '#' + HUD_ID + '.is-bad{color:#ff5a4d}',
    '#' + HUD_ID + ' .slnb-hud-dot{width:7px;height:7px;border-radius:50%;background:currentColor;box-shadow:0 0 6px currentColor;flex-shrink:0}',
    '#' + HUD_ID + '.is-bad .slnb-hud-dot{animation:slnb-hud-pulse 1.2s ease-in-out infinite}',
    '#' + HUD_ID + ' .slnb-hud-text{overflow:hidden;text-overflow:ellipsis}',
    '#' + HUD_ID + ' .slnb-hud-more{color:#cfd3d5;font-weight:normal;flex-shrink:0}',
    '#' + HUD_ID + ' .slnb-hud-tip{position:absolute;left:0;top:100%;margin-top:6px;z-index:30;width:300px;padding:6px 10px;border-radius:4px;background:rgba(15,15,15,.96);border:1px solid rgba(255,255,255,.22);color:#cfd3d5;font-size:12px;font-weight:normal;text-shadow:none;white-space:normal;line-height:1.45;opacity:0;transition:opacity .12s ease;pointer-events:none}',
    '#' + HUD_ID + ':hover .slnb-hud-tip{opacity:1}',
    '#' + HUD_ID + ' .slnb-hud-tip b{display:block;color:#ffd970;font-weight:600;margin-bottom:3px}',
    '#' + HUD_ID + ' .slnb-hud-tip div{padding-left:9px;border-left:2px solid #FFC107;margin-top:4px}',
    '#' + HUD_ID + ' .slnb-hud-tip div.is-bad{border-left-color:#ff5a4d}',
    '@keyframes slnb-hud-pulse{0%,100%{opacity:1}50%{opacity:.35}}'
  ].join('\n');
  var hud = { data: null, key: '', drawn: '' };
  var hasReportedHud = false;

  function setHud(data) {
    hud.data = data || null;
    // Kept with the data, so the 400 ms check compares it without building it again.
    hud.key = hud.data ? JSON.stringify(hud.data) : '';

    try {
      patchHud();
    } catch (e) {}
  }

  function drawHud(doc, line) {
    var data = hud.data;

    line.className = data.tone === 'bad' ? 'is-bad' : 'is-warn';
    line.textContent = '';
    line.appendChild(make(doc, 'span', 'slnb-hud-dot'));
    line.appendChild(make(doc, 'span', 'slnb-hud-text', data.text));

    if (data.more) line.appendChild(make(doc, 'span', 'slnb-hud-more', data.more));

    var tip = make(doc, 'span', 'slnb-hud-tip');

    tip.appendChild(make(doc, 'b', null, data.title));
    (data.all || []).forEach(function (alert) {
      tip.appendChild(make(doc, 'div', alert.tone === 'bad' ? 'is-bad' : '', alert.text));
    });
    line.appendChild(tip);
  }

  function patchHudIn(doc) {
    var line = doc.getElementById(HUD_ID);

    if (!hud.data) {
      if (line) line.parentNode.removeChild(line);

      return;
    }

    // The neighbor's panel is the one with her wish chips; the warehouse rescue uses the same class without them. While
    // the line and the wish row it was put next to are both still on the page, the panel is not searched for again.
    if (line && line.__slnbWish && line.__slnbWish.isConnected && line.isConnected && line.parentNode === line.__slnbPanel) {
      if (line.__slnbDrawn !== hud.key) {
        drawHud(doc, line);
        line.__slnbDrawn = hud.key;
      }

      return;
    }

    var wish = doc.querySelector('.rescue-panel .rescue-wishrow');
    var panel = wish && wish.closest('.rescue-panel');

    if (!panel) {
      if (line) line.parentNode.removeChild(line);

      return;
    }

    if (!doc.getElementById(HUD_STYLE_ID)) {
      var style = doc.createElement('style');

      style.id = HUD_STYLE_ID;
      style.textContent = HUD_STYLE;
      (doc.head || doc.documentElement).appendChild(style);
    }

    var key = hud.key;

    if (!line || line.parentNode !== panel) {
      if (line) line.parentNode.removeChild(line);

      line = doc.createElement('div');
      line.id = HUD_ID;
      line.setAttribute('data-interactive', '');
      panel.appendChild(line);
      line.__slnbDrawn = '';
    }

    line.__slnbWish = wish;
    line.__slnbPanel = panel;

    if (line.__slnbDrawn !== key) {
      drawHud(doc, line);
      line.__slnbDrawn = key;
    }
  }

  function patchHud() {
    var frames = document.getElementsByTagName('iframe');

    for (var i = 0; i < frames.length; i++) {
      if (!/CoreUI1\.html/.test(frames[i].src || '')) continue;

      var doc = null;

      try {
        doc = frames[i].contentDocument;
      } catch (e) {}

      if (!doc || !doc.body) {
        if (hud.data && !hasReportedHud) {
          hasReportedHud = true;
          send('diag|cannot reach the HUD page document (iframe ' + frames[i].src + ')');
        }

        continue;
      }

      patchHudIn(doc);
    }
  }

  watchFrames();

  // The station is checked more often than the rest, so it opens and closes with the basket.
  setInterval(function () {
    try {
      patchBasket();
    } catch (e) {}
  }, 150);

  setInterval(function () {
    try {
      patchPhones();
    } catch (e) {}

    try {
      patchHud();
    } catch (e) {}
  }, 400);

  window.__slneighborbasket = { status: status, setup: setup, app: app, station: setStation, hud: setHud };
  send('ready');
})();
