// Just enough of the root document and the game's phone page for page.js to run in Jint: elements that report their
// changes to mutation observers, frames whose document is swapped when a page loads, and intervals and animation
// frames the tests run by hand. Observers are called only by flush(), the way a browser calls them before it paints.
// Test code only; nothing here ships.
var window = this;
var intervals = [];
var frameCallbacks = [];
var observers = [];
var pending = [];
var posted = [];

window.vuplex = { postMessage: function (message) { posted.push(String(message)); } };
window.getComputedStyle = function (el) { return { display: el.style.display === 'none' ? 'none' : 'block', visibility: 'visible' }; };

function setInterval(callback) {
  intervals.push(callback);

  return intervals.length;
}

function setTimeout(callback) {
  return 0;
}

function clearTimeout() {}

// One round of every page check, as the intervals would run it.
function tick() {
  intervals.forEach(function (callback) { callback(); });
}

function requestAnimationFrame(callback) {
  frameCallbacks.push(callback);

  return frameCallbacks.length;
}

// One animation frame: the callbacks asked for before it, then the observers.
function nextFrame() {
  var due = frameCallbacks;

  frameCallbacks = [];
  due.forEach(function (callback) { callback(); });
  flush();
}

function MutationObserver(callback) {
  this.callback = callback;
  this.targets = [];
  this.records = [];
}

MutationObserver.prototype.observe = function (target, options) {
  this.targets.push({ node: target, options: options });

  if (observers.indexOf(this) < 0) observers.push(this);
};

MutationObserver.prototype.disconnect = function () {
  this.targets = [];
};

function isInside(node, ancestor) {
  for (var at = node; at; at = at.parentNode) {
    if (at === ancestor) return true;
  }

  return false;
}

function report(record) {
  observers.forEach(function (observer) {
    var hears = observer.targets.some(function (t) {
      if (record.type === 'attributes' && !t.options.attributes) return false;
      if (record.type === 'childList' && !t.options.childList) return false;
      if (record.type === 'attributes' && t.options.attributeFilter && t.options.attributeFilter.indexOf(record.attributeName) < 0) return false;

      return t.node === record.target || (t.options.subtree && isInside(record.target, t.node));
    });

    if (!hears) return;

    observer.records.push(record);

    if (pending.indexOf(observer) < 0) pending.push(observer);
  });
}

// Calls the observers with what changed, again while they change things, as a browser does before it paints. An
// observer that keeps waking itself fails the test instead of hanging it.
function flush() {
  for (var round = 0; pending.length > 0; round++) {
    if (round > 50) throw new Error('an observer keeps waking itself');

    var due = pending;

    pending = [];
    due.forEach(function (observer) {
      var records = observer.records;

      observer.records = [];
      observer.callback(records);
    });
  }
}

function Element(doc, tag) {
  var self = this;

  this.ownerDocument = doc;
  this.tagName = String(tag).toUpperCase();
  this.nodeType = 1;
  this.id = '';
  this.classes = [];
  this.children = [];
  this.parentNode = null;
  this.text = '';
  this.style = { display: '' };
  this.attributes = {};
  this.listeners = {};
  this.classList = {
    contains: function (name) { return self.classes.indexOf(name) >= 0; },
    add: function (name) { if (self.classes.indexOf(name) < 0) self.classes.push(name); },
    remove: function (name) { self.classes = self.classes.filter(function (c) { return c !== name; }); },
    toggle: function (name, on) { if (on) this.add(name); else this.remove(name); }
  };
}

Object.defineProperty(Element.prototype, 'className', {
  get: function () { return this.classes.join(' '); },
  set: function (value) { this.classes = String(value).split(' ').filter(function (c) { return c !== ''; }); }
});

Object.defineProperty(Element.prototype, 'textContent', {
  get: function () { return this.text + this.children.map(function (c) { return c.textContent; }).join(''); },
  set: function (value) { this.children.slice().forEach(function (c) { this.removeChild(c); }, this); this.text = value == null ? '' : String(value); }
});

Object.defineProperty(Element.prototype, 'isConnected', {
  get: function () { return isInside(this, this.ownerDocument); }
});

