// Neighbor Basket's web half. Runs in Root.html, the document every game page iframe sits in. Puts Fill (and, when
// turned on, Call back) beside the game's Send button on the basket panel, and shows a short toast when Exiled's
// Toolkit is not there to. Talks to the plugin through vuplex.postMessage("slmod|neighborbasket|<verb>") and is driven
// by window.__slneighborbasket.
(function () {
  if (window.__slneighborbasket) return;

  var PREFIX = 'slmod|neighborbasket|';
  var BUSY_TIMEOUT_MS = 4000;
  var TOAST_MS = 4500;
  var FILL_ID = 'slnb-fill';
  var CALLBACK_ID = 'slnb-callback';

  var settings = { fill: true, callBack: false, fillLabel: 'Fill', fillTitle: '', callBackLabel: 'Call back', callBackTitle: '' };
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

  function setup(fill, callBack, fillLabel, fillTitle, callBackLabel, callBackTitle) {
    settings = { fill: !!fill, callBack: !!callBack, fillLabel: fillLabel, fillTitle: fillTitle, callBackLabel: callBackLabel, callBackTitle: callBackTitle };
    isBusy = false;
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
      place(doc, CALLBACK_ID, settings.callBack && state.isBasket && state.isAway, settings.callBackLabel, settings.callBackTitle, 'callback');
    }
  }

  setInterval(function () {
    try {
      patchBasket();
    } catch (e) {}
  }, 400);

  window.__slneighborbasket = { status: status, setup: setup };
  send('ready');
})();
