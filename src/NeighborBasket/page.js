// Neighbor Basket's web half. Runs in Root.html, the document every game page iframe sits in. While the neighbor's
// basket is open it lays the basket station over the inventory page: every storage at home and the bag on the left,
// the basket's grid with her wishes, weight and progress in the middle, her status on the right, with the Fill, Put
// away, Call back and Send buttons. A "Game screen" button hides it until the basket is opened again. It also adds the
// Neighbor app to the phone and shows a short toast when Exiled's Toolkit is not there to. Talks to the plugin through
// vuplex.postMessage("slmod|neighborbasket|<verb>") and is driven by window.__slneighborbasket.
(function () {
  if (window.__slneighborbasket) return;

  var PREFIX = 'slmod|neighborbasket|';
  var BUSY_TIMEOUT_MS = 4000;
  var TOAST_MS = 4500;
  var STATION_ID = 'slnb-station';
  var OPEN_ID = 'slnb-station-open';
  var STATION_REFRESH_MS = 1000;
  var AFTER_MOVE_MS = 250;
  // One basket cell on screen, and the gap between cells.
  var CELL = 70;
  var PITCH = 74;
  var APP_CLASS = 'slnb-app';
  var SCREEN_CLASS = 'slnb-phone';
  var PHONE_STYLE_ID = 'slnb-phone-style';
  var APP_REFRESH_MS = 5000;
  var SVG_NS = 'http://www.w3.org/2000/svg';
  // A basket: the body and its handle.
  var BASKET_STROKES = ['M4 10h16l-2 9H6z', 'M8 10l4-6 4 6', 'M9 13v3', 'M15 13v3'];

  var settings = { fill: true, callBack: false, putAway: false, app: true, fillLabel: 'Fill', fillTitle: '', callBackLabel: 'Call back', callBackTitle: '', putAwayLabel: 'Put away', putAwayTitle: '', appLabel: 'Neighbor' };
  var phone = { screen: null, data: null, refreshAt: 0 };
  // The station's data from the plugin and the player's own choices in it. isOff is the Game screen button; it lasts
  // until the basket closes.
  var station = { data: null, json: '', askAt: 0, isOff: false, isDead: false, sel: 'all', filt: '', query: '', drawn: '' };
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
    removeApps();
  }

  // The basket panel's Vue state; a game update could rename it, so a failure keeps the station hidden and is
  // reported once. Her wish chips, progress and the basket's kg come from the game's own panel.
  function basketState(doc) {
    var state = { isBasket: false, setup: null, wish: null, maxKg: 0, usedKg: 0 };

    try {
      var setup = doc.getElementById('app')._vnode.component.setupState;

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

    if (!data || !(data.max > 0) || !(state.maxKg > 0)) return '';

    return (units * state.maxKg / data.max).toFixed(1) + ' kg';
  }

  function placeName(id) {
    var data = station.data;

    if (id === data.bag) return data.labels.bag;

    for (var i = 0; i < data.places.length; i++) {
      if (data.places[i].id === id) return data.places[i].name;
    }

    return '';
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

  function shown() {
    var scope = inScope();
    var filt = filterOf(scope);
    var query = station.query.toLowerCase();

    return scope.filter(function (item) {
      return (filt !== 'wanted' || item.wanted) && (!query || String(item.n).toLowerCase().indexOf(query) >= 0);
    });
  }

  function itemById(id) {
    var data = station.data;
    var all = data.items.concat(data.basket);

    for (var i = 0; i < all.length; i++) {
      if (String(all[i].id) === String(id)) return all[i];
    }

    return null;
  }

  function sourceHtml(state) {
    var data = station.data;
    var labels = data.labels;
    var storages = data.places.filter(function (place) { return place.id !== data.bag; });
    var countOf = function (id) { return data.items.filter(function (item) { return item.owner === id; }).length; };
    var hasWant = function (id) { return data.items.some(function (item) { return item.owner === id && item.wanted; }); };
    var place = function (key, name, count, isWant) {
      return '<div class="nbs-place' + (station.sel === key ? ' is-on' : '') + '" data-p="' + esc(key) + '"><span class="nbs-dot' + (isWant ? ' is-w' : '') + '"></span><span class="nbs-pname">' + esc(name) + '</span><span class="nbs-cnt">' + count + '</span></div>';
    };
    var html = '<div class="nbs-sep">' + esc(labels.storage) + '</div>';

    html += place('all', labels.allStorage, data.items.filter(function (item) { return item.owner !== data.bag; }).length, true);
    storages.forEach(function (p) { html += place(String(p.id), p.name, countOf(p.id), hasWant(p.id)); });

    if (data.bag) html += '<div class="nbs-sep">' + esc(labels.onYou) + '</div>' + place(String(data.bag), labels.bag, countOf(data.bag), hasWant(data.bag));

    var scope = inScope();
    var filt = filterOf(scope);
    var wantN = scope.filter(function (item) { return item.wanted; }).length;
    var items = shown();
    var list = items.map(function (item) {
      return '<div class="nbs-row' + (item.no ? ' is-no' : '') + '" data-add="' + item.id + '"><img src="' + esc(item.ic) + '"><span class="nbs-nm">' + esc(item.n) + '</span>' +
        (item.wanted ? '<span class="nbs-tag">' + esc(labels.wanted) + '</span>' : '') +
        '<span class="nbs-meta">' + item.w + 'x' + item.h + ' &middot; x' + item.q + (kgOf(item.kg, state) ? ' &middot; ' + kgOf(item.kg, state) : '') + '</span>' +
        (station.sel === 'all' ? '<span class="nbs-where">' + esc(placeName(item.owner)) + '</span>' : '') + '</div>';
    }).join('');

    if (!items.length) list = '<div class="nbs-hint">' + esc(filt === 'wanted' ? labels.nothingWanted : labels.nothing) + '</div>';

    return '<div class="nbs-pane nbs-source"><div class="nbs-head"><span class="nbs-title">' + esc(labels.takeFrom) + '</span><span class="nbs-sub">' + esc(labels.takeFromSub) + '</span></div>' +
      '<div class="nbs-split"><div class="nbs-places">' + html + '</div><div class="nbs-col">' +
      '<div class="nbs-search"><span>&#9906;</span><input data-q="1" placeholder="' + esc(labels.search) + '" value="' + esc(station.query) + '"></div>' +
      '<div class="nbs-scope"><button data-f="wanted" class="' + (filt === 'wanted' ? 'is-on' : '') + '">' + esc(labels.sheWants) + ' <b>' + wantN + '</b></button>' +
      '<button data-f="all" class="' + (filt === 'all' ? 'is-on' : '') + '">' + esc(labels.everything) + ' <b>' + scope.length + '</b></button></div>' +
      '<div class="nbs-list">' + list + '</div><div class="nbs-hint">' + esc(labels.addHint) + '</div></div></div></div>';
  }

  function cellHtml(item, className, attr) {
    var width = item.w * PITCH - (PITCH - CELL);
    var height = item.h * PITCH - (PITCH - CELL);
    var size = Math.max(16, Math.min(width, height) - 14);

    return '<div class="nbs-cell ' + className + '"' + attr + ' style="left:' + item.x * PITCH + 'px;top:' + item.y * PITCH + 'px;width:' + width + 'px;height:' + height + 'px">' +
      '<img src="' + esc(item.ic) + '" style="width:' + size + 'px;height:' + size + 'px"><span class="nbs-q">' + (item.q > 1 ? item.q : '') + '</span></div>';
  }

  function basketHtml(state) {
    var data = station.data;
    var labels = data.labels;
    var wish = state.wish || {};
    var cols = data.cols || 5;
    var rows = data.gridRows || 3;
    var cells = '';
    var used = 0;

    for (var c = 0; c < cols * rows; c++) cells += '<div class="nbs-cell" style="left:' + (c % cols) * PITCH + 'px;top:' + Math.floor(c / cols) * PITCH + 'px"></div>';

    data.basket.forEach(function (item) {
      used += item.w * item.h;
      cells += cellHtml(item, 'is-item' + (item.wanted ? ' is-w' : ''), ' data-take="' + item.id + '"');
    });

    // Where Fill would put each stack, drawn faintly in the free cells.
    (data.picks || []).forEach(function (pick) {
      var item = itemById(pick.id);

      if (item) cells += cellHtml({ x: pick.x, y: pick.y, w: item.w, h: item.h, ic: item.ic, q: item.q }, 'is-ghost', '');
    });

    var chips = (wish.chips || []).map(function (chip) {
      return '<span class="nbs-chip ' + (chip.hit ? 'is-hit' : 'is-miss') + '">' + (chip.hit ? '&#10003; ' : '') + esc(chip.name) + '</span>';
    }).join('');

    if (wish.crisisShow && wish.crisisChipName) chips += '<span class="nbs-chip ' + (wish.crisisHit ? 'is-hit' : 'is-miss') + '">' + (wish.crisisHit ? '&#10003; ' : '') + esc(wish.crisisChipName) + '</span>';

    var html = '<div class="nbs-pane nbs-basketpane"><div class="nbs-head"><span class="nbs-title">' + esc(labels.basket) + '</span><span class="nbs-sub">' +
      esc(labels.cells.replace('{0}', used).replace('{1}', cols * rows)) + '</span></div>' +
      '<div class="nbs-basket" style="width:' + (cols * PITCH - (PITCH - CELL)) + 'px;height:' + (rows * PITCH - (PITCH - CELL)) + 'px">' + cells + '</div>' +
      '<div class="nbs-hint" style="text-align:center">' + esc(labels.takeHint) + '</div>';

    if (chips) html += '<div class="nbs-lbl">' + esc(labels.sheWants) + '</div><div class="nbs-chips">' + chips + '</div>';

    if (state.maxKg > 0) {
      var kw = Math.min(100, state.usedKg / state.maxKg * 100);

      html += '<div class="nbs-lbl">' + esc(labels.weight) + '</div><div class="nbs-meter"><i style="width:' + kw + '%"></i></div>' +
        '<div class="nbs-mline"><span><b>' + state.usedKg.toFixed(1) + '</b> / ' + state.maxKg.toFixed(1) + ' kg</span><span>' + esc(labels.kgFree.replace('{0}', Math.max(0, state.maxKg - state.usedKg).toFixed(1))) + '</span></div>';
    }

    var cur = num(wish.cur);
    var max = num(wish.max);
    var gain = num(wish.gain);

    if (max > 0) {
      var pw = Math.min(cur, max) / max * 100;
      var gw = Math.max(0, Math.min(gain, max - cur)) / max * 100;

      html += '<div class="nbs-lbl">' + esc(labels.progress) + '</div><div class="nbs-meter"><i style="width:' + pw + '%"></i><i class="is-gain" style="width:' + gw + '%"></i></div>' +
        '<div class="nbs-mline"><span><b>' + cur + '</b> / ' + max + '</span><span class="nbs-g">' + (gain > 0 ? esc(labels.ifSent.replace('{0}', gain)) : '') + '</span></div>';
    }

    html += '<div class="nbs-plan">' + esc(data.plan || '') + '</div>';

    var buttons = '';

    if (settings.fill && !data.away) buttons += '<button class="nbs-btn" data-act="fill" title="' + esc(labels.fillTitle) + '">' + esc(labels.fill) + '</button>';
    if (settings.putAway && !data.away) buttons += '<button class="nbs-btn is-quiet" data-act="putaway" title="' + esc(labels.putAwayTitle) + '">' + esc(labels.putAway) + '</button>';
    if (settings.callBack && data.away) buttons += '<button class="nbs-btn is-quiet" data-act="callback" title="' + esc(labels.callBackTitle) + '">' + esc(labels.callBack) + '</button>';

    html += '<div class="nbs-btns" style="margin-top:14px">' + buttons + '</div>';
    html += '<div class="nbs-btns"><button class="nbs-btn is-main" data-act="send"' + (data.away || !data.basket.length ? ' disabled' : '') + '>' + esc(data.away ? labels.away : labels.send) + '</button></div>';

    return html + '</div>';
  }

  function sideHtml(state) {
    var data = station.data;
    var labels = data.labels;
    var tierName = state.wish && state.wish.tierName ? String(state.wish.tierName) : '';
    var rows = (data.rows || []).map(function (row) {
      var name = row.tier ? (tierName || row.tier) : '';
      var value = name && row.value ? name + ', ' + row.value : (name || row.value);

      return '<div class="nbs-stat"><span>' + esc(row.label) + '</span><span class="' + (row.tone ? 'is-' + row.tone : '') + '">' + esc(value) + '</span></div>';
    }).join('');

    return '<div class="nbs-side"><div class="nbs-pane"><div class="nbs-head"><span class="nbs-title">' + esc(labels.nextDoor) + '</span><span class="nbs-sub">' + esc(data.day) + '</span></div>' + rows + '</div>' +
      '<button class="nbs-btn is-quiet" data-act="game" style="flex:none">' + esc(labels.game) + '</button></div>';
  }

  // The station lives in the page's body, outside the game's Vue app, so a re-render of the inventory leaves it alone.
  function stationNode(doc) {
    var node = doc.getElementById(STATION_ID);

    if (node) return node;

    node = doc.createElement('div');
    node.id = STATION_ID;
    node.className = 'unity-interactive';
    // Clicks and keys stay in the station, so the inventory never reads them as its own.
    node.addEventListener('mousedown', function (e) { e.stopPropagation(); });
    node.addEventListener('keydown', function (e) { if (e.key !== 'Escape') e.stopPropagation(); });
    node.addEventListener('click', function (e) { e.stopPropagation(); onClick(doc, e); });
    node.addEventListener('input', function (e) {
      if (!e.target.getAttribute('data-q')) return;

      station.query = e.target.value;
      draw(doc, true);
    });
    node.addEventListener('mousemove', function (e) { onHover(doc, e); });
    doc.body.appendChild(node);

    var open = doc.createElement('button');

    open.id = OPEN_ID;
    open.className = 'unity-interactive';
    open.addEventListener('mousedown', function (e) { e.stopPropagation(); });
    open.addEventListener('click', function (e) {
      e.stopPropagation();
      station.isOff = false;
      station.drawn = '';
      station.askAt = 0;
    });
    doc.body.appendChild(open);

    return node;
  }

  function onClick(doc, e) {
    var target = e.target.closest('[data-p],[data-f],[data-add],[data-take],[data-act]');

    if (!target) return;

    var data = station.data;

    if (target.hasAttribute('data-p')) {
      station.sel = target.getAttribute('data-p');
      draw(doc, true);
    } else if (target.hasAttribute('data-f')) {
      station.filt = target.getAttribute('data-f');
      draw(doc, true);
    } else if (target.hasAttribute('data-add')) {
      var item = itemById(target.getAttribute('data-add'));

      if (!item) return;
      if (item.no) {
        status(data.labels.cannotTake);

        return;
      }

      send('add|' + item.id + '|' + (e.shiftKey ? 'all' : 'one'));
      askSoon();
    } else if (target.hasAttribute('data-take')) {
      send('take|' + target.getAttribute('data-take'));
      askSoon();
    } else {
      var act = target.getAttribute('data-act');

      if (act === 'game') {
        station.isOff = true;
        hideStation(doc);
      } else if (act === 'send') {
        sendBasket(doc);
      } else {
        press(act);
        askSoon();
      }
    }
  }

  // Send is the game's own button press, so the game's checks and its sending all happen as usual.
  function sendBasket(doc) {
    var state = basketState(doc);

    try {
      if (state.setup && typeof state.setup.onBasketSend === 'function') state.setup.onBasketSend();
      else doc.querySelector('.basket-send-btn').click();
    } catch (err) {
      send('diag|send failed: ' + err);
    }

    askSoon();
  }

  function askSoon() {
    station.askAt = Date.now() + AFTER_MOVE_MS;
  }

  function onHover(doc, e) {
    var tip = doc.querySelector('.nbs-tip');

    if (!tip) return;

    var target = e.target.closest('[data-add],[data-take]');
    var item = target && itemById(target.getAttribute('data-add') || target.getAttribute('data-take'));

    if (!item) {
      tip.style.display = 'none';

      return;
    }

    var state = basketState(doc);
    var kg = kgOf(item.kg, state);

    tip.innerHTML = '<b>' + esc(item.n) + '</b><span>' + item.w + 'x' + item.h + ' &middot; x' + item.q + (kg ? ' &middot; ' + kg : '') + '<br>' + esc(placeName(item.owner)) + '</span>';
    tip.style.display = 'block';

    var view = doc.defaultView;
    var left = e.clientX + 14;
    var top = e.clientY + 14;

    if (left + tip.offsetWidth > view.innerWidth - 8) left = e.clientX - tip.offsetWidth - 14;
    if (top + tip.offsetHeight > view.innerHeight - 8) top = e.clientY - tip.offsetHeight - 14;

    tip.style.left = left + 'px';
    tip.style.top = top + 'px';
  }

  // Redraws only when the data, the panel's own numbers or the player's choices changed, keeping the search box's
  // focus and the lists' scroll.
  function draw(doc, isForced) {
    var node = stationNode(doc);
    var state = basketState(doc);
    var wish = state.wish || {};
    var key = station.json + '|' + station.sel + '|' + station.filt + '|' + station.query + '|' + state.maxKg + '|' + state.usedKg + '|' +
      [wish.cur, wish.max, wish.gain, wish.tierName, JSON.stringify(wish.chips || []), wish.crisisShow, wish.crisisHit].join('|');

    if (!isForced && key === station.drawn) return;

    station.drawn = key;

    var list = node.querySelector('.nbs-list');
    var places = node.querySelector('.nbs-places');
    var scroll = list ? list.scrollTop : 0;
    var placeScroll = places ? places.scrollTop : 0;
    var input = node.querySelector('[data-q]');
    var hasFocus = !!input && doc.activeElement === input;

    node.innerHTML = sourceHtml(state) + basketHtml(state) + sideHtml(state) + '<div class="nbs-tip"></div>';

    list = node.querySelector('.nbs-list');
    places = node.querySelector('.nbs-places');

    if (list) list.scrollTop = scroll;
    if (places) places.scrollTop = placeScroll;

    if (hasFocus) {
      input = node.querySelector('[data-q]');
      input.focus();
      input.setSelectionRange(input.value.length, input.value.length);
    }
  }

  function hideStation(doc) {
    var node = doc.getElementById(STATION_ID);
    var open = doc.getElementById(OPEN_ID);

    if (node) node.style.display = 'none';
    if (open) open.style.display = 'none';
  }

  function patchStation(doc, state) {
    if (!state.isBasket || station.isDead) {
      hideStation(doc);

      // Each time the basket opens, the station opens with it, on all storage.
      if (!state.isBasket) {
        station.isOff = false;
        station.sel = 'all';
        station.filt = '';
        station.query = '';
        station.data = null;
        station.json = '';
      }

      return;
    }

    if (station.isOff) {
      var open = stationNode(doc) && doc.getElementById(OPEN_ID);

      doc.getElementById(STATION_ID).style.display = 'none';
      open.textContent = station.data ? station.data.labels.station : 'Basket station';
      open.style.display = 'block';

      return;
    }

    if (Date.now() >= station.askAt) {
      station.askAt = Date.now() + STATION_REFRESH_MS;
      send('station');
    }

    if (!station.data) return;

    var node = stationNode(doc);

    doc.getElementById(OPEN_ID).style.display = 'none';
    draw(doc, false);
    node.style.display = 'flex';
  }

  // The plugin's answer to 'station'; null turns the station off until the next start, leaving the game's screen.
  function setStation(data) {
    if (!data) {
      station.isDead = true;
      station.data = null;

      return;
    }

    var json = JSON.stringify(data);

    if (json === station.json) return;

    station.json = json;
    station.data = data;
  }

  function patchBasket() {
    var frames = document.querySelectorAll('iframe');

    for (var i = 0; i < frames.length; i++) {
      if (!/BackpackUI\.html/.test(frames[i].src || '')) continue;

      var doc = null;

      try {
        doc = frames[i].contentDocument;
      } catch (e) {}

      if (!doc || !doc.head) {
        if (!hasReportedAccess) send('diag|cannot reach the inventory page document (iframe ' + frames[i].src + ')');
        hasReportedAccess = true;
        continue;
      }

      var state = basketState(doc);

      addStyle(doc);
      patchStation(doc, state);
    }
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
    var frames = document.querySelectorAll('iframe');

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

  function patchPhone(doc, frame) {
    var grid = doc.querySelector('.app-grid');

    // Exiled's Toolkit moves the app into the phone's dock, so look for it anywhere on the page, not only in the grid.
    if (settings.app && grid && !doc.querySelector('.' + APP_CLASS)) {
      addPhoneStyle(doc);
      grid.appendChild(phoneApp(doc));
    }

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

  function askForData() {
    phone.refreshAt = Date.now() + APP_REFRESH_MS;
    send('app');
  }

  // The plugin's answer to 'app': her status rows and the log, grouped by day, newest first.
  function app(data) {
    phone.data = data;

    if (phone.screen) render();
  }

  function render() {
    var doc = phone.screen.ownerDocument;
    var body = phone.screen.querySelector('.slnb-phone-body');
    var data = phone.data;

    if (!body || !data) return;

    var scroll = body.scrollTop;

    phone.screen.querySelector('.slnb-phone-title').textContent = data.title;
    phone.screen.querySelector('.slnb-phone-back').textContent = data.back;
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

    (data.days || []).forEach(function (day) {
      body.appendChild(make(doc, 'div', 'slnb-day', day.label));
      day.entries.forEach(function (entry) {
        var node = make(doc, 'div', 'slnb-entry is-' + entry.kind);

        node.appendChild(make(doc, 'div', 'slnb-entry-head', entry.time + '  ' + entry.label));
        node.appendChild(make(doc, 'div', 'slnb-entry-text', entry.text));

        if (entry.note) node.appendChild(make(doc, 'div', 'slnb-entry-note', entry.note));

        body.appendChild(node);
      });
    });
    body.scrollTop = scroll;
  }

  function patchPhones() {
    var frames = document.querySelectorAll('iframe');
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

  setInterval(function () {
    try {
      patchBasket();
    } catch (e) {}

    try {
      patchPhones();
    } catch (e) {}
  }, 400);

  window.__slneighborbasket = { status: status, setup: setup, app: app, station: setStation };
  send('ready');
})();