Element.prototype.getAttribute = function (name) { return name in this.attributes ? this.attributes[name] : null; };
Element.prototype.hasAttribute = function (name) { return name in this.attributes; };
Element.prototype.addEventListener = function (type, callback) { (this.listeners[type] = this.listeners[type] || []).push(callback); };

Element.prototype.setAttribute = function (name, value) {
  this.attributes[name] = String(value);
  report({ type: 'attributes', target: this, attributeName: name, addedNodes: [] });
};

Element.prototype.removeChild = function (child) {
  this.children = this.children.filter(function (c) { return c !== child; });
  child.parentNode = null;
  report({ type: 'childList', target: this, addedNodes: [] });

  return child;
};

Element.prototype.remove = function () {
  if (this.parentNode) this.parentNode.removeChild(this);
};

Element.prototype.appendChild = function (child) {
  if (child.parentNode) child.parentNode.removeChild(child);

  child.parentNode = this;
  this.children.push(child);
  report({ type: 'childList', target: this, addedNodes: [child] });

  return child;
};

Element.prototype.all = function () {
  var found = [];

  this.children.forEach(function (child) {
    found.push(child);
    found = found.concat(child.all());
  });

  return found;
};

Element.prototype.getElementsByTagName = function (tag) {
  return this.all().filter(function (el) { return el.tagName === tag.toUpperCase(); });
};

Element.prototype.querySelectorAll = function (selector) {
  return this.all().filter(function (el) { return matches(el, selector); });
};

Element.prototype.querySelector = function (selector) { return this.querySelectorAll(selector)[0] || null; };

// Only the selectors the phone code uses: one class, or a tag.
function matches(el, selector) {
  if (selector.charAt(0) === '.') return el.classList.contains(selector.substring(1));

  return el.tagName === selector.toUpperCase();
}

function makeDocument(href) {
  var doc = { nodeType: 9, parentNode: null };

  doc.defaultView = { location: { href: href }, MutationObserver: MutationObserver };
  doc.createElement = function (tag) { return new Element(doc, tag); };
  doc.createElementNS = function (ns, tag) { return new Element(doc, tag); };
  doc.documentElement = new Element(doc, 'html');
  doc.documentElement.parentNode = doc;
  doc.head = doc.documentElement.appendChild(new Element(doc, 'head'));
  doc.body = doc.documentElement.appendChild(new Element(doc, 'body'));
  doc.addEventListener = function () {};
  doc.getElementById = function (id) {
    return doc.documentElement.all().filter(function (el) { return el.id === id; })[0] || null;
  };
  doc.querySelectorAll = function (selector) { return doc.documentElement.querySelectorAll(selector); };
  doc.querySelector = function (selector) { return doc.querySelectorAll(selector)[0] || null; };
  doc.getElementsByTagName = function (tag) { return doc.documentElement.getElementsByTagName(tag); };

  return doc;
}

var document = makeDocument('file:///WebUI/Root.html');
var container = document.body.appendChild(document.createElement('div'));
var PHONE_URL = 'file:///WebUI/UI/PhoneSMS/PhoneSMS.html';

// A game page frame, as Root.html makes one: appended, then sent to its address, still holding an empty document.
function openFrame(url) {
  var frame = document.createElement('iframe');

  frame.contentDocument = makeDocument('about:blank');
  container.appendChild(frame);
  frame.src = url;

  return frame;
}

// The page arrives in the frame: a new document, its body still empty.
function arrive(frame) {
  frame.contentDocument = makeDocument(frame.src);

  return frame.contentDocument;
}

// The game builds the phone's panel and home screen, with the app grid it leaves empty.
function buildPhone(doc) {
  var panel = doc.body.appendChild(doc.createElement('div'));
  var home = panel.appendChild(doc.createElement('div'));

  panel.className = 'sms-panel';
  home.className = 'home-stage';
  home.appendChild(doc.createElement('div')).className = 'app-grid';

  return doc;
}

function fireLoad(frame) {
  (frame.listeners.load || []).forEach(function (callback) { callback(); });
}

// Root.html sending a frame it already has to a new address: the old page goes, an empty document holds the frame.
function navigate(frame, url) {
  frame.src = url;
  frame.contentDocument = makeDocument('about:blank');
  report({ type: 'attributes', target: frame, attributeName: 'src', addedNodes: [] });
}

function apps(doc) {
  return doc.querySelectorAll('.slnb-app').length;
}
