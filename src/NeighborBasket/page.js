// Neighbor Basket's web half. Runs in Root.html, the document every game page iframe sits in. Puts Fill, Put away
// (with Storage Hub) and, when turned on, Call back beside the game's Send button on the basket panel, adds the
// Neighbor app to the phone, and shows a short toast when Exiled's Toolkit is not there to. Talks to the plugin through
// vuplex.postMessage("slmod|neighborbasket|<verb>") and is driven by window.__slneighborbasket.
(function () {
  if (window.__slneighborbasket) return;

  var PREFIX = 'slmod|neighborbasket|';
  var BUSY_TIMEOUT_MS = 4000;
  var TOAST_MS = 4500;
  var FILL_ID = 'slnb-fill';
  var CALLBACK_ID = 'slnb-callback';
  var PUTAWAY_ID = 'slnb-putaway';
  var APP_CLASS = 'slnb-app';
  var SCREEN_CLASS = 'slnb-phone';
  var PHONE_STYLE_ID = 'slnb-phone-style';
  var APP_REFRESH_MS = 5000;
  var SVG_NS = 'http://www.w3.org/2000/svg';
  // A basket: the body and its handle.
  var BASKET_STROKES = ['M4 10h16l-2 9H6z', 'M8 10l4-6 4 6', 'M9 13v3', 'M15 13v3'];

  var settings = { fill: true, callBack: false, putAway: false, app: true, fillLabel: 'Fill', fillTitle: '', callBackLabel: 'Call back', callBackTitle: '', putAwayLabel: 'Put away', putAwayTitle: '', appLabel: 'Neighbor' };
  var phone = { screen: null, data: null, refreshAt: 0 };
  var isBusy = false;
  var busyTimer = 0;
  var toastTimer = 0;
  var hasReportedAccess = false;
  var hasReportedState = false;

  var TOAST_STYLE = '#slnb-toast{position:fixed;left:50%;bottom:150px;transform:translateX(-50%);z-index:2147483002;pointer-events:none;display:none;max-width:640px;text-align:center;padding:10px 18px;border-radius:8px;border:1px solid rgba(255,193,7,.75);background:linear-gradient(180deg,rgba(30,30,33,.94),rgba(18,18,20,.96));color:#eee;font-family:"Microsoft YaHei","PingFang SC",sans-serif;font-size:14px;box-shadow:0 6px 20px rgba(0,0,0,.6)}';
  // A quieter twin of the game's Send button, so Send stays the main action.
  var BUTTON_STYLE = '.slnb-btn{min-width:72px;margin-right:8px;padding:8px 14px;font-size:14px;font-weight:600;letter-spacing:1px;color:#FFCA28;background:rgba(255,193,7,.08);border:1px solid rgba(255,193,7,.6);border-radius:6px;cursor:pointer;transition:all 160ms ease}' +
    '.slnb-btn:hover{background:rgba(255,193,7,.2);transform:translateY(-1px)}';

  function send(message) {
    try {
      if (window.vuplex) window.vuplex.postMessage(PREFIX + message);
    } catch (e) {}
  }

  // One press at a time; the plugin's answer (a toast, here or through the Toolkit) frees the button, and the timer
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

  // The basket panel's Vue state; a game update could rename it, so a failure hides the buttons and is reported once.
  function basketState(doc) {
    try {
      var setup = doc.getElementById('app')._vnode.component.setupState;

      return { isBasket: !!setup.isBasket, isAway: !!(setup.basketWish && setup.basketWish.away) };
    } catch (e) {
      if (!hasReportedState) send('diag|cannot read the basket panel state, the buttons stay hidden: ' + e);
      hasReportedState = true;

      return { isBasket: false, isAway: false };
    }
  }

  function addStyle(doc) {
    if (doc.getElementById('slnb-style')) return;

    var style = doc.createElement('style');
    style.id = 'slnb-style';
    style.textContent = BUTTON_STYLE;
    doc.head.appendChild(style);
  }

  // Vue drops foreign nodes when it re-renders the footer, so the buttons are checked and put back on every poll.
  function place(doc, id, isWanted, label, title, verb) {
    var node = doc.getElementById(id);
    var sendButton = doc.querySelector('.basket-footer .basket-send-btn');

    if (!isWanted || !sendButton) {
      if (node) node.remove();

      return;
    }

    if (!node) {
      node = doc.createElement('button');
      node.id = id;
      node.className = 'slnb-btn unity-interactive';
      node.addEventListener('click', function (event) {
        event.stopPropagation();
        press(verb);
      });
    }

    if (node.textContent !== label) node.textContent = label;
    if (node.title !== title) node.title = title;
    if (node.nextSibling !== sendButton) sendButton.parentNode.insertBefore(node, sendButton);
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
      place(doc, FILL_ID, settings.fill && state.isBasket && !state.isAway, settings.fillLabel, settings.fillTitle, 'fill');
      place(doc, PUTAWAY_ID, settings.putAway && state.isBasket && !state.isAway, settings.putAwayLabel, settings.putAwayTitle, 'putaway');
      place(doc, CALLBACK_ID, settings.callBack && state.isBasket && state.isAway, settings.callBackLabel, settings.callBackTitle, 'callback');
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

    if (settings.app && grid && !grid.querySelector('.' + APP_CLASS)) {
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

  // The plugin's answer to 'app': the tracker rows and the log, grouped by day, newest first.
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

      line.appendChild(make(doc, 'span', 'slnb-row-label', row.label));
      line.appendChild(make(doc, 'span', 'slnb-row-value', row.value));
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

  window.__slneighborbasket = { status: status, setup: setup, app: app };
  send('ready');
})();
